using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.InputSystem;

namespace LethalKeys
{
    /// <summary>Résultat d'un tirage : la touche déplacée, sa nouvelle place et les actions concernées.</summary>
    internal sealed class ScrambleResult
    {
        public string FromPath;
        public string ToPath;
        /// <summary>Noms lisibles des actions éligibles touchées (ex. "Sauter").</summary>
        public List<string> Actions = new List<string>();

        public string Describe() =>
            $"{string.Join(", ", Actions)} : {KeyScrambler.Describe(FromPath)} -> {KeyScrambler.Describe(ToPath)}";
    }

    /// <summary>État final d'un binding modifié, tel qu'on l'enregistre dans la sauvegarde.</summary>
    internal readonly struct StoredBinding
    {
        public readonly string ActionName;
        public readonly int BindingIndex;
        public readonly string ToPath;

        public StoredBinding(string actionName, int bindingIndex, string toPath)
        {
            ActionName = actionName;
            BindingIndex = bindingIndex;
            ToPath = toPath;
        }
    }

    /// <summary>
    /// Cœur du mod : déplace une touche vers une autre pour toutes les actions qui l'utilisent,
    /// sait tout annuler, se mettre en retrait le temps que le jeu enregistre les réglages du
    /// joueur, et exporter / réimporter son état pour la sauvegarde.
    /// Les changements sont des overrides en mémoire : jamais écrits dans les réglages du joueur.
    /// </summary>
    internal static class KeyScrambler
    {
        /// <summary>Un binding modifié par le mod.</summary>
        private sealed class Change
        {
            public InputAction Action;
            public int BindingIndex;
            /// <summary>Override en place avant notre passage (null = aucun) : c'est ce qu'on restaure.</summary>
            public string PreviousOverride;
            /// <summary>Touche donnée par le mod.</summary>
            public string ToPath;

            public string CurrentOverride => Action.bindings[BindingIndex].overridePath;
        }

        private static readonly List<Change> Changes = new List<Change>();
        private static readonly Random Rng = new Random();
        private static bool _suspended;

        /// <summary>Noms affichés au joueur (sans accents : la police du HUD ne les gère pas).</summary>
        private static readonly Dictionary<string, string> DisplayNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Move"] = "Deplacements",
            ["Move/up"] = "Avancer",
            ["Move/down"] = "Reculer",
            ["Move/left"] = "Gauche",
            ["Move/right"] = "Droite",
            ["Jump"] = "Sauter",
            ["Sprint"] = "Courir",
            ["Crouch"] = "S'accroupir",
            ["Interact"] = "Interagir",
            ["Discard"] = "Lacher",
            ["ItemSecondaryUse"] = "Objet (secondaire)",
            ["ReloadBatteries"] = "Recharger",
            ["InspectItem"] = "Inspecter",
            ["VoiceButton"] = "Parler",
            ["Emote1"] = "Emote 1",
            ["Emote2"] = "Emote 2",
            ["BuildMode"] = "Construction",
            ["ConfirmBuildMode"] = "Placer",
            ["Delete"] = "Ranger meuble",
            ["UseUtilitySlot"] = "Slot utilitaire",
            ["PingScan"] = "Scanner",
            ["ActivateItem"] = "Utiliser objet",
            ["Use"] = "Utiliser",
        };

        private static readonly Dictionary<string, string> MouseNames = new Dictionary<string, string>
        {
            [KeyPool.MousePrefix + "leftbutton"] = "Clic gauche",
            [KeyPool.MousePrefix + "rightbutton"] = "Clic droit",
            [KeyPool.MousePrefix + "middlebutton"] = "Clic molette",
            [KeyPool.MousePrefix + "forwardbutton"] = "Souris avant",
            [KeyPool.MousePrefix + "backbutton"] = "Souris arriere",
        };

        /// <summary>
        /// Les assets d'actions à modifier. Le joueur lit ses touches dans
        /// <c>InputSystem.actions</c> (PlayerControllerB.OnEnable/Update → FindAction), tandis que
        /// les réglages utilisent la copie <c>IngamePlayerSettings.playerInput.actions</c>.
        /// On modifie les deux pour qu'ils restent cohérents.
        /// </summary>
        internal static IEnumerable<InputActionAsset> Assets
        {
            get
            {
                InputActionAsset global = InputSystem.actions;
                if (global != null) yield return global;

                InputActionAsset settings = IngamePlayerSettings.Instance != null && IngamePlayerSettings.Instance.playerInput != null
                    ? IngamePlayerSettings.Instance.playerInput.actions
                    : null;
                if (settings != null && settings != global) yield return settings;
            }
        }

        internal static bool HasChanges => Changes.Count > 0;

        // ------------------------------------------------------------------ tirage

        /// <summary>
        /// Tire au sort une touche utilisée par une action éligible et l'envoie sur une touche
        /// libre. Renvoie null si rien n'est possible (aucune source ou aucune touche libre).
        /// </summary>
        internal static ScrambleResult Scramble(ICollection<string> eligibleActions, IEnumerable<string> allowedKeys)
        {
            List<string> sources = KeyPool.SourceKeys(eligibleActions);
            List<string> targets = KeyPool.FreeKeys(allowedKeys);

            if (sources.Count == 0)
            {
                Plugin.Log.LogWarning("Scramble : aucune touche source (aucune action éligible sur le clavier ?).");
                return null;
            }
            if (targets.Count == 0)
            {
                Plugin.Log.LogWarning("Scramble : aucune touche libre, rien n'est changé.");
                return null;
            }

            string from = sources[Rng.Next(sources.Count)];
            string to = targets[Rng.Next(targets.Count)];
            Plugin.Log.LogInfo($"Scramble : {sources.Count} source(s) [{string.Join(" ", sources)}], {targets.Count} cible(s) libre(s).");

            List<string> affected = RemapKey(from, to);
            var result = new ScrambleResult
            {
                FromPath = from,
                ToPath = to,
                Actions = affected
                    .Where(n => eligibleActions.Contains(n.Split('/')[0]))
                    .Select(DisplayName)
                    .Distinct()
                    .ToList(),
            };
            Plugin.Log.LogInfo($"Scramble : {result.Describe()}");
            return result;
        }

        /// <summary>
        /// Fait passer tous les bindings qui utilisent <paramref name="fromPath"/> sur
        /// <paramref name="toPath"/>, dans tous les assets. Renvoie les actions touchées
        /// ("Jump", "Move/up"…), sans doublon.
        /// </summary>
        internal static List<string> RemapKey(string fromPath, string toPath)
        {
            var actionNames = new List<string>();
            int assetCount = 0;

            foreach (InputActionAsset asset in Assets)
            {
                assetCount++;
                foreach (InputAction action in asset)
                {
                    for (int i = 0; i < action.bindings.Count; i++)
                    {
                        InputBinding b = action.bindings[i];
                        if (b.isComposite) continue;
                        if (!string.Equals(b.effectivePath, fromPath, StringComparison.OrdinalIgnoreCase)) continue;

                        Apply(action, i, toPath);

                        string name = BindingName(action, i);
                        if (!actionNames.Contains(name)) actionNames.Add(name);
                        Plugin.Log.LogInfo($"  [{asset.name}] {action.name}#{i} ({b.name}) : {fromPath} -> {toPath}");
                    }
                }
            }

            if (assetCount == 0)
                Plugin.Log.LogWarning("RemapKey : aucun asset d'actions trouvé.");

            Plugin.Log.LogInfo($"RemapKey {Describe(fromPath)} -> {Describe(toPath)} : {actionNames.Count} action(s) modifiée(s).");
            return actionNames;
        }

        private static void Apply(InputAction action, int index, string toPath)
        {
            Changes.Add(new Change
            {
                Action = action,
                BindingIndex = index,
                PreviousOverride = action.bindings[index].overridePath,
                ToPath = toPath,
            });
            action.ApplyBindingOverride(index, toPath);
        }

        // ------------------------------------------------------------------ annulation

        /// <summary>Annule tous les changements du mod, dans l'ordre inverse.</summary>
        internal static void ResetAll()
        {
            if (!_suspended) RestorePrevious(dropIfChangedByPlayer: false);
            Plugin.Log.LogInfo($"ResetAll : {Changes.Count} binding(s) restauré(s).");
            Changes.Clear();
            _suspended = false;
        }

        /// <summary>
        /// Retire temporairement nos overrides pour que le jeu n'enregistre que ceux du joueur.
        /// Si <paramref name="dropIfChangedByPlayer"/> est vrai, un binding que le joueur vient de
        /// réassigner lui-même est abandonné par le mod : son choix l'emporte.
        /// </summary>
        internal static void Suspend(bool dropIfChangedByPlayer)
        {
            if (_suspended || Changes.Count == 0) return;
            RestorePrevious(dropIfChangedByPlayer);
            _suspended = true;
        }

        /// <summary>Réapplique nos overrides après <see cref="Suspend"/>.</summary>
        internal static void Resume()
        {
            if (!_suspended) return;
            _suspended = false;
            foreach (Change c in Changes)
            {
                c.PreviousOverride = c.CurrentOverride;
                c.Action.ApplyBindingOverride(c.BindingIndex, c.ToPath);
            }
        }

        /// <summary>
        /// Le jeu vient de recharger ou d'effacer les overrides (annuler / réinitialiser les
        /// réglages) : on remet les nôtres par-dessus l'état actuel du joueur.
        /// </summary>
        internal static void Reapply(string reason)
        {
            if (Changes.Count == 0) return;
            Suspend(dropIfChangedByPlayer: false);
            Resume();
            Plugin.Log.LogInfo($"Reapply ({reason}) : {Changes.Count} binding(s) réappliqué(s).");
        }

        private static void RestorePrevious(bool dropIfChangedByPlayer)
        {
            for (int k = Changes.Count - 1; k >= 0; k--)
            {
                Change c = Changes[k];
                try
                {
                    bool stillOurs = string.Equals(c.CurrentOverride, c.ToPath, StringComparison.OrdinalIgnoreCase);
                    if (!stillOurs)
                    {
                        // Le binding a changé sans nous : le jeu l'a déjà remis, ou le joueur l'a réassigné.
                        if (dropIfChangedByPlayer)
                        {
                            Plugin.Log.LogInfo($"{c.Action.name}#{c.BindingIndex} réassigné par le joueur : abandonné par le mod.");
                            Changes.RemoveAt(k);
                        }
                        continue;
                    }

                    if (string.IsNullOrEmpty(c.PreviousOverride))
                        c.Action.RemoveBindingOverride(c.BindingIndex);
                    else
                        c.Action.ApplyBindingOverride(c.BindingIndex, c.PreviousOverride);
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning($"Restauration impossible de {c.Action?.name}#{c.BindingIndex} : {e.Message}");
                }
            }
        }

        // ------------------------------------------------------------------ sauvegarde

        /// <summary>État final de chaque binding modifié (asset global uniquement).</summary>
        internal static List<StoredBinding> Export()
        {
            InputActionAsset global = InputSystem.actions;
            var result = new List<StoredBinding>();
            var seen = new HashSet<string>();

            // Le dernier changement d'un binding est son état final : on parcourt à l'envers.
            for (int k = Changes.Count - 1; k >= 0; k--)
            {
                Change c = Changes[k];
                if (c.Action.actionMap?.asset != global) continue;
                string id = $"{c.Action.name}#{c.BindingIndex}";
                if (!seen.Add(id)) continue;
                result.Add(new StoredBinding(c.Action.name, c.BindingIndex, c.ToPath));
            }

            result.Reverse();
            return result;
        }

        /// <summary>Réapplique un état sauvegardé sur tous les assets. Renvoie le nombre de bindings appliqués.</summary>
        internal static int Import(IEnumerable<StoredBinding> stored)
        {
            int count = 0;
            foreach (StoredBinding s in stored)
            {
                foreach (InputActionAsset asset in Assets)
                {
                    InputAction action = asset.FindAction(s.ActionName);
                    if (action == null || s.BindingIndex < 0 || s.BindingIndex >= action.bindings.Count)
                    {
                        Plugin.Log.LogWarning($"Import : {s.ActionName}#{s.BindingIndex} introuvable dans {asset.name}, ignoré.");
                        continue;
                    }
                    Apply(action, s.BindingIndex, s.ToPath);
                    if (asset == InputSystem.actions) count++;
                }
            }
            Plugin.Log.LogInfo($"Import : {count} binding(s) restauré(s) depuis la sauvegarde.");
            return count;
        }

        /// <summary>
        /// Une ligne par action modifiée, avec sa touche d'origine et sa touche actuelle :
        /// « Sauter : Espace -> K ». Seules les actions éligibles sont listées.
        /// </summary>
        internal static List<string> Summary(ICollection<string> eligibleActions)
        {
            InputActionAsset global = InputSystem.actions;
            var lines = new List<string>();
            var seen = new HashSet<string>();

            foreach (Change first in Changes)
            {
                if (first.Action.actionMap?.asset != global) continue;
                if (!eligibleActions.Contains(first.Action.name)) continue;

                string id = $"{first.Action.name}#{first.BindingIndex}";
                if (!seen.Add(id)) continue;

                InputBinding b = first.Action.bindings[first.BindingIndex];
                string original = string.IsNullOrEmpty(first.PreviousOverride) ? b.path : first.PreviousOverride;
                string current = b.effectivePath;
                if (string.Equals(original, current, StringComparison.OrdinalIgnoreCase)) continue;

                lines.Add($"{DisplayName(BindingName(first.Action, first.BindingIndex))} : {Describe(original)} -> {Describe(current)}");
            }
            return lines;
        }

        // ------------------------------------------------------------------ noms

        private static string BindingName(InputAction action, int index)
        {
            string part = action.bindings[index].name;
            return string.IsNullOrEmpty(part) ? action.name : $"{action.name}/{part}";
        }

        internal static string DisplayName(string bindingName) =>
            DisplayNames.TryGetValue(bindingName, out string d) ? d : bindingName;

        /// <summary>
        /// Nom de la touche tel qu'il apparaît sur le clavier du joueur
        /// (ex. "&lt;Keyboard&gt;/w" donne "Z" sur AZERTY).
        /// </summary>
        internal static string Describe(string path)
        {
            if (string.IsNullOrEmpty(path)) return "?";

            string norm = KeyPool.Normalize(path);
            if (MouseNames.TryGetValue(norm, out string mouseName)) return mouseName;

            // Rangée des chiffres : en AZERTY, le nom affiché serait « é », « à », « ( »…
            // On montre le chiffre gravé sur la touche, plus lisible et sans accent.
            if (norm.Length == KeyPool.KeyboardPrefix.Length + 1 && norm.StartsWith(KeyPool.KeyboardPrefix) && char.IsDigit(norm[norm.Length - 1]))
                return "Touche " + norm[norm.Length - 1];

            try
            {
                InputControl control = Keyboard.current != null && KeyPool.IsKeyboard(path)
                    ? InputControlPath.TryFindControl(Keyboard.current, path)
                    : null;
                if (control != null && !string.IsNullOrEmpty(control.displayName))
                    return control.displayName;

                return InputControlPath.ToHumanReadableString(path, InputControlPath.HumanReadableStringOptions.OmitDevice);
            }
            catch
            {
                return path;
            }
        }
    }
}

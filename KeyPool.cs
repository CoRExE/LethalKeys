using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.InputSystem;

namespace LethalKeys
{
    /// <summary>
    /// Sait quelles touches (clavier + boutons de souris) sont occupées, lesquelles peuvent
    /// être tirées comme source (touche à déplacer) et lesquelles comme cible (touche libre).
    /// Tous les chemins sont normalisés en minuscules : "&lt;keyboard&gt;/space", "&lt;mouse&gt;/rightbutton".
    /// </summary>
    internal static class KeyPool
    {
        internal const string KeyboardPrefix = "<keyboard>/";
        internal const string MousePrefix = "<mouse>/";

        /// <summary>Boutons de souris gérés (pas le mouvement ni la molette, qui sont des axes).</summary>
        internal static readonly string[] MouseButtons =
        {
            MousePrefix + "leftbutton", MousePrefix + "rightbutton", MousePrefix + "middlebutton",
            MousePrefix + "forwardbutton", MousePrefix + "backbutton",
        };

        /// <summary>
        /// Touches proposées dans la config (une case à cocher chacune), par groupe.
        /// Noms Unity Input System, position QWERTY ; "mouse/..." pour la souris.
        /// </summary>
        internal static readonly (string Group, string[] Keys)[] DefaultKeyGroups =
        {
            ("Touches - Lettres", "abcdefghijklmnopqrstuvwxyz".Select(c => c.ToString()).ToArray()),
            ("Touches - Chiffres", "0123456789".Select(c => c.ToString()).ToArray()),
            ("Touches - Speciales", new[]
            {
                "space", "shift", "ctrl", "tab",
                "comma", "period", "semicolon", "quote", "leftBracket", "rightBracket", "minus", "equals",
            }),
            ("Touches - Souris", new[]
            {
                "mouse/leftButton", "mouse/rightButton", "mouse/middleButton", "mouse/forwardButton", "mouse/backButton",
            }),
        };

        /// <summary>"a" → "&lt;keyboard&gt;/a", "mouse/leftButton" → "&lt;mouse&gt;/leftbutton".</summary>
        internal static string ToPath(string name)
        {
            string k = name.Trim();
            if (k.StartsWith("<")) return Normalize(k);
            if (k.StartsWith("mouse/", StringComparison.OrdinalIgnoreCase)) return MousePrefix + k.Substring(6).ToLowerInvariant();
            return KeyboardPrefix + k.ToLowerInvariant();
        }

        /// <summary>Actions qui ne doivent jamais perdre leur touche (sinon le joueur est bloqué).</summary>
        private static readonly HashSet<string> LockedActions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "OpenMenu", "EnableChat", "SubmitChat",
        };

        internal static string Normalize(string path) =>
            string.IsNullOrEmpty(path) ? "" : path.Trim().ToLowerInvariant();

        internal static bool IsKeyboard(string path) =>
            Normalize(path).StartsWith(KeyboardPrefix, StringComparison.Ordinal);

        internal static bool IsMouseButton(string path) =>
            Array.IndexOf(MouseButtons, Normalize(path)) >= 0;

        /// <summary>Touche clavier ou bouton de souris : ce que le mod sait déplacer.</summary>
        internal static bool IsBindable(string path) => IsKeyboard(path) || IsMouseButton(path);

        /// <summary>
        /// Touches utilisées par n'importe quelle action : l'asset du jeu (avec les réglages
        /// du joueur) et toutes les actions activées, y compris celles des autres mods (InputUtils).
        /// </summary>
        internal static HashSet<string> OccupiedKeys()
        {
            var occupied = new HashSet<string>();

            void Collect(InputAction action)
            {
                foreach (InputBinding b in action.bindings)
                {
                    if (b.isComposite) continue;
                    if (IsBindable(b.effectivePath)) occupied.Add(Normalize(b.effectivePath));
                }
            }

            if (InputSystem.actions != null)
                foreach (InputAction a in InputSystem.actions) Collect(a);

            foreach (InputAction a in InputSystem.ListEnabledActions()) Collect(a);

            return occupied;
        }

        /// <summary>
        /// Touches qu'on peut déplacer : utilisées par au moins une action éligible
        /// et par aucune action verrouillée.
        /// </summary>
        internal static List<string> SourceKeys(ICollection<string> eligibleActions)
        {
            var sources = new HashSet<string>();
            var locked = new HashSet<string>();

            InputActionAsset asset = InputSystem.actions;
            if (asset == null) return new List<string>();

            foreach (InputAction action in asset)
            {
                bool isEligible = eligibleActions.Contains(action.name);
                bool isLocked = LockedActions.Contains(action.name);
                if (!isEligible && !isLocked) continue;

                foreach (InputBinding b in action.bindings)
                {
                    if (b.isComposite || !IsBindable(b.effectivePath)) continue;
                    string key = Normalize(b.effectivePath);
                    if (isLocked) locked.Add(key);
                    else sources.Add(key);
                }
            }

            sources.ExceptWith(locked);
            return sources.ToList();
        }

        /// <summary>
        /// Touches libres où l'on peut envoyer une action. On écarte aussi les touches dont le
        /// nom ne s'afficherait pas correctement dans le HUD (ex. « ù » en AZERTY).
        /// </summary>
        internal static List<string> FreeKeys(IEnumerable<string> allowedKeys)
        {
            HashSet<string> occupied = OccupiedKeys();
            return allowedKeys
                .Where(IsBindable)
                .Where(k => !occupied.Contains(k))
                .Where(k => IsHudFriendly(KeyScrambler.Describe(k)))
                .ToList();
        }

        private static bool IsHudFriendly(string name) =>
            !string.IsNullOrEmpty(name) && name.All(c => c >= 0x20 && c < 0x7F);

        /// <summary>"a, space, mouse/middleButton" → chemins normalisés, sans doublon, dans l'ordre.</summary>
        internal static List<string> ParseKeyList(string csv)
        {
            var result = new List<string>();
            foreach (string raw in (csv ?? "").Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string k = raw.Trim();
                string path = ToPath(k);

                bool unknownKey = IsKeyboard(path) && Keyboard.current != null
                    && InputControlPath.TryFindControl(Keyboard.current, path) == null;
                if (!IsBindable(path) || unknownKey)
                {
                    Plugin.Log.LogWarning($"ExtraKeys : '{k}' ignorée (ni une touche clavier, ni un bouton de souris).");
                    continue;
                }
                if (!result.Contains(path)) result.Add(path);
            }
            return result;
        }

        internal static HashSet<string> ParseNameList(string csv) =>
            new HashSet<string>(
                (csv ?? "").Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()),
                StringComparer.OrdinalIgnoreCase);
    }
}

using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using LethalConfig;
using LethalConfig.ConfigItems;
using LethalConfig.ConfigItems.Options;

namespace LethalKeys.Compat
{
    /// <summary>
    /// Menu de configuration en jeu via LethalConfig (dépendance optionnelle).
    /// Toutes les références à LethalConfig restent dans cette classe, appelée seulement si le
    /// mod est installé : sans lui, LethalKeys fonctionne normalement (config par fichier).
    /// Les listes sont des cases à cocher : le champ texte de LethalConfig tronque à ~120 caractères.
    /// </summary>
    internal static class LethalConfigCompat
    {
        internal const string Guid = "ainavt.lc.lethalconfig";

        internal static bool IsAvailable => Chainloader.PluginInfos.ContainsKey(Guid);

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void Register()
        {
            LethalConfigManager.SkipAutoGen();
            LethalConfigManager.SetModDescription(
                "Chaque mort reassigne une de tes touches au hasard. Les changements durent jusqu'au renvoi de l'equipage.");

            // --- Gameplay
            AddCheckBox(Plugin.ShowTip, "Afficher les messages");

            // --- Actions
            foreach (KeyValuePair<string, ConfigEntry<bool>> kv in Plugin.ActionToggles)
                AddCheckBox(kv.Value, KeyScrambler.DisplayName(kv.Key));
            AddGroupButtons(Plugin.ActionsSection, Plugin.ActionToggles.Values.ToList());

            // --- Touches : libellé = nom sur le clavier du joueur, + nom QWERTY s'il diffère
            foreach ((string group, string[] _) in KeyPool.DefaultKeyGroups)
            {
                var entries = new List<ConfigEntry<bool>>();
                foreach (KeyValuePair<string, (string Name, ConfigEntry<bool> Entry)> kv in Plugin.KeyToggles)
                {
                    if (kv.Value.Entry.Definition.Section != group) continue;
                    AddCheckBox(kv.Value.Entry, KeyLabel(kv.Key, kv.Value.Name));
                    entries.Add(kv.Value.Entry);
                }
                AddGroupButtons(group, entries);
            }

            LethalConfigManager.AddConfigItem(new TextInputFieldConfigItem(Plugin.ExtraKeys,
                new TextInputFieldOptions { Name = "Touches supplementaires", NumberOfLines = 2, TrimText = true, RequiresRestart = false }));

            // --- Debug
            AddCheckBox(Plugin.DumpBindings, "Dump des touches dans le log");
#if DEBUG
            AddCheckBox(Plugin.DebugKeys, "Touches de test (F8 / F9 / F10)");
#endif

            Plugin.Log.LogInfo("Menu LethalConfig enregistré.");
        }

        private static void AddCheckBox(ConfigEntry<bool> entry, string label)
        {
            LethalConfigManager.AddConfigItem(new BoolCheckBoxConfigItem(entry,
                new BoolCheckBoxOptions { Name = Hud.StripAccents(label), RequiresRestart = false }));
        }

        private static void AddGroupButtons(string section, List<ConfigEntry<bool>> entries)
        {
            LethalConfigManager.AddConfigItem(new GenericButtonConfigItem(section, "Tout cocher",
                "Coche toutes les cases de cette section.", "Tout cocher",
                () => entries.ForEach(e => e.Value = true)));
            LethalConfigManager.AddConfigItem(new GenericButtonConfigItem(section, "Tout decocher",
                "Decoche toutes les cases de cette section.", "Tout decocher",
                () => entries.ForEach(e => e.Value = false)));
        }

        private static string KeyLabel(string path, string qwertyName)
        {
            string local = Hud.StripAccents(KeyScrambler.Describe(path));
            return KeyPool.IsMouseButton(path) || string.Equals(local, qwertyName, System.StringComparison.OrdinalIgnoreCase)
                ? local
                : $"{local}  ({qwertyName})";
        }
    }
}

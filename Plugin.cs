using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace LethalKeys
{
    [BepInPlugin(Guid, Name, Version)]
    [BepInDependency(Compat.LethalConfigCompat.Guid, BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "lethalkeys";
        public const string Name = "LethalKeys";
        public const string Version = "0.1.0";

        internal const string ActionsSection = "Actions";

        /// <summary>Actions proposées dans la config (une case à cocher chacune).</summary>
        internal static readonly string[] DefaultActions =
        {
            "Move", "Jump", "Sprint", "Crouch", "Interact", "Discard", "ItemSecondaryUse", "ReloadBatteries",
            "InspectItem", "VoiceButton", "Emote1", "Emote2", "BuildMode", "ConfirmBuildMode", "Delete",
            "UseUtilitySlot", "PingScan", "Use", "ActivateItem",
        };

        internal static ManualLogSource Log;

        internal static ConfigEntry<bool> ShowTip;
        internal static ConfigEntry<string> ExtraKeys;
        internal static ConfigEntry<bool> DumpBindings;
#if DEBUG
        internal static ConfigEntry<bool> DebugKeys;
#endif

        /// <summary>Une case par action : nom interne → entrée.</summary>
        internal static readonly Dictionary<string, ConfigEntry<bool>> ActionToggles = new Dictionary<string, ConfigEntry<bool>>();
        /// <summary>Une case par touche : chemin ("&lt;keyboard&gt;/a") → (nom QWERTY, entrée).</summary>
        internal static readonly Dictionary<string, (string Name, ConfigEntry<bool> Entry)> KeyToggles = new Dictionary<string, (string, ConfigEntry<bool>)>();

        internal static HashSet<string> EligibleActionSet = new HashSet<string>();
        internal static List<string> AllowedKeyList = new List<string>();

        private readonly Harmony _harmony = new Harmony(Guid);

        private void Awake()
        {
            Log = Logger;

            // Descriptions sans accents : LethalConfig les affiche avec la police du jeu, qui ne les gère pas.
            ShowTip = Config.Bind("Gameplay", "ShowTip", true,
                "Afficher un message a l'ecran quand une touche change, et un resume au chargement d'une partie.");

            foreach (string action in DefaultActions)
            {
                ActionToggles[action] = Config.Bind(ActionsSection, action, true,
                    $"{KeyScrambler.DisplayName(action)} : sa touche peut etre tiree au sort a la mort.");
                ActionToggles[action].SettingChanged += (_, __) => RefreshSets();
            }

            foreach ((string group, string[] keys) in KeyPool.DefaultKeyGroups)
            {
                foreach (string key in keys)
                {
                    string path = KeyPool.ToPath(key);
                    // Clé de config stable (nom QWERTY) ; "/" remplacé car réservé dans les noms LethalConfig.
                    ConfigEntry<bool> entry = Config.Bind(group, key.Replace('/', ' '), true,
                        $"La touche {Hud.StripAccents(KeyScrambler.Describe(path))} peut etre attribuee a une action.");
                    entry.SettingChanged += (_, __) => RefreshSets();
                    KeyToggles[path] = (key, entry);
                }
            }

            ExtraKeys = Config.Bind("Touches - Autres", "ExtraKeys", "",
                "Touches supplementaires, separees par des virgules (noms Unity Input System, position QWERTY) : " +
                "ex. numpad5, leftAlt, f1. Une touche deja utilisee n'est jamais attribuee, et Echap, Entree et / ne bougent jamais.");
            ExtraKeys.SettingChanged += (_, __) => RefreshSets();

            DumpBindings = Config.Bind("Debug", "DumpBindings", false,
                "Liste toutes les actions et leurs touches dans le log en rejoignant une partie.");
#if DEBUG
            DebugKeys = Config.Bind("Debug", "DebugKeys", true,
                "Touches de test (build Debug uniquement) : F9 = simuler une mort, F10 = tout reinitialiser, F8 = dump.");
#endif

            RemoveObsoleteEntries();
            RefreshSets();

            if (Compat.LethalConfigCompat.IsAvailable)
                Compat.LethalConfigCompat.Register();

            _harmony.PatchAll(typeof(Plugin).Assembly);
            Log.LogInfo($"{Name} {Version} chargé : {EligibleActionSet.Count} action(s), {AllowedKeyList.Count} touche(s) autorisée(s).");
        }

        private static void RefreshSets()
        {
            EligibleActionSet = new HashSet<string>(
                ActionToggles.Where(kv => kv.Value.Value).Select(kv => kv.Key),
                System.StringComparer.OrdinalIgnoreCase);

            var keys = KeyToggles.Where(kv => kv.Value.Entry.Value).Select(kv => kv.Key).ToList();
            foreach (string extra in KeyPool.ParseKeyList(ExtraKeys.Value))
                if (!keys.Contains(extra)) keys.Add(extra);
            AllowedKeyList = keys;
        }

        /// <summary>Anciennes listes texte (EligibleActions, AllowedKeys), remplacées par les cases à cocher.</summary>
        private void RemoveObsoleteEntries()
        {
            var orphans = Traverse.Create(Config).Property("OrphanedEntries").GetValue<Dictionary<ConfigDefinition, string>>();
            if (orphans == null) return;

            int removed = 0;
            foreach (var def in orphans.Keys.Where(d => d.Section == "Gameplay" && (d.Key == "EligibleActions" || d.Key == "AllowedKeys")).ToList())
            {
                orphans.Remove(def);
                removed++;
            }
            if (removed > 0)
            {
                Config.Save();
                Log.LogInfo($"{removed} ancienne(s) option(s) retirée(s) de la config.");
            }
        }
    }
}

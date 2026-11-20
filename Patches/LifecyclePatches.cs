using System.Collections.Generic;
using GameNetcodeStuff;
using HarmonyLib;

namespace LethalKeys.Patches
{
    /// <summary>
    /// Cycle de vie d'une partie :
    /// - arrivée dans la partie → on recharge les touches sauvegardées et on affiche un résumé ;
    /// - sauvegarde automatique de fin de journée → on enregistre l'état actuel ;
    /// - équipage renvoyé (game over) → tout revient à la normale et la sauvegarde du mod est effacée ;
    /// - sortie du lobby → tout revient à la normale (la sauvegarde est gardée pour la prochaine fois).
    /// </summary>
    [HarmonyPatch]
    internal static class LifecyclePatches
    {
        [HarmonyPostfix, HarmonyPatch(typeof(PlayerControllerB), nameof(PlayerControllerB.ConnectClientToPlayerObject))]
        private static void OnJoined()
        {
            if (KeyScrambler.HasChanges) KeyScrambler.ResetAll();
            Hud.ClearQueue();

            List<StoredBinding> stored = Persistence.Load();
            if (stored.Count == 0) return;

            KeyScrambler.Import(stored);

            List<string> lines = KeyScrambler.Summary(Plugin.EligibleActionSet);
            if (Plugin.ShowTip.Value && lines.Count > 0)
            {
                foreach (string line in lines)
                    Hud.Enqueue($"Touches modifiees ({lines.Count})", line);
            }
        }

        /// <summary>Le jeu sauvegarde en fin de journée, une fois le vaisseau en orbite (hôte et clients).</summary>
        [HarmonyPostfix, HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.AutoSaveShipData))]
        private static void OnAutoSave()
        {
            Persistence.Save(KeyScrambler.Export());
        }

        [HarmonyPostfix, HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.playersFiredGameOver))]
        private static void OnPlayersFired()
        {
            Plugin.Log.LogInfo("Équipage renvoyé : remise à zéro des touches.");
            bool hadChanges = KeyScrambler.HasChanges;
            KeyScrambler.ResetAll();
            Persistence.Clear();
            Hud.ClearQueue();

            if (hadChanges && Plugin.ShowTip.Value)
                Hud.Enqueue("Equipage renvoye", "Toutes tes touches sont revenues a la normale.");
        }

        [HarmonyPostfix, HarmonyPatch(typeof(GameNetworkManager), nameof(GameNetworkManager.Disconnect))]
        private static void OnDisconnect()
        {
            if (!KeyScrambler.HasChanges) return;
            Plugin.Log.LogInfo("Sortie du lobby : remise à zéro des touches (la sauvegarde est conservée).");
            KeyScrambler.ResetAll();
            Hud.ClearQueue();
        }
    }
}

using GameNetcodeStuff;
using HarmonyLib;

namespace LethalKeys.Patches
{
    /// <summary>
    /// Mort du joueur local → une touche est tirée au sort.
    /// KillPlayer peut sortir tôt (pas propriétaire, déjà mort…), donc on compare
    /// isPlayerDead avant et après l'appel.
    /// Le message est mis en file d'attente : il s'affichera quand le joueur sera de nouveau
    /// vivant et que l'ATH sera libre (après les stats, le quota et les jours restants).
    /// </summary>
    [HarmonyPatch(typeof(PlayerControllerB), nameof(PlayerControllerB.KillPlayer))]
    internal static class DeathPatch
    {
        [HarmonyPrefix]
        private static void Prefix(PlayerControllerB __instance, out bool __state)
        {
            __state = __instance.isPlayerDead;
        }

        [HarmonyPostfix]
        private static void Postfix(PlayerControllerB __instance, bool __state)
        {
            bool wasDead = __state;
            if (wasDead || !__instance.isPlayerDead) return;
            if (GameNetworkManager.Instance == null || __instance != GameNetworkManager.Instance.localPlayerController) return;

            OnLocalPlayerDeath();
        }

        internal static void OnLocalPlayerDeath()
        {
            ScrambleResult result = KeyScrambler.Scramble(Plugin.EligibleActionSet, Plugin.AllowedKeyList);
            if (result == null) return;

            if (Plugin.ShowTip.Value)
                Hud.Enqueue("Touches modifiees !", result.Describe());
        }
    }
}

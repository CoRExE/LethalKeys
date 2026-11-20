#if DEBUG
// Compilé uniquement en Debug : F10 (tout réinitialiser) serait une triche dans la version publiée.
using GameNetcodeStuff;
using HarmonyLib;
using LethalKeys.Patches;
using UnityEngine.InputSystem;

namespace LethalKeys.Debug
{
    /// <summary>
    /// Touches de test, actives seulement si Debug.DebugKeys = true.
    ///   F9  : simuler une mort (tirage aléatoire, comme en jeu)
    ///   F10 : tout remettre à la normale
    ///   F8  : refaire le dump des actions dans le log
    /// </summary>
    [HarmonyPatch(typeof(PlayerControllerB), "Update")]
    internal static class DebugKeys
    {
        [HarmonyPostfix]
        private static void Postfix(PlayerControllerB __instance)
        {
            if (!Plugin.DebugKeys.Value) return;
            if (GameNetworkManager.Instance == null || __instance != GameNetworkManager.Instance.localPlayerController) return;

            Keyboard kb = Keyboard.current;
            if (kb == null) return;

            if (kb.f9Key.wasPressedThisFrame)
            {
                DeathPatch.OnLocalPlayerDeath();
            }
            else if (kb.f10Key.wasPressedThisFrame)
            {
                KeyScrambler.ResetAll();
                Hud.ClearQueue();
                Hud.Tip("LethalKeys (debug)", "Touches remises à la normale");
            }
            else if (kb.f8Key.wasPressedThisFrame)
            {
                BindingDumper.Dump();
                Hud.Tip("LethalKeys (debug)", "Dump écrit dans le log");
            }
        }
    }
}
#endif

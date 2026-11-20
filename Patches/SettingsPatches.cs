using HarmonyLib;

namespace LethalKeys.Patches
{
    /// <summary>
    /// Protège les réglages de touches du joueur.
    /// Le jeu enregistre ses touches avec InputSystem.actions.SaveBindingOverridesAsJson(), qui
    /// inclurait aussi nos overrides. On les retire donc le temps de cet appel, puis on les remet.
    /// Quand le jeu recharge ou efface les overrides (annuler, réinitialiser), on remet les nôtres.
    /// </summary>
    [HarmonyPatch]
    internal static class SettingsPatches
    {
        /// <summary>Le joueur vient de réassigner une touche : c'est ici que le jeu sérialise les overrides.</summary>
        [HarmonyPrefix, HarmonyPatch(typeof(IngamePlayerSettings), nameof(IngamePlayerSettings.CompleteRebind))]
        private static void BeforeCompleteRebind() => KeyScrambler.Suspend(dropIfChangedByPlayer: true);

        [HarmonyFinalizer, HarmonyPatch(typeof(IngamePlayerSettings), nameof(IngamePlayerSettings.CompleteRebind))]
        private static void AfterCompleteRebind() => KeyScrambler.Resume();

        [HarmonyPostfix, HarmonyPatch(typeof(IngamePlayerSettings), nameof(IngamePlayerSettings.DiscardChangedSettings))]
        private static void AfterDiscard() => KeyScrambler.Reapply("annulation des réglages");

        [HarmonyPostfix, HarmonyPatch(typeof(IngamePlayerSettings), nameof(IngamePlayerSettings.ResetAllKeybinds))]
        private static void AfterResetAll() => KeyScrambler.Reapply("réinitialisation des touches");

        [HarmonyPostfix, HarmonyPatch(typeof(IngamePlayerSettings), nameof(IngamePlayerSettings.LoadSettingsFromPrefs))]
        private static void AfterLoad() => KeyScrambler.Reapply("chargement des réglages");
    }
}

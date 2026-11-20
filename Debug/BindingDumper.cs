using System.Text;
using GameNetcodeStuff;
using HarmonyLib;
using UnityEngine.InputSystem;

namespace LethalKeys.Debug
{
    /// <summary>
    /// Étape 2 : liste toutes les actions du jeu et leurs touches dans le log BepInEx,
    /// au moment où le joueur local rejoint une partie.
    /// </summary>
    [HarmonyPatch(typeof(PlayerControllerB), nameof(PlayerControllerB.ConnectClientToPlayerObject))]
    internal static class BindingDumper
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            if (!Plugin.DumpBindings.Value) return;
            Dump();
        }

        internal static void Dump()
        {
            foreach (InputActionAsset asset in KeyScrambler.Assets)
                Dump(asset);
        }

        private static void Dump(InputActionAsset asset)
        {
            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine($"===== LethalKeys : dump des actions ({asset.name}) =====");

            foreach (InputActionMap map in asset.actionMaps)
            {
                sb.AppendLine($"[Map] {map.name}  (activée : {map.enabled})");
                foreach (InputAction action in map.actions)
                {
                    sb.AppendLine($"  [Action] {action.name}  type={action.type}  control={action.expectedControlType}");
                    for (int i = 0; i < action.bindings.Count; i++)
                    {
                        InputBinding b = action.bindings[i];
                        string kind = b.isComposite ? "COMPOSITE" : b.isPartOfComposite ? "  partie" : "binding";
                        string over = string.IsNullOrEmpty(b.overridePath) ? "" : $"  override={b.overridePath}";
                        sb.AppendLine($"    #{i} {kind,-9} name='{b.name}' path='{b.path}' effectif='{b.effectivePath}' groupes='{b.groups}'{over}");
                    }
                }
            }

            sb.AppendLine("----- Overrides enregistrés (JSON) -----");
            sb.AppendLine(asset.SaveBindingOverridesAsJson());
            sb.AppendLine("===== fin du dump =====");
            Plugin.Log.LogInfo(sb.ToString());
        }
    }
}

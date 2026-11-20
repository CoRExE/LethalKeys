using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using GameNetcodeStuff;
using HarmonyLib;
using UnityEngine;

namespace LethalKeys
{
    /// <summary>
    /// Messages à l'écran.
    /// - La police du HUD ne contient pas les lettres accentuées : on les retire (é → e…).
    /// - Le jeu masque l'ATH pendant la mort, les stats de fin de journée, l'annonce du quota
    ///   et des jours restants. Les messages du mod sont donc mis en file d'attente et affichés
    ///   seulement quand l'ATH est visible depuis un moment et que rien d'autre ne s'affiche.
    /// </summary>
    internal static class Hud
    {
        /// <summary>Durée pendant laquelle l'ATH doit rester libre avant d'afficher (secondes).</summary>
        private const float QuietDelay = 1.5f;
        /// <summary>Durée considérée comme occupée après une annonce du jeu (secondes).</summary>
        private const float GameAnnouncementDuration = 8f;
        /// <summary>Nombre maximal de lignes dans un message (le panneau est petit).</summary>
        private const int MaxLines = 4;
        /// <summary>Temps entre deux pages d'un long message (secondes).</summary>
        private const float PageInterval = 6f;

        private static int _pageNumber;
        private static float _nextPageAt;

        private static readonly List<string> QueuedLines = new List<string>();
        private static string _queuedHeader;
        private static float _busyUntil;
        private static float _readySince = -1f;

        /// <summary>Affiche tout de suite (pour le debug).</summary>
        internal static void Tip(string header, string body, bool warning = false)
        {
            if (HUDManager.Instance == null) return;
            HUDManager.Instance.DisplayTip(StripAccents(header), StripAccents(body), warning);
        }

        /// <summary>Ajoute une ligne au prochain message, affiché dès que l'ATH est disponible.</summary>
        internal static void Enqueue(string header, string line)
        {
            _queuedHeader = header;
            QueuedLines.Add(line);
            Plugin.Log.LogInfo($"Message en attente : {StripAccents(line)}");
        }

        internal static void ClearQueue()
        {
            QueuedLines.Clear();
            _readySince = -1f;
            _pageNumber = 0;
            _nextPageAt = 0f;
        }

        /// <summary>Le jeu commence une annonce : on attend qu'elle soit finie.</summary>
        internal static void MarkBusy(string reason)
        {
            _busyUntil = Time.time + GameAnnouncementDuration;
            _readySince = -1f;
            if (QueuedLines.Count > 0)
                Plugin.Log.LogInfo($"ATH occupé ({reason}), message reporté.");
        }

        /// <summary>Appelé à chaque frame pour le joueur local.</summary>
        internal static void Tick(PlayerControllerB local)
        {
            if (QueuedLines.Count == 0) return;

            if (!IsHudFree(local))
            {
                _readySince = -1f;
                return;
            }

            if (_readySince < 0f) _readySince = Time.time;
            if (Time.time - _readySince < QuietDelay) return;
            if (Time.time < _nextPageAt) return;

            // Plusieurs lignes → plusieurs pages de MaxLines, affichées l'une après l'autre.
            List<string> page = QueuedLines.Take(MaxLines).ToList();
            QueuedLines.RemoveRange(0, page.Count);

            string header = _queuedHeader ?? "LethalKeys";
            if (_pageNumber > 0 || QueuedLines.Count > 0) header += $" {_pageNumber + 1}/{_pageNumber + 1 + (QueuedLines.Count + MaxLines - 1) / MaxLines}";
            string body = string.Join("\n", page);

            Tip(header, body, warning: true);
            Plugin.Log.LogInfo($"Message affiché : {StripAccents(header)} - {StripAccents(body).Replace("\n", " | ")}");

            if (QueuedLines.Count > 0)
            {
                _pageNumber++;
                _nextPageAt = Time.time + PageInterval;
            }
            else
            {
                ClearQueue();
            }
        }

        private static bool IsHudFree(PlayerControllerB local)
        {
            HUDManager hud = HUDManager.Instance;
            if (hud == null || local == null) return false;
            if (local.isPlayerDead || !local.isPlayerControlled) return false;
            if (hud.hudHidden || hud.displayingNewQuota) return false;
            if (Time.time < _busyUntil) return false;
            return true;
        }

        internal static string StripAccents(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            string decomposed = text.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(decomposed.Length);
            foreach (char ch in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                    sb.Append(ch);
            }
            return sb.ToString().Normalize(NormalizationForm.FormC);
        }
    }

    namespace Patches
    {
        /// <summary>Fait tourner la file d'attente des messages à chaque frame du joueur local.</summary>
        [HarmonyPatch(typeof(PlayerControllerB), "Update")]
        internal static class HudTickPatch
        {
            [HarmonyPostfix]
            private static void Postfix(PlayerControllerB __instance)
            {
                if (GameNetworkManager.Instance == null || __instance != GameNetworkManager.Instance.localPlayerController) return;
                Hud.Tick(__instance);
            }
        }

        /// <summary>Annonces du jeu pendant lesquelles on ne doit rien afficher.</summary>
        [HarmonyPatch]
        internal static class HudBusyPatches
        {
            [HarmonyPostfix, HarmonyPatch(typeof(HUDManager), nameof(HUDManager.FillEndGameStats))]
            private static void EndGameStats() => Hud.MarkBusy("stats de fin de journée");

            [HarmonyPostfix, HarmonyPatch(typeof(HUDManager), nameof(HUDManager.DisplayDaysLeft))]
            private static void DaysLeft() => Hud.MarkBusy("jours restants");

            [HarmonyPostfix, HarmonyPatch(typeof(HUDManager), nameof(HUDManager.DisplayNewDeadline))]
            private static void NewDeadline() => Hud.MarkBusy("nouveau quota");

            [HarmonyPostfix, HarmonyPatch(typeof(HUDManager), nameof(HUDManager.ApplyPenalty))]
            private static void Penalty() => Hud.MarkBusy("pénalité");

            [HarmonyPostfix, HarmonyPatch(typeof(HUDManager), nameof(HUDManager.ShowPlayersFiredScreen))]
            private static void Fired() => Hud.MarkBusy("équipage renvoyé");
        }
    }
}

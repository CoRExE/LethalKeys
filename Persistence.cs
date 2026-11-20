using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;

namespace LethalKeys
{
    /// <summary>
    /// Enregistre l'état des touches modifiées, par partie :
    /// - <b>Hôte</b> : dans le fichier de sauvegarde du jeu (LCSaveFile1…) via ES3. Supprimer
    ///   la sauvegarde efface donc aussi nos données.
    /// - <b>Client</b> : dans BepInEx/config/LethalKeys/host_&lt;SteamID de l'hôte&gt;.txt,
    ///   puisque seul l'hôte possède le fichier de sauvegarde.
    /// Format : une ligne par binding, « Action|index|chemin ».
    /// </summary>
    internal static class Persistence
    {
        private const string Es3Key = "LethalKeys_Bindings";

        private static string ClientDir => Path.Combine(Paths.ConfigPath, "LethalKeys");

        internal static void Save(List<StoredBinding> bindings)
        {
            string data = Serialize(bindings);
            try
            {
                if (IsHost(out string saveFile))
                {
                    ES3.Save(Es3Key, data, saveFile);
                    Plugin.Log.LogInfo($"Sauvegarde (hôte, {saveFile}) : {bindings.Count} binding(s).");
                }
                else
                {
                    string file = ClientFile();
                    Directory.CreateDirectory(ClientDir);
                    File.WriteAllText(file, data);
                    Plugin.Log.LogInfo($"Sauvegarde (client, {Path.GetFileName(file)}) : {bindings.Count} binding(s).");
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Sauvegarde impossible : {e}");
            }
        }

        internal static List<StoredBinding> Load()
        {
            try
            {
                string data;
                if (IsHost(out string saveFile))
                {
                    data = ES3.KeyExists(Es3Key, saveFile) ? ES3.Load(Es3Key, saveFile, "") : "";
                    Plugin.Log.LogInfo($"Chargement (hôte, {saveFile}) : {(string.IsNullOrEmpty(data) ? "rien" : "données trouvées")}.");
                }
                else
                {
                    string file = ClientFile();
                    data = File.Exists(file) ? File.ReadAllText(file) : "";
                    Plugin.Log.LogInfo($"Chargement (client, {Path.GetFileName(file)}) : {(string.IsNullOrEmpty(data) ? "rien" : "données trouvées")}.");
                }
                return Deserialize(data);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Chargement impossible : {e}");
                return new List<StoredBinding>();
            }
        }

        internal static void Clear()
        {
            try
            {
                if (IsHost(out string saveFile))
                {
                    if (ES3.KeyExists(Es3Key, saveFile)) ES3.DeleteKey(Es3Key, saveFile);
                    Plugin.Log.LogInfo($"Données effacées (hôte, {saveFile}).");
                }
                else
                {
                    string file = ClientFile();
                    if (File.Exists(file)) File.Delete(file);
                    Plugin.Log.LogInfo($"Données effacées (client, {Path.GetFileName(file)}).");
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Effacement impossible : {e}");
            }
        }

        private static bool IsHost(out string saveFile)
        {
            GameNetworkManager gnm = GameNetworkManager.Instance;
            saveFile = gnm != null ? gnm.currentSaveFileName : null;
            return gnm != null && gnm.isHostingGame && !string.IsNullOrEmpty(saveFile);
        }

        /// <summary>Fichier du client, identifié par le compte Steam de l'hôte (ou "lan").</summary>
        private static string ClientFile()
        {
            string id = "lan";
            GameNetworkManager gnm = GameNetworkManager.Instance;
            if (gnm != null && !gnm.disableSteam && gnm.currentLobby.HasValue)
                id = gnm.currentLobby.Value.Owner.Id.Value.ToString();
            return Path.Combine(ClientDir, $"host_{id}.txt");
        }

        private static string Serialize(IEnumerable<StoredBinding> bindings) =>
            string.Join("\n", bindings.Select(b => $"{b.ActionName}|{b.BindingIndex}|{b.ToPath}"));

        private static List<StoredBinding> Deserialize(string data)
        {
            var result = new List<StoredBinding>();
            if (string.IsNullOrWhiteSpace(data)) return result;

            foreach (string line in data.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] parts = line.Split('|');
                if (parts.Length == 3 && int.TryParse(parts[1], out int index))
                    result.Add(new StoredBinding(parts[0], index, parts[2]));
                else
                    Plugin.Log.LogWarning($"Ligne de sauvegarde ignorée : '{line}'");
            }
            return result;
        }
    }
}

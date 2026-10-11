using System;
using System.IO;
using PodcastTycoon.Core;
using UnityEngine;

namespace PodcastTycoon.Game
{
    /// <summary>The save file on the device: one autosave slot in the app's private data folder.
    /// Writes go to a temp file first and are then swapped in, so a crash or a killed app in the
    /// middle of saving can never leave a half-written save behind.</summary>
    public static class SaveStore
    {
        /// <summary>Tests point this at a scratch folder.</summary>
        public static string DirectoryOverride;

        static string Dir => DirectoryOverride ?? Application.persistentDataPath;
        public static string SavePath => Path.Combine(Dir, "save.json");
        static string TempPath => Path.Combine(Dir, "save.json.tmp");
        static string BadPath => Path.Combine(Dir, "save.damaged.json");

        public static bool Exists() => File.Exists(SavePath);

        public static bool Save(Engine engine)
        {
            try
            {
                Directory.CreateDirectory(Dir);
                File.WriteAllText(TempPath, SaveGame.Serialize(engine));
                if (File.Exists(SavePath)) File.Delete(SavePath);
                File.Move(TempPath, SavePath);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Autosave failed: " + ex.Message);
                return false;
            }
        }

        /// <summary>The summary for the load screen, or null if there is no usable save.</summary>
        public static SaveHeader ReadHeader()
        {
            try
            {
                if (!Exists()) return null;
                return SaveGame.ReadHeader(File.ReadAllText(SavePath));
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Loads the saved run. On failure returns null and sets <paramref name="error"/>;
        /// a damaged file is kept aside (not deleted) so it isn't lost for good.</summary>
        public static Engine Load(IRng rng, out string error)
        {
            error = null;
            if (!Exists()) { error = "There's no saved game yet."; return null; }
            try
            {
                return SaveGame.Deserialize(File.ReadAllText(SavePath), rng);
            }
            catch (Exception ex)
            {
                error = ex is SaveFormatException ? ex.Message : "The save file couldn't be read.";
                try
                {
                    if (File.Exists(BadPath)) File.Delete(BadPath);
                    File.Move(SavePath, BadPath);
                }
                catch (Exception) { /* keep going: the error is what matters */ }
                return null;
            }
        }

        public static void Delete()
        {
            try { if (File.Exists(SavePath)) File.Delete(SavePath); }
            catch (Exception ex) { Debug.LogWarning("Couldn't delete the save: " + ex.Message); }
        }
    }
}

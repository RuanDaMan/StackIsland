using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace StackIsland
{
    [Serializable]
    public class PadSave
    {
        public string id;
        public int level;
        public double paid;
    }

    [Serializable]
    public class PileSave
    {
        public string id;
        public int count;
        public double cash;
    }

    [Serializable]
    public class SaveData
    {
        public const int CurrentVersion = 1;
        public int version = CurrentVersion;
        public double cash;
        public double totalEarned;
        public double playTime;
        public long lastSaveUnix;
        public float px, pz;
        public bool managersPaused;   // Island Managers stop auto-buying (pause menu toggle)
        public List<int> stack = new List<int>();
        public List<PadSave> pads = new List<PadSave>();
        public List<PileSave> piles = new List<PileSave>();

        public PadSave Pad(string id)
        {
            foreach (var p in pads) if (p.id == id) return p;
            var n = new PadSave { id = id };
            pads.Add(n);
            return n;
        }

        public PileSave Pile(string id)
        {
            foreach (var p in piles) if (p.id == id) return p;
            var n = new PileSave { id = id };
            piles.Add(n);
            return n;
        }
    }

    [Serializable]
    public class SettingsData
    {
        public float volume = 0.8f;
        public bool windowed;
    }

    /// <summary>One JSON file; written to a temp file first and the previous save kept as a backup.</summary>
    public static class SaveSystem
    {
        public static string SlotName = "stackisland_v1";
        static string Folder => Application.persistentDataPath;
        static string MainPath => Path.Combine(Folder, SlotName + ".json");
        static string BackupPath => Path.Combine(Folder, SlotName + ".bak.json");
        static string SettingsPath => Path.Combine(Folder, "settings.json");

        public static long NowUnix() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        public static bool HasSave() => File.Exists(MainPath) || File.Exists(BackupPath);

        public static void Save(SaveData d)
        {
            try
            {
                d.version = SaveData.CurrentVersion;
                d.lastSaveUnix = NowUnix();
                Directory.CreateDirectory(Folder);
                string tmp = MainPath + ".tmp";
                File.WriteAllText(tmp, JsonUtility.ToJson(d));
                if (File.Exists(MainPath)) File.Copy(MainPath, BackupPath, true);
                File.Copy(tmp, MainPath, true);
                File.Delete(tmp);
            }
            catch (Exception e) { Debug.LogError("[Save] " + e); }
        }

        public static SaveData Load() => TryRead(MainPath) ?? TryRead(BackupPath);

        static SaveData TryRead(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                var json = File.ReadAllText(path);
                return string.IsNullOrWhiteSpace(json) ? null : JsonUtility.FromJson<SaveData>(json);
            }
            catch (Exception e) { Debug.LogWarning("[Save] " + e.Message); return null; }
        }

        public static void Delete()
        {
            foreach (var p in new[] { MainPath, BackupPath }) if (File.Exists(p)) File.Delete(p);
        }

        public static SettingsData LoadSettings()
        {
            try { if (File.Exists(SettingsPath)) return JsonUtility.FromJson<SettingsData>(File.ReadAllText(SettingsPath)) ?? new SettingsData(); }
            catch { }
            return new SettingsData();
        }

        public static void SaveSettings(SettingsData s)
        {
            try { Directory.CreateDirectory(Folder); File.WriteAllText(SettingsPath, JsonUtility.ToJson(s, true)); }
            catch (Exception e) { Debug.LogWarning("[Save] settings: " + e.Message); }
        }
    }
}

using System.IO;
using System.Text.Json;

namespace UmlForge;

public static class ProjectStore
{
    public static string Dir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "UmlForge");
    public static string AutosavePath => Path.Combine(Dir, "autosave.uforge");
    public static string BackupPath => Path.Combine(Dir, "autosave.bak");
    public static string SettingsPath => Path.Combine(Dir, "settings.json");

    public static void EnsureDir() => Directory.CreateDirectory(Dir);

    public static void Save(Project p, string path)
    {
        EnsureDir();
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(p, Editor.Json));
        if (File.Exists(path)) File.Replace(tmp, path, null); else File.Move(tmp, path);
    }
    public static Project Load(string path) => JsonSerializer.Deserialize<Project>(File.ReadAllText(path), Editor.Json);

    public static void Autosave(Project p)
    {
        try
        {
            EnsureDir();
            if (File.Exists(AutosavePath)) File.Copy(AutosavePath, BackupPath, true);
            File.WriteAllText(AutosavePath, JsonSerializer.Serialize(p, Editor.Json));
        }
        catch { /* never crash on autosave */ }
    }
    public static Project LoadAutosave()
    {
        foreach (var path in new[] { AutosavePath, BackupPath })
        {
            try { if (File.Exists(path)) return Load(path); } catch { }
        }
        return null;
    }
    public static AppSettings LoadSettings()
    {
        try { if (File.Exists(SettingsPath)) return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath), Editor.Json); } catch { }
        return new AppSettings();
    }
    public static void SaveSettings(AppSettings s)
    {
        try { EnsureDir(); File.WriteAllText(SettingsPath, JsonSerializer.Serialize(s, Editor.Json)); } catch { }
    }
}
using System;
using System.Collections.Generic;
using System.IO;
using MGSC;

namespace QuasiRobotics;

// Every text the mod shows, by key, in one file per game language under
// Localization/ (see Localization/README.md). The files are built into the DLL;
// a copy in the installed mod's Localization folder overrides them, so
// translators can test without building. Missing or empty texts fall back to
// English. Served to native widgets through RobotNamePatch.
public static class ModText
{
    public const string DismantleConfirm = "quasirobotics.dialog.dismantle_robot";
    public const string DismantleButton = "quasirobotics.ui.dismantle";
    public const string PrintyardStatus = "quasirobotics.department.printyard_status";
    public const string SalvageDronesStatus = "quasirobotics.department.salvage_drones_status";
    public const string DroidPackageBlocked = "quasirobotics.notification.droid_package_blocked";
    public const string SelectRobot = "quasirobotics.ui.select_robot";
    public const string Robots = "quasirobotics.ui.robots";
    public const string NoRobots = "quasirobotics.ui.no_robots";

    public const string FolderName = "Localization";
    public const string ResourcePrefix = "QuasiRobotics.Localization.";
    public const Localization.Lang Fallback = Localization.Lang.EnglishUS;

    // Problems met while loading, for the log and the tests. Declared first: Tables uses it.
    public static readonly List<string> LoadErrors = new List<string>();
    private static readonly Dictionary<Localization.Lang, Dictionary<string, string>> Tables = LoadAll();

    private static Dictionary<Localization.Lang, Dictionary<string, string>> LoadAll()
    {
        var tables = new Dictionary<Localization.Lang, Dictionary<string, string>>();
        foreach (Localization.Lang lang in Enum.GetValues(typeof(Localization.Lang)))
            tables[lang] = Load(lang);
        return tables;
    }

    private static Dictionary<string, string> Load(Localization.Lang lang)
    {
        var table = new Dictionary<string, string>();
        string file = lang + ".json";
        try
        {
            using (var stream = typeof(ModText).Assembly.GetManifestResourceStream(ResourcePrefix + file))
            {
                if (stream == null) Log("Built-in " + file + " is missing from the DLL.");
                else using (var reader = new StreamReader(stream)) Merge(table, reader.ReadToEnd());
            }
        }
        catch (Exception ex) { Log("Built-in " + file + " could not be read: " + ex.Message); }
        try
        {
            string location = typeof(ModText).Assembly.Location;
            string path = string.IsNullOrEmpty(location) ? null : Path.Combine(Path.GetDirectoryName(location), FolderName, file);
            if (path != null && File.Exists(path)) Merge(table, File.ReadAllText(path));
        }
        catch (Exception ex) { Log(FolderName + "/" + file + " could not be read, using the built-in texts: " + ex.Message); }
        return table;
    }

    private static void Merge(Dictionary<string, string> table, string json)
    {
        foreach (var pair in ParseFlatJson(json)) table[pair.Key] = pair.Value;
    }

    // The language files are one flat JSON object of strings. Parsed here so the
    // mod needs no JSON library (the game's Newtonsoft build does not load everywhere).
    public static Dictionary<string, string> ParseFlatJson(string json)
    {
        var result = new Dictionary<string, string>();
        int i = 0;
        Skip(json, ref i);
        Expect(json, ref i, '{');
        Skip(json, ref i);
        if (i < json.Length && json[i] == '}') return result;
        while (true)
        {
            Skip(json, ref i);
            string key = ReadString(json, ref i);
            Skip(json, ref i);
            Expect(json, ref i, ':');
            Skip(json, ref i);
            result[key] = ReadString(json, ref i);
            Skip(json, ref i);
            if (i < json.Length && json[i] == ',') { i++; continue; }
            Expect(json, ref i, '}');
            return result;
        }
    }
    private static void Skip(string s, ref int i)
    {
        while (i < s.Length && (char.IsWhiteSpace(s[i]) || s[i] == '\uFEFF')) i++;
    }
    private static void Expect(string s, ref int i, char c)
    {
        if (i >= s.Length || s[i] != c) throw new FormatException("Expected '" + c + "' at character " + i + ".");
        i++;
    }
    private static string ReadString(string s, ref int i)
    {
        Expect(s, ref i, '"');
        var text = new System.Text.StringBuilder();
        while (true)
        {
            if (i >= s.Length) throw new FormatException("Unterminated text.");
            char c = s[i++];
            if (c == '"') return text.ToString();
            if (c != '\\') { text.Append(c); continue; }
            if (i >= s.Length) throw new FormatException("Unterminated text.");
            char e = s[i++];
            switch (e)
            {
                case 'n': text.Append('\n'); break;
                case 't': text.Append('\t'); break;
                case 'r': text.Append('\r'); break;
                case 'b': text.Append('\b'); break;
                case 'f': text.Append('\f'); break;
                case 'u': text.Append((char)Convert.ToInt32(s.Substring(i, 4), 16)); i += 4; break;
                default: text.Append(e); break;
            }
        }
    }

    private static void Log(string message)
    {
        LoadErrors.Add(message);
        try { Plugin.Logger.LogWarning(message); } catch { }
    }

    public static Localization.Lang CurrentLang
    {
        get
        {
            try { return Singleton<Localization>.Instance?.CurrentLang ?? Fallback; }
            catch { return Fallback; }
        }
    }

    public static bool TryGet(string key, out string text)
    {
        text = null;
        if (key == null) return false;
        if (Tables.TryGetValue(CurrentLang, out var table) && table.TryGetValue(key, out text) && !string.IsNullOrEmpty(text)) return true;
        return Tables[Fallback].TryGetValue(key, out text);
    }

    public static bool Has(Localization.Lang lang, string key) => Tables[lang].ContainsKey(key);
    public static IEnumerable<string> Keys(Localization.Lang lang) => Tables[lang].Keys;

    // The text for a key, or the key itself when no language has it.
    public static string Get(string key) => TryGet(key, out var text) ? text : key;

    public static string Format(string key, params object[] args)
    {
        string text = Get(key);
        try { return string.Format(text, args); }
        catch (FormatException) { return Tables[Fallback].TryGetValue(key, out var english) ? string.Format(english, args) : text; }
    }
}

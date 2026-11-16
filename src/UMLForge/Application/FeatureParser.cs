using System.Text.RegularExpressions;

namespace UmlForge;

public static class FeatureParser
{
    public static object Parse(string input)
    {
        string s = (input ?? "").Trim();
        if (s.Length == 0) return null;
        var vis = Visibility.Public;
        if (s.Length > 0 && "+-#~".Contains(s[0]))
        {
            vis = s[0] switch { '+' => Visibility.Public, '-' => Visibility.Private, '#' => Visibility.Protected, _ => Visibility.Package };
            s = s[1..].Trim();
        }
        bool isStatic = false;
        if (s.StartsWith("static ", StringComparison.OrdinalIgnoreCase)) { isStatic = true; s = s[7..].Trim(); }
        bool ro = s.Contains("readOnly");
        isStatic |= s.Contains("{static}") || Regex.IsMatch(s, @"\{[^}]*static[^}]*\}");
        s = Regex.Replace(s, @"\{[^}]*\}", "").Trim();
        string def = null;
        var dm = Regex.Match(s, @"=\s*(.+)$");
        if (dm.Success) { def = dm.Groups[1].Value.Trim(); s = s[..dm.Index].Trim(); }
        string mult = null;
        var mm = Regex.Match(s, @"\[([^\]]*)\]\s*$");
        if (mm.Success) { mult = mm.Groups[1].Value.Trim(); s = s[..mm.Index].Trim(); }
        string type = null;
        var tm = Regex.Match(s, @":\s*([^:]+)$");
        if (tm.Success) { type = tm.Groups[1].Value.Trim(); s = s[..tm.Index].Trim(); }
        var pm = Regex.Match(s, @"^(\w+)\s*\((.*)\)\s*$");
        if (pm.Success)
            return new UmlOperation { Visibility = vis, Name = pm.Groups[1].Value, Parameters = pm.Groups[2].Value.Trim(), ReturnType = type, IsStatic = isStatic };
        bool derived = s.StartsWith("/");
        return new UmlAttribute { Visibility = vis, Name = derived ? s[1..] : s, Type = type, Multiplicity = mult, Default = def, IsStatic = isStatic, IsReadOnly = ro, IsDerived = derived };
    }

    public static void ParseTransition(UmlEdge e, string label)
    {
        string s = label ?? "";
        var gm = Regex.Match(s, @"\[([^\]]*)\]");
        e.Guard = gm.Success ? gm.Groups[1].Value.Trim() : "";
        if (gm.Success) s = s.Remove(gm.Index, gm.Length);
        int slash = s.IndexOf('/');
        if (slash >= 0) { e.Effect = s[(slash + 1)..].Trim(); e.Trigger = s[..slash].Trim(); }
        else { e.Trigger = s.Trim(); e.Effect = ""; }
    }
}
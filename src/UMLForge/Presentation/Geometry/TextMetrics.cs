using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace UmlForge;

public static class TextMetrics
{
    public static readonly Typeface Face = new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
    public static double Dpi = 1.0;

    public static Size Measure(string s, double size, bool bold = false, bool italic = false)
    {
        if (string.IsNullOrEmpty(s)) return new Size(0, size + 4);
        var ft = new FormattedText(s, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface(Face.FontFamily, italic ? FontStyles.Italic : FontStyles.Normal,
                bold ? FontWeights.SemiBold : FontWeights.Normal, FontStretches.Normal), size, Brushes.Black, Dpi);
        return new Size(ft.Width, ft.Height);
    }

    public static string AttrText(UmlAttribute a)
    {
        var s = $"{Catalog.VisSymbol(a.Visibility)} {(a.IsDerived ? "/" : "")}{a.Name}";
        if (!string.IsNullOrWhiteSpace(a.Type)) s += " : " + a.Type;
        if (!string.IsNullOrWhiteSpace(a.Multiplicity)) s += $" [{a.Multiplicity}]";
        if (!string.IsNullOrWhiteSpace(a.Default)) s += " = " + a.Default;
        if (a.IsReadOnly) s += " {readOnly}";
        return s;
    }

    public static string OpText(UmlOperation o)
    {
        var s = $"{Catalog.VisSymbol(o.Visibility)} {o.Name}({o.Parameters})";
        if (!string.IsNullOrWhiteSpace(o.ReturnType)) s += " : " + o.ReturnType;
        return s;
    }

    public static string TransitionText(UmlEdge e)
    {
        var s = e.Trigger ?? "";
        if (!string.IsNullOrWhiteSpace(e.Guard)) s += (s.Length > 0 ? " " : "") + "[" + e.Guard + "]";
        if (!string.IsNullOrWhiteSpace(e.Effect)) s += " / " + e.Effect;
        return s.Trim();
    }

    public static List<string> Wrap(string text, double width, double fs)
    {
        var lines = new List<string>();
        foreach (var para in (text ?? "").Split('\n'))
        {
            var words = para.Split(' ');
            var cur = "";
            foreach (var w in words)
            {
                var test = cur.Length == 0 ? w : cur + " " + w;
                if (Measure(test, fs).Width > width && cur.Length > 0) { lines.Add(cur); cur = w; }
                else cur = test;
            }
            lines.Add(cur);
        }
        return lines;
    }
}
using System.Windows;
using System.Windows.Media;

namespace UmlForge;

public class Theme
{
    public PaperMode Mode;
    public Brush Paper, Ink, Grid, NodeFill, NoteFill, Selection, Guide, Valid, Invalid, Accent;
    public Pen InkPen, DashPen, ThinPen, SelPen, GuidePen, PreviewPen, BadPen;

    public static Theme For(PaperMode m)
    {
        var t = new Theme { Mode = m };
        Color paper, ink, grid, fill, note;
        switch (m)
        {
            case PaperMode.Cream: paper = C("#F6F1E4"); ink = C("#26303F"); grid = C("#E2D9C2"); fill = C("#FFFDF6"); note = C("#FFF3C9"); break;
            case PaperMode.Blueprint: paper = C("#122A52"); ink = C("#D9E7FF"); grid = C("#1E3A6B"); fill = C("#17325F"); note = C("#1D3D73"); break;
            default: paper = C("#FBFAF7"); ink = C("#1C2536"); grid = C("#E7E4DA"); fill = C("#FFFFFF"); note = C("#FFF6D8"); break;
        }
        t.Paper = F(paper); t.Ink = F(ink); t.Grid = F(grid); t.NodeFill = F(fill); t.NoteFill = F(note);
        t.Selection = F(C("#3E7BFA")); t.Guide = F(C("#FF3D7F")); t.Valid = F(C("#2FBF71")); t.Invalid = F(C("#F0426B")); t.Accent = F(C("#22D3EE"));
        t.InkPen = P(t.Ink, 1.5); t.ThinPen = P(t.Ink, 1.1);
        t.DashPen = new Pen(t.Ink, 1.4) { DashStyle = new DashStyle(new double[] { 5, 3.5 }, 0) }; t.DashPen.Freeze();
        t.SelPen = P(t.Selection, 1.6); t.GuidePen = P(t.Guide, 1.0);
        t.PreviewPen = new Pen(t.Selection, 1.6) { DashStyle = new DashStyle(new double[] { 4, 3 }, 0) }; t.PreviewPen.Freeze();
        t.BadPen = new Pen(t.Invalid, 1.6) { DashStyle = new DashStyle(new double[] { 4, 3 }, 0) }; t.BadPen.Freeze();
        return t;
    }

    static Color C(string hex) => (Color)ColorConverter.ConvertFromString(hex);
    static Brush F(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }
    static Pen P(Brush b, double w) { var p = new Pen(b, w); p.Freeze(); return p; }
}

public class RenderState
{
    public Theme Theme;
    public double FontSize = 13, Zoom = 1, GridSize = 20;
    public bool Grid, Shadows;
    public HashSet<string> SelNodes = new(), SelEdges = new(), Contents = new();
    public string HoverNode;
    public List<(Point a, Point b)> Guides = new();
    public Rect? Marquee;
    public UmlNode PreviewSource;
    public Point PreviewStart, PreviewPoint;
    public bool PreviewValid;
    public EdgeKind PreviewEdge;
    public NodeKind? GhostNode;
    public Point GhostPoint;
    public Dictionary<string, Severity> Badges = new();
    public bool Export;
}
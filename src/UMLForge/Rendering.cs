using System.Globalization;
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
    public Theme Theme; public double FontSize = 13, Zoom = 1, GridSize = 20;
    public bool Grid, Shadows;
    public HashSet<string> SelNodes = new(), SelEdges = new();
    public HashSet<string> Contents = new(); // <-- ADD THIS
    public string HoverNode;
    public List<(Point a, Point b)> Guides = new();
    public Rect? Marquee;
    public UmlNode PreviewSource; public Point PreviewPoint; public bool PreviewValid; public EdgeKind PreviewEdge;
    public NodeKind? GhostNode; public Point GhostPoint;
    public Dictionary<string, Severity> Badges = new();
    public bool Export;
}

public class Renderer
{
    readonly DrawingContext dc; readonly RenderState st; readonly Theme t; readonly double fs;
    public Renderer(DrawingContext dc, RenderState st) { this.dc = dc; this.st = st; t = st.Theme; fs = st.FontSize; }

    // ---------- primitives ----------
    void Rect(Rect r, Brush fill, Pen pen) => dc.DrawRectangle(fill, pen, r);
    void Round(Rect r, double rad, Brush fill, Pen pen) => dc.DrawRoundedRectangle(fill, pen, r, rad, rad);
    void Ellipse(Point c, double rx, double ry, Brush fill, Pen pen) => dc.DrawEllipse(fill, pen, c, rx, ry);
    void Line(Point a, Point b, Pen pen) => dc.DrawLine(pen, a, b);
    void Poly(IEnumerable<Point> pts, Brush fill, Pen pen)
    {
        var g = new StreamGeometry();
        using (var c = g.Open()) { bool first = true; foreach (var p in pts) { if (first) { c.BeginFigure(p, fill != null, true); first = false; } else c.LineTo(p, true, false); } }
        g.Freeze(); dc.DrawGeometry(fill, pen, g);
    }
    public void Text(string s, double x, double y, Brush brush, double size = 0, bool bold = false, bool italic = false, bool underline = false, string align = "left")
    {
        if (string.IsNullOrEmpty(s)) return;
        size = size <= 0 ? fs : size;
        var ft = new FormattedText(s, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface(Geo.Face.FontFamily, italic ? FontStyles.Italic : FontStyles.Normal, bold ? FontWeights.SemiBold : FontWeights.Normal, FontStretches.Normal),
            size, brush, Geo.Dpi);
        if (underline) ft.SetTextDecorations(TextDecorations.Underline);
        double w = ft.Width;
        double xx = align == "center" ? x - w / 2 : align == "right" ? x - w : x;
        dc.DrawText(ft, new Point(xx, y));
    }
    void HaloText(EdgeLabel l)
    {
        var size = Geo.Measure(l.Text, l.Size, l.Bold, l.Italic);
        double x = l.Align == "middle" ? l.X - size.Width / 2 : l.Align == "end" ? l.X - size.Width : l.X;
        Text(l.Text, l.X, l.Y, t.Ink, l.Size, l.Bold, l.Italic, false, l.Align);
    }
    Brush FillOf(UmlNode n)
    {
        if (!string.IsNullOrEmpty(n.Fill)) { try { var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(n.Fill)); b.Freeze(); return b; } catch { } }
        return n.Kind == NodeKind.Note ? t.NoteFill : t.NodeFill;
    }
    void Shadow(Rect r)
    {
        if (!st.Shadows || st.Export && st.Theme.Mode == PaperMode.Blueprint) return;
        var sh = new SolidColorBrush(Color.FromArgb(26, 10, 16, 30)); sh.Freeze();
        dc.DrawRectangle(sh, null, new Rect(r.X + 3, r.Y + 4, r.Width, r.Height));
    }

    // ---------- nodes ----------
    public void DrawNode(UmlNode n)
    {
        var b = Geo.Bounds(n, fs);
        switch (n.Kind)
        {
            case NodeKind.Class or NodeKind.Interface or NodeKind.Enumeration or NodeKind.DataType or NodeKind.Object: Classifier(n, b); break;
            case NodeKind.Fork: Rect(b, t.Mode == PaperMode.Blueprint ? t.Ink : Brushes.Black, null); break;
            case NodeKind.Actor: Actor(n, b); break;
            case NodeKind.UseCase: Ellipse(Geo.Center(b), b.Width / 2, b.Height / 2, FillOf(n), t.InkPen); CenteredWrap(n.Name, b, fs); break;
            case NodeKind.Boundary: Boundary(n, b); break;
            case NodeKind.Lifeline: Lifeline(n, b); break;
            case NodeKind.Fragment: Fragment(n, b); break;
            case NodeKind.InteractionUse: Rect(b, FillOf(n), t.InkPen); Pentagon(b.X, b.Y, "ref"); Text(n.Name, b.X + b.Width / 2, b.Y + b.Height / 2 - fs / 2, t.Ink, fs, false, false, false, "center"); break;
            case NodeKind.Initial or NodeKind.StateInitial: Ellipse(Geo.Center(b), b.Width / 2, b.Height / 2, t.Ink, null); break;
            case NodeKind.ActivityFinal or NodeKind.StateFinal:
                Ellipse(Geo.Center(b), b.Width / 2, b.Height / 2, t.NodeFill, new Pen(t.Ink, 2));
                Ellipse(Geo.Center(b), b.Width / 2 - 6, b.Height / 2 - 6, t.Ink, null); break;
            case NodeKind.FlowFinal:
                Ellipse(Geo.Center(b), b.Width / 2, b.Height / 2, t.NodeFill, new Pen(t.Ink, 1.6));
                Line(new Point(b.X + 6, b.Y + 6), new Point(b.Right - 6, b.Bottom - 6), t.InkPen);
                Line(new Point(b.Right - 6, b.Y + 6), new Point(b.X + 6, b.Bottom - 6), t.InkPen); break;
            case NodeKind.Action: Round(b, 12, FillOf(n), t.InkPen); CenteredWrap(n.Name, b, fs); break;
            case NodeKind.Decision or NodeKind.Choice:
                Poly(new[] { new Point(b.X + b.Width / 2, b.Y), new Point(b.Right, b.Y + b.Height / 2), new Point(b.X + b.Width / 2, b.Bottom), new Point(b.X, b.Y + b.Height / 2) }, FillOf(n), t.InkPen);
                if (!string.IsNullOrWhiteSpace(n.Name)) Text(n.Name, b.Right + 6, b.Y + b.Height / 2 - fs / 2, t.Ink, fs - 1);
                break;
            case NodeKind.SendSignal:
                Poly(new[] { new Point(b.X, b.Y), new Point(b.Right - 14, b.Y), new Point(b.Right, b.Y + b.Height / 2), new Point(b.Right - 14, b.Bottom), new Point(b.X, b.Bottom) }, FillOf(n), t.InkPen);
                Text(n.Name, b.X + (b.Width - 14) / 2, b.Y + b.Height / 2 - fs / 2, t.Ink, fs, false, false, false, "center"); break;
            case NodeKind.AcceptSignal:
                Poly(new[] { new Point(b.X, b.Y), new Point(b.Right, b.Y), new Point(b.Right, b.Bottom), new Point(b.X, b.Bottom), new Point(b.X + 14, b.Y + b.Height / 2) }, FillOf(n), t.InkPen);
                Text(n.Name, b.X + 14 + (b.Width - 14) / 2, b.Y + b.Height / 2 - fs / 2, t.Ink, fs, false, false, false, "center"); break;
            case NodeKind.TimeEvent:
                Poly(new[] { new Point(b.X, b.Y), new Point(b.Right, b.Y), new Point(b.X, b.Bottom), new Point(b.Right, b.Bottom) }, FillOf(n), t.InkPen);
                if (!string.IsNullOrWhiteSpace(n.Name)) Text(n.Name, b.X + b.Width / 2, b.Bottom + 4, t.Ink, fs - 1, false, false, false, "center");
                break;
            case NodeKind.ObjectNode: Rect(b, FillOf(n), t.InkPen); CenteredWrap(n.Name, b, fs); break;
            case NodeKind.Partition: Partition(n, b); break;
            case NodeKind.State: State(n, b); break;
            case NodeKind.Junction: Ellipse(Geo.Center(b), b.Width / 2, b.Height / 2, t.Ink, null); break;
            case NodeKind.Terminate:
                Line(new Point(b.X, b.Y), new Point(b.Right, b.Bottom), new Pen(t.Ink, 2.4));
                Line(new Point(b.Right, b.Y), new Point(b.X, b.Bottom), new Pen(t.Ink, 2.4)); break;
            case NodeKind.HistoryShallow or NodeKind.HistoryDeep:
                Ellipse(Geo.Center(b), b.Width / 2, b.Height / 2, t.NodeFill, t.InkPen);
                Text(n.Kind == NodeKind.HistoryShallow ? "H" : "H*", Geo.Center(b).X, Geo.Center(b).Y - fs / 2, t.Ink, fs, true, false, false, "center"); break;
            case NodeKind.Component: Component(n, b); break;
            case NodeKind.Port: Rect(b, FillOf(n), t.InkPen); if (!string.IsNullOrWhiteSpace(n.Name)) Text(n.Name, b.Right + 5, b.Y - 2, t.Ink, fs - 2); break;
            case NodeKind.ProvidedInterface: Lollipop(n, b); break;
            case NodeKind.RequiredInterface: Socket(n, b); break;
            case NodeKind.DeviceNode or NodeKind.ExecEnvNode: Cube(n, b); break;
            case NodeKind.Artifact: Artifact(n, b); break;
            case NodeKind.Package: Package(n, b); break;
            case NodeKind.Note: Note(n, b); break;
        }
        if (!st.Export && st.Badges.TryGetValue(n.Id, out var sev))
        {
            var brush = sev == Severity.Error ? t.Invalid : sev == Severity.Warning ? new SolidColorBrush(Color.FromRgb(255, 178, 36)) : t.Accent;
            Ellipse(new Point(b.Right - 2, b.Y + 2), 5, 5, brush, new Pen(t.Paper, 1.5));
        }
    }

    void CenteredWrap(string text, Rect b, double size)
    {
        var lines = Geo.Wrap(text, b.Width - 16, size);
        double y = b.Y + b.Height / 2 - lines.Count * (size + 4) / 2;
        foreach (var l in lines) { Text(l, b.X + b.Width / 2, y, t.Ink, size, false, false, false, "center"); y += size + 4; }
    }

    void Classifier(UmlNode n, Rect b)
    {
        var L = Geo.LayoutClassifier(n, fs);
        Shadow(b); Rect(b, FillOf(n), t.InkPen);
        double y = b.Y + 8;
        if (!string.IsNullOrEmpty(L.Stereo)) { Text(L.Stereo, b.X + b.Width / 2, y, t.Ink, fs - 1, false, false, false, "center"); y += fs + 4; }
        string name = n.Kind == NodeKind.Object
            ? (string.IsNullOrWhiteSpace(n.Name) ? "" : n.Name) + (string.IsNullOrWhiteSpace(n.Represents) ? "" : " : " + n.Represents)
            : n.Name;
        Text(name, b.X + b.Width / 2, y, t.Ink, fs, true, n.IsAbstract, n.Kind == NodeKind.Object, "center");
        double y1 = b.Y + L.NameH; Line(new Point(b.X, y1), new Point(b.Right, y1), t.InkPen);
        double lh = fs + 6, ty = y1 + 5;
        foreach (var (text, ul, it) in L.Attrs) { Text(text, b.X + 9, ty, t.Ink, fs, false, it, ul); ty += lh; }
        if (L.OpH > 0)
        {
            double y2 = y1 + L.AttrH; Line(new Point(b.X, y2), new Point(b.Right, y2), t.InkPen);
            ty = y2 + 5;
            foreach (var (text, ul, it) in L.Ops) { Text(text, b.X + 9, ty, t.Ink, fs, false, it, ul); ty += lh; }
        }
    }

    void Actor(UmlNode n, Rect b)
    {
        double cx = b.X + b.Width / 2, s = b.Height / 96;
        double headR = 9 * s, top = b.Y + 2;
        Ellipse(new Point(cx, top + headR), headR, headR, t.NodeFill, t.InkPen);
        Line(new Point(cx, top + headR * 2), new Point(cx, top + 52 * s), t.InkPen);
        Line(new Point(cx - 17 * s, top + 32 * s), new Point(cx + 17 * s, top + 32 * s), t.InkPen);
        Line(new Point(cx, top + 52 * s), new Point(cx - 15 * s, top + 74 * s), t.InkPen);
        Line(new Point(cx, top + 52 * s), new Point(cx + 15 * s, top + 74 * s), t.InkPen);
        Text(n.Name, cx, b.Bottom - fs - 2, t.Ink, fs, false, false, false, "center");
    }

    void Boundary(UmlNode n, Rect b)
    {
        Rect(b, null, t.InkPen);
        Text(n.Name, b.X + b.Width / 2, b.Y + 8, t.Ink, fs, true, false, false, "center");
    }

    void Lifeline(UmlNode n, Rect b)
    {
        var head = new Rect(b.X, b.Y, b.Width, 36);
        Rect(head, FillOf(n), t.InkPen);
        string label = string.IsNullOrWhiteSpace(n.Represents) ? n.Name : n.Name + " : " + n.Represents;
        Text(label, head.X + head.Width / 2, head.Y + 18 - fs / 2, t.Ink, fs, false, false, false, "center");
        double cx = b.X + b.Width / 2;
        dc.DrawLine(t.DashPen, new Point(cx, head.Bottom), new Point(cx, b.Bottom));
        foreach (var a in n.Activations)
        {
            double start = Math.Max(a.Start, head.Bottom), end = Math.Min(a.End, b.Bottom);
            if (end > start) Rect(new Rect(cx - 7, start, 14, end - start), t.NodeFill, t.InkPen);
        }
    }

    void Pentagon(double x, double y, string op)
    {
        double w = Geo.Measure(op, fs - 1, true).Width + 18;
        var pts = new[] { new Point(x, y), new Point(x + w, y), new Point(x + w, y + 14), new Point(x + w - 8, y + 22), new Point(x, y + 22) };
        Poly(pts, t.NodeFill, t.InkPen);
        Text(op, x + 7, y + 3, t.Ink, fs - 1, true);
    }

    void Fragment(UmlNode n, Rect b)
    {
        Rect(b, null, t.InkPen);
        Pentagon(b.X, b.Y, n.Operator);
        double y = b.Y + 26;
        for (int i = 0; i < n.Operands.Count; i++)
        {
            var op = n.Operands[i];
            if (!string.IsNullOrWhiteSpace(op.Guard)) Text("[" + op.Guard + "]", b.X + 10, y + 2, t.Ink, fs - 1, false, true);
            y += op.Height;
            if (i < n.Operands.Count - 1) dc.DrawLine(t.DashPen, new Point(b.X, y), new Point(b.Right, y));
        }
    }

    void Partition(UmlNode n, Rect b)
    {
        Rect(b, null, t.InkPen);
        var lanes = n.Lanes.Count == 0 ? new List<string> { n.Name } : n.Lanes.ToList();
        bool vert = n.Orientation == "Vertical";
        if (vert)
        {
            double lw = b.Width / lanes.Count;
            Line(new Point(b.X, b.Y + 28), new Point(b.Right, b.Y + 28), t.InkPen);
            for (int i = 0; i < lanes.Count; i++)
            {
                Text(lanes[i], b.X + i * lw + lw / 2, b.Y + 6, t.Ink, fs - 1, true, false, false, "center");
                if (i > 0) Line(new Point(b.X + i * lw, b.Y + 28), new Point(b.X + i * lw, b.Bottom), t.InkPen);
            }
        }
        else
        {
            double lh = b.Height / lanes.Count;
            Line(new Point(b.X + 28, b.Y), new Point(b.X + 28, b.Bottom), t.InkPen);
            for (int i = 0; i < lanes.Count; i++)
            {
                Text(lanes[i], b.X + 6, b.Y + i * lh + lh / 2 - fs / 2, t.Ink, fs - 1, true);
                if (i > 0) Line(new Point(b.X + 28, b.Y + i * lh), new Point(b.Right, b.Y + i * lh), t.InkPen);
            }
        }
    }

    void State(UmlNode n, Rect b)
    {
        Shadow(b); Round(b, 12, FillOf(n), t.InkPen);
        var acts = Geo.StateActivities(n);
        double nameH = acts.Count == 0 ? b.Height : 32;
        Text(n.Name, b.X + b.Width / 2, b.Y + nameH / 2 - fs / 2, t.Ink, fs, true, false, false, "center");
        if (acts.Count > 0)
        {
            Line(new Point(b.X, b.Y + 32), new Point(b.Right, b.Y + 32), t.InkPen);
            double y = b.Y + 37;
            foreach (var a in acts) { Text(a, b.X + 10, y, t.Ink, fs - 1); y += fs + 6; }
        }
    }

    void Component(UmlNode n, Rect b)
    {
        Shadow(b); Rect(b, FillOf(n), t.InkPen);
        Text("«component»", b.X + b.Width / 2, b.Y + 8, t.Ink, fs - 1, false, false, false, "center");
        Text(n.Name, b.X + b.Width / 2, b.Y + 8 + fs + 4, t.Ink, fs, true, false, false, "center");
        double ix = b.Right - 26, iy = b.Y + 8;
        Rect(new Rect(ix, iy, 18, 14), t.NodeFill, t.ThinPen);
        Rect(new Rect(ix - 5, iy + 2, 8, 4), t.NodeFill, t.ThinPen);
        Rect(new Rect(ix - 5, iy + 8, 8, 4), t.NodeFill, t.ThinPen);
    }

    void AttachStem(UmlNode n, Rect b, out Point outer)
    {
        outer = Geo.Center(b);
    }

    void Lollipop(UmlNode n, Rect b)
    {
        var c = Geo.Center(b); double r = b.Width / 2;
        if (!string.IsNullOrEmpty(n.AttachParentId))
        {
            Point stem = n.AttachSide switch
            {
                "left" => new Point(b.Right, c.Y), "right" => new Point(b.X, c.Y),
                "top" => new Point(c.X, b.Bottom), _ => new Point(c.X, b.Y)
            };
            Line(stem, c, t.InkPen);
        }
        Ellipse(c, r, r, t.NodeFill, t.InkPen);
        if (!string.IsNullOrWhiteSpace(n.Name)) Text(n.Name, c.X, b.Bottom + 4, t.Ink, fs - 2, false, false, false, "center");
    }

    void Socket(UmlNode n, Rect b)
    {
        var c = Geo.Center(b); double r = b.Width / 2;
        if (!string.IsNullOrEmpty(n.AttachParentId))
        {
            Point stem = n.AttachSide switch
            { "left" => new Point(b.Right, c.Y), "right" => new Point(b.X, c.Y), "top" => new Point(c.X, b.Bottom), _ => new Point(c.X, b.Y) };
            Line(stem, c, t.InkPen);
        }
        var g = new StreamGeometry();
        using (var ctx = g.Open())
        {
            ctx.BeginFigure(new Point(c.X, c.Y - r), false, false);
            ctx.ArcTo(new Point(c.X, c.Y + r), new Size(r, r), 0, false, SweepDirection.Counterclockwise, true, false);
        }
        g.Freeze(); dc.DrawGeometry(null, t.InkPen, g);
        if (!string.IsNullOrWhiteSpace(n.Name)) Text(n.Name, c.X, b.Bottom + 4, t.Ink, fs - 2, false, false, false, "center");
    }

    void Cube(UmlNode n, Rect b)
    {
        double d = 16;
        var front = new Rect(b.X, b.Y + d, b.Width - d, b.Height - d);
        Shadow(front);
        Poly(new[] { new Point(b.X, b.Y + d), new Point(b.X + d, b.Y), new Point(b.Right, b.Y), new Point(b.Right - d, b.Y + d) }, FillOf(n), t.InkPen);
        Poly(new[] { new Point(b.Right - d, b.Y + d), new Point(b.Right, b.Y), new Point(b.Right, b.Bottom - d), new Point(b.Right - d, b.Bottom) }, FillOf(n), t.InkPen);
        Rect(front, FillOf(n), t.InkPen);
        string stereo = n.Kind == NodeKind.DeviceNode ? "«device»" : "«execution environment»";
        Text(stereo, front.X + front.Width / 2, front.Y + 8, t.Ink, fs - 2, false, false, false, "center");
        Text(n.Name, front.X + front.Width / 2, front.Y + 8 + fs + 2, t.Ink, fs, true, false, false, "center");
    }

    void Artifact(UmlNode n, Rect b)
    {
        Shadow(b); Rect(b, FillOf(n), t.InkPen);
        Text("«artifact»", b.X + b.Width / 2, b.Y + 8, t.Ink, fs - 2, false, false, false, "center");
        Text(n.Name, b.X + b.Width / 2, b.Y + 8 + fs + 2, t.Ink, fs, true, false, false, "center");
        double ix = b.Right - 24, iy = b.Y + 7;
        Poly(new[] { new Point(ix, iy), new Point(ix + 10, iy), new Point(ix + 15, iy + 5), new Point(ix + 15, iy + 18), new Point(ix, iy + 18) }, t.NodeFill, t.ThinPen);
        Poly(new[] { new Point(ix + 10, iy), new Point(ix + 10, iy + 5), new Point(ix + 15, iy + 5) }, null, t.ThinPen);
    }

    void Package(UmlNode n, Rect b)
    {
        double tabW = Math.Min(70, b.Width * 0.4), tabH = 18;
        Shadow(new Rect(b.X, b.Y + tabH, b.Width, b.Height - tabH));
        Rect(new Rect(b.X, b.Y, tabW, tabH), FillOf(n), t.InkPen);
        Rect(new Rect(b.X, b.Y + tabH, b.Width, b.Height - tabH), FillOf(n), t.InkPen);
        bool hasContents = st.Contents != null && st.Contents.Contains(n.Id);
        if (hasContents) Text(n.Name, b.X + tabW / 2, b.Y + 2, t.Ink, fs - 2, true, false, false, "center");
        else Text(n.Name, b.X + b.Width / 2, b.Y + tabH + (b.Height - tabH) / 2 - fs / 2, t.Ink, fs, true, false, false, "center");
    }

    void Note(UmlNode n, Rect b)
    {
        double f = 14;
        Shadow(b);
        Poly(new[] { new Point(b.X, b.Y), new Point(b.Right - f, b.Y), new Point(b.Right, b.Y + f), new Point(b.Right, b.Bottom), new Point(b.X, b.Bottom) }, FillOf(n), t.InkPen);
        Poly(new[] { new Point(b.Right - f, b.Y), new Point(b.Right - f, b.Y + f), new Point(b.Right, b.Y + f) }, null, t.InkPen);
        var lines = Geo.Wrap(n.Name, b.Width - 16, fs - 1);
        double y = b.Y + 8;
        foreach (var l in lines) { Text(l, b.X + 8, y, t.Ink, fs - 1); y += fs + 3; }
    }

    // ---------- edges ----------
    public void DrawEdge(UmlEdge e, Diagram d)
    {
        var p = Geo.BuildEdgePath(e, d, fs);
        if (p.Points.Count < 2) return;
        var sNode = d.Node(e.SourceId);
        var tNode = d.Node(e.TargetId);
        bool srcPin = e.Kind == EdgeKind.ObjectFlow && sNode?.Kind == NodeKind.Action;
        bool tgtPin = e.Kind == EdgeKind.ObjectFlow && tNode?.Kind == NodeKind.Action;

        var startTip = srcPin ? Geo.Add(p.StartTip, p.StartDir * 5) : p.StartTip;
        var endTip = tgtPin ? Geo.Add(p.EndTip, p.EndDir * -5) : p.EndTip;

        var pen = p.Dashed ? t.DashPen : t.InkPen;
        double startTrim = Geo.MarkerLen(p.StartMarker), endTrim = Geo.MarkerLen(p.EndMarker);
        var pts = new List<Point>(p.Points);
        pts[0] = Geo.Add(startTip, p.StartDir * startTrim);
        pts[^1] = Geo.Add(endTip, p.EndDir * -endTrim);
        for (int i = 0; i < pts.Count - 1; i++) Line(pts[i], pts[i + 1], pen);

        if (srcPin) Rect(new Rect(p.StartTip.X - 5, p.StartTip.Y - 5, 10, 10), t.NodeFill, t.InkPen);
        if (tgtPin) Rect(new Rect(p.EndTip.X - 5, p.EndTip.Y - 5, 10, 10), t.NodeFill, t.InkPen);

        DrawMarker(p.StartMarker, startTip, p.StartDir);
        DrawMarker(p.EndMarker, endTip, p.EndDir);
        if (e.Kind is EdgeKind.Association or EdgeKind.Aggregation or EdgeKind.Composition)
        {
            if (e.SourceNavigable && e.Kind == EdgeKind.Association) DrawMarker(MarkerKind.Open, startTip, p.StartDir * -1);
            if (e.TargetNavigable) DrawMarker(MarkerKind.Open, endTip, p.EndDir);
        }
        foreach (var l in p.Labels) HaloText(l);
        if (!st.Export && st.Badges.TryGetValue(e.Id, out var sev))
        {
            var mid = new Point((startTip.X + endTip.X) / 2, (startTip.Y + endTip.Y) / 2);
            Ellipse(mid, 5, 5, sev == Severity.Error ? t.Invalid : new SolidColorBrush(Color.FromRgb(255, 178, 36)), new Pen(t.Paper, 1.5));
        }
    }

    void DrawMarker(MarkerKind m, Point tip, Vector dir)
    {
        if (m == MarkerKind.None) return;
        dir = Geo.Norm(dir); var perp = Geo.Perp(dir);
        Point B(double l) => Geo.Add(tip, dir * -l);
        switch (m)
        {
            case MarkerKind.Triangle:
                Poly(new[] { tip, Geo.Add(B(15), perp * 7.5), Geo.Add(B(15), perp * -7.5) }, t.Paper, t.InkPen); break;
            case MarkerKind.FilledTriangle:
                Poly(new[] { tip, Geo.Add(B(12), perp * 6), Geo.Add(B(12), perp * -6) }, t.Ink, null); break;
            case MarkerKind.Open:
                Line(tip, Geo.Add(B(12), perp * 6), t.InkPen);
                Line(tip, Geo.Add(B(12), perp * -6), t.InkPen); break;
            case MarkerKind.DiamondHollow or MarkerKind.DiamondFilled:
                {
                    // tip touches the whole; diamond extends along +dir
                    Point back = Geo.Add(tip, dir * 18); Point m1 = Geo.Add(tip, dir * 9 + perp * 6.5); Point m2 = Geo.Add(tip, dir * 9 + perp * -6.5);
                    Poly(new[] { tip, m1, back, m2 }, m == MarkerKind.DiamondFilled ? t.Ink : t.Paper, t.InkPen);
                    break;
                }
            case MarkerKind.Cross:
                Line(Geo.Add(tip, perp * 6 + dir * -6), Geo.Add(tip, perp * -6 + dir * 6), new Pen(t.Ink, 2));
                Line(Geo.Add(tip, perp * 6 + dir * 6), Geo.Add(tip, perp * -6 + dir * -6), new Pen(t.Ink, 2));
                break;
        }
    }

    // ---------- overlay ----------
    public void DrawOverlay(Diagram d)
    {
        double z = st.Zoom;
        foreach (var id in st.SelNodes)
        {
            var n = d.Node(id); if (n == null) continue;
            var b = Geo.Bounds(n, fs); b.Inflate(4 / z, 4 / z);
            dc.DrawRectangle(null, t.SelPen, b);
            if (st.SelNodes.Count == 1 && !Catalog.IsFixed(n.Kind))
            {
                double hs = 8 / z;
                foreach (var p in Handles(b))
                    dc.DrawRectangle(t.NodeFill, t.SelPen, new Rect(p.X - hs / 2, p.Y - hs / 2, hs, hs));
            }
        }
        foreach (var id in st.SelEdges)
        {
            var e = d.Edge(id); if (e == null) continue;
            var p = Geo.BuildEdgePath(e, d, fs);
            if (p.Points.Count < 2) continue;
            var hl = new Pen(t.Selection, 2.2 / z);
            for (int i = 0; i < p.Points.Count - 1; i++) Line(p.Points[i], p.Points[i + 1], hl);
            double hs = 9 / z;
            dc.DrawEllipse(t.NodeFill, t.SelPen, p.StartTip, hs / 2, hs / 2);
            dc.DrawEllipse(t.NodeFill, t.SelPen, p.EndTip, hs / 2, hs / 2);
            if (e.Routing == Routing.Orthogonal && p.Points.Count == 4)
            {
                var mid = new Point((p.Points[1].X + p.Points[2].X) / 2, (p.Points[1].Y + p.Points[2].Y) / 2);
                dc.DrawRectangle(t.NodeFill, t.SelPen, new Rect(mid.X - hs / 2, mid.Y - hs / 2, hs, hs));
            }
        }
        if (st.HoverNode != null)
        {
            var n = d.Node(st.HoverNode);
            if (n != null)
            {
                var b = Geo.Bounds(n, fs);
                foreach (var p in PortPoints(b)) dc.DrawEllipse(t.Selection, new Pen(t.Paper, 1.4 / z), p, 5 / z, 5 / z);
            }
        }
        foreach (var (a, b) in st.Guides) Line(a, b, t.GuidePen);
        if (st.Marquee is Rect m) dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(28, 62, 123, 250)), t.SelPen, m);
        if (st.PreviewSource != null)
        {
            var a = Geo.Anchor(st.PreviewSource, st.PreviewPoint, fs);
            var pen = st.PreviewValid ? t.PreviewPen : t.BadPen;
            Line(a, st.PreviewPoint, pen);
            var dir = Geo.Norm(st.PreviewPoint - a);
            DrawMarker(Catalog.Edges[st.PreviewEdge].End, st.PreviewPoint, dir);
        }
        if (st.GhostNode is NodeKind gk)
        {
            var def = Catalog.Nodes[gk];
            var r = new Rect(st.GhostPoint.X - def.W / 2, st.GhostPoint.Y - def.H / 2, def.W, def.H);
            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(20, 62, 123, 250)), t.PreviewPen, r);
        }
    }

    public static IEnumerable<Point> Handles(Rect b)
    {
        yield return new Point(b.X, b.Y); yield return new Point(b.X + b.Width / 2, b.Y); yield return new Point(b.Right, b.Y);
        yield return new Point(b.Right, b.Y + b.Height / 2); yield return new Point(b.Right, b.Bottom);
        yield return new Point(b.X + b.Width / 2, b.Bottom); yield return new Point(b.X, b.Bottom); yield return new Point(b.X, b.Y + b.Height / 2);
    }
    public static IEnumerable<Point> PortPoints(Rect b)
    {
        yield return new Point(b.X + b.Width / 2, b.Y); yield return new Point(b.Right, b.Y + b.Height / 2);
        yield return new Point(b.X + b.Width / 2, b.Bottom); yield return new Point(b.X, b.Y + b.Height / 2);
    }
}
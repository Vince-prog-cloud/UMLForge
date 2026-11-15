using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace UmlForge;

public class EdgeLabel { public double X, Y; public string Text; public bool Bold, Italic; public double Size; public string Align = "middle"; public bool Movable = true; }
public class EdgePath
{
    public List<Point> Points = new();
    public MarkerKind StartMarker, EndMarker;
    public Point StartTip, EndTip; public Vector StartDir, EndDir;
    public List<EdgeLabel> Labels = new();
    public bool Dashed;
}

public static class Geo
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

    public static string Vis(Visibility v) => Catalog.VisSymbol(v);

    public static string AttrText(UmlAttribute a)
    {
        var s = $"{Vis(a.Visibility)} {(a.IsDerived ? "/" : "")}{a.Name}";
        if (!string.IsNullOrWhiteSpace(a.Type)) s += " : " + a.Type;
        if (!string.IsNullOrWhiteSpace(a.Multiplicity)) s += $" [{a.Multiplicity}]";
        if (!string.IsNullOrWhiteSpace(a.Default)) s += " = " + a.Default;
        if (a.IsReadOnly) s += " {readOnly}";
        return s;
    }
    public static string OpText(UmlOperation o)
    {
        var s = $"{Vis(o.Visibility)} {o.Name}({o.Parameters})";
        if (!string.IsNullOrWhiteSpace(o.ReturnType)) s += " : " + o.ReturnType;
        return s;
    }

    public class ClassLayout
    {
        public double NameH, AttrH, OpH, TotalH, Width;
        public List<(string text, bool underline, bool italic)> Attrs = new(), Ops = new();
        public string Stereo;
    }

    public static ClassLayout LayoutClassifier(UmlNode n, double fs)
    {
        var L = new ClassLayout();
        double lh = fs + 6, pad = 8;
        L.Stereo = n.Kind switch { NodeKind.Interface => "«interface»", NodeKind.Enumeration => "«enumeration»", NodeKind.DataType => "«dataType»", _ => n.Stereotype };
        if (!string.IsNullOrWhiteSpace(L.Stereo) && !L.Stereo.StartsWith("«")) L.Stereo = "«" + L.Stereo + "»";
        double nameW = Measure(n.Name, fs, true, n.IsAbstract).Width;
        double stereoW = string.IsNullOrEmpty(L.Stereo) ? 0 : Measure(L.Stereo, fs - 1).Width;
        L.NameH = pad * 2 + lh + (string.IsNullOrEmpty(L.Stereo) ? 0 : lh - 2);
        double maxW = Math.Max(nameW, stereoW);

        if (n.Kind == NodeKind.Enumeration)
        {
            foreach (var lit in n.Literals) { L.Attrs.Add((lit, false, false)); maxW = Math.Max(maxW, Measure(lit, fs).Width); }
            L.AttrH = Math.Max(12, n.Literals.Count * lh + pad);
            L.OpH = 12;
        }
        else if (n.Kind == NodeKind.Object)
        {
            foreach (var slot in n.Slots) { L.Attrs.Add((slot, false, false)); maxW = Math.Max(maxW, Measure(slot, fs).Width); }
            L.AttrH = Math.Max(12, n.Slots.Count * lh + pad);
            L.OpH = 0;
        }
        else
        {
            foreach (var a in n.Attributes) { var t = AttrText(a); L.Attrs.Add((t, a.IsStatic, false)); maxW = Math.Max(maxW, Measure(t, fs).Width); }
            foreach (var o in n.Operations) { var t = OpText(o); L.Ops.Add((t, o.IsStatic, o.IsAbstract)); maxW = Math.Max(maxW, Measure(t, fs, false, o.IsAbstract).Width); }
            L.AttrH = Math.Max(12, n.Attributes.Count * lh + pad);
            L.OpH = Math.Max(12, n.Operations.Count * lh + pad);
        }
        L.Width = Math.Max(n.W, maxW + pad * 2 + 6);
        L.TotalH = Math.Max(n.H, L.NameH + L.AttrH + L.OpH);
        return L;
    }

    public static List<string> StateActivities(UmlNode n)
    {
        var list = new List<string>();
        if (!string.IsNullOrWhiteSpace(n.Entry)) list.Add("entry / " + n.Entry);
        if (!string.IsNullOrWhiteSpace(n.Exit)) list.Add("exit / " + n.Exit);
        if (!string.IsNullOrWhiteSpace(n.Do)) list.Add("do / " + n.Do);
        foreach (var i in n.Internals)
        {
            var t = i.Trigger + (string.IsNullOrWhiteSpace(i.Guard) ? "" : $" [{i.Guard}]");
            list.Add(t + " / " + i.Effect);
        }
        return list;
    }

    public static Rect Bounds(UmlNode n, double fs)
    {
        switch (n.Kind)
        {
            case NodeKind.Class or NodeKind.Interface or NodeKind.Enumeration or NodeKind.DataType or NodeKind.Object:
                var l = LayoutClassifier(n, fs); return new Rect(n.X, n.Y, l.Width, l.TotalH);
            case NodeKind.State:
                var acts = StateActivities(n);
                double h = Math.Max(n.H, 34 + (acts.Count == 0 ? 0 : acts.Count * (fs + 6) + 10));
                double w = Math.Max(n.W, Measure(n.Name, fs, true).Width + 28);
                foreach (var a in acts) w = Math.Max(w, Measure(a, fs).Width + 24);
                return new Rect(n.X, n.Y, w, h);
            case NodeKind.Lifeline:
                return new Rect(n.X, n.Y, n.W, n.H);
            default:
                return new Rect(n.X, n.Y, n.W, n.H);
        }
    }

    public static Point Center(Rect r) => new(r.X + r.Width / 2, r.Y + r.Height / 2);

    public static string ShapeOf(NodeKind k) => k switch
    {
        NodeKind.UseCase => "ellipse",
        NodeKind.Initial or NodeKind.StateInitial or NodeKind.ActivityFinal or NodeKind.StateFinal or NodeKind.FlowFinal
            or NodeKind.Junction or NodeKind.HistoryShallow or NodeKind.HistoryDeep or NodeKind.ProvidedInterface => "circle",
        NodeKind.Decision or NodeKind.Choice => "diamond",
        _ => "rect"
    };

    public static Point Anchor(UmlNode n, Point toward, double fs)
    {
        var b = Bounds(n, fs); var c = Center(b);
        double dx = toward.X - c.X, dy = toward.Y - c.Y;
        if (dx == 0 && dy == 0) dx = 1;
        switch (ShapeOf(n.Kind))
        {
            case "ellipse":
                { double rx = b.Width / 2, ry = b.Height / 2; double t = 1 / Math.Sqrt(dx * dx / (rx * rx) + dy * dy / (ry * ry)); return new Point(c.X + dx * t, c.Y + dy * t); }
            case "circle":
                { double r = Math.Min(b.Width, b.Height) / 2; double l = Math.Sqrt(dx * dx + dy * dy); return new Point(c.X + dx / l * r, c.Y + dy / l * r); }
            case "diamond":
                { double rx = b.Width / 2, ry = b.Height / 2; double t = 1 / (Math.Abs(dx) / rx + Math.Abs(dy) / ry); return new Point(c.X + dx * t, c.Y + dy * t); }
            default:
                { double rx = b.Width / 2, ry = b.Height / 2;
                  double t = Math.Min(rx / Math.Max(1e-9, Math.Abs(dx)), ry / Math.Max(1e-9, Math.Abs(dy)));
                  return new Point(c.X + dx * t, c.Y + dy * t); }
        }
    }

    public static Vector Norm(Vector v) { double l = v.Length; return l < 1e-9 ? new Vector(1, 0) : v / l; }
    public static Vector Perp(Vector v) => new(-v.Y, v.X);
    public static Point Add(Point p, Vector v) => new(p.X + v.X, p.Y + v.Y);

    public static double MarkerLen(MarkerKind m) => m switch
    { MarkerKind.Triangle => 15, MarkerKind.FilledTriangle => 12, MarkerKind.DiamondHollow or MarkerKind.DiamondFilled => 18, MarkerKind.Cross => 7, _ => 0 };

    public static Rect ContentBounds(Diagram d, double fs)
    {
        if (d.Nodes.Count == 0) return new Rect(0, 0, 800, 600);
        Rect r = Rect.Empty;
        foreach (var n in d.Nodes) r.Union(Bounds(n, fs));
        foreach (var e in d.Edges) { var p = BuildEdgePath(e, d, fs); foreach (var pt in p.Points) r.Union(new Rect(pt, new Size(1, 1))); }
        r.Inflate(50, 50); return r;
    }

    // ================= Edge routing =================
    public static EdgePath BuildEdgePath(UmlEdge e, Diagram d, double fs)
    {
        var s = d.Node(e.SourceId); var t = d.Node(e.TargetId);
        var def = Catalog.Edges[e.Kind];
        var p = new EdgePath { Dashed = def.Dashed, StartMarker = def.Start, EndMarker = def.End };
        if (s == null || t == null) return p;

        if (d.Kind == DiagramKind.Sequence && e.IsMessage) { BuildMessage(e, s, t, d, fs, p); return p; }

        // Compute automatic offset when multiple edges connect the same two nodes
        var siblings = d.Edges.Where(x => !(d.Kind == DiagramKind.Sequence && x.IsMessage) &&
            ((x.SourceId == s.Id && x.TargetId == t.Id) || (x.SourceId == t.Id && x.TargetId == s.Id))).ToList();
        int idx = siblings.IndexOf(e);
        double autoOffset = siblings.Count > 1 ? (idx - (siblings.Count - 1) / 2.0) * 26.0 : 0.0;
        if (e.SourceId != siblings[0].SourceId) autoOffset = -autoOffset;
        double totalOffset = autoOffset + e.BendOffset;

        if (s == t)
        {
            var b = Bounds(s, fs);
            double spread = idx * 18.0;
            double x1 = b.X + b.Width * 0.65 - spread * 0.4, yTop = b.Y - 36 - spread, xR = b.Right + 36 + spread, yMid = b.Y + b.Height * 0.5 + spread * 0.4;
            p.Points = new() { new(x1, b.Y), new(x1, yTop), new(xR, yTop), new(xR, yMid), new(b.Right, yMid) };
            p.StartTip = p.Points[0]; p.EndTip = p.Points[^1];
            p.StartDir = new Vector(0, -1); p.EndDir = new Vector(-1, 0);
        }
        else if (e.Routing == Routing.Orthogonal)
        {
            var sb = Bounds(s, fs); var tb = Bounds(t, fs);
            var sc = Center(sb); var tc = Center(tb);
            double dx = tc.X - sc.X, dy = tc.Y - sc.Y;
            if (Math.Abs(dx) >= Math.Abs(dy))
            {
                double sx = dx > 0 ? sb.Right : sb.Left, tx = dx > 0 ? tb.Left : tb.Right;
                double mid = (sx + tx) / 2 + totalOffset;
                p.Points = new() { new(sx, sc.Y + autoOffset * 0.35), new(mid, sc.Y + autoOffset * 0.35), new(mid, tc.Y + autoOffset * 0.35), new(tx, tc.Y + autoOffset * 0.35) };
                p.StartDir = new Vector(Math.Sign(dx), 0); p.EndDir = new Vector(Math.Sign(dx), 0);
            }
            else
            {
                double sy = dy > 0 ? sb.Bottom : sb.Top, ty = dy > 0 ? tb.Top : tb.Bottom;
                double mid = (sy + ty) / 2 + totalOffset;
                p.Points = new() { new(sc.X + autoOffset * 0.35, sy), new(sc.X + autoOffset * 0.35, mid), new(tc.X + autoOffset * 0.35, mid), new(tc.X + autoOffset * 0.35, ty) };
                p.StartDir = new Vector(0, Math.Sign(dy)); p.EndDir = new Vector(0, Math.Sign(dy));
            }
            p.StartTip = p.Points[0]; p.EndTip = p.Points[^1];
        }
        else
        {
            var sc = Center(Bounds(s, fs));
            var tc = Center(Bounds(t, fs));
            var baseDir = Norm(tc - sc);
            var perp = Perp(baseDir);
            var shift = perp * totalOffset;

            var a1 = Anchor(s, tc + shift, fs);
            var a2 = Anchor(t, sc + shift, fs);
            if (Math.Abs(totalOffset) > 0.5)
            {
                var midPt = new Point((a1.X + a2.X) / 2 + shift.X, (a1.Y + a2.Y) / 2 + shift.Y);
                a1 = Anchor(s, midPt, fs);
                a2 = Anchor(t, midPt, fs);
                p.Points = new() { a1, midPt, a2 };
                p.StartTip = a1; p.EndTip = a2;
                p.StartDir = Norm(midPt - a1);
                p.EndDir = Norm(a2 - midPt);
            }
            else
            {
                var dir = Norm(a2 - a1);
                p.Points = new() { a1, a2 };
                p.StartTip = a1; p.EndTip = a2; p.StartDir = dir; p.EndDir = dir;
            }
        }
        AddStandardLabels(e, s, t, d, fs, p, def);
        return p;
    }

    static void AddStandardLabels(UmlEdge e, UmlNode s, UmlNode t, Diagram d, double fs, EdgePath p, EdgeDef def)
    {
        bool isSelf = s == t && p.Points.Count >= 4;
        var dir = Norm(p.EndTip - p.StartTip); var perp = Perp(dir);
        if (perp.Y > 0) perp = -perp;
        var mid = isSelf
            ? new Point(p.Points[2].X + 8 + e.LabelDx, p.Points[1].Y + e.LabelDy)
            : new Point((p.StartTip.X + p.EndTip.X) / 2 + e.LabelDx, (p.StartTip.Y + p.EndTip.Y) / 2 + e.LabelDy);
        string align = isSelf ? "start" : "middle";

        if (!string.IsNullOrEmpty(def.Stereo))
            p.Labels.Add(new EdgeLabel { X = mid.X + (isSelf ? 0 : perp.X * 12), Y = mid.Y + (isSelf ? 0 : perp.Y * 12) - fs / 2, Text = "«" + def.Stereo + "»", Size = fs - 1, Align = align });

        if (e.Kind is EdgeKind.Association or EdgeKind.Aggregation or EdgeKind.Composition or EdgeKind.Link or EdgeKind.CommunicationPath or EdgeKind.CommunicationAssociation)
        {
            if (!string.IsNullOrWhiteSpace(e.SourceMultiplicity))
                p.Labels.Add(new EdgeLabel { X = p.StartTip.X + dir.X * 16 + perp.X * 12, Y = p.StartTip.Y + dir.Y * 16 + perp.Y * 12 - fs / 2, Text = e.SourceMultiplicity, Size = fs - 1, Align = "start", Movable = false });
            if (!string.IsNullOrWhiteSpace(e.TargetMultiplicity))
                p.Labels.Add(new EdgeLabel { X = p.EndTip.X - dir.X * 16 + perp.X * 12, Y = p.EndTip.Y - dir.Y * 16 + perp.Y * 12 - fs / 2, Text = e.TargetMultiplicity, Size = fs - 1, Align = "end", Movable = false });
            if (!string.IsNullOrWhiteSpace(e.SourceRole))
                p.Labels.Add(new EdgeLabel { X = p.StartTip.X + dir.X * 34 + perp.X * 13, Y = p.StartTip.Y + dir.Y * 34 + perp.Y * 13 - fs / 2, Text = e.SourceRole, Size = fs - 1, Italic = true, Align = "start", Movable = false });
            if (!string.IsNullOrWhiteSpace(e.TargetRole))
                p.Labels.Add(new EdgeLabel { X = p.EndTip.X - dir.X * 34 + perp.X * 13, Y = p.EndTip.Y - dir.Y * 34 + perp.Y * 13 - fs / 2, Text = e.TargetRole, Size = fs - 1, Italic = true, Align = "end", Movable = false });
            if (!string.IsNullOrWhiteSpace(e.Name))
                p.Labels.Add(new EdgeLabel { X = mid.X + (isSelf ? 0 : perp.X * 11), Y = mid.Y + (isSelf ? 0 : perp.Y * 11) - fs / 2, Text = e.Name + (e.ShowDirection ? " ▶" : ""), Size = fs - 1, Align = align });
        }
        else if (e.Kind == EdgeKind.Transition)
        {
            var txt = TransitionText(e);
            if (!string.IsNullOrEmpty(txt)) p.Labels.Add(new EdgeLabel { X = mid.X, Y = isSelf ? mid.Y : mid.Y - fs - 2, Text = txt, Size = fs - 1, Align = align });
        }
        else if (e.Kind is EdgeKind.ControlFlow or EdgeKind.ObjectFlow)
        {
            if (!string.IsNullOrWhiteSpace(e.Guard)) p.Labels.Add(new EdgeLabel { X = mid.X, Y = mid.Y - fs - 2, Text = "[" + e.Guard + "]", Size = fs - 1, Align = align });
            if (!string.IsNullOrWhiteSpace(e.Name)) p.Labels.Add(new EdgeLabel { X = mid.X, Y = mid.Y + 4, Text = e.Name, Size = fs - 1, Align = align });
        }
        else if (e.Kind == EdgeKind.Extend && !string.IsNullOrWhiteSpace(e.Condition))
            p.Labels.Add(new EdgeLabel { X = mid.X, Y = mid.Y + 6, Text = "(" + e.Condition + ")", Size = fs - 2, Italic = true, Align = align });
        else if (!string.IsNullOrWhiteSpace(e.Name) && string.IsNullOrEmpty(def.Stereo))
            p.Labels.Add(new EdgeLabel { X = mid.X + (isSelf ? 0 : perp.X * 11), Y = mid.Y + (isSelf ? 0 : perp.Y * 11) - fs / 2, Text = e.Name, Size = fs - 1, Align = align });
    }

    public static string TransitionText(UmlEdge e)
    {
        var s = e.Trigger ?? "";
        if (!string.IsNullOrWhiteSpace(e.Guard)) s += (s.Length > 0 ? " " : "") + "[" + e.Guard + "]";
        if (!string.IsNullOrWhiteSpace(e.Effect)) s += " / " + e.Effect;
        return s.Trim();
    }

    public static double ActivationEdge(UmlNode lifeline, double y, int sign)
    {
        double cx = lifeline.X + lifeline.W / 2;
        var act = lifeline.Activations.FirstOrDefault(a => a.Start <= y + 0.5 && a.End >= y - 0.5);
        return act == null ? cx : cx + sign * 7;
    }

    static void BuildMessage(UmlEdge e, UmlNode s, UmlNode t, Diagram d, double fs, EdgePath p)
    {
        double y = e.Y;
        if (e.Kind == EdgeKind.MessageCreate) y = t.Y + 18;
        double scx = s.X + s.W / 2, tcx = t.X + t.W / 2;
        if (s == t)
        {
            double x = scx + 7;
            p.Points = new() { new(x, y), new(x + 46, y), new(x + 46, y + 26), new(x, y + 26) };
            p.StartTip = p.Points[0]; p.EndTip = p.Points[^1];
            p.StartDir = new Vector(1, 0); p.EndDir = new Vector(-1, 0);
        }
        else
        {
            int sign = tcx > scx ? 1 : -1;
            double sx = ActivationEdge(s, y, sign), tx = ActivationEdge(t, y, -sign);
            if (e.Kind == EdgeKind.MessageCreate) tx = tcx - sign * (t.W / 2);
            p.Points = new() { new(sx, y), new(tx, y) };
            p.StartTip = p.Points[0]; p.EndTip = p.Points[^1];
            p.StartDir = new Vector(sign, 0); p.EndDir = new Vector(sign, 0);
        }
        string num = "";
        if (d.ShowSequenceNumbers)
        {
            var ordered = d.Edges.Where(x => x.IsMessage).OrderBy(x => x.Kind == EdgeKind.MessageCreate ? d.Node(x.TargetId)?.Y + 18 ?? x.Y : x.Y).ToList();
            int i = ordered.FindIndex(x => x.Id == e.Id);
            if (i >= 0) num = (i + 1) + ": ";
        }
        var label = num + (string.IsNullOrWhiteSpace(e.Name) ? "message()" : e.Name);
        double mx = (p.StartTip.X + p.EndTip.X) / 2;
        p.Labels.Add(new EdgeLabel { X = mx, Y = y - fs - 4, Text = label, Size = fs - 1, Movable = false });
    }

    public static double DistToSegment(Point p, Point a, Point b)
    {
        var v = b - a; var w = p - a;
        double len2 = v.LengthSquared;
        double t = len2 < 1e-9 ? 0 : Math.Clamp((w.X * v.X + w.Y * v.Y) / len2, 0, 1);
        var proj = a + v * t;
        return (p - proj).Length;
    }

    public static List<string> Wrap(string text, double width, double fs)
    {
        var lines = new List<string>();
        foreach (var para in (text ?? "").Split('\n'))
        {
            var words = para.Split(' '); var cur = "";
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
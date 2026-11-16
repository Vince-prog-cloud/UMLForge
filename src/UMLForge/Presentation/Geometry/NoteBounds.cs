using System.Windows;

namespace UmlForge;

public static class NodeBounds
{
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
        double nameW = TextMetrics.Measure(n.Name, fs, true, n.IsAbstract).Width;
        double stereoW = string.IsNullOrEmpty(L.Stereo) ? 0 : TextMetrics.Measure(L.Stereo, fs - 1).Width;
        L.NameH = pad * 2 + lh + (string.IsNullOrEmpty(L.Stereo) ? 0 : lh - 2);
        double maxW = Math.Max(nameW, stereoW);

        if (n.Kind == NodeKind.Enumeration)
        {
            foreach (var lit in n.Literals) { L.Attrs.Add((lit, false, false)); maxW = Math.Max(maxW, TextMetrics.Measure(lit, fs).Width); }
            L.AttrH = Math.Max(12, n.Literals.Count * lh + pad);
            L.OpH = 12;
        }
        else if (n.Kind == NodeKind.Object)
        {
            foreach (var slot in n.Slots) { L.Attrs.Add((slot, false, false)); maxW = Math.Max(maxW, TextMetrics.Measure(slot, fs).Width); }
            L.AttrH = Math.Max(12, n.Slots.Count * lh + pad);
            L.OpH = 0;
        }
        else
        {
            foreach (var a in n.Attributes) { var t = TextMetrics.AttrText(a); L.Attrs.Add((t, a.IsStatic, false)); maxW = Math.Max(maxW, TextMetrics.Measure(t, fs).Width); }
            foreach (var o in n.Operations) { var t = TextMetrics.OpText(o); L.Ops.Add((t, o.IsStatic, o.IsAbstract)); maxW = Math.Max(maxW, TextMetrics.Measure(t, fs, false, o.IsAbstract).Width); }
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
                var l = LayoutClassifier(n, fs);
                return new Rect(n.X, n.Y, l.Width, l.TotalH);
            case NodeKind.State:
                var acts = StateActivities(n);
                double h = Math.Max(n.H, 34 + (acts.Count == 0 ? 0 : acts.Count * (fs + 6) + 10));
                double w = Math.Max(n.W, TextMetrics.Measure(n.Name, fs, true).Width + 28);
                foreach (var a in acts) w = Math.Max(w, TextMetrics.Measure(a, fs).Width + 24);
                return new Rect(n.X, n.Y, w, h);
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
        var b = Bounds(n, fs);
        var c = Center(b);
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
                {
                    double rx = b.Width / 2, ry = b.Height / 2;
                    double t = Math.Min(rx / Math.Max(1e-9, Math.Abs(dx)), ry / Math.Max(1e-9, Math.Abs(dy)));
                    return new Point(c.X + dx * t, c.Y + dy * t);
                }
        }
    }

    public static Rect ContentBounds(Diagram d, double fs)
    {
        if (d.Nodes.Count == 0) return new Rect(0, 0, 800, 600);
        Rect r = Rect.Empty;
        foreach (var n in d.Nodes) r.Union(Bounds(n, fs));
        foreach (var e in d.Edges) { var p = EdgeRouter.BuildEdgePath(e, d, fs); foreach (var pt in p.Points) r.Union(new Rect(pt, new Size(1, 1))); }
        r.Inflate(50, 50);
        return r;
    }
}
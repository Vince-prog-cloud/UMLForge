using System.Windows;

namespace UmlForge;

public static class CanvasHitTester
{
    public static UmlNode HitNode(Diagram d, Point w, double fontSize)
    {
        if (d == null) return null;
        for (int i = d.Nodes.Count - 1; i >= 0; i--)
        {
            var n = d.Nodes[i];
            if (n.Kind == NodeKind.Lifeline)
            {
                var head = new Rect(n.X, n.Y, n.W, 36);
                double cx = n.X + n.W / 2;
                var stem = new Rect(cx - 14, n.Y, 28, n.H);
                if (head.Contains(w) || stem.Contains(w)) return n;
                continue;
            }
            var b = Geo.Bounds(n, fontSize);
            if (b.Contains(w)) return n;
        }
        return null;
    }

    public static UmlEdge HitEdge(Diagram d, Point w, double zoom, double fontSize)
    {
        if (d == null) return null;
        double tol = 6 / zoom;
        foreach (var e in d.Edges)
        {
            var p = Geo.BuildEdgePath(e, d, fontSize);
            for (int i = 0; i < p.Points.Count - 1; i++)
                if (Geo.DistToSegment(w, p.Points[i], p.Points[i + 1]) < tol) return e;
            if (d.Kind == DiagramKind.Sequence && e.IsMessage)
            {
                foreach (var l in p.Labels)
                {
                    var sz = Geo.Measure(l.Text, l.Size, l.Bold, l.Italic);
                    var r = new Rect(l.X - sz.Width / 2 - 4, l.Y - 2, sz.Width + 8, sz.Height + 4);
                    if (r.Contains(w)) return e;
                }
            }
        }
        return null;
    }

    public static bool HitEdgeLabel(Diagram d, Point w, double zoom, double fontSize, out UmlEdge hitEdge)
    {
        hitEdge = null;
        if (d == null) return false;
        double pad = 4 / zoom;
        for (int i = d.Edges.Count - 1; i >= 0; i--)
        {
            var e = d.Edges[i];
            var p = Geo.BuildEdgePath(e, d, fontSize);
            foreach (var l in p.Labels)
            {
                if (!l.Movable) continue;
                var sz = Geo.Measure(l.Text, l.Size, l.Bold, l.Italic);
                double x = l.Align == "middle" ? l.X - sz.Width / 2 : l.Align == "end" ? l.X - sz.Width : l.X;
                var r = new Rect(x - pad, l.Y - pad, sz.Width + pad * 2, sz.Height + pad * 2);
                if (r.Contains(w))
                {
                    hitEdge = e;
                    return true;
                }
            }
        }
        return false;
    }

    public static string HitHandle(Point w, UmlNode n, double zoom, double fontSize)
    {
        if (n == null || Catalog.IsFixed(n.Kind)) return null;
        var b = Geo.Bounds(n, fontSize);
        b.Inflate(4 / zoom, 4 / zoom);
        double half = 7 / zoom;
        var names = new[] { "nw", "n", "ne", "e", "se", "s", "sw", "w" };
        int i = 0;
        foreach (var h in Renderer.Handles(b))
        {
            if (Math.Abs(w.X - h.X) <= half && Math.Abs(w.Y - h.Y) <= half)
                return names[i];
            i++;
        }
        return null;
    }

    public static string HitPort(Point w, UmlNode n, double zoom, double fontSize)
    {
        if (n == null) return null;
        var b = Geo.Bounds(n, fontSize);
        double tol = 8 / zoom;
        var names = new[] { "top", "right", "bottom", "left" };
        int i = 0;
        foreach (var p in Renderer.PortPoints(b))
        {
            if ((w - p).Length < tol) return names[i];
            i++;
        }
        return null;
    }

    public static bool HitActivationEdge(Diagram d, Point w, double zoom, out UmlNode lifeline, out Activation act, out string side)
    {
        lifeline = null; act = null; side = null;
        if (d == null || d.Kind != DiagramKind.Sequence) return false;
        double tol = 6 / zoom;
        foreach (var n in d.Nodes.Where(n => n.Kind == NodeKind.Lifeline))
        {
            double cx = n.X + n.W / 2;
            if (Math.Abs(w.X - cx) > 16) continue;
            foreach (var a in n.Activations)
            {
                if (Math.Abs(w.Y - a.End) <= tol) { lifeline = n; act = a; side = "bottom"; return true; }
                if (Math.Abs(w.Y - a.Start) <= tol) { lifeline = n; act = a; side = "top"; return true; }
            }
        }
        return false;
    }
}
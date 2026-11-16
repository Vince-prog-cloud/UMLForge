using System.Windows;
using System.Windows.Media;

namespace UmlForge;

public class EdgeRenderer
{
    readonly DrawingPrimitives g;
    readonly RenderState st;
    readonly Theme t;

    public EdgeRenderer(DrawingPrimitives g)
    {
        this.g = g;
        st = g.State;
        t = g.Theme;
    }

    public void Draw(UmlEdge e, Diagram d)
    {
        var p = Geo.BuildEdgePath(e, d, g.Fs);
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
        for (int i = 0; i < pts.Count - 1; i++) g.Line(pts[i], pts[i + 1], pen);

        if (srcPin) g.Rect(new Rect(p.StartTip.X - 5, p.StartTip.Y - 5, 10, 10), t.NodeFill, t.InkPen);
        if (tgtPin) g.Rect(new Rect(p.EndTip.X - 5, p.EndTip.Y - 5, 10, 10), t.NodeFill, t.InkPen);

        DrawMarker(p.StartMarker, startTip, p.StartDir);
        DrawMarker(p.EndMarker, endTip, p.EndDir);
        if (e.Kind is EdgeKind.Association or EdgeKind.Aggregation or EdgeKind.Composition)
        {
            if (e.SourceNavigable && e.Kind == EdgeKind.Association) DrawMarker(MarkerKind.Open, startTip, p.StartDir * -1);
            if (e.TargetNavigable) DrawMarker(MarkerKind.Open, endTip, p.EndDir);
        }
        foreach (var l in p.Labels) g.Text(l.Text, l.X, l.Y, t.Ink, l.Size, l.Bold, l.Italic, false, l.Align);
        if (!st.Export && st.Badges.TryGetValue(e.Id, out var sev))
        {
            var mid = new Point((startTip.X + endTip.X) / 2, (startTip.Y + endTip.Y) / 2);
            g.Ellipse(mid, 5, 5, sev == Severity.Error ? t.Invalid : new SolidColorBrush(Color.FromRgb(255, 178, 36)), new Pen(t.Paper, 1.5));
        }
    }

    public void DrawMarker(MarkerKind m, Point tip, Vector dir)
    {
        if (m == MarkerKind.None) return;
        dir = Geo.Norm(dir);
        var perp = Geo.Perp(dir);
        Point B(double l) => Geo.Add(tip, dir * -l);
        switch (m)
        {
            case MarkerKind.Triangle:
                g.Poly(new[] { tip, Geo.Add(B(15), perp * 7.5), Geo.Add(B(15), perp * -7.5) }, t.Paper, t.InkPen); break;
            case MarkerKind.FilledTriangle:
                g.Poly(new[] { tip, Geo.Add(B(12), perp * 6), Geo.Add(B(12), perp * -6) }, t.Ink, null); break;
            case MarkerKind.Open:
                g.Line(tip, Geo.Add(B(12), perp * 6), t.InkPen);
                g.Line(tip, Geo.Add(B(12), perp * -6), t.InkPen); break;
            case MarkerKind.DiamondHollow or MarkerKind.DiamondFilled:
                Point back = Geo.Add(tip, dir * 18), m1 = Geo.Add(tip, dir * 9 + perp * 6.5), m2 = Geo.Add(tip, dir * 9 + perp * -6.5);
                g.Poly(new[] { tip, m1, back, m2 }, m == MarkerKind.DiamondFilled ? t.Ink : t.Paper, t.InkPen); break;
            case MarkerKind.Cross:
                g.Line(Geo.Add(tip, perp * 6 + dir * -6), Geo.Add(tip, perp * -6 + dir * 6), new Pen(t.Ink, 2));
                g.Line(Geo.Add(tip, perp * 6 + dir * 6), Geo.Add(tip, perp * -6 + dir * -6), new Pen(t.Ink, 2)); break;
        }
    }
}
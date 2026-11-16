using System.Windows;
using System.Windows.Media;

namespace UmlForge;

public class OverlayRenderer
{
    readonly DrawingPrimitives g;
    readonly EdgeRenderer edges;
    readonly RenderState st;
    readonly Theme t;
    readonly double fs;

    public OverlayRenderer(DrawingPrimitives g, EdgeRenderer edges)
    {
        this.g = g;
        this.edges = edges;
        st = g.State;
        t = g.Theme;
        fs = g.Fs;
    }

    public void Draw(Diagram d)
    {
        double z = st.Zoom;
        foreach (var id in st.SelNodes)
        {
            var n = d.Node(id); if (n == null) continue;
            var b = Geo.Bounds(n, fs); b.Inflate(4 / z, 4 / z);
            g.Dc.DrawRectangle(null, t.SelPen, b);
            if (st.SelNodes.Count == 1 && !Catalog.IsFixed(n.Kind))
            {
                double hs = 8 / z;
                foreach (var p in Handles(b))
                    g.Dc.DrawRectangle(t.NodeFill, t.SelPen, new Rect(p.X - hs / 2, p.Y - hs / 2, hs, hs));
            }
        }
        foreach (var id in st.SelEdges)
        {
            var e = d.Edge(id); if (e == null) continue;
            var p = Geo.BuildEdgePath(e, d, fs);
            if (p.Points.Count < 2) continue;
            var hl = new Pen(t.Selection, 2.2 / z);
            for (int i = 0; i < p.Points.Count - 1; i++) g.Line(p.Points[i], p.Points[i + 1], hl);
            double hs = 9 / z;
            g.Dc.DrawEllipse(t.NodeFill, t.SelPen, p.StartTip, hs / 2, hs / 2);
            g.Dc.DrawEllipse(t.NodeFill, t.SelPen, p.EndTip, hs / 2, hs / 2);
            if (e.Routing == Routing.Orthogonal && p.Points.Count == 4)
            {
                var mid = new Point((p.Points[1].X + p.Points[2].X) / 2, (p.Points[1].Y + p.Points[2].Y) / 2);
                g.Dc.DrawRectangle(t.NodeFill, t.SelPen, new Rect(mid.X - hs / 2, mid.Y - hs / 2, hs, hs));
            }
        }
        if (st.PreviewSource != null)
        {
            Point a, bPt;
            if (d.Kind == DiagramKind.Sequence && st.PreviewSource.Kind == NodeKind.Lifeline)
            {
                double y = Math.Max(st.PreviewSource.Y + 42, st.PreviewStart.Y);
                int sign = st.PreviewPoint.X >= (st.PreviewSource.X + st.PreviewSource.W / 2) ? 1 : -1;
                double sx = Geo.ActivationEdge(st.PreviewSource, y, sign);
                a = new Point(sx, y);
                bPt = new Point(st.PreviewPoint.X, y);
            }
            else
            {
                a = Geo.Anchor(st.PreviewSource, st.PreviewPoint, fs);
                bPt = st.PreviewPoint;
            }
            var pen = st.PreviewValid ? t.PreviewPen : t.BadPen;
            g.Line(a, bPt, pen);
            edges.DrawMarker(Catalog.Edges[st.PreviewEdge].End, bPt, Geo.Norm(bPt - a));
        }
        foreach (var (a, b) in st.Guides) g.Line(a, b, t.GuidePen);
        if (st.Marquee is Rect m) g.Dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(28, 62, 123, 250)), t.SelPen, m);
        if (st.GhostNode is NodeKind gk)
        {
            var def = Catalog.Nodes[gk];
            var r = new Rect(st.GhostPoint.X - def.W / 2, st.GhostPoint.Y - def.H / 2, def.W, def.H);
            g.Dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(20, 62, 123, 250)), t.PreviewPen, r);
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

public class Renderer
{
    readonly NodeRenderer nodes;
    readonly EdgeRenderer edges;
    readonly OverlayRenderer overlay;

    public Renderer(DrawingContext dc, RenderState st)
    {
        var primitives = new DrawingPrimitives(dc, st);
        nodes = new NodeRenderer(primitives);
        edges = new EdgeRenderer(primitives);
        overlay = new OverlayRenderer(primitives, edges);
    }

    public void DrawNode(UmlNode n) => nodes.Draw(n);
    public void DrawEdge(UmlEdge e, Diagram d) => edges.Draw(e, d);
    public void DrawOverlay(Diagram d) => overlay.Draw(d);

    public static IEnumerable<Point> Handles(Rect b) => OverlayRenderer.Handles(b);
    public static IEnumerable<Point> PortPoints(Rect b) => OverlayRenderer.PortPoints(b);
}
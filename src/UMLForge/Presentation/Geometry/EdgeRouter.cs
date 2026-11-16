using System.Windows;
using System.Windows.Media;

namespace UmlForge;

public class EdgeLabel
{
    public double X, Y;
    public string Text;
    public bool Bold, Italic;
    public double Size;
    public string Align = "middle";
    public bool Movable = true;
}

public class EdgePath
{
    public List<Point> Points = new();
    public MarkerKind StartMarker, EndMarker;
    public Point StartTip, EndTip;
    public Vector StartDir, EndDir;
    public List<EdgeLabel> Labels = new();
    public bool Dashed;
}

public static class EdgeRouter
{
    public static Vector Norm(Vector v) { double l = v.Length; return l < 1e-9 ? new Vector(1, 0) : v / l; }
    public static Vector Perp(Vector v) => new(-v.Y, v.X);
    public static Point Add(Point p, Vector v) => new(p.X + v.X, p.Y + v.Y);

    public static double MarkerLen(MarkerKind m) => m switch
    {
        MarkerKind.Triangle => 15,
        MarkerKind.FilledTriangle => 12,
        MarkerKind.DiamondHollow or MarkerKind.DiamondFilled => 18,
        MarkerKind.Cross => 7,
        _ => 0
    };

    public static EdgePath BuildEdgePath(UmlEdge e, Diagram d, double fs)
    {
        var s = d.Node(e.SourceId);
        var t = d.Node(e.TargetId);
        var def = Catalog.Edges[e.Kind];
        var p = new EdgePath { Dashed = def.Dashed, StartMarker = def.Start, EndMarker = def.End };
        if (s == null || t == null) return p;

        if (d.Kind == DiagramKind.Sequence && e.IsMessage)
        {
            BuildMessage(e, s, t, d, fs, p);
            return p;
        }

        var siblings = d.Edges.Where(x => !(d.Kind == DiagramKind.Sequence && x.IsMessage) &&
            ((x.SourceId == s.Id && x.TargetId == t.Id) || (x.SourceId == t.Id && x.TargetId == s.Id))).ToList();
        int idx = siblings.IndexOf(e);
        double autoOffset = siblings.Count > 1 ? (idx - (siblings.Count - 1) / 2.0) * 26.0 : 0.0;
        if (e.SourceId != siblings[0].SourceId) autoOffset = -autoOffset;
        double totalOffset = autoOffset + e.BendOffset;

        if (s == t)
        {
            var b = NodeBounds.Bounds(s, fs);
            double spread = idx * 18.0;
            double x1 = b.X + b.Width * 0.65 - spread * 0.4, yTop = b.Y - 36 - spread, xR = b.Right + 36 + spread, yMid = b.Y + b.Height * 0.5 + spread * 0.4;
            p.Points = new() { new(x1, b.Y), new(x1, yTop), new(xR, yTop), new(xR, yMid), new(b.Right, yMid) };
            p.StartTip = p.Points[0]; p.EndTip = p.Points[^1];
            p.StartDir = new Vector(0, -1); p.EndDir = new Vector(-1, 0);
        }
        else if (e.Routing == Routing.Orthogonal)
        {
            var sb = NodeBounds.Bounds(s, fs); var tb = NodeBounds.Bounds(t, fs);
            var sc = NodeBounds.Center(sb); var tc = NodeBounds.Center(tb);
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
            var sc = NodeBounds.Center(NodeBounds.Bounds(s, fs));
            var tc = NodeBounds.Center(NodeBounds.Bounds(t, fs));
            var shift = Perp(Norm(tc - sc)) * totalOffset;

            var a1 = NodeBounds.Anchor(s, tc + shift, fs);
            var a2 = NodeBounds.Anchor(t, sc + shift, fs);
            if (Math.Abs(totalOffset) > 0.5)
            {
                var midPt = new Point((a1.X + a2.X) / 2 + shift.X, (a1.Y + a2.Y) / 2 + shift.Y);
                a1 = NodeBounds.Anchor(s, midPt, fs);
                a2 = NodeBounds.Anchor(t, midPt, fs);
                p.Points = new() { a1, midPt, a2 };
                p.StartTip = a1; p.EndTip = a2;
                p.StartDir = Norm(midPt - a1); p.EndDir = Norm(a2 - midPt);
            }
            else
            {
                var dir = Norm(a2 - a1);
                p.Points = new() { a1, a2 };
                p.StartTip = a1; p.EndTip = a2; p.StartDir = dir; p.EndDir = dir;
            }
        }
        AddStandardLabels(e, s, t, fs, p, def);
        return p;
    }

    static void AddStandardLabels(UmlEdge e, UmlNode s, UmlNode t, double fs, EdgePath p, EdgeDef def)
    {
        bool isSelf = s == t && p.Points.Count >= 4;
        var dir = Norm(p.EndTip - p.StartTip);
        var perp = Perp(dir);
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
            var txt = TextMetrics.TransitionText(e);
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

    public static double ActivationEdge(UmlNode lifeline, double y, int sign, int maxDepth = int.MaxValue)
    {
        double cx = lifeline.X + lifeline.W / 2;
        var act = lifeline.Activations
            .Where(a => a.Depth <= maxDepth && a.Start <= y + 0.5 && a.End >= y - 0.5)
            .OrderByDescending(a => a.Depth)
            .FirstOrDefault();
        if (act == null) return cx;
        return sign > 0 ? cx + 7 + act.Depth * 7 : cx - 7 + act.Depth * 7;
    }

    static void BuildMessage(UmlEdge e, UmlNode s, UmlNode t, Diagram d, double fs, EdgePath p)
    {
        double y = e.Y;
        if (e.Kind == EdgeKind.MessageCreate) y = t.Y + 18;
        double scx = s.X + s.W / 2, tcx = t.X + t.W / 2;

        if (s == t && (e.IsSelfReply || e.Kind == EdgeKind.MessageReply))
        {
            var inner = s.Activations.Where(a => Math.Abs(a.End - y) < 6).OrderByDescending(a => a.Depth).FirstOrDefault();
            int outerDepth = inner != null ? inner.Depth - 1 : 0;
            double targetX = ActivationEdge(s, y, 1, outerDepth);
            double stubStart = targetX + 34;
            p.Points = new() { new(stubStart, y), new(targetX, y) };
            p.StartTip = p.Points[0]; p.EndTip = p.Points[^1];
            p.StartDir = new Vector(-1, 0); p.EndDir = new Vector(-1, 0);
        }
        else if (s == t)
        {
            var inner = s.Activations.FirstOrDefault(a => a.MessageId == e.Id);
            int innerDepth = inner?.Depth ?? 1;
            double startX = ActivationEdge(s, y, 1, innerDepth - 1);
            double endX = ActivationEdge(s, y, 1, innerDepth);
            double loopR = Math.Max(startX, endX) + 34;
            double drop = inner != null ? Math.Min(16, Math.Max(10, (inner.End - inner.Start) * 0.35)) : 14;
            p.Points = new() { new(startX, y), new(loopR, y), new(loopR, y + drop), new(endX, y + drop) };
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
        double mx = s == t ? p.Points[1].X + 6 : (p.StartTip.X + p.EndTip.X) / 2;
        string align = s == t ? "start" : "middle";
        p.Labels.Add(new EdgeLabel { X = mx + e.LabelDx, Y = y - fs - 7 + e.LabelDy, Text = label, Size = fs - 1, Align = align, Movable = true });
    }

    public static double DistToSegment(Point p, Point a, Point b)
    {
        var v = b - a;
        var w = p - a;
        double len2 = v.LengthSquared;
        double t = len2 < 1e-9 ? 0 : Math.Clamp((w.X * v.X + w.Y * v.Y) / len2, 0, 1);
        var proj = a + v * t;
        return (p - proj).Length;
    }
}

public static class Geo
{
    public static Typeface Face => TextMetrics.Face;
    public static double Dpi { get => TextMetrics.Dpi; set => TextMetrics.Dpi = value; }
    public static Size Measure(string s, double size, bool bold = false, bool italic = false) => TextMetrics.Measure(s, size, bold, italic);
    public static string Vis(Visibility v) => Catalog.VisSymbol(v);
    public static string AttrText(UmlAttribute a) => TextMetrics.AttrText(a);
    public static string OpText(UmlOperation o) => TextMetrics.OpText(o);
    public static string TransitionText(UmlEdge e) => TextMetrics.TransitionText(e);
    public static List<string> Wrap(string text, double width, double fs) => TextMetrics.Wrap(text, width, fs);

    public static NodeBounds.ClassLayout LayoutClassifier(UmlNode n, double fs) => NodeBounds.LayoutClassifier(n, fs);
    public static List<string> StateActivities(UmlNode n) => NodeBounds.StateActivities(n);
    public static Rect Bounds(UmlNode n, double fs) => NodeBounds.Bounds(n, fs);
    public static Point Center(Rect r) => NodeBounds.Center(r);
    public static string ShapeOf(NodeKind k) => NodeBounds.ShapeOf(k);
    public static Point Anchor(UmlNode n, Point toward, double fs) => NodeBounds.Anchor(n, toward, fs);
    public static Rect ContentBounds(Diagram d, double fs) => NodeBounds.ContentBounds(d, fs);

    public static Vector Norm(Vector v) => EdgeRouter.Norm(v);
    public static Vector Perp(Vector v) => EdgeRouter.Perp(v);
    public static Point Add(Point p, Vector v) => EdgeRouter.Add(p, v);
    public static double MarkerLen(MarkerKind m) => EdgeRouter.MarkerLen(m);
    public static EdgePath BuildEdgePath(UmlEdge e, Diagram d, double fs) => EdgeRouter.BuildEdgePath(e, d, fs);
    public static double ActivationEdge(UmlNode lifeline, double y, int sign, int maxDepth = int.MaxValue) => EdgeRouter.ActivationEdge(lifeline, y, sign, maxDepth);
    public static double DistToSegment(Point p, Point a, Point b) => EdgeRouter.DistToSegment(p, a, b);
}
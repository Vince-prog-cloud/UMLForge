using System.Windows;
using System.Windows.Media;

namespace UmlForge;

public class NodeRenderer
{
    readonly DrawingPrimitives g;
    readonly RenderState st;
    readonly Theme t;
    readonly double fs;

    public NodeRenderer(DrawingPrimitives g)
    {
        this.g = g;
        st = g.State;
        t = g.Theme;
        fs = g.Fs;
    }

    public void Draw(UmlNode n)
    {
        var b = Geo.Bounds(n, fs);
        switch (n.Kind)
        {
            case NodeKind.Class or NodeKind.Interface or NodeKind.Enumeration or NodeKind.DataType or NodeKind.Object: Classifier(n, b); break;
            case NodeKind.Fork: g.Rect(b, t.Mode == PaperMode.Blueprint ? t.Ink : Brushes.Black, null); break;
            case NodeKind.Actor: Actor(n, b); break;
            case NodeKind.UseCase: g.Ellipse(Geo.Center(b), b.Width / 2, b.Height / 2, g.FillOf(n), t.InkPen); g.CenteredWrap(n.Name, b, fs); break;
            case NodeKind.Boundary: g.Rect(b, null, t.InkPen); g.Text(n.Name, b.X + b.Width / 2, b.Y + 8, t.Ink, fs, true, false, false, "center"); break;
            case NodeKind.Lifeline: Lifeline(n, b); break;
            case NodeKind.Fragment: Fragment(n, b); break;
            case NodeKind.InteractionUse: g.Rect(b, g.FillOf(n), t.InkPen); Pentagon(b.X, b.Y, "ref"); g.Text(n.Name, b.X + b.Width / 2, b.Y + b.Height / 2 - fs / 2, t.Ink, fs, false, false, false, "center"); break;
            case NodeKind.Initial or NodeKind.StateInitial: g.Ellipse(Geo.Center(b), b.Width / 2, b.Height / 2, t.Ink, null); break;
            case NodeKind.ActivityFinal or NodeKind.StateFinal:
                g.Ellipse(Geo.Center(b), b.Width / 2, b.Height / 2, t.NodeFill, new Pen(t.Ink, 2));
                g.Ellipse(Geo.Center(b), b.Width / 2 - 6, b.Height / 2 - 6, t.Ink, null); break;
            case NodeKind.FlowFinal:
                g.Ellipse(Geo.Center(b), b.Width / 2, b.Height / 2, t.NodeFill, new Pen(t.Ink, 1.6));
                g.Line(new Point(b.X + 6, b.Y + 6), new Point(b.Right - 6, b.Bottom - 6), t.InkPen);
                g.Line(new Point(b.Right - 6, b.Y + 6), new Point(b.X + 6, b.Bottom - 6), t.InkPen); break;
            case NodeKind.Action: g.Round(b, 12, g.FillOf(n), t.InkPen); g.CenteredWrap(n.Name, b, fs); break;
            case NodeKind.Decision or NodeKind.Choice:
                g.Poly(new[] { new Point(b.X + b.Width / 2, b.Y), new Point(b.Right, b.Y + b.Height / 2), new Point(b.X + b.Width / 2, b.Bottom), new Point(b.X, b.Y + b.Height / 2) }, g.FillOf(n), t.InkPen);
                if (!string.IsNullOrWhiteSpace(n.Name)) g.Text(n.Name, b.Right + 6, b.Y + b.Height / 2 - fs / 2, t.Ink, fs - 1);
                break;
            case NodeKind.SendSignal:
                g.Poly(new[] { new Point(b.X, b.Y), new Point(b.Right - 14, b.Y), new Point(b.Right, b.Y + b.Height / 2), new Point(b.Right - 14, b.Bottom), new Point(b.X, b.Bottom) }, g.FillOf(n), t.InkPen);
                g.Text(n.Name, b.X + (b.Width - 14) / 2, b.Y + b.Height / 2 - fs / 2, t.Ink, fs, false, false, false, "center"); break;
            case NodeKind.AcceptSignal:
                g.Poly(new[] { new Point(b.X, b.Y), new Point(b.Right, b.Y), new Point(b.Right, b.Bottom), new Point(b.X, b.Bottom), new Point(b.X + 14, b.Y + b.Height / 2) }, g.FillOf(n), t.InkPen);
                g.Text(n.Name, b.X + 14 + (b.Width - 14) / 2, b.Y + b.Height / 2 - fs / 2, t.Ink, fs, false, false, false, "center"); break;
            case NodeKind.TimeEvent:
                g.Poly(new[] { new Point(b.X, b.Y), new Point(b.Right, b.Y), new Point(b.X, b.Bottom), new Point(b.Right, b.Bottom) }, g.FillOf(n), t.InkPen);
                if (!string.IsNullOrWhiteSpace(n.Name)) g.Text(n.Name, b.X + b.Width / 2, b.Bottom + 4, t.Ink, fs - 1, false, false, false, "center");
                break;
            case NodeKind.ObjectNode: g.Rect(b, g.FillOf(n), t.InkPen); g.CenteredWrap(n.Name, b, fs); break;
            case NodeKind.Partition: Partition(n, b); break;
            case NodeKind.State: State(n, b); break;
            case NodeKind.Junction: g.Ellipse(Geo.Center(b), b.Width / 2, b.Height / 2, t.Ink, null); break;
            case NodeKind.Terminate:
                g.Line(new Point(b.X, b.Y), new Point(b.Right, b.Bottom), new Pen(t.Ink, 2.4));
                g.Line(new Point(b.Right, b.Y), new Point(b.X, b.Bottom), new Pen(t.Ink, 2.4)); break;
            case NodeKind.HistoryShallow or NodeKind.HistoryDeep:
                g.Ellipse(Geo.Center(b), b.Width / 2, b.Height / 2, t.NodeFill, t.InkPen);
                g.Text(n.Kind == NodeKind.HistoryShallow ? "H" : "H*", Geo.Center(b).X, Geo.Center(b).Y - fs / 2, t.Ink, fs, true, false, false, "center"); break;
            case NodeKind.Component: Component(n, b); break;
            case NodeKind.Port: g.Rect(b, g.FillOf(n), t.InkPen); if (!string.IsNullOrWhiteSpace(n.Name)) g.Text(n.Name, b.Right + 5, b.Y - 2, t.Ink, fs - 2); break;
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
            g.Ellipse(new Point(b.Right - 2, b.Y + 2), 5, 5, brush, new Pen(t.Paper, 1.5));
        }
    }

    void Classifier(UmlNode n, Rect b)
    {
        var L = Geo.LayoutClassifier(n, fs);
        g.Shadow(b); g.Rect(b, g.FillOf(n), t.InkPen);
        double y = b.Y + 8;
        if (!string.IsNullOrEmpty(L.Stereo)) { g.Text(L.Stereo, b.X + b.Width / 2, y, t.Ink, fs - 1, false, false, false, "center"); y += fs + 4; }
        string name = n.Kind == NodeKind.Object
            ? (string.IsNullOrWhiteSpace(n.Name) ? "" : n.Name) + (string.IsNullOrWhiteSpace(n.Represents) ? "" : " : " + n.Represents)
            : n.Name;
        g.Text(name, b.X + b.Width / 2, y, t.Ink, fs, true, n.IsAbstract, n.Kind == NodeKind.Object, "center");
        double y1 = b.Y + L.NameH; g.Line(new Point(b.X, y1), new Point(b.Right, y1), t.InkPen);
        double lh = fs + 6, ty = y1 + 5;
        foreach (var (text, ul, it) in L.Attrs) { g.Text(text, b.X + 9, ty, t.Ink, fs, false, it, ul); ty += lh; }
        if (L.OpH > 0)
        {
            double y2 = y1 + L.AttrH; g.Line(new Point(b.X, y2), new Point(b.Right, y2), t.InkPen);
            ty = y2 + 5;
            foreach (var (text, ul, it) in L.Ops) { g.Text(text, b.X + 9, ty, t.Ink, fs, false, it, ul); ty += lh; }
        }
    }

    void Actor(UmlNode n, Rect b)
    {
        double cx = b.X + b.Width / 2, s = b.Height / 96, headR = 9 * s, top = b.Y + 2;
        g.Ellipse(new Point(cx, top + headR), headR, headR, t.NodeFill, t.InkPen);
        g.Line(new Point(cx, top + headR * 2), new Point(cx, top + 52 * s), t.InkPen);
        g.Line(new Point(cx - 17 * s, top + 32 * s), new Point(cx + 17 * s, top + 32 * s), t.InkPen);
        g.Line(new Point(cx, top + 52 * s), new Point(cx - 15 * s, top + 74 * s), t.InkPen);
        g.Line(new Point(cx, top + 52 * s), new Point(cx + 15 * s, top + 74 * s), t.InkPen);
        g.Text(n.Name, cx, b.Bottom - fs - 2, t.Ink, fs, false, false, false, "center");
    }

    void Lifeline(UmlNode n, Rect b)
    {
        var head = new Rect(b.X, b.Y, b.Width, 36);
        g.Rect(head, g.FillOf(n), t.InkPen);
        string label = string.IsNullOrWhiteSpace(n.Represents) ? n.Name : n.Name + " : " + n.Represents;
        g.Text(label, head.X + head.Width / 2, head.Y + 18 - fs / 2, t.Ink, fs, false, false, false, "center");
        double cx = b.X + b.Width / 2;
        g.Line(new Point(cx, head.Bottom), new Point(cx, b.Bottom), t.DashPen);
        foreach (var a in n.Activations.OrderBy(a => a.Depth))
        {
            double start = Math.Max(a.Start, head.Bottom), end = Math.Min(a.End, b.Bottom);
            double x = cx - 7 + a.Depth * 7;
            if (end > start) g.Rect(new Rect(x, start, 14, end - start), t.NodeFill, t.InkPen);
        }
    }

    void Pentagon(double x, double y, string op)
    {
        double w = Geo.Measure(op, fs - 1, true).Width + 18;
        var pts = new[] { new Point(x, y), new Point(x + w, y), new Point(x + w, y + 14), new Point(x + w - 8, y + 22), new Point(x, y + 22) };
        g.Poly(pts, t.NodeFill, t.InkPen);
        g.Text(op, x + 7, y + 3, t.Ink, fs - 1, true);
    }

    void Fragment(UmlNode n, Rect b)
    {
        g.Rect(b, null, t.InkPen);
        Pentagon(b.X, b.Y, n.Operator);
        double y = b.Y + 26;
        for (int i = 0; i < n.Operands.Count; i++)
        {
            var op = n.Operands[i];
            if (!string.IsNullOrWhiteSpace(op.Guard)) g.Text("[" + op.Guard + "]", b.X + 10, y + 2, t.Ink, fs - 1, false, true);
            y += op.Height;
            if (i < n.Operands.Count - 1) g.Line(new Point(b.X, y), new Point(b.Right, y), t.DashPen);
        }
    }

    void Partition(UmlNode n, Rect b)
    {
        g.Rect(b, null, t.InkPen);
        var lanes = n.Lanes.Count == 0 ? new List<string> { n.Name } : n.Lanes.ToList();
        if (n.Orientation == "Vertical")
        {
            double lw = b.Width / lanes.Count;
            g.Line(new Point(b.X, b.Y + 28), new Point(b.Right, b.Y + 28), t.InkPen);
            for (int i = 0; i < lanes.Count; i++)
            {
                g.Text(lanes[i], b.X + i * lw + lw / 2, b.Y + 6, t.Ink, fs - 1, true, false, false, "center");
                if (i > 0) g.Line(new Point(b.X + i * lw, b.Y + 28), new Point(b.X + i * lw, b.Bottom), t.InkPen);
            }
        }
        else
        {
            double lh = b.Height / lanes.Count;
            g.Line(new Point(b.X + 28, b.Y), new Point(b.X + 28, b.Bottom), t.InkPen);
            for (int i = 0; i < lanes.Count; i++)
            {
                g.Text(lanes[i], b.X + 6, b.Y + i * lh + lh / 2 - fs / 2, t.Ink, fs - 1, true);
                if (i > 0) g.Line(new Point(b.X + 28, b.Y + i * lh), new Point(b.Right, b.Y + i * lh), t.InkPen);
            }
        }
    }

    void State(UmlNode n, Rect b)
    {
        g.Shadow(b); g.Round(b, 12, g.FillOf(n), t.InkPen);
        var acts = Geo.StateActivities(n);
        double nameH = acts.Count == 0 ? b.Height : 32;
        g.Text(n.Name, b.X + b.Width / 2, b.Y + nameH / 2 - fs / 2, t.Ink, fs, true, false, false, "center");
        if (acts.Count > 0)
        {
            g.Line(new Point(b.X, b.Y + 32), new Point(b.Right, b.Y + 32), t.InkPen);
            double y = b.Y + 37;
            foreach (var a in acts) { g.Text(a, b.X + 10, y, t.Ink, fs - 1); y += fs + 6; }
        }
    }

    void Component(UmlNode n, Rect b)
    {
        g.Shadow(b); g.Rect(b, g.FillOf(n), t.InkPen);
        g.Text("«component»", b.X + b.Width / 2, b.Y + 8, t.Ink, fs - 1, false, false, false, "center");
        g.Text(n.Name, b.X + b.Width / 2, b.Y + 8 + fs + 4, t.Ink, fs, true, false, false, "center");
        double ix = b.Right - 26, iy = b.Y + 8;
        g.Rect(new Rect(ix, iy, 18, 14), t.NodeFill, t.ThinPen);
        g.Rect(new Rect(ix - 5, iy + 2, 8, 4), t.NodeFill, t.ThinPen);
        g.Rect(new Rect(ix - 5, iy + 8, 8, 4), t.NodeFill, t.ThinPen);
    }

    void Lollipop(UmlNode n, Rect b)
    {
        var c = Geo.Center(b); double r = b.Width / 2;
        if (!string.IsNullOrEmpty(n.AttachParentId))
        {
            Point stem = n.AttachSide switch { "left" => new Point(b.Right, c.Y), "right" => new Point(b.X, c.Y), "top" => new Point(c.X, b.Bottom), _ => new Point(c.X, b.Y) };
            g.Line(stem, c, t.InkPen);
        }
        g.Ellipse(c, r, r, t.NodeFill, t.InkPen);
        if (!string.IsNullOrWhiteSpace(n.Name)) g.Text(n.Name, c.X, b.Bottom + 4, t.Ink, fs - 2, false, false, false, "center");
    }

    void Socket(UmlNode n, Rect b)
    {
        var c = Geo.Center(b); double r = b.Width / 2;
        if (!string.IsNullOrEmpty(n.AttachParentId))
        {
            Point stem = n.AttachSide switch { "left" => new Point(b.Right, c.Y), "right" => new Point(b.X, c.Y), "top" => new Point(c.X, b.Bottom), _ => new Point(c.X, b.Y) };
            g.Line(stem, c, t.InkPen);
        }
        var sg = new StreamGeometry();
        using (var ctx = sg.Open())
        {
            ctx.BeginFigure(new Point(c.X, c.Y - r), false, false);
            ctx.ArcTo(new Point(c.X, c.Y + r), new Size(r, r), 0, false, SweepDirection.Counterclockwise, true, false);
        }
        sg.Freeze(); g.Dc.DrawGeometry(null, t.InkPen, sg);
        if (!string.IsNullOrWhiteSpace(n.Name)) g.Text(n.Name, c.X, b.Bottom + 4, t.Ink, fs - 2, false, false, false, "center");
    }

    void Cube(UmlNode n, Rect b)
    {
        double d = 16;
        var front = new Rect(b.X, b.Y + d, b.Width - d, b.Height - d);
        g.Shadow(front);
        g.Poly(new[] { new Point(b.X, b.Y + d), new Point(b.X + d, b.Y), new Point(b.Right, b.Y), new Point(b.Right - d, b.Y + d) }, g.FillOf(n), t.InkPen);
        g.Poly(new[] { new Point(b.Right - d, b.Y + d), new Point(b.Right, b.Y), new Point(b.Right, b.Bottom - d), new Point(b.Right - d, b.Bottom) }, g.FillOf(n), t.InkPen);
        g.Rect(front, g.FillOf(n), t.InkPen);
        string stereo = n.Kind == NodeKind.DeviceNode ? "«device»" : "«execution environment»";
        g.Text(stereo, front.X + front.Width / 2, front.Y + 8, t.Ink, fs - 2, false, false, false, "center");
        g.Text(n.Name, front.X + front.Width / 2, front.Y + 8 + fs + 2, t.Ink, fs, true, false, false, "center");
    }

    void Artifact(UmlNode n, Rect b)
    {
        g.Shadow(b); g.Rect(b, g.FillOf(n), t.InkPen);
        g.Text("«artifact»", b.X + b.Width / 2, b.Y + 8, t.Ink, fs - 2, false, false, false, "center");
        g.Text(n.Name, b.X + b.Width / 2, b.Y + 8 + fs + 2, t.Ink, fs, true, false, false, "center");
        double ix = b.Right - 24, iy = b.Y + 7;
        g.Poly(new[] { new Point(ix, iy), new Point(ix + 10, iy), new Point(ix + 15, iy + 5), new Point(ix + 15, iy + 18), new Point(ix, iy + 18) }, t.NodeFill, t.ThinPen);
        g.Poly(new[] { new Point(ix + 10, iy), new Point(ix + 10, iy + 5), new Point(ix + 15, iy + 5) }, null, t.ThinPen);
    }

    void Package(UmlNode n, Rect b)
    {
        double tabW = Math.Min(70, b.Width * 0.4), tabH = 18;
        g.Shadow(new Rect(b.X, b.Y + tabH, b.Width, b.Height - tabH));
        g.Rect(new Rect(b.X, b.Y, tabW, tabH), g.FillOf(n), t.InkPen);
        g.Rect(new Rect(b.X, b.Y + tabH, b.Width, b.Height - tabH), g.FillOf(n), t.InkPen);
        bool hasContents = st.Contents != null && st.Contents.Contains(n.Id);
        if (hasContents) g.Text(n.Name, b.X + tabW / 2, b.Y + 2, t.Ink, fs - 2, true, false, false, "center");
        else g.Text(n.Name, b.X + b.Width / 2, b.Y + tabH + (b.Height - tabH) / 2 - fs / 2, t.Ink, fs, true, false, false, "center");
    }

    void Note(UmlNode n, Rect b)
    {
        double f = 14;
        g.Shadow(b);
        g.Poly(new[] { new Point(b.X, b.Y), new Point(b.Right - f, b.Y), new Point(b.Right, b.Y + f), new Point(b.Right, b.Bottom), new Point(b.X, b.Bottom) }, g.FillOf(n), t.InkPen);
        g.Poly(new[] { new Point(b.Right - f, b.Y), new Point(b.Right - f, b.Y + f), new Point(b.Right, b.Y + f) }, null, t.InkPen);
        var lines = Geo.Wrap(n.Name, b.Width - 16, fs - 1);
        double y = b.Y + 8;
        foreach (var l in lines) { g.Text(l, b.X + 8, y, t.Ink, fs - 1); y += fs + 3; }
    }
}
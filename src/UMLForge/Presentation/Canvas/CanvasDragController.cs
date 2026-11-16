using System.Windows;
using System.Windows.Input;

namespace UmlForge;

public class DragState
{
    public Activation Act;
    public string Mode;
    public Point StartWorld, StartScreen;
    public UmlNode Node;
    public UmlEdge Edge;
    public string Handle;
    public Dictionary<string, (double x, double y)> Orig = new();
    public Dictionary<string, double> OrigYMap = new();
    public List<UmlNode> Contained = new();
    public List<UmlEdge> ContainedMsgs = new();
    public List<(Point a, Point b)> Guides = new();
    public bool Moved;
    public double OrigBend, OrigLdx, OrigLdy, OrigY, OrigW, OrigH;
    public int SepIndex;
    public UmlNode ReconnectTarget;
    public EdgeKind ConnectKind;
    public Point PreviewPoint;
    public bool PreviewValid;
    public Rect? Marquee;
}

public class CanvasDragController
{
    readonly Editor ed;
    readonly DiagramCanvas canvas;
    public DragState Current { get; private set; }

    public CanvasDragController(Editor ed, DiagramCanvas canvas)
    {
        this.ed = ed;
        this.canvas = canvas;
    }

    public bool IsActive => Current != null;
    public bool IsConnecting => Current?.Mode == "connect";

    public void StartPan(Point screen) => Current = new DragState { Mode = "pan", StartScreen = screen };

    public void StartConnect(UmlNode source, EdgeKind kind, Point w)
    {
        Current = new DragState { Mode = "connect", Node = source, ConnectKind = kind, StartWorld = w, PreviewPoint = w };
        canvas.Hint(Catalog.Edges[kind].Hint);
    }

    public void Cancel()
    {
        Current = null;
        canvas.Hint("");
        canvas.InvalidateVisual();
    }

    public bool TryCompleteConnect(UmlNode target)
    {
        if (!IsConnecting) return false;
        if (target != null)
        {
            var (ok, reason) = ed.TryConnect(Current.ConnectKind, Current.Node, target, Current.StartWorld.Y);
            if (!ok) canvas.Hint(reason);
        }
        Current = null;
        canvas.Hint("");
        if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) && ed.Tool.Mode == ToolMode.Connect)
            ed.SetTool(ToolSpec.Select);
        canvas.InvalidateVisual();
        return true;
    }

    public void BeginResize(UmlNode sel, string handle, Point w)
    {
        var b = Geo.Bounds(sel, canvas.Settings.FontSize);
        ed.Begin();
        Current = new DragState { Mode = "resize", Node = sel, Handle = handle, StartWorld = w, OrigW = b.Width, OrigH = b.Height };
        Current.Orig[sel.Id] = (sel.X, sel.Y);
    }

    public void BeginReconnect(UmlEdge edge, string handle, Point w)
    {
        ed.Begin();
        Current = new DragState { Mode = "reconnect", Edge = edge, Handle = handle, StartWorld = w };
    }

    public void BeginBend(UmlEdge edge, Point w)
    {
        ed.Begin();
        Current = new DragState { Mode = "bend", Edge = edge, StartWorld = w, OrigBend = edge.BendOffset };
    }

    public void BeginLabel(UmlEdge edge, Point w)
    {
        ed.Begin();
        Current = new DragState { Mode = "label", Edge = edge, StartWorld = w, OrigLdx = edge.LabelDx, OrigLdy = edge.LabelDy };
    }

    public void BeginActivation(UmlNode lifeline, Activation act, string side, Point w)
    {
        ed.Begin();
        Current = new DragState { Mode = "activation", Node = lifeline, Act = act, Handle = side, StartWorld = w, OrigY = side == "top" ? act.Start : act.End };
    }

    public void BeginAttach(UmlNode node, Point w)
    {
        ed.Begin();
        Current = new DragState { Mode = "attach", Node = node, StartWorld = w };
        Current.Orig[node.Id] = (node.X, node.Y);
    }

    public void BeginSeparator(UmlNode node, int sepIndex, Point w)
    {
        ed.Begin();
        Current = new DragState { Mode = "sep", Node = node, SepIndex = sepIndex, StartWorld = w, OrigH = node.Operands[sepIndex].Height };
    }

    public void BeginMessage(UmlEdge edge, Point w)
    {
        double startY = edge.Kind == EdgeKind.MessageCreate
            ? (ed.Current.Node(edge.TargetId)?.Y + 18 ?? edge.Y)
            : edge.Y;
        ed.Begin();
        Current = new DragState { Mode = "message", Edge = edge, StartWorld = w, OrigY = startY };
    }

    public void BeginMarquee(Point w) => Current = new DragState { Mode = "marquee", StartWorld = w };

    public void BeginMove(Point w)
    {
        ed.Begin();
        Current = new DragState { Mode = "move", StartWorld = w };
        foreach (var id in ed.SelNodes)
        {
            var n = ed.Current.Node(id);
            if (n == null) continue;
            Current.Orig[id] = (n.X, n.Y);
            if (Catalog.IsContainer(n.Kind) && n.Kind != NodeKind.Fragment)
            {
                var b = Geo.Bounds(n, canvas.Settings.FontSize);
                foreach (var c in ed.Current.Nodes)
                {
                    if (c == n || ed.SelNodes.Contains(c.Id) || !string.IsNullOrEmpty(c.AttachParentId)) continue;
                    if (b.Contains(new Point(c.X + c.W / 2, c.Y + c.H / 2)))
                    {
                        Current.Contained.Add(c);
                        Current.Orig[c.Id] = (c.X, c.Y);
                    }
                }
            }
        }
        foreach (var m in Current.ContainedMsgs) Current.OrigYMap[m.Id] = m.Y;
    }

    public void OnMove(Point screen, Point w)
    {
        if (Current == null) return;
        double zoom = canvas.Zoom;
        var settings = canvas.Settings;

        if (Current.Mode != "connect" || (w - Current.StartWorld).Length > 6 / zoom)
            Current.Moved = true;

        switch (Current.Mode)
        {
            case "pan":
                canvas.Pan(screen.X - Current.StartScreen.X, screen.Y - Current.StartScreen.Y);
                Current.StartScreen = screen;
                break;
            case "move":
                HandleMove(w, zoom, settings);
                break;
            case "resize":
                HandleResize(w);
                break;
            case "connect":
                {
                    Current.PreviewPoint = w;
                    var target = CanvasHitTester.HitNode(ed.Current, w, settings.FontSize);
                    Current.ReconnectTarget = target;
                    var (ok, reason) = target != null ? Catalog.CanConnect(Current.ConnectKind, Current.Node, target, ed.Current.Kind) : (false, "");
                    Current.PreviewValid = ok;
                    canvas.Hint(target != null && !ok ? reason : Catalog.Edges[Current.ConnectKind].Hint);
                    break;
                }
            case "reconnect":
                {
                    Current.PreviewPoint = w;
                    var target = CanvasHitTester.HitNode(ed.Current, w, settings.FontSize);
                    Current.ReconnectTarget = target;
                    if (target != null)
                    {
                        var s = Current.Handle == "start" ? target : ed.Current.Node(Current.Edge.SourceId);
                        var t = Current.Handle == "end" ? target : ed.Current.Node(Current.Edge.TargetId);
                        var (ok, reason) = Catalog.CanConnect(Current.Edge.Kind, s, t, ed.Current.Kind);
                        Current.PreviewValid = ok;
                        canvas.Hint(ok ? "" : reason);
                    }
                    else
                    {
                        Current.PreviewValid = false;
                        canvas.Hint("Drop on a valid element to reconnect.");
                    }
                    break;
                }
            case "marquee":
                {
                    var a = Current.StartWorld;
                    Current.Marquee = new Rect(Math.Min(a.X, w.X), Math.Min(a.Y, w.Y), Math.Abs(w.X - a.X), Math.Abs(w.Y - a.Y));
                    break;
                }
            case "bend":
                {
                    var d = w - Current.StartWorld;
                    Current.Edge.BendOffset = Current.OrigBend + (Math.Abs(d.X) > Math.Abs(d.Y) ? d.X : d.Y);
                    break;
                }
            case "label":
                {
                    var d = w - Current.StartWorld;
                    Current.Edge.LabelDx = Current.OrigLdx + d.X;
                    Current.Edge.LabelDy = Current.OrigLdy + d.Y;
                    break;
                }
            case "message":
                HandleMessageMove(w);
                break;
            case "sep":
                Current.Node.Operands[Current.SepIndex].Height = Math.Max(34, Current.OrigH + (w.Y - Current.StartWorld.Y));
                break;
            case "attach":
                {
                    var n = Current.Node;
                    n.X = Current.Orig[n.Id].x + (w.X - Current.StartWorld.X);
                    n.Y = Current.Orig[n.Id].y + (w.Y - Current.StartWorld.Y);
                    n.AttachParentId = null;
                    break;
                }
            case "activation":
                {
                    double ny = Current.OrigY + (w.Y - Current.StartWorld.Y);
                    if (Current.Handle == "bottom")
                    {
                        Current.Act.End = Math.Max(Current.Act.Start + 14, ny);
                        var replyMsg = ed.Current.Edges.FirstOrDefault(x => x.Kind == EdgeKind.MessageReply && x.SourceId == Current.Node.Id && Math.Abs(x.Y - Current.OrigY) < 6);
                        if (replyMsg != null) replyMsg.Y = Current.Act.End;
                    }
                    else
                    {
                        Current.Act.Start = Math.Min(Current.Act.End - 14, ny);
                    }
                    break;
                }
        }
        canvas.InvalidateVisual();
    }

    void HandleMove(Point w, double zoom, AppSettings settings)
    {
        var delta = w - Current.StartWorld;
        double dx = delta.X, dy = delta.Y;
        Current.Guides.Clear();
        var primary = Current.Orig.Keys.FirstOrDefault(id => ed.SelNodes.Contains(id));
        if (primary != null)
        {
            var n = ed.Current.Node(primary);
            var b = Geo.Bounds(n, settings.FontSize);
            double nx = Current.Orig[primary].x + dx, ny = Current.Orig[primary].y + dy;
            if (settings.Snap)
            {
                nx = Math.Round(nx / settings.GridSize) * settings.GridSize;
                ny = Math.Round(ny / settings.GridSize) * settings.GridSize;
                dx = nx - Current.Orig[primary].x;
                dy = ny - Current.Orig[primary].y;
            }
            if (settings.Guides)
            {
                double tol = 6 / zoom, bestDx = tol, bestDy = tol;
                double? snapX = null, snapY = null;
                var mineX = new[] { nx, nx + b.Width / 2, nx + b.Width };
                var mineY = new[] { ny, ny + b.Height / 2, ny + b.Height };

                foreach (var other in ed.Current.Nodes)
                {
                    if (Current.Orig.ContainsKey(other.Id)) continue;
                    var ob = Geo.Bounds(other, settings.FontSize);
                    var ox = new[] { ob.X, ob.X + ob.Width / 2, ob.Right };
                    var oy = new[] { ob.Y, ob.Y + ob.Height / 2, ob.Bottom };
                    foreach (var mx in mineX) foreach (var x in ox)
                    {
                        double diff = x - mx;
                        if (Math.Abs(diff) < Math.Abs(bestDx)) { bestDx = diff; snapX = x; }
                    }
                    foreach (var my in mineY) foreach (var y in oy)
                    {
                        double diff = y - my;
                        if (Math.Abs(diff) < Math.Abs(bestDy)) { bestDy = diff; snapY = y; }
                    }
                }
                if (snapX is double sx)
                {
                    dx += bestDx; nx += bestDx;
                    double minY = ny, maxY = ny + b.Height;
                    foreach (var other in ed.Current.Nodes.Where(o => !Current.Orig.ContainsKey(o.Id)))
                    {
                        var ob = Geo.Bounds(other, settings.FontSize);
                        if (Math.Abs(ob.X - sx) < 0.5 || Math.Abs(ob.X + ob.Width / 2 - sx) < 0.5 || Math.Abs(ob.Right - sx) < 0.5)
                        { minY = Math.Min(minY, ob.Y); maxY = Math.Max(maxY, ob.Bottom); }
                    }
                    Current.Guides.Add((new Point(sx, minY - 24), new Point(sx, maxY + 24)));
                }
                if (snapY is double sy)
                {
                    dy += bestDy; ny += bestDy;
                    double minX = nx, maxX = nx + b.Width;
                    foreach (var other in ed.Current.Nodes.Where(o => !Current.Orig.ContainsKey(o.Id)))
                    {
                        var ob = Geo.Bounds(other, settings.FontSize);
                        if (Math.Abs(ob.Y - sy) < 0.5 || Math.Abs(ob.Y + ob.Height / 2 - sy) < 0.5 || Math.Abs(ob.Bottom - sy) < 0.5)
                        { minX = Math.Min(minX, ob.X); maxX = Math.Max(maxX, ob.Right); }
                    }
                    Current.Guides.Add((new Point(minX - 24, sy), new Point(maxX + 24, sy)));
                }
            }
        }
        foreach (var (id, (ox, oy)) in Current.Orig)
        {
            var n = ed.Current.Node(id);
            if (n != null) { n.X = ox + dx; n.Y = oy + dy; }
        }
        foreach (var m in Current.ContainedMsgs) m.Y = Current.OrigYMap[m.Id] + dy;
    }

    void HandleResize(Point w)
    {
        var n = Current.Node;
        var d = w - Current.StartWorld;
        double x = Current.Orig[n.Id].x, y = Current.Orig[n.Id].y, ww = Current.OrigW, hh = Current.OrigH;
        if (Current.Handle.Contains('e')) ww = Math.Max(40, Current.OrigW + d.X);
        if (Current.Handle.Contains('s')) hh = Math.Max(24, Current.OrigH + d.Y);
        if (Current.Handle.Contains('w')) { ww = Math.Max(40, Current.OrigW - d.X); x = Current.Orig[n.Id].x + (Current.OrigW - ww); }
        if (Current.Handle.Contains('n')) { hh = Math.Max(24, Current.OrigH - d.Y); y = Current.Orig[n.Id].y + (Current.OrigH - hh); }
        n.X = x; n.Y = y; n.W = ww; n.H = hh;
    }

    void HandleMessageMove(Point w)
    {
        var edge = Current.Edge;
        var s = ed.Current.Node(edge.SourceId);
        var t = ed.Current.Node(edge.TargetId);
        if (edge.Kind == EdgeKind.MessageCreate)
        {
            double ny = Math.Max(40, Current.OrigY + (w.Y - Current.StartWorld.Y));
            edge.Y = ny;
            t.Y = ny - 18;
            return;
        }
        double minY = Math.Max(s.Y + 36, t.Y + 36) + 10;
        double maxY = Math.Min(s.Y + s.H, t.Y + t.H) - 10;

        if (edge.Kind == EdgeKind.MessageReply)
        {
            var sa = s.Activations.Where(a => a.Start <= Current.OrigY + 2).OrderByDescending(a => a.Start).FirstOrDefault();
            if (sa != null) minY = Math.Max(minY, sa.Start + 16);
            edge.Y = Math.Clamp(Current.OrigY + (w.Y - Current.StartWorld.Y), minY, Math.Max(minY, maxY));
            if (sa != null) sa.End = edge.Y;
        }
        else
        {
            double newY = Math.Clamp(Current.OrigY + (w.Y - Current.StartWorld.Y), minY, Math.Max(minY, maxY));
            edge.Y = newY;
            var act = t.Activations.FirstOrDefault(a => a.MessageId == edge.Id);
            if (act != null)
            {
                double prevEnd = act.End;
                act.Start = newY;
                if (act.End < newY + 16)
                {
                    act.End = newY + 16;
                    var reply = ed.Current.Edges.FirstOrDefault(x => x.Kind == EdgeKind.MessageReply && x.SourceId == t.Id && Math.Abs(x.Y - prevEnd) < 6);
                    if (reply != null) reply.Y = act.End;
                }
            }
        }
    }

    public void OnUp(Point w)
    {
        if (Current == null) return;
        switch (Current.Mode)
        {
            case "move":
            case "resize":
            case "bend":
            case "label":
            case "message":
            case "sep":
            case "activation":
                if (Current.Moved) ed.Commit("Edit"); else ed.CancelPending();
                break;
            case "attach":
                {
                    var n = Current.Node;
                    var center = new Point(n.X + n.W / 2, n.Y + n.H / 2);
                    var parent = ed.Current.Nodes.LastOrDefault(p => p.Kind is NodeKind.Component or NodeKind.DeviceNode or NodeKind.ExecEnvNode
                        && Geo.Bounds(p, canvas.Settings.FontSize).Contains(center));
                    if (parent != null)
                    {
                        var b = Geo.Bounds(parent, canvas.Settings.FontSize);
                        double dl = Math.Abs(center.X - b.X), dr = Math.Abs(center.X - b.Right), dt = Math.Abs(center.Y - b.Y), db = Math.Abs(center.Y - b.Bottom);
                        double min = Math.Min(Math.Min(dl, dr), Math.Min(dt, db));
                        n.AttachParentId = parent.Id;
                        n.AttachSide = min == dl ? "left" : min == dr ? "right" : min == dt ? "top" : "bottom";
                        n.AttachT = n.AttachSide is "left" or "right" ? (center.Y - b.Y) / b.Height : (center.X - b.X) / b.Width;
                    }
                    ed.Commit("Attach");
                    break;
                }
            case "connect":
                {
                    if (!Current.Moved)
                        return; // Keep connect preview active until second click on target
                    var target = CanvasHitTester.HitNode(ed.Current, w, canvas.Settings.FontSize);
                    TryCompleteConnect(target);
                    return;
                }
            case "reconnect":
                {
                    var target = CanvasHitTester.HitNode(ed.Current, w, canvas.Settings.FontSize);
                    if (target != null)
                    {
                        var e2 = Current.Edge;
                        var s = Current.Handle == "start" ? target : ed.Current.Node(e2.SourceId);
                        var t = Current.Handle == "end" ? target : ed.Current.Node(e2.TargetId);
                        var (ok, reason) = Catalog.CanConnect(e2.Kind, s, t, ed.Current.Kind);
                        if (ok) { if (Current.Handle == "start") e2.SourceId = target.Id; else e2.TargetId = target.Id; ed.Commit("Reconnect"); }
                        else { ed.CancelPending(); canvas.Hint(reason); }
                    }
                    else ed.CancelPending();
                    break;
                }
            case "marquee":
                {
                    if (Current.Marquee is Rect m)
                    {
                        foreach (var n in ed.Current.Nodes)
                            if (m.IntersectsWith(Geo.Bounds(n, canvas.Settings.FontSize))) ed.SelNodes.Add(n.Id);
                        ed.NotifySelection();
                    }
                    break;
                }
        }
        Current = null;
        canvas.Hint("");
        canvas.InvalidateVisual();
    }
}
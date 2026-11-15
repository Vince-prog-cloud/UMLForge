using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace UmlForge;

public class DiagramCanvas : FrameworkElement
{
    readonly Editor ed;
    public AppSettings Settings;
    public Dictionary<string, Severity> Badges = new();
    public Action<string> Hint = _ => { };
    public Action<Point> CursorMoved = _ => { };
    public event Action<Rect, string, Action<string>> InlineEditRequested;
    public event Action ViewChanged;

    public double Zoom = 1, PanX = 40, PanY = 40;
    readonly Dictionary<string, (double x, double y, double z)> cams = new();
    Theme theme;
    RenderState rs;
    string hover;
    bool spaceDown;
    DragState drag;
    Point lastMouse;

    class DragState
    {
        public Activation Act;
        public string Mode; public Point StartWorld, StartScreen;
        public UmlNode Node; public UmlEdge Edge; public string Handle;
        public Dictionary<string, (double x, double y)> Orig = new();
        public Dictionary<string, double> OrigYMap = new();
        public List<UmlNode> Contained = new();
        public List<UmlEdge> ContainedMsgs = new();
        public List<(Point a, Point b)> Guides = new();
        public bool Moved;
        public double OrigBend, OrigLdx, OrigLdy, OrigY, OrigW, OrigH;
        public int SepIndex; public UmlNode ReconnectTarget;
        public EdgeKind ConnectKind;
        public Point PreviewPoint;
        public bool PreviewValid;
        public Rect? Marquee;
    }

    public DiagramCanvas(Editor ed, AppSettings settings)
    {
        this.ed = ed; Settings = settings;
        theme = Theme.For(settings.Paper);
        Focusable = true;
        ClipToBounds = true;
        ed.Changed += () => { SyncAttached(); InvalidateVisual(); };
        ed.SelectionChanged += InvalidateVisual;
        ed.ToolChanged += () => { UpdateCursor(); InvalidateVisual(); };
        UpdateCursor();
    }

    public void RefreshTheme() { theme = Theme.For(Settings.Paper); InvalidateVisual(); }

    public Point ToWorld(Point screen) => new((screen.X - PanX) / Zoom, (screen.Y - PanY) / Zoom);
    public Point ToScreen(Point world) => new(world.X * Zoom + PanX, world.Y * Zoom + PanY);
    public Rect VisibleWorld => new(-PanX / Zoom, -PanY / Zoom, ActualWidth / Zoom, ActualHeight / Zoom);

    public void SaveCam() { if (ed.Current != null) cams[ed.Current.Id] = (PanX, PanY, Zoom); }
    public void RestoreCam()
    {
        if (ed.Current != null && cams.TryGetValue(ed.Current.Id, out var c)) { PanX = c.x; PanY = c.y; Zoom = c.z; }
        else Fit();
        InvalidateVisual(); ViewChanged?.Invoke();
    }
    public void Fit()
    {
        if (ed.Current == null || ActualWidth < 10) return;
        var b = Geo.ContentBounds(ed.Current, Settings.FontSize);
        Zoom = Math.Clamp(Math.Min(ActualWidth / b.Width, ActualHeight / b.Height), 0.2, 1.5);
        PanX = (ActualWidth - b.Width * Zoom) / 2 - b.X * Zoom;
        PanY = (ActualHeight - b.Height * Zoom) / 2 - b.Y * Zoom;
        InvalidateVisual(); ViewChanged?.Invoke();
    }
    public void ZoomAt(double factor, Point screen)
    {
        var w = ToWorld(screen);
        Zoom = Math.Clamp(Zoom * factor, 0.2, 3);
        PanX = screen.X - w.X * Zoom; PanY = screen.Y - w.Y * Zoom;
        InvalidateVisual(); ViewChanged?.Invoke();
    }
    public void SetZoom(double z) => ZoomAt(z / Zoom, new Point(ActualWidth / 2, ActualHeight / 2));
    public void CenterOn(string elementId)
    {
        var n = ed.Current?.Node(elementId);
        if (n != null) { var c = Geo.Center(Geo.Bounds(n, Settings.FontSize)); PanX = ActualWidth / 2 - c.X * Zoom; PanY = ActualHeight / 2 - c.Y * Zoom; }
        else
        {
            var e = ed.Current?.Edge(elementId);
            if (e != null) { var p = Geo.BuildEdgePath(e, ed.Current, Settings.FontSize); if (p.Points.Count > 0) { var c = p.Points[0]; PanX = ActualWidth / 2 - c.X * Zoom; PanY = ActualHeight / 2 - c.Y * Zoom; } }
        }
        InvalidateVisual(); ViewChanged?.Invoke();
    }

    void UpdateCursor()
    {
        Cursor = ed.Tool.Mode switch
        {
            ToolMode.Pan => Cursors.Hand,
            ToolMode.Place or ToolMode.Connect => Cursors.Cross,
            _ => Cursors.Arrow
        };
    }

    // ---------------- rendering ----------------
    protected override void OnRender(DrawingContext dc)
    {
        Geo.Dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        dc.DrawRectangle(theme.Paper, null, new Rect(0, 0, ActualWidth, ActualHeight));
        if (ed.Current == null) return;
        SyncAttached();
        dc.PushTransform(new TransformGroup { Children = { new ScaleTransform(Zoom, Zoom), new TranslateTransform(PanX, PanY) } });
        if (Settings.Grid)
        {
            var v = VisibleWorld; double g = Settings.GridSize;
            double x0 = Math.Floor(v.X / g) * g, y0 = Math.Floor(v.Y / g) * g;
            var dot = theme.Grid;
            for (double x = x0; x < v.Right; x += g)
                for (double y = y0; y < v.Bottom; y += g)
                    dc.DrawEllipse(dot, null, new Point(x, y), 1.1, 1.1);
        }
        rs = new RenderState
        {
            Theme = theme, FontSize = Settings.FontSize, Zoom = Zoom, GridSize = Settings.GridSize,
            Grid = Settings.Grid, Shadows = Settings.Shadows,
            SelNodes = ed.SelNodes, SelEdges = ed.SelEdges, HoverNode = hover,
            Guides = drag?.Guides ?? new(), Marquee = drag?.Marquee, Badges = Badges,
            Contents = ContentsSet()
        };
        if (drag?.Mode == "connect" && drag.Node != null)
        {
            rs.PreviewSource = drag.Node; rs.PreviewPoint = drag.PreviewPoint; rs.PreviewValid = drag.PreviewValid; rs.PreviewEdge = drag.ConnectKind;
        }
        if (ed.Tool.Mode == ToolMode.Place && drag == null)
        { rs.GhostNode = ed.Tool.Node; rs.GhostPoint = lastMouseWorld; }
        var r = new Renderer(dc, rs);
        foreach (var e in ed.Current.Edges) r.DrawEdge(e, ed.Current);
        foreach (var n in ed.Current.Nodes) r.DrawNode(n);
        r.DrawOverlay(ed.Current);
        dc.Pop();
    }

    HashSet<string> ContentsSet()
    {
        var set = new HashSet<string>();
        if (ed.Current == null) return set;
        foreach (var pkg in ed.Current.Nodes.Where(n => n.Kind == NodeKind.Package))
        {
            var b = Geo.Bounds(pkg, Settings.FontSize);
            if (ed.Current.Nodes.Any(n => n != pkg && b.Contains(new Point(n.X + n.W / 2, n.Y + n.H / 2)))) set.Add(pkg.Id);
        }
        return set;
    }

    void SyncAttached()
    {
        if (ed.Current == null) return;
        foreach (var n in ed.Current.Nodes.Where(n => !string.IsNullOrEmpty(n.AttachParentId)))
        {
            var p = ed.Current.Node(n.AttachParentId);
            if (p == null) { n.AttachParentId = null; continue; }
            var b = Geo.Bounds(p, Settings.FontSize);
            double t = Math.Clamp(n.AttachT, 0.05, 0.95);
            Point pos = n.AttachSide switch
            {
                "left" => new Point(b.X, b.Y + b.Height * t),
                "right" => new Point(b.Right, b.Y + b.Height * t),
                "top" => new Point(b.X + b.Width * t, b.Y),
                _ => new Point(b.X + b.Width * t, b.Bottom)
            };
            double stem = n.Kind == NodeKind.Port ? 0 : 20;
            Vector outv = n.AttachSide switch { "left" => new Vector(-1, 0), "right" => new Vector(1, 0), "top" => new Vector(0, -1), _ => new Vector(0, 1) };
            var c = pos + outv * stem;
            n.X = c.X - n.W / 2; n.Y = c.Y - n.H / 2;
        }
    }

    // ---------------- hit testing ----------------
    UmlNode HitNode(Point w)
    {
        for (int i = ed.Current.Nodes.Count - 1; i >= 0; i--)
        {
            var n = ed.Current.Nodes[i];
            var b = Geo.Bounds(n, Settings.FontSize);
            if (b.Contains(w)) return n;
        }
        return null;
    }
    UmlEdge HitEdge(Point w)
    {
        double tol = 6 / Zoom;
        foreach (var e in ed.Current.Edges)
        {
            var p = Geo.BuildEdgePath(e, ed.Current, Settings.FontSize);
            for (int i = 0; i < p.Points.Count - 1; i++)
                if (Geo.DistToSegment(w, p.Points[i], p.Points[i + 1]) < tol) return e;
        }
        return null;
    }
    bool HitEdgeLabel(Point w, out UmlEdge hitEdge)
    {
        hitEdge = null;
        double pad = 4 / Zoom;
        for (int i = ed.Current.Edges.Count - 1; i >= 0; i--)
        {
            var e = ed.Current.Edges[i];
            var p = Geo.BuildEdgePath(e, ed.Current, Settings.FontSize);
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
    string HitHandle(Point w, UmlNode n)
    {
        if (n == null || Catalog.IsFixed(n.Kind)) return null;
        var b = Geo.Bounds(n, Settings.FontSize);
        b.Inflate(4 / Zoom, 4 / Zoom);
        double half = 7 / Zoom;
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
    string HitPort(Point w, UmlNode n)
    {
        if (n == null) return null;
        var b = Geo.Bounds(n, Settings.FontSize);
        double tol = 8 / Zoom;
        var names = new[] { "top", "right", "bottom", "left" };
        int i = 0;
        foreach (var p in Renderer.PortPoints(b)) { if ((w - p).Length < tol) return names[i]; i++; }
        return null;
    }
    bool HitActivationEdge(Point w, out UmlNode lifeline, out Activation act, out string side)
    {
        lifeline = null; act = null; side = null;
        if (ed.Current.Kind != DiagramKind.Sequence) return false;
        double tol = 6 / Zoom;
        foreach (var n in ed.Current.Nodes.Where(n => n.Kind == NodeKind.Lifeline))
        {
            double cx = n.X + n.W / 2;
            if (Math.Abs(w.X - cx) > 10) continue;
            foreach (var a in n.Activations)
            {
                if (Math.Abs(w.Y - a.End) <= tol) { lifeline = n; act = a; side = "bottom"; return true; }
                if (Math.Abs(w.Y - a.Start) <= tol) { lifeline = n; act = a; side = "top"; return true; }
            }
        }
        return false;
    }

    // ---------------- mouse ----------------
    Point lastMouseWorld;
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        Focus(); CaptureMouse();
        var screen = e.GetPosition(this); var w = ToWorld(screen);
        lastMouse = screen; lastMouseWorld = w;
        if (ed.Current == null) return;

        if (spaceDown || ed.Tool.Mode == ToolMode.Pan)
        { drag = new DragState { Mode = "pan", StartScreen = screen }; return; }

        if (ed.Tool.Mode == ToolMode.Place)
        {
            var pos = SnapPoint(w);
            var n = ed.AddNode(ed.Tool.Node, pos.X, pos.Y);
            if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) ed.SetTool(ToolSpec.Select);
            if (n.Name.Length > 0) RequestRename(n);
            e.Handled = true; return;
        }
        if (HitEdgeLabel(w, out var labelEdge))
        {
            ed.SelectOnly(labelEdge);
            ed.Begin();
            drag = new DragState { Mode = "label", Edge = labelEdge, StartWorld = w, OrigLdx = labelEdge.LabelDx, OrigLdy = labelEdge.LabelDy };
            return;
        }
        var node = HitNode(w);
        if (ed.Tool.Mode == ToolMode.Connect)
        {
            if (node != null) StartConnect(node, ed.Tool.Edge, w);
            return;
        }
        // selection tool
        if (ed.SelNodes.Count == 1)
        {
            var sel = ed.Current.Node(ed.SelNodes.First());
            if (sel != null)
            {
                string h = HitHandle(w, sel);
                if (h != null)
                {
                    var b = Geo.Bounds(sel, Settings.FontSize);
                    ed.Begin();
                    drag = new DragState { Mode = "resize", Node = sel, Handle = h, StartWorld = w, OrigW = b.Width, OrigH = b.Height };
                    drag.Orig[sel.Id] = (sel.X, sel.Y);
                    return;
                }
                string port = HitPort(w, sel);
                if (port != null) { StartConnect(sel, Catalog.Diagrams[ed.Current.Kind].Quick, w); return; }
            }
        }
        // 1. Check selected edge endpoints/bend handle BEFORE HitNode so endpoints on lifelines can be dragged
        if (ed.SelEdges.Count == 1)
        {
            var selEdge = ed.Current.Edge(ed.SelEdges.First());
            if (selEdge != null)
            {
                var p = Geo.BuildEdgePath(selEdge, ed.Current, Settings.FontSize);
                double tol = 9 / Zoom;
                if (p.Points.Count > 0 && (w - p.StartTip).Length < tol) { ed.Begin(); drag = new DragState { Mode = "reconnect", Edge = selEdge, Handle = "start", StartWorld = w }; return; }
                if (p.Points.Count > 0 && (w - p.EndTip).Length < tol) { ed.Begin(); drag = new DragState { Mode = "reconnect", Edge = selEdge, Handle = "end", StartWorld = w }; return; }
                if (p.Points.Count >= 2 && !(ed.Current.Kind == DiagramKind.Sequence && selEdge.IsMessage))
                {
                    var mid = p.Points.Count == 4
                        ? new Point((p.Points[1].X + p.Points[2].X) / 2, (p.Points[1].Y + p.Points[2].Y) / 2)
                        : p.Points.Count == 3
                            ? p.Points[1]
                            : new Point((p.Points[0].X + p.Points[1].X) / 2, (p.Points[0].Y + p.Points[1].Y) / 2);
                    if ((w - mid).Length < tol) { ed.Begin(); drag = new DragState { Mode = "bend", Edge = selEdge, StartWorld = w, OrigBend = selEdge.BendOffset }; return; }
                }
            }
        }

        // 2. Check activation rectangle top/bottom edges before selecting the whole lifeline
        if (HitActivationEdge(w, out var actLifeline, out var hitAct, out var actSide))
        {
            ed.SelectOnly(actLifeline);
            ed.Begin();
            drag = new DragState { Mode = "activation", Node = actLifeline, Act = hitAct, Handle = actSide, StartWorld = w, OrigY = actSide == "top" ? hitAct.Start : hitAct.End };
            return;
        }
        if (node != null && (node.Kind is NodeKind.Port or NodeKind.ProvidedInterface or NodeKind.RequiredInterface))
        {
            Select(node, e);
            ed.Begin();
            drag = new DragState { Mode = "attach", Node = node, StartWorld = w };
            drag.Orig[node.Id] = (node.X, node.Y);
            return;
        }
        if (node != null)
        {
            // sequence: activation bottom / message handled via edges; fragment separators
            if (node.Kind == NodeKind.Fragment)
            {
                var b = Geo.Bounds(node, Settings.FontSize);
                double y = b.Y + 26;
                for (int i = 0; i < node.Operands.Count - 1; i++)
                {
                    y += node.Operands[i].Height;
                    if (Math.Abs(w.Y - y) < 6 / Zoom && w.X > b.X && w.X < b.Right)
                    { ed.Begin(); drag = new DragState { Mode = "sep", Node = node, SepIndex = i, StartWorld = w, OrigH = node.Operands[i].Height }; return; }
                }
            }
            Select(node, e);
            ed.Begin();
            drag = new DragState { Mode = "move", StartWorld = w };
            foreach (var id in ed.SelNodes)
            {
                var n = ed.Current.Node(id); if (n == null) continue;
                drag.Orig[id] = (n.X, n.Y);
                if (Catalog.IsContainer(n.Kind))
                {
                    var b = Geo.Bounds(n, Settings.FontSize);
                    foreach (var c in ed.Current.Nodes)
                    {
                        if (c == n || ed.SelNodes.Contains(c.Id) || !string.IsNullOrEmpty(c.AttachParentId)) continue;
                        if (n.Kind == NodeKind.Fragment && c.Kind is NodeKind.Lifeline or NodeKind.Fragment) continue;
                        if (b.Contains(new Point(c.X + c.W / 2, c.Y + c.H / 2)))
                        { drag.Contained.Add(c); drag.Orig[c.Id] = (c.X, c.Y); }
                    }
                    if (n.Kind == NodeKind.Fragment)
                        foreach (var m in ed.Current.Edges.Where(x => x.IsMessage))
                        {
                            var s = ed.Current.Node(m.SourceId); var t = ed.Current.Node(m.TargetId);
                            if (s == null || t == null) continue;
                            if (m.Y > b.Y && m.Y < b.Bottom && s.X + s.W / 2 > b.X && s.X + s.W / 2 < b.Right && t.X + t.W / 2 > b.X && t.X + t.W / 2 < b.Right)
                                drag.ContainedMsgs.Add(m);
                        }
                }
            }
            foreach (var m in drag.ContainedMsgs) drag.OrigYMap[m.Id] = m.Y;
            return;
        }
        var edge = HitEdge(w);
        if (edge != null)
        {
            if (ed.SelEdges.Count == 1 && ed.SelEdges.Contains(edge.Id))
            {
                var p = Geo.BuildEdgePath(edge, ed.Current, Settings.FontSize);
                double tol = 8 / Zoom;
                if (p.Points.Count > 0 && (w - p.StartTip).Length < tol) { ed.Begin(); drag = new DragState { Mode = "reconnect", Edge = edge, Handle = "start", StartWorld = w }; return; }
                if (p.Points.Count > 0 && (w - p.EndTip).Length < tol) { ed.Begin(); drag = new DragState { Mode = "reconnect", Edge = edge, Handle = "end", StartWorld = w }; return; }
                if (p.Points.Count >= 2)
                {
                    var mid = p.Points.Count == 4
                        ? new Point((p.Points[1].X + p.Points[2].X) / 2, (p.Points[1].Y + p.Points[2].Y) / 2)
                        : p.Points.Count == 3
                            ? p.Points[1]
                            : new Point((p.Points[0].X + p.Points[1].X) / 2, (p.Points[0].Y + p.Points[1].Y) / 2);
                    if ((w - mid).Length < tol) { ed.Begin(); drag = new DragState { Mode = "bend", Edge = edge, StartWorld = w, OrigBend = edge.BendOffset }; return; }
                }
            }
            ed.SelectOnly(edge);
            if (ed.Current.Kind == DiagramKind.Sequence && edge.IsMessage && edge.Kind != EdgeKind.MessageCreate)
            { ed.Begin(); drag = new DragState { Mode = "message", Edge = edge, StartWorld = w, OrigY = edge.Y }; return; }
            ed.Begin();
            drag = new DragState { Mode = "label", Edge = edge, StartWorld = w, OrigLdx = edge.LabelDx, OrigLdy = edge.LabelDy };
            return;
        }
        if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) ed.ClearSelection();
        drag = new DragState { Mode = "marquee", StartWorld = w };
        e.Handled = true;
    }

    void Select(UmlNode n, MouseButtonEventArgs e)
    {
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        { if (!ed.SelNodes.Remove(n.Id)) ed.SelNodes.Add(n.Id); ed.NotifySelection(); }
        else if (!ed.SelNodes.Contains(n.Id)) ed.SelectOnly(n);
    }

    void StartConnect(UmlNode source, EdgeKind kind, Point w)
    {
        drag = new DragState { Mode = "connect", Node = source, ConnectKind = kind, PreviewPoint = w };
        Hint(Catalog.Edges[kind].Hint);
    }

    Point SnapPoint(Point p)
    {
        if (!Settings.Snap) return p;
        double g = Settings.GridSize;
        return new Point(Math.Round(p.X / g) * g, Math.Round(p.Y / g) * g);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        var screen = e.GetPosition(this); var w = ToWorld(screen);
        lastMouse = screen; lastMouseWorld = w;
        CursorMoved?.Invoke(w);
        if (ed.Current == null) return;

        if (drag == null)
        {
            string handleHover = (ed.Tool.Mode == ToolMode.Select && ed.SelNodes.Count == 1)
                ? HitHandle(w, ed.Current.Node(ed.SelNodes.First()))
                : null;
            if (handleHover != null)
            {
                Cursor = handleHover switch
                {
                    "nw" or "se" => Cursors.SizeNWSE,
                    "ne" or "sw" => Cursors.SizeNESW,
                    "n" or "s" => Cursors.SizeNS,
                    _ => Cursors.SizeWE
                };
            }
            else if (ed.Tool.Mode == ToolMode.Select && HitActivationEdge(w, out _, out _, out _))
                Cursor = Cursors.SizeNS;
            else if (ed.Tool.Mode == ToolMode.Select && HitActivationEdge(w, out _, out _, out _))
                Cursor = Cursors.SizeNS;
            else if (ed.Tool.Mode == ToolMode.Select && HitEdgeLabel(w, out _))
                Cursor = Cursors.SizeAll;
            else
                UpdateCursor();

            var h = HitNode(w)?.Id;
            if (h != hover) { hover = h; InvalidateVisual(); }
            if (ed.Tool.Mode == ToolMode.Place) InvalidateVisual();
            return;
        }
        drag.Moved = true;
        switch (drag.Mode)
        {
            case "pan":
                PanX += screen.X - drag.StartScreen.X; PanY += screen.Y - drag.StartScreen.Y;
                drag.StartScreen = screen; ViewChanged?.Invoke(); break;
            case "move":
                {
                    var delta = w - drag.StartWorld;
                    double dx = delta.X, dy = delta.Y;
                    drag.Guides.Clear();
                    var primary = drag.Orig.Keys.FirstOrDefault(id => ed.SelNodes.Contains(id));
                    if (primary != null)
                    {
                        var n = ed.Current.Node(primary);
                        var b = Geo.Bounds(n, Settings.FontSize);
                        double nx = drag.Orig[primary].x + dx, ny = drag.Orig[primary].y + dy;
                        if (Settings.Snap)
                        {
                            nx = Math.Round(nx / Settings.GridSize) * Settings.GridSize;
                            ny = Math.Round(ny / Settings.GridSize) * Settings.GridSize;
                            dx = nx - drag.Orig[primary].x;
                            dy = ny - drag.Orig[primary].y;
                        }
                        if (Settings.Guides)
                        {
                            double tol = 6 / Zoom;
                            double bestDx = tol, bestDy = tol;
                            double? snapX = null, snapY = null;
                            var mineX = new[] { nx, nx + b.Width / 2, nx + b.Width };
                            var mineY = new[] { ny, ny + b.Height / 2, ny + b.Height };

                            foreach (var other in ed.Current.Nodes)
                            {
                                if (drag.Orig.ContainsKey(other.Id)) continue;
                                var ob = Geo.Bounds(other, Settings.FontSize);
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
                                foreach (var other in ed.Current.Nodes.Where(o => !drag.Orig.ContainsKey(o.Id)))
                                {
                                    var ob = Geo.Bounds(other, Settings.FontSize);
                                    if (Math.Abs(ob.X - sx) < 0.5 || Math.Abs(ob.X + ob.Width / 2 - sx) < 0.5 || Math.Abs(ob.Right - sx) < 0.5)
                                    { minY = Math.Min(minY, ob.Y); maxY = Math.Max(maxY, ob.Bottom); }
                                }
                                drag.Guides.Add((new Point(sx, minY - 24), new Point(sx, maxY + 24)));
                            }
                            if (snapY is double sy)
                            {
                                dy += bestDy; ny += bestDy;
                                double minX = nx, maxX = nx + b.Width;
                                foreach (var other in ed.Current.Nodes.Where(o => !drag.Orig.ContainsKey(o.Id)))
                                {
                                    var ob = Geo.Bounds(other, Settings.FontSize);
                                    if (Math.Abs(ob.Y - sy) < 0.5 || Math.Abs(ob.Y + ob.Height / 2 - sy) < 0.5 || Math.Abs(ob.Bottom - sy) < 0.5)
                                    { minX = Math.Min(minX, ob.X); maxX = Math.Max(maxX, ob.Right); }
                                }
                                drag.Guides.Add((new Point(minX - 24, sy), new Point(maxX + 24, sy)));
                            }
                        }
                    }
                    foreach (var (id, (ox, oy)) in drag.Orig)
                    { var n = ed.Current.Node(id); if (n != null) { n.X = ox + dx; n.Y = oy + dy; } }
                    foreach (var m in drag.ContainedMsgs) m.Y = drag.OrigYMap[m.Id] + dy;
                    break;
                }
            case "resize":
                {
                    var n = drag.Node; var d = w - drag.StartWorld;
                    double x = drag.Orig[n.Id].x, y = drag.Orig[n.Id].y, ww = drag.OrigW, hh = drag.OrigH;
                    if (drag.Handle.Contains('e')) ww = Math.Max(40, drag.OrigW + d.X);
                    if (drag.Handle.Contains('s')) hh = Math.Max(24, drag.OrigH + d.Y);
                    if (drag.Handle.Contains('w')) { ww = Math.Max(40, drag.OrigW - d.X); x = drag.Orig[n.Id].x + (drag.OrigW - ww); }
                    if (drag.Handle.Contains('n')) { hh = Math.Max(24, drag.OrigH - d.Y); y = drag.Orig[n.Id].y + (drag.OrigH - hh); }
                    n.X = x; n.Y = y; n.W = ww; n.H = hh;
                    break;
                }
            case "connect":
                {
                    drag.PreviewPoint = w;
                    var target = HitNode(w);
                    drag.ReconnectTarget = target;
                    var (ok, reason) = target != null ? Catalog.CanConnect(drag.ConnectKind, drag.Node, target, ed.Current.Kind) : (false, "");
                    drag.PreviewValid = ok;
                    Hint(target != null && !ok ? reason : Catalog.Edges[drag.ConnectKind].Hint);
                    break;
                }
            case "reconnect":
                {
                    drag.PreviewPoint = w;
                    var target = HitNode(w);
                    drag.ReconnectTarget = target;
                    if (target != null)
                    {
                        var s = drag.Handle == "start" ? target : ed.Current.Node(drag.Edge.SourceId);
                        var t = drag.Handle == "end" ? target : ed.Current.Node(drag.Edge.TargetId);
                        var (ok, reason) = Catalog.CanConnect(drag.Edge.Kind, s, t, ed.Current.Kind);
                        drag.PreviewValid = ok;
                        Hint(ok ? "" : reason);
                    }
                    else
                    {
                        drag.PreviewValid = false;
                        Hint("Drop on a valid element to reconnect.");
                    }
                    break;
                }
            case "marquee":
                {
                    var a = drag.StartWorld;
                    drag.Marquee = new Rect(Math.Min(a.X, w.X), Math.Min(a.Y, w.Y), Math.Abs(w.X - a.X), Math.Abs(w.Y - a.Y));
                    break;
                }
            case "bend":
                {
                    var d = w - drag.StartWorld;
                    drag.Edge.BendOffset = drag.OrigBend + (Math.Abs(d.X) > Math.Abs(d.Y) ? d.X : d.Y);
                    break;
                }
            case "label":
                {
                    var d = w - drag.StartWorld;
                    drag.Edge.LabelDx = drag.OrigLdx + d.X; drag.Edge.LabelDy = drag.OrigLdy + d.Y;
                    break;
                }
            case "message":
                {
                    var edge = drag.Edge;
                    var s = ed.Current.Node(edge.SourceId); var t = ed.Current.Node(edge.TargetId);
                    double minY = Math.Max(s.Y + 36, t.Y + 36) + 10;
                    double maxY = Math.Min(s.Y + s.H, t.Y + t.H) - 10;
                    edge.Y = Math.Clamp(drag.OrigY + (w.Y - drag.StartWorld.Y), minY, Math.Max(minY, maxY));
                    var act = t.Activations.FirstOrDefault(a => a.MessageId == edge.Id);
                    if (act != null) { act.Start = edge.Y; act.End = Math.Max(act.End, edge.Y + 20); }
                    if (edge.Kind == EdgeKind.MessageReply)
                    {
                        var sa = s.Activations.Where(a => a.Start <= edge.Y).OrderByDescending(a => a.Start).FirstOrDefault();
                        if (sa != null) sa.End = edge.Y;
                    }
                    break;
                }
            case "sep":
                {
                    var d = w.Y - drag.StartWorld.Y;
                    drag.Node.Operands[drag.SepIndex].Height = Math.Max(34, drag.OrigH + d);
                    break;
                }
            case "attach":
                {
                    var n = drag.Node;
                    n.X = drag.Orig[n.Id].x + (w.X - drag.StartWorld.X);
                    n.Y = drag.Orig[n.Id].y + (w.Y - drag.StartWorld.Y);
                    n.AttachParentId = null;
                    break;
                }
            case "activation":
                {
                    double ny = drag.OrigY + (w.Y - drag.StartWorld.Y);
                    if (drag.Handle == "bottom")
                    {
                        drag.Act.End = Math.Max(drag.Act.Start + 14, ny);
                        // Keep linked reply message aligned with the bottom of the activation
                        var replyMsg = ed.Current.Edges.FirstOrDefault(x => x.Kind == EdgeKind.MessageReply && x.SourceId == drag.Node.Id && Math.Abs(x.Y - drag.OrigY) < 6);
                        if (replyMsg != null) replyMsg.Y = drag.Act.End;
                    }
                    else
                    {
                        drag.Act.Start = Math.Min(drag.Act.End - 14, ny);
                    }
                    break;
                }
        }
        InvalidateVisual();
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        ReleaseMouseCapture();
        if (drag == null) return;
        var w = ToWorld(e.GetPosition(this));
        switch (drag.Mode)
        {
            case "move":
            case "resize":
            case "bend":
            case "label":
            case "message":
            case "sep":
            case "activation":
                if (drag.Moved) ed.Commit("Edit"); else ed.CancelPending();
                break;
            case "attach":
                {
                    var n = drag.Node;
                    var center = new Point(n.X + n.W / 2, n.Y + n.H / 2);
                    var parent = ed.Current.Nodes.LastOrDefault(p => p.Kind is NodeKind.Component or NodeKind.DeviceNode or NodeKind.ExecEnvNode
                        && Geo.Bounds(p, Settings.FontSize).Contains(center));
                    if (parent != null)
                    {
                        var b = Geo.Bounds(parent, Settings.FontSize);
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
                    var target = HitNode(w);
                    if (target != null)
                    {
                        var (ok, reason) = ed.TryConnect(drag.ConnectKind, drag.Node, target);
                        if (!ok) Hint(reason);
                    }
                    if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) && ed.Tool.Mode == ToolMode.Connect) ed.SetTool(ToolSpec.Select);
                    break;
                }
            case "reconnect":
                {
                    var target = HitNode(w);
                    if (target != null)
                    {
                        var e2 = drag.Edge;
                        var s = drag.Handle == "start" ? target : ed.Current.Node(e2.SourceId);
                        var t = drag.Handle == "end" ? target : ed.Current.Node(e2.TargetId);
                        var (ok, reason) = Catalog.CanConnect(e2.Kind, s, t, ed.Current.Kind);
                        if (ok) { if (drag.Handle == "start") e2.SourceId = target.Id; else e2.TargetId = target.Id; ed.Commit("Reconnect"); }
                        else { ed.CancelPending(); Hint(reason); }
                    }
                    else ed.CancelPending();
                    break;
                }
            case "marquee":
                {
                    if (drag.Marquee is Rect m)
                    {
                        foreach (var n in ed.Current.Nodes)
                            if (m.IntersectsWith(Geo.Bounds(n, Settings.FontSize))) ed.SelNodes.Add(n.Id);
                        ed.NotifySelection();
                    }
                    break;
                }
        }
        drag = null;
        Hint("");
        InvalidateVisual();
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        ZoomAt(e.Delta > 0 ? 1.12 : 1 / 1.12, e.GetPosition(this));
        e.Handled = true;
    }

    protected override void OnMouseRightButtonUp(MouseButtonEventArgs e)
    {
        var w = ToWorld(e.GetPosition(this));
        var node = HitNode(w); var edge = node == null ? HitEdge(w) : null;
        if (node != null) ed.SelectOnly(node); else if (edge != null) ed.SelectOnly(edge);
        var menu = new ContextMenu();
        void Item(string header, Action a, string gesture = null)
        {
            var mi = new MenuItem { Header = header };
            if (gesture != null) mi.InputGestureText = gesture;
            mi.Click += (_, _) => a();
            menu.Items.Add(mi);
        }
        if (node != null)
        {
            Item("Rename", () => RequestRename(node), "F2");
            if (node.Kind == NodeKind.Class)
            {
                Item("Add attribute", () => QuickAdd(node, "attrs"));
                Item("Add operation", () => QuickAdd(node, "ops"));
            }
            Item("Duplicate", () => ed.DuplicateSelection(), "Ctrl+D");
            Item("Delete", () => ed.DeleteSelection(), "Del");
            menu.Items.Add(new Separator());
            Item("Bring to front", () => ed.Mutate(() => { ed.Current.Nodes.Remove(node); ed.Current.Nodes.Add(node); }, "Order"));
            Item("Send to back", () => ed.Mutate(() => { ed.Current.Nodes.Remove(node); ed.Current.Nodes.Insert(0, node); }, "Order"));
            if (!string.IsNullOrEmpty(node.AttachParentId))
                Item("Detach from parent", () => ed.Mutate(() => node.AttachParentId = null, "Detach"));
        }
        else if (edge != null)
        {
            Item("Edit label", () => RequestEdgeLabel(edge));
            Item("Reverse direction", () => ed.Mutate(() => { (edge.SourceId, edge.TargetId) = (edge.TargetId, edge.SourceId); (edge.SourceRole, edge.TargetRole) = (edge.TargetRole, edge.SourceRole); (edge.SourceMultiplicity, edge.TargetMultiplicity) = (edge.TargetMultiplicity, edge.SourceMultiplicity); }, "Reverse"));
            Item(edge.Routing == Routing.Straight ? "Orthogonal routing" : "Straight routing",
                () => ed.Mutate(() => edge.Routing = edge.Routing == Routing.Straight ? Routing.Orthogonal : Routing.Straight, "Routing"));
            menu.Items.Add(new Separator());
            foreach (var k in Catalog.EdgeKindsFor(ed.Current.Kind).Where(k => k != edge.Kind))
            {
                var kk = k;
                Item("Change to " + Catalog.Edges[kk].Label, () =>
                {
                    var s = ed.Current.Node(edge.SourceId); var t = ed.Current.Node(edge.TargetId);
                    var (ok, reason) = Catalog.CanConnect(kk, s, t, ed.Current.Kind);
                    if (ok) ed.Mutate(() => edge.Kind = kk, "Change type"); else Hint(reason);
                });
            }
            menu.Items.Add(new Separator());
            Item("Delete", () => ed.DeleteSelection(), "Del");
        }
        else
        {
            Item("Select all", () => { foreach (var n in ed.Current.Nodes) ed.SelNodes.Add(n.Id); ed.NotifySelection(); }, "Ctrl+A");
            Item("Fit diagram", () => Fit(), "Ctrl+0");
            Item("Reset zoom", () => SetZoom(1), "Ctrl+1");
            menu.Items.Add(new Separator());
            Item(Settings.Grid ? "Hide grid" : "Show grid", () => { Settings.Grid = !Settings.Grid; InvalidateVisual(); });
            Item(Settings.Snap ? "Disable snap" : "Enable snap", () => { Settings.Snap = !Settings.Snap; });
        }
        ContextMenu = menu; menu.IsOpen = true;
        e.Handled = true;
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Middle)
        { CaptureMouse(); drag = new DragState { Mode = "pan", StartScreen = e.GetPosition(this) }; e.Handled = true; }
        base.OnMouseDown(e);
    }
    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Middle && drag?.Mode == "pan") { ReleaseMouseCapture(); drag = null; }
        base.OnMouseUp(e);
    }

    protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            var w = ToWorld(e.GetPosition(this));
            var node = HitNode(w);
            if (node != null)
            {
                if (node.Kind is NodeKind.Class or NodeKind.Interface or NodeKind.DataType)
                {
                    var b = Geo.Bounds(node, Settings.FontSize);
                    var L = Geo.LayoutClassifier(node, Settings.FontSize);
                    if (w.Y > b.Y + L.NameH + L.AttrH) { QuickAdd(node, "ops"); e.Handled = true; return; }
                    if (w.Y > b.Y + L.NameH) { QuickAdd(node, "attrs"); e.Handled = true; return; }
                }
                if (node.Kind == NodeKind.Enumeration && w.Y > node.Y + 30) { QuickAddLiteral(node); e.Handled = true; return; }
                RequestRename(node); e.Handled = true; return;
            }
            if (HitEdgeLabel(w, out var lblEdge)) { RequestEdgeLabel(lblEdge); e.Handled = true; return; }
            var edge = HitEdge(w);
            if (edge != null) { RequestEdgeLabel(edge); e.Handled = true; }
        }
        base.OnPreviewMouseLeftButtonDown(e);
    }

    public void RequestRename(UmlNode n)
    {
        var b = Geo.Bounds(n, Settings.FontSize);
        var rect = new Rect(ToScreen(new Point(b.X, b.Y)), ToScreen(new Point(b.Right, b.Y + 30)));
        InlineEditRequested?.Invoke(rect, n.Name, text => ed.Mutate(() => n.Name = text, "Rename"));
    }
    void QuickAdd(UmlNode n, string region)
    {
        var b = Geo.Bounds(n, Settings.FontSize);
        var L = Geo.LayoutClassifier(n, Settings.FontSize);
        double y = region == "ops" ? b.Y + L.NameH + L.AttrH : b.Y + L.NameH;
        var rect = new Rect(ToScreen(new Point(b.X + 4, y)), ToScreen(new Point(b.Right - 4, y + 22)));
        InlineEditRequested?.Invoke(rect, "", text =>
        {
            var f = FeatureParser.Parse(text);
            if (f is UmlAttribute a) ed.Mutate(() => n.Attributes.Add(a), "Add attribute");
            else if (f is UmlOperation o) ed.Mutate(() => n.Operations.Add(o), "Add operation");
        });
    }
    void QuickAddLiteral(UmlNode n)
    {
        var rect = new Rect(ToScreen(new Point(n.X + 4, n.Y + n.H - 22)), ToScreen(new Point(n.X + n.W - 4, n.Y + n.H)));
        InlineEditRequested?.Invoke(rect, "", text => { if (text.Trim().Length > 0) ed.Mutate(() => n.Literals.Add(text.Trim()), "Add literal"); });
    }
    void RequestEdgeLabel(UmlEdge e)
    {
        var p = Geo.BuildEdgePath(e, ed.Current, Settings.FontSize);
        if (p.Points.Count == 0) return;
        var mid = new Point((p.StartTip.X + p.EndTip.X) / 2, (p.StartTip.Y + p.EndTip.Y) / 2);
        var s = ToScreen(mid);
        var rect = new Rect(s.X - 110, s.Y - 30, 220, 24);
        string current = e.Kind == EdgeKind.Transition ? Geo.TransitionText(e) : e.Name;
        InlineEditRequested?.Invoke(rect, current, text =>
        {
            ed.Mutate(() => { if (e.Kind == EdgeKind.Transition) FeatureParser.ParseTransition(e, text); else e.Name = text; }, "Edit label");
        });
    }

    public void SetSpace(bool down) { spaceDown = down; if (down) Cursor = Cursors.Hand; else UpdateCursor(); }
    public void Nudge(double dx, double dy)
    {
        if (ed.SelNodes.Count == 0) return;
        ed.Mutate(() => { foreach (var id in ed.SelNodes) { var n = ed.Current.Node(id); if (n != null) { n.X += dx; n.Y += dy; } } }, "Nudge");
    }
}

// extra drag fields
public partial class DragExtra { }
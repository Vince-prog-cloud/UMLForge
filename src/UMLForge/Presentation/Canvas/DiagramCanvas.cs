using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace UmlForge;

public class DiagramCanvas : FrameworkElement
{
    readonly Editor ed;
    readonly CanvasDragController drag;
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
    Point lastMouseWorld;

    public DiagramCanvas(Editor ed, AppSettings settings)
    {
        this.ed = ed;
        Settings = settings;
        drag = new CanvasDragController(ed, this);
        theme = Theme.For(settings.Paper);
        Focusable = true;
        ClipToBounds = true;
        ed.Changed += () => { SyncAttached(); InvalidateVisual(); };
        ed.SelectionChanged += InvalidateVisual;
        ed.ToolChanged += () => { if (!drag.IsConnecting) UpdateCursor(); InvalidateVisual(); };
        UpdateCursor();
    }

    public void RefreshTheme() { theme = Theme.For(Settings.Paper); InvalidateVisual(); }
    public Point ToWorld(Point screen) => new((screen.X - PanX) / Zoom, (screen.Y - PanY) / Zoom);
    public Point ToScreen(Point world) => new(world.X * Zoom + PanX, world.Y * Zoom + PanY);
    public Rect VisibleWorld => new(-PanX / Zoom, -PanY / Zoom, ActualWidth / Zoom, ActualHeight / Zoom);

    public void Pan(double dx, double dy) { PanX += dx; PanY += dy; ViewChanged?.Invoke(); }
    public void SaveCam() { if (ed.Current != null) cams[ed.Current.Id] = (PanX, PanY, Zoom); }
    public void RestoreCam()
    {
        if (ed.Current != null && cams.TryGetValue(ed.Current.Id, out var c)) { PanX = c.x; PanY = c.y; Zoom = c.z; }
        else Fit();
        InvalidateVisual();
        ViewChanged?.Invoke();
    }

    public void Fit()
    {
        if (ed.Current == null || ActualWidth < 10) return;
        var b = Geo.ContentBounds(ed.Current, Settings.FontSize);
        Zoom = Math.Clamp(Math.Min(ActualWidth / b.Width, ActualHeight / b.Height), 0.2, 1.5);
        PanX = (ActualWidth - b.Width * Zoom) / 2 - b.X * Zoom;
        PanY = (ActualHeight - b.Height * Zoom) / 2 - b.Y * Zoom;
        InvalidateVisual();
        ViewChanged?.Invoke();
    }

    public void ZoomAt(double factor, Point screen)
    {
        var w = ToWorld(screen);
        Zoom = Math.Clamp(Zoom * factor, 0.2, 3);
        PanX = screen.X - w.X * Zoom;
        PanY = screen.Y - w.Y * Zoom;
        InvalidateVisual();
        ViewChanged?.Invoke();
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
        InvalidateVisual();
        ViewChanged?.Invoke();
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
            for (double x = x0; x < v.Right; x += g)
                for (double y = y0; y < v.Bottom; y += g)
                    dc.DrawEllipse(theme.Grid, null, new Point(x, y), 1.1, 1.1);
        }
        rs = new RenderState
        {
            Theme = theme,
            FontSize = Settings.FontSize,
            Zoom = Zoom,
            GridSize = Settings.GridSize,
            Grid = Settings.Grid,
            Shadows = Settings.Shadows,
            SelNodes = ed.SelNodes,
            SelEdges = ed.SelEdges,
            HoverNode = hover,
            Guides = drag.Current?.Guides ?? new(),
            Marquee = drag.Current?.Marquee,
            Badges = Badges,
            Contents = ContentsSet()
        };
        if (drag.IsConnecting && drag.Current.Node != null)
        {
            rs.PreviewSource = drag.Current.Node;
            rs.PreviewStart = drag.Current.StartWorld;
            rs.PreviewPoint = drag.Current.PreviewPoint;
            rs.PreviewValid = drag.Current.PreviewValid;
            rs.PreviewEdge = drag.Current.ConnectKind;
        }
        if (ed.Tool.Mode == ToolMode.Place && !drag.IsActive)
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

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        Focus(); CaptureMouse();
        var screen = e.GetPosition(this);
        var w = ToWorld(screen);
        lastMouseWorld = w;
        if (ed.Current == null) return;

        if (spaceDown || ed.Tool.Mode == ToolMode.Pan) { drag.StartPan(screen); return; }

        if (ed.Tool.Mode == ToolMode.Place)
        {
            var pos = SnapPoint(w);
            var n = ed.AddNode(ed.Tool.Node, pos.X, pos.Y);
            if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) ed.SetTool(ToolSpec.Select);
            if (n.Name.Length > 0) RequestRename(n);
            e.Handled = true;
            return;
        }

        var node = CanvasHitTester.HitNode(ed.Current, w, Settings.FontSize);
        if (ed.Tool.Mode == ToolMode.Connect)
        {
            if (drag.IsConnecting) { drag.TryCompleteConnect(node); return; }
            if (node != null) { drag.StartConnect(node, ed.Tool.Edge, w); InvalidateVisual(); }
            return;
        }
        if (drag.IsConnecting) { drag.TryCompleteConnect(node); return; }

        if (CanvasHitTester.HitEdgeLabel(ed.Current, w, Zoom, Settings.FontSize, out var labelEdge))
        {
            ed.SelectOnly(labelEdge);
            drag.BeginLabel(labelEdge, w);
            return;
        }

        if (ed.SelNodes.Count == 1)
        {
            var sel = ed.Current.Node(ed.SelNodes.First());
            if (sel != null)
            {
                string h = CanvasHitTester.HitHandle(w, sel, Zoom, Settings.FontSize);
                if (h != null) { drag.BeginResize(sel, h, w); return; }
                string port = CanvasHitTester.HitPort(w, sel, Zoom, Settings.FontSize);
                if (port != null) { drag.StartConnect(sel, Catalog.Diagrams[ed.Current.Kind].Quick, w); InvalidateVisual(); return; }
            }
        }

        if (ed.SelEdges.Count == 1)
        {
            var selEdge = ed.Current.Edge(ed.SelEdges.First());
            if (selEdge != null && TryHitSelectedEdgeHandles(selEdge, w)) return;
        }

        if (CanvasHitTester.HitActivationEdge(ed.Current, w, Zoom, out var actLifeline, out var hitAct, out var actSide))
        {
            ed.SelectOnly(actLifeline);
            drag.BeginActivation(actLifeline, hitAct, actSide, w);
            return;
        }

        if (node != null && Catalog.IsContainer(node.Kind) && CanvasHitTester.HitEdge(ed.Current, w, Zoom, Settings.FontSize) != null)
            node = null;

        if (node != null && (node.Kind is NodeKind.Port or NodeKind.ProvidedInterface or NodeKind.RequiredInterface))
        {
            Select(node);
            drag.BeginAttach(node, w);
            return;
        }

        if (node != null)
        {
            if (node.Kind == NodeKind.Fragment)
            {
                var b = Geo.Bounds(node, Settings.FontSize);
                double y = b.Y + 26;
                for (int i = 0; i < node.Operands.Count - 1; i++)
                {
                    y += node.Operands[i].Height;
                    if (Math.Abs(w.Y - y) < 6 / Zoom && w.X > b.X && w.X < b.Right)
                    { drag.BeginSeparator(node, i, w); return; }
                }
            }
            Select(node);
            drag.BeginMove(w);
            return;
        }

        var edge = CanvasHitTester.HitEdge(ed.Current, w, Zoom, Settings.FontSize);
        if (edge != null)
        {
            if (ed.SelEdges.Count == 1 && ed.SelEdges.Contains(edge.Id) && TryHitSelectedEdgeHandles(edge, w)) return;
            ed.SelectOnly(edge);
            if (ed.Current.Kind == DiagramKind.Sequence && edge.IsMessage) drag.BeginMessage(edge, w);
            else drag.BeginLabel(edge, w);
            return;
        }

        if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) ed.ClearSelection();
        drag.BeginMarquee(w);
        e.Handled = true;
    }

    bool TryHitSelectedEdgeHandles(UmlEdge edge, Point w)
    {
        var p = Geo.BuildEdgePath(edge, ed.Current, Settings.FontSize);
        double tol = 9 / Zoom;
        if (p.Points.Count > 0 && (w - p.StartTip).Length < tol) { drag.BeginReconnect(edge, "start", w); return true; }
        if (p.Points.Count > 0 && (w - p.EndTip).Length < tol) { drag.BeginReconnect(edge, "end", w); return true; }
        if (p.Points.Count >= 2 && !(ed.Current.Kind == DiagramKind.Sequence && edge.IsMessage))
        {
            var mid = p.Points.Count == 4
                ? new Point((p.Points[1].X + p.Points[2].X) / 2, (p.Points[1].Y + p.Points[2].Y) / 2)
                : p.Points.Count == 3
                    ? p.Points[1]
                    : new Point((p.Points[0].X + p.Points[1].X) / 2, (p.Points[0].Y + p.Points[1].Y) / 2);
            if ((w - mid).Length < tol) { drag.BeginBend(edge, w); return true; }
        }
        return false;
    }

    void Select(UmlNode n)
    {
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        { if (!ed.SelNodes.Remove(n.Id)) ed.SelNodes.Add(n.Id); ed.NotifySelection(); }
        else if (!ed.SelNodes.Contains(n.Id)) ed.SelectOnly(n);
    }

    Point SnapPoint(Point p)
    {
        if (!Settings.Snap) return p;
        double g = Settings.GridSize;
        return new Point(Math.Round(p.X / g) * g, Math.Round(p.Y / g) * g);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        var screen = e.GetPosition(this);
        var w = ToWorld(screen);
        lastMouseWorld = w;
        CursorMoved?.Invoke(w);
        if (ed.Current == null) return;

        if (!drag.IsActive)
        {
            UpdateHoverCursor(w);
            var h = CanvasHitTester.HitNode(ed.Current, w, Settings.FontSize)?.Id;
            if (h != hover) { hover = h; InvalidateVisual(); }
            if (ed.Tool.Mode == ToolMode.Place) InvalidateVisual();
            return;
        }
        drag.OnMove(screen, w);
    }

    void UpdateHoverCursor(Point w)
    {
        string handleHover = (ed.Tool.Mode == ToolMode.Select && ed.SelNodes.Count == 1)
            ? CanvasHitTester.HitHandle(w, ed.Current.Node(ed.SelNodes.First()), Zoom, Settings.FontSize)
            : null;
        if (handleHover != null)
            Cursor = handleHover switch { "nw" or "se" => Cursors.SizeNWSE, "ne" or "sw" => Cursors.SizeNESW, "n" or "s" => Cursors.SizeNS, _ => Cursors.SizeWE };
        else if (ed.Tool.Mode == ToolMode.Select && CanvasHitTester.HitActivationEdge(ed.Current, w, Zoom, out _, out _, out _))
            Cursor = Cursors.SizeNS;
        else if (ed.Tool.Mode == ToolMode.Select && CanvasHitTester.HitEdgeLabel(ed.Current, w, Zoom, Settings.FontSize, out _))
            Cursor = Cursors.SizeAll;
        else
            UpdateCursor();
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        ReleaseMouseCapture();
        if (!drag.IsActive) return;
        drag.OnUp(ToWorld(e.GetPosition(this)));
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        ZoomAt(e.Delta > 0 ? 1.12 : 1 / 1.12, e.GetPosition(this));
        e.Handled = true;
    }

    protected override void OnMouseRightButtonUp(MouseButtonEventArgs e)
    {
        if (drag.IsConnecting) { drag.Cancel(); e.Handled = true; return; }
        var w = ToWorld(e.GetPosition(this));
        var node = CanvasHitTester.HitNode(ed.Current, w, Settings.FontSize);
        var edge = node == null ? CanvasHitTester.HitEdge(ed.Current, w, Zoom, Settings.FontSize) : null;
        if (node != null) ed.SelectOnly(node); else if (edge != null) ed.SelectOnly(edge);
        ContextMenu = CanvasContextMenu.Build(ed, this, node, edge);
        ContextMenu.IsOpen = true;
        e.Handled = true;
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Middle) { CaptureMouse(); drag.StartPan(e.GetPosition(this)); e.Handled = true; }
        base.OnMouseDown(e);
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Middle && drag.Current?.Mode == "pan") { ReleaseMouseCapture(); drag.Cancel(); }
        base.OnMouseUp(e);
    }

    protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2 && ed.Current != null)
        {
            var w = ToWorld(e.GetPosition(this));
            var node = CanvasHitTester.HitNode(ed.Current, w, Settings.FontSize);
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
                RequestRename(node);
                e.Handled = true;
                return;
            }
            if (CanvasHitTester.HitEdgeLabel(ed.Current, w, Zoom, Settings.FontSize, out var lblEdge)) { RequestEdgeLabel(lblEdge); e.Handled = true; return; }
            var edge = CanvasHitTester.HitEdge(ed.Current, w, Zoom, Settings.FontSize);
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

    public void QuickAdd(UmlNode n, string region)
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

    public void RequestEdgeLabel(UmlEdge e)
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

    public void CancelInteraction() => drag.Cancel();
    public void SetSpace(bool down) { spaceDown = down; if (down) Cursor = Cursors.Hand; else UpdateCursor(); }
    public void Nudge(double dx, double dy)
    {
        if (ed.SelNodes.Count == 0) return;
        ed.Mutate(() => { foreach (var id in ed.SelNodes) { var n = ed.Current.Node(id); if (n != null) { n.X += dx; n.Y += dy; } } }, "Nudge");
    }
}
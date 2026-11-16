using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using Path = System.Windows.Shapes.Path;
using WinVisibility = System.Windows.Visibility;

namespace UmlForge;

public partial class MainWindow : Window
{
    public Editor Editor { get; } = new();
    readonly AppSettings settings;
    DiagramCanvas canvas;
    PropertiesPanel props;
    readonly DispatcherTimer autosaveTimer, validateTimer;
    string currentFile = "";
    TextBox inlineBox;
    Action<string> inlineCommit;
    static object clipboardData;
    List<Issue> issues = new();

    static Brush IvoryBrush => new SolidColorBrush(Color.FromRgb(252, 249, 236));     // #FCF9EC
    static Brush LavenderBrush => new SolidColorBrush(Color.FromRgb(201, 181, 224));  // #C9B5E0
    static Brush IndigoBrush => new SolidColorBrush(Color.FromRgb(96, 97, 182));      // #6061B6
    static Brush BorderIdleBrush => new SolidColorBrush(Color.FromRgb(61, 66, 148));  // #3D4294

    public MainWindow() : this(Seed.Build(), null, "", ProjectStore.LoadSettings()) { }

    public MainWindow(Project startProj, UmlPackage focusPkg, string startFile, AppSettings loadedSettings)
    {
        InitializeComponent();
        settings = loadedSettings;
        Editor.Project = startProj;
        currentFile = startFile ?? "";
        if (focusPkg?.Diagrams.Count > 0)
            Editor.OpenDiagram(focusPkg.Diagrams[0]);

        canvas = new DiagramCanvas(Editor, settings)
        {
            Hint = s => HintStatus.Text = s,
            CursorMoved = p => CoordsStatus.Text = $"{p.X:0}, {p.Y:0}"
        };
        canvas.ViewChanged += () => ZoomStatus.Text = $"{canvas.Zoom * 100:0}%";
        canvas.InlineEditRequested += ShowInline;
        CanvasHost.Child = canvas;
        props = new PropertiesPanel(Editor, settings);
        PropsHost.Child = props;

        autosaveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(900) };
        autosaveTimer.Tick += (_, _) => { autosaveTimer.Stop(); ProjectStore.Autosave(Editor.Project); SaveStatus.Text = "Autosaved " + DateTime.Now.ToString("HH:mm"); };
        validateTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        validateTimer.Tick += (_, _) => { validateTimer.Stop(); RunValidation(); };

        Editor.Changed += OnChanged;
        Editor.SelectionChanged += OnSelectionChanged;
        Editor.ToolChanged += MarkTool;
        WireUi();
        BuildExplorer();
        BuildPalette();
        BuildTabs();
        OnChanged();
        Loaded += (_, _) =>
        {
            var wa = SystemParameters.WorkArea;
            if (Width > wa.Width) Width = wa.Width;
            if (Height > wa.Height) Height = wa.Height;
            if (Top < wa.Top) Top = wa.Top;
            if (Left < wa.Left) Left = wa.Left;
            canvas.Fit();
            ProjectNameBox.Text = Editor.Project.Name;
            MarkTool();
        };
        Closing += (_, _) => { ProjectStore.Autosave(Editor.Project); ProjectStore.SaveSettings(settings); };
        PreviewKeyDown += OnKey;
        PreviewKeyUp += (_, e) => { if (e.Key == Key.Space) canvas.SetSpace(false); };
    }

    void OnChanged()
    {
        BuildExplorer(); BuildTabs(); BuildPalette(); MarkTool();
        UndoBtn.IsEnabled = Editor.CanUndo;
        RedoBtn.IsEnabled = Editor.CanRedo;
        EmptyHint.Visibility = Editor.Current == null || Editor.Current.Nodes.Count == 0 ? WinVisibility.Visible : WinVisibility.Collapsed;
        canvas.InvalidateVisual();
        autosaveTimer.Stop(); autosaveTimer.Start();
        validateTimer.Stop(); validateTimer.Start();
        Title = $"UmlForge — {Editor.Project.Name}{(Editor.Dirty ? " •" : "")}{(currentFile.Length > 0 ? "  [" + System.IO.Path.GetFileName(currentFile) + "]" : "")}";
        if (ProjectNameBox.Text != Editor.Project.Name && !ProjectNameBox.IsFocused) ProjectNameBox.Text = Editor.Project.Name;
    }

    void OnSelectionChanged() => canvas.InvalidateVisual();

    void WireUi()
    {
        ProjectNameBox.LostFocus += (_, _) => { if (ProjectNameBox.Text.Trim().Length > 0) Editor.Mutate(() => Editor.Project.Name = ProjectNameBox.Text.Trim(), "Rename project"); };
        UndoBtn.Click += (_, _) => Editor.Undo(); RedoBtn.Click += (_, _) => Editor.Redo();
        UndoMenu.Click += (_, _) => Editor.Undo(); RedoMenu.Click += (_, _) => Editor.Redo();
        SaveBtn.Click += (_, _) => SaveProject(false); SaveMenu.Click += (_, _) => SaveProject(false);
        SaveAsMenu.Click += (_, _) => SaveProject(true);
        OpenMenu.Click += (_, _) => OpenProject();
        NewProjectMenu.Click += (_, _) => NewProject();
        ResetSampleMenu.Click += (_, _) => { if (MessageBox.Show("Replace the current project with the sample library model?", "UmlForge", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes) { Editor.Project = Seed.Build(); currentFile = ""; canvas.Fit(); } };
        ExitMenu.Click += (_, _) => Close();
        ExportBtn.Click += (_, _) => { ExportBtn.ContextMenu = BuildExportMenu(); ExportBtn.ContextMenu.IsOpen = true; };
        ExportPngMenu.Click += (_, _) => ExportPng(); ExportXmiMenu.Click += (_, _) => ExportXmi();
        DupMenu.Click += (_, _) => Editor.DuplicateSelection();
        DeleteMenu.Click += (_, _) => Editor.DeleteSelection();
        SelectAllMenu.Click += (_, _) => { foreach (var n in Editor.Current.Nodes) Editor.SelNodes.Add(n.Id); Editor.NotifySelection(); };
        GridMenu.Click += (_, _) => { settings.Grid = GridMenu.IsChecked; canvas.InvalidateVisual(); };
        SnapMenu.Click += (_, _) => settings.Snap = SnapMenu.IsChecked;
        GuidesMenu.Click += (_, _) => settings.Guides = GuidesMenu.IsChecked;
        ShadowsMenu.Click += (_, _) => { settings.Shadows = ShadowsMenu.IsChecked; canvas.InvalidateVisual(); };
        PaperWhite.Click += (_, _) => SetPaper(PaperMode.White);
        PaperCream.Click += (_, _) => SetPaper(PaperMode.Cream);
        PaperBlueprint.Click += (_, _) => SetPaper(PaperMode.Blueprint);
        FontMinus.Click += (_, _) => { settings.FontSize = Math.Max(10, settings.FontSize - 1); canvas.InvalidateVisual(); };
        FontPlus.Click += (_, _) => { settings.FontSize = Math.Min(18, settings.FontSize + 1); canvas.InvalidateVisual(); };
        GridMinus.Click += (_, _) => { settings.GridSize = Math.Max(8, settings.GridSize - 4); canvas.InvalidateVisual(); };
        GridPlus.Click += (_, _) => { settings.GridSize = Math.Min(48, settings.GridSize + 4); canvas.InvalidateVisual(); };
        FitMenu.Click += (_, _) => canvas.Fit(); ActualMenu.Click += (_, _) => canvas.SetZoom(1);
        FitBtn.Click += (_, _) => canvas.Fit(); ActualBtn.Click += (_, _) => canvas.SetZoom(1);
        ZoomInBtn.Click += (_, _) => canvas.ZoomAt(1.2, new Point(canvas.ActualWidth / 2, canvas.ActualHeight / 2));
        ZoomOutBtn.Click += (_, _) => canvas.ZoomAt(1 / 1.2, new Point(canvas.ActualWidth / 2, canvas.ActualHeight / 2));
        SelectToolBtn.Click += (_, _) => Editor.SetTool(ToolSpec.Select);
        PanToolBtn.Click += (_, _) => Editor.SetTool(ToolSpec.Pan);
        NewDiagramMenu.Click += (_, _) => NewDiagram(); NewTabBtn.Click += (_, _) => NewDiagram(); AddDiagramBtn.Click += (_, _) => NewDiagram();
        AddPackageMenu.Click += (_, _) => AddPackage(); AddPkgBtn.Click += (_, _) => AddPackage();
        RenameDiagramMenu.Click += (_, _) => RenameDiagram();
        DuplicateDiagramMenu.Click += (_, _) => DuplicateDiagram();
        DeleteDiagramMenu.Click += (_, _) => DeleteDiagram();
        HelpMenu.Click += (_, _) => Dialogs.Help(this);
        AboutMenu.Click += (_, _) => Dialogs.Help(this);
        SearchBox.TextChanged += (_, _) => BuildExplorer();
        ExplorerTree.SelectedItemChanged += (_, e) =>
        {
            if (e.NewValue is Diagram d) { canvas.SaveCam(); Editor.OpenDiagram(d); canvas.RestoreCam(); }
            else if (e.NewValue is UmlNode n)
            {
                var owner = Editor.Project.AllDiagrams.FirstOrDefault(x => x.Nodes.Contains(n));
                if (owner != null) { canvas.SaveCam(); Editor.OpenDiagram(owner); canvas.RestoreCam(); Editor.SelectOnly(n); canvas.CenterOn(n.Id); }
            }
        };
        ChecksList.MouseDoubleClick += (_, _) =>
        {
            if ((ChecksList.SelectedItem as ListBoxItem)?.Tag is Issue it && it.ElementId.Length > 0)
            {
                var n = Editor.Current.Node(it.ElementId);
                if (n != null) Editor.SelectOnly(n); else { var e = Editor.Current.Edge(it.ElementId); if (e != null) Editor.SelectOnly(e); }
                canvas.CenterOn(it.ElementId);
            }
        };
        GridMenu.IsChecked = settings.Grid; SnapMenu.IsChecked = settings.Snap; GuidesMenu.IsChecked = settings.Guides; ShadowsMenu.IsChecked = settings.Shadows;
    }

    ContextMenu BuildExportMenu()
    {
        var m = new ContextMenu();
        var png = new MenuItem { Header = "Diagram as PNG…" }; png.Click += (_, _) => ExportPng();
        var xmi = new MenuItem { Header = "Model as XMI 2.5…" }; xmi.Click += (_, _) => ExportXmi();
        var json = new MenuItem { Header = "Project as .uforge…" }; json.Click += (_, _) => SaveProject(true);
        m.Items.Add(png); m.Items.Add(xmi); m.Items.Add(json);
        return m;
    }

    void SetPaper(PaperMode m) { settings.Paper = m; canvas.RefreshTheme(); ProjectStore.SaveSettings(settings); }

    // ---------------- tabs ----------------
    bool buildingTabs;
    void BuildTabs()
    {
        if (buildingTabs) return;
        buildingTabs = true;
        try
        {
            DiagramTabs.SelectionChanged -= Tabs_SelectionChanged;
            DiagramTabs.Items.Clear();
            foreach (var d in Editor.Open)
            {
                var sp = new StackPanel { Orientation = Orientation.Horizontal };
                sp.Children.Add(new Path { Data = Catalog.Icon(d.Kind.ToString()), Stroke = LavenderBrush, StrokeThickness = 1.5, Width = 14, Height = 14, Stretch = Stretch.Uniform, Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center });
                sp.Children.Add(new TextBlock { Text = d.Name, VerticalAlignment = VerticalAlignment.Center });
                var close = new Button { Content = "✕", FontSize = 9, Padding = new Thickness(4, 1, 4, 1), Margin = new Thickness(8, 0, 0, 0), Background = Brushes.Transparent, BorderThickness = new Thickness(0) };
                var dd = d;
                close.Click += (_, _) => { Editor.Open.Remove(dd); if (Editor.Current == dd) Editor.Current = Editor.Open.FirstOrDefault(); BuildTabs(); };
                sp.Children.Add(close);
                var tab = new TabItem { Header = sp, Tag = dd };
                tab.MouseDoubleClick += (_, _) => RenameDiagram(dd);
                DiagramTabs.Items.Add(tab);
            }
            DiagramTabs.SelectedItem = DiagramTabs.Items.Cast<TabItem>().FirstOrDefault(t => t.Tag == Editor.Current);
            DiagramTabs.SelectionChanged += Tabs_SelectionChanged;
        }
        finally
        {
            buildingTabs = false;
        }
    }
    void Tabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DiagramTabs.SelectedItem is TabItem { Tag: Diagram d } && d != Editor.Current)
        { canvas.SaveCam(); Editor.Current = d; canvas.RestoreCam(); BuildPalette(); }
    }

    // ---------------- explorer ----------------
    void BuildExplorer()
    {
        string q = SearchBox.Text?.Trim().ToLowerInvariant() ?? "";
        if (q.Length > 0)
        {
            ExplorerTree.Visibility = WinVisibility.Collapsed; SearchResults.Visibility = WinVisibility.Visible;
            SearchResults.Items.Clear();
            foreach (var d in Editor.Project.AllDiagrams)
            {
                if (d.Name.ToLowerInvariant().Contains(q))
                    SearchResults.Items.Add(new ListBoxItem { Content = "📐 " + d.Name, Tag = d });
                foreach (var n in d.Nodes.Where(n => n.Name.Length > 0 && n.Name.ToLowerInvariant().Contains(q)))
                    SearchResults.Items.Add(new ListBoxItem { Content = "   ◦ " + n.Name + "  (" + d.Name + ")", Tag = n });
            }
            SearchResults.SelectionChanged -= SearchResults_Sel; SearchResults.SelectionChanged += SearchResults_Sel;
            return;
        }
        ExplorerTree.Visibility = WinVisibility.Visible; SearchResults.Visibility = WinVisibility.Collapsed;
        ExplorerTree.Items.Clear();
        foreach (var pkg in Editor.Project.Packages)
        {
            var pkgItem = new TreeViewItem { Header = "📦 " + pkg.Name, Tag = pkg, IsExpanded = true };
            pkgItem.ContextMenu = PackageMenu(pkg);
            foreach (var d in pkg.Diagrams)
            {
                var dItem = new TreeViewItem { Header = "📐 " + d.Name, Tag = d, IsExpanded = false };
                dItem.ContextMenu = DiagramMenu(d);
                foreach (var n in d.Nodes.Where(n => n.Name.Length > 0))
                    dItem.Items.Add(new TreeViewItem { Header = "◦ " + n.Name, Tag = n });
                pkgItem.Items.Add(dItem);
            }
            ExplorerTree.Items.Add(pkgItem);
        }
    }
    void SearchResults_Sel(object sender, SelectionChangedEventArgs e)
    {
        if (SearchResults.SelectedItem is not ListBoxItem { Tag: object tag }) return;
        if (tag is Diagram d) { canvas.SaveCam(); Editor.OpenDiagram(d); canvas.RestoreCam(); }
        else if (tag is UmlNode n)
        {
            var owner = Editor.Project.AllDiagrams.FirstOrDefault(x => x.Nodes.Contains(n));
            if (owner != null) { canvas.SaveCam(); Editor.OpenDiagram(owner); canvas.RestoreCam(); Editor.SelectOnly(n); canvas.CenterOn(n.Id); }
        }
    }
    ContextMenu PackageMenu(UmlPackage pkg)
    {
        var m = new ContextMenu();
        var add = new MenuItem { Header = "New diagram…" }; add.Click += (_, _) => NewDiagram(pkg);
        var ren = new MenuItem { Header = "Rename…" }; ren.Click += (_, _) => { var v = Dialogs.Input(this, "Rename package", "Package name:", pkg.Name); if (v != null) Editor.Mutate(() => pkg.Name = v, "Rename package"); };
        var del = new MenuItem { Header = "Delete package" }; del.Click += (_, _) => Editor.Mutate(() => Editor.Project.Packages.Remove(pkg), "Delete package");
        m.Items.Add(add); m.Items.Add(ren); m.Items.Add(del);
        return m;
    }
    ContextMenu DiagramMenu(Diagram d)
    {
        var m = new ContextMenu();
        var open = new MenuItem { Header = "Open" }; open.Click += (_, _) => { canvas.SaveCam(); Editor.OpenDiagram(d); canvas.RestoreCam(); };
        var ren = new MenuItem { Header = "Rename…" }; ren.Click += (_, _) => RenameDiagram(d);
        var dup = new MenuItem { Header = "Duplicate" }; dup.Click += (_, _) => DuplicateDiagram(d);
        var del = new MenuItem { Header = "Delete" }; del.Click += (_, _) => DeleteDiagram(d);
        m.Items.Add(open); m.Items.Add(ren); m.Items.Add(dup); m.Items.Add(del);
        return m;
    }

    // ---------------- palette ----------------
    void BuildPalette()
    {
        PalettePanel.Children.Clear();
        if (Editor.Current == null) return;
        var info = Catalog.Diagrams[Editor.Current.Kind];
        void Section(string title) => PalettePanel.Children.Add(new TextBlock { Text = title, FontSize = 10, FontWeight = FontWeights.Bold, Foreground = LavenderBrush, Margin = new Thickness(4, 8, 0, 4) });
        var nodeGrid = new System.Windows.Controls.Primitives.UniformGrid { Columns = 2 };
        var edgeGrid = new System.Windows.Controls.Primitives.UniformGrid { Columns = 1 };
        int idx = 1;
        Section("ELEMENTS");
        foreach (var k in info.Nodes)
        {
            var b = ToolButton(Catalog.Nodes[k].Label, Catalog.Icon(k.ToString()), idx++, ToolSpec.Place(k), Catalog.Nodes[k].Hint);
            nodeGrid.Children.Add(b);
        }
        PalettePanel.Children.Add(nodeGrid);
        Section("CONNECTORS");
        foreach (var k in info.Edges)
            edgeGrid.Children.Add(ToolButton(Catalog.Edges[k].Label, Catalog.Icon(k.ToString()), idx++, ToolSpec.Connect(k), Catalog.Edges[k].Hint));
        PalettePanel.Children.Add(edgeGrid);
    }
    Button ToolButton(string label, Geometry icon, int index, ToolSpec tool, string hint)
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal };
        sp.Children.Add(new Path { Data = icon, Stroke = IvoryBrush, StrokeThickness = 1.5, Width = 18, Height = 18, Stretch = Stretch.Uniform, Margin = new Thickness(0, 0, 7, 0) });
        sp.Children.Add(new TextBlock { Text = label, Foreground = IvoryBrush, FontSize = 11.5, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis });
        var b = new Button { Content = sp, ToolTip = $"{label}  ({index})\n{hint}", HorizontalContentAlignment = HorizontalAlignment.Left, Margin = new Thickness(2), Padding = new Thickness(7, 5, 7, 5), Tag = tool };
        b.Click += (_, _) => { Editor.SetTool(tool); MarkTool(); };
        return b;
    }
    void MarkTool()
    {
        SelectToolBtn.Background = Editor.Tool.Mode == ToolMode.Select ? IndigoBrush : Brushes.Transparent;
        PanToolBtn.Background = Editor.Tool.Mode == ToolMode.Pan ? IndigoBrush : Brushes.Transparent;
        foreach (var grid in PalettePanel.Children.OfType<System.Windows.Controls.Primitives.UniformGrid>())
            foreach (var b in grid.Children.OfType<Button>())
            {
                bool active = b.Tag is ToolSpec t && t.Mode == Editor.Tool.Mode &&
                              (t.Mode == ToolMode.Place ? t.Node == Editor.Tool.Node : t.Mode == ToolMode.Connect && t.Edge == Editor.Tool.Edge);
                b.BorderBrush = active ? IvoryBrush : BorderIdleBrush;
                b.Background = active ? IndigoBrush : (Brush)FindResource("PanelBrush");
            }
    }

    // ---------------- project / diagram ops ----------------
    void NewProject()
    {
        if (MessageBox.Show("Start a new empty project? The current model stays in autosave until you save over it.", "UmlForge", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        var p = new Project { Name = "Untitled Project" };
        var pkg = new UmlPackage { Name = "Model" };
        pkg.Diagrams.Add(new Diagram { Kind = DiagramKind.Class, Name = "Main" });
        p.Packages.Add(pkg);
        Editor.Project = p; currentFile = ""; canvas.Fit();
    }
    void NewDiagram(UmlPackage target = null)
    {
        var r = Dialogs.NewDiagram(this);
        if (r == null) return;
        var d = new Diagram { Kind = r.Value.kind, Name = r.Value.name };
        var pkg = target ?? Editor.Project.PackageOf(Editor.Current) ?? Editor.Project.Packages.FirstOrDefault();
        if (pkg == null) { pkg = new UmlPackage { Name = "Model" }; Editor.Project.Packages.Add(pkg); }
        Editor.Mutate(() => pkg.Diagrams.Add(d), "New diagram");
        canvas.SaveCam(); Editor.OpenDiagram(d); canvas.Fit();
    }
    void RenameDiagram(Diagram d = null)
    {
        d ??= Editor.Current; if (d == null) return;
        var v = Dialogs.Input(this, "Rename diagram", "Diagram name:", d.Name);
        if (v != null && v.Trim().Length > 0) Editor.Mutate(() => d.Name = v.Trim(), "Rename diagram");
    }
    void DuplicateDiagram(Diagram d = null)
    {
        d ??= Editor.Current; if (d == null) return;
        var pkg = Editor.Project.PackageOf(d);
        var copy = System.Text.Json.JsonSerializer.Deserialize<Diagram>(System.Text.Json.JsonSerializer.Serialize(d, Editor.Json), Editor.Json);
        copy.Id = Guid.NewGuid().ToString("N")[..10]; copy.Name = d.Name + " Copy";
        Editor.Mutate(() => pkg.Diagrams.Add(copy), "Duplicate diagram");
        canvas.SaveCam(); Editor.OpenDiagram(copy); canvas.Fit();
    }
    void DeleteDiagram(Diagram d = null)
    {
        d ??= Editor.Current; if (d == null) return;
        var pkg = Editor.Project.PackageOf(d);
        Editor.Mutate(() => { pkg.Diagrams.Remove(d); Editor.Open.Remove(d); }, "Delete diagram");
        if (Editor.Current == d) Editor.Current = Editor.Open.FirstOrDefault() ?? Editor.Project.AllDiagrams.FirstOrDefault();
        HintStatus.Text = "Diagram deleted — Ctrl+Z to undo.";
    }
    void AddPackage()
    {
        var v = Dialogs.Input(this, "New package", "Package name:", "NewPackage");
        if (v == null || v.Trim().Length == 0) return;
        Editor.Mutate(() => Editor.Project.Packages.Add(new UmlPackage { Name = v.Trim() }), "Add package");
    }
    public void OpenFile(string path)
    {
        try { Editor.Project = ProjectStore.Load(path); currentFile = path; settings.LastFile = path; canvas.Fit(); }
        catch (Exception ex) { MessageBox.Show("Could not open file:\n" + ex.Message, "UmlForge", MessageBoxButton.OK, MessageBoxImage.Error); }
    }
    void OpenProject()
    {
        var dlg = new OpenFileDialog { Filter = "UmlForge project (*.uforge)|*.uforge|JSON (*.json)|*.json|All files (*.*)|*.*", Title = "Open project" };
        if (dlg.ShowDialog() == true) OpenFile(dlg.FileName);
    }
    void SaveProject(bool asNew)
    {
        if (asNew || currentFile.Length == 0)
        {
            var dlg = new SaveFileDialog { Filter = "UmlForge project (*.uforge)|*.uforge", FileName = Editor.Project.Name + ".uforge", Title = "Save project" };
            if (dlg.ShowDialog() != true) return;
            currentFile = dlg.FileName;
        }
        try { ProjectStore.Save(Editor.Project, currentFile); Editor.SavedAtCommit = Editor.CommitCount; settings.LastFile = currentFile; ProjectStore.SaveSettings(settings); SaveStatus.Text = "Saved " + DateTime.Now.ToString("HH:mm"); OnChanged(); }
        catch (Exception ex) { MessageBox.Show("Could not save:\n" + ex.Message, "UmlForge", MessageBoxButton.OK, MessageBoxImage.Error); }
    }
    void ExportPng()
    {
        if (Editor.Current == null) return;
        var dlg = new SaveFileDialog { Filter = "PNG image (*.png)|*.png", FileName = Editor.Current.Name + ".png" };
        if (dlg.ShowDialog() != true) return;
        try { PngExporter.Export(Editor.Current, settings, dlg.FileName); HintStatus.Text = "PNG exported."; }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Export failed"); }
    }
    void ExportXmi()
    {
        var dlg = new SaveFileDialog { Filter = "XMI 2.5 (*.xmi)|*.xmi|XML (*.xml)|*.xml", FileName = Editor.Project.Name + ".xmi" };
        if (dlg.ShowDialog() != true) return;
        try { XmiExporter.Export(Editor.Project, dlg.FileName); HintStatus.Text = "XMI exported (UML 2.5)."; }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Export failed"); }
    }

    // ---------------- validation ----------------
    void RunValidation()
    {
        if (Editor.Current == null) return;
        issues = ValidationService.Validate(Editor.Current);
        canvas.Badges = issues.GroupBy(i => i.ElementId).Where(g => g.Key.Length > 0)
            .ToDictionary(g => g.Key, g => g.Min(i => i.Severity));
        int err = issues.Count(i => i.Severity == Severity.Error), warn = issues.Count(i => i.Severity == Severity.Warning);
        ChecksStatus.Text = err + warn == 0 ? "✓ UML checks passed" : $"✗ {err} errors   ⚠ {warn} warnings";
        ChecksStatus.Foreground = err > 0 ? new SolidColorBrush(Color.FromRgb(255, 77, 109)) : warn > 0 ? new SolidColorBrush(Color.FromRgb(255, 178, 36)) : new SolidColorBrush(Color.FromRgb(53, 208, 127));
        ChecksTab.Header = err + warn == 0 ? "Checks" : $"Checks ({err + warn})";
        ChecksList.Items.Clear();
        foreach (var i in issues.OrderByDescending(i => i.Severity))
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(2) };
            sp.Children.Add(new TextBlock { Text = i.Severity == Severity.Error ? "⛔" : i.Severity == Severity.Warning ? "⚠" : "ℹ", Margin = new Thickness(0, 0, 8, 0) });
            sp.Children.Add(new TextBlock { Text = i.Message, TextWrapping = TextWrapping.Wrap, MaxWidth = 250, FontSize = 12 });
            ChecksList.Items.Add(new ListBoxItem { Content = sp, Tag = i });
        }
        canvas.InvalidateVisual();
    }

    // ---------------- inline editing ----------------
    void ShowInline(Rect screenRect, string text, Action<string> commit)
    {
        CloseInline(true);
        inlineCommit = commit;
        inlineBox = new TextBox { Text = text, FontSize = Math.Max(11, 13 * canvas.Zoom), MinWidth = 120 };
        OverlayCanvas.IsHitTestVisible = true;
        Canvas.SetLeft(inlineBox, screenRect.X); Canvas.SetTop(inlineBox, screenRect.Y);
        inlineBox.Width = Math.Max(120, screenRect.Width);
        OverlayCanvas.Children.Add(inlineBox);
        inlineBox.Focus(); inlineBox.SelectAll();
        inlineBox.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) { CloseInline(true); e.Handled = true; }
            if (e.Key == Key.Escape) { CloseInline(false); e.Handled = true; }
        };
        inlineBox.LostFocus += (_, _) => CloseInline(true);
    }
    void CloseInline(bool commit)
    {
        if (inlineBox == null) return;
        var box = inlineBox; var cb = inlineCommit;
        inlineBox = null; inlineCommit = null;
        OverlayCanvas.Children.Remove(box);
        OverlayCanvas.IsHitTestVisible = false;
        if (commit) cb?.Invoke(box.Text);
        canvas.Focus();
    }

    // ---------------- keyboard ----------------
    void OnKey(object sender, KeyEventArgs e)
    {
        bool typing = Keyboard.FocusedElement is TextBox || Keyboard.FocusedElement is DataGridCell;
        if (e.Key == Key.Space && !typing) { canvas.SetSpace(true); e.Handled = true; return; }
        if (typing) return;
        var ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        var shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
        switch (e.Key)
        {
            case Key.Delete: case Key.Back: Editor.DeleteSelection(); e.Handled = true; break;
            case Key.Z when ctrl && shift: Editor.Redo(); e.Handled = true; break;
            case Key.Z when ctrl: Editor.Undo(); e.Handled = true; break;
            case Key.Y when ctrl: Editor.Redo(); e.Handled = true; break;
            case Key.S when ctrl: SaveProject(false); e.Handled = true; break;
            case Key.O when ctrl: OpenProject(); e.Handled = true; break;
            case Key.A when ctrl: foreach (var n in Editor.Current.Nodes) Editor.SelNodes.Add(n.Id); Editor.NotifySelection(); e.Handled = true; break;
            case Key.D when ctrl: Editor.DuplicateSelection(); e.Handled = true; break;
            case Key.C when ctrl: CopySelection(); e.Handled = true; break;
            case Key.X when ctrl: CopySelection(); Editor.DeleteSelection(); e.Handled = true; break;
            case Key.V when ctrl: Paste(); e.Handled = true; break;
            case Key.V: Editor.SetTool(ToolSpec.Select); MarkTool(); break;
            case Key.H: Editor.SetTool(ToolSpec.Pan); MarkTool(); break;
            case Key.F2: if (Editor.SelNodes.Count == 1) canvas.RequestRename(Editor.Current.Node(Editor.SelNodes.First())); break;
            case Key.F1: Dialogs.Help(this); e.Handled = true; break;
            case Key.D0 when ctrl: canvas.Fit(); e.Handled = true; break;
            case Key.D1 when ctrl: canvas.SetZoom(1); e.Handled = true; break;
            case Key.Escape: canvas.CancelInteraction(); Editor.SetTool(ToolSpec.Select); break;
            case Key.Left: canvas.Nudge(shift ? -10 : -1, 0); break;
            case Key.Right: canvas.Nudge(shift ? 10 : 1, 0); break;
            case Key.Up: canvas.Nudge(0, shift ? -10 : -1); break;
            case Key.Down: canvas.Nudge(0, shift ? 10 : 1); break;
            case Key.OemPlus: canvas.ZoomAt(1.15, new Point(canvas.ActualWidth / 2, canvas.ActualHeight / 2)); break;
            case Key.OemMinus: canvas.ZoomAt(1 / 1.15, new Point(canvas.ActualWidth / 2, canvas.ActualHeight / 2)); break;
            default:
                if (e.Key >= Key.D1 && e.Key <= Key.D9 && !ctrl)
                {
                    int i = e.Key - Key.D1;
                    var buttons = PalettePanel.Children.OfType<System.Windows.Controls.Primitives.UniformGrid>().SelectMany(g => g.Children.OfType<Button>()).ToList();
                    if (i < buttons.Count) buttons[i].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                }
                break;
        }
    }
    void CopySelection()
    {
        if (Editor.SelNodes.Count == 0) return;
        var nodes = Editor.Current.Nodes.Where(n => Editor.SelNodes.Contains(n.Id)).ToList();
        var edges = Editor.Current.Edges.Where(e => Editor.SelNodes.Contains(e.SourceId) && Editor.SelNodes.Contains(e.TargetId)).ToList();
        clipboardData = (nodes, edges);
        HintStatus.Text = $"Copied {nodes.Count} element(s).";
    }
    void Paste()
    {
        if (clipboardData is not (List<UmlNode> nodes, List<UmlEdge> edges)) return;
        Editor.Mutate(() =>
        {
            var map = new Dictionary<string, string>();
            Editor.SelNodes.Clear();
            foreach (var n in nodes)
            {
                var copy = System.Text.Json.JsonSerializer.Deserialize<UmlNode>(System.Text.Json.JsonSerializer.Serialize(n, Editor.Json), Editor.Json);
                copy.Id = Guid.NewGuid().ToString("N")[..10]; copy.X += 32; copy.Y += 32; copy.AttachParentId = null;
                Editor.Current.Nodes.Add(copy); map[n.Id] = copy.Id; Editor.SelNodes.Add(copy.Id);
            }
            foreach (var e in edges)
            {
                var copy = System.Text.Json.JsonSerializer.Deserialize<UmlEdge>(System.Text.Json.JsonSerializer.Serialize(e, Editor.Json), Editor.Json);
                copy.Id = Guid.NewGuid().ToString("N")[..10]; copy.SourceId = map[e.SourceId]; copy.TargetId = map[e.TargetId];
                Editor.Current.Edges.Add(copy);
            }
        }, "Paste");
        Editor.NotifySelection();
    }
}
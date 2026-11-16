using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace UmlForge;

public static class Dialogs
{
    static Brush Bg => new SolidColorBrush(Color.FromRgb(18, 23, 58));        // #091A6E
    static Brush Fg => new SolidColorBrush(Color.FromRgb(252, 249, 236));     // #FCF9EC
    static Brush Muted => new SolidColorBrush(Color.FromRgb(201, 181, 224));  // #C9B5E0

    public static string Input(Window owner, string title, string label, string value)
    {
        var w = new Window { Title = title, Width = 380, SizeToContent = SizeToContent.Height, WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = owner, Background = Bg, ResizeMode = ResizeMode.NoResize, WindowStyle = WindowStyle.ToolWindow };
        var sp = new StackPanel { Margin = new Thickness(16) };
        sp.Children.Add(new TextBlock { Text = label, Foreground = Muted, Margin = new Thickness(0, 0, 0, 6) });
        var tb = new TextBox { Text = value };
        sp.Children.Add(tb);
        var ok = new Button { Content = "OK", Width = 80, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0), IsDefault = true };
        ok.Click += (_, _) => { w.DialogResult = true; };
        sp.Children.Add(ok);
        w.Content = sp; w.Loaded += (_, _) => { tb.Focus(); tb.SelectAll(); };
        return w.ShowDialog() == true ? tb.Text : null;
    }
    public static (Project project, UmlPackage focusPkg, string filePath)? StartupPicker(Window owner, Project autosaved, string lastFile)
    {
        var w = new Window
        {
            Title = "UmlForge — Select Package",
            Width = 540,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Background = new SolidColorBrush(Color.FromRgb(0, 16, 91)), // #00105B
            ResizeMode = ResizeMode.NoResize
        };

        var outer = new Border { BorderBrush = new SolidColorBrush(Color.FromRgb(96, 97, 182)), BorderThickness = new Thickness(1) };
        var root = new StackPanel();

        // Top Indigo Header Strip (#6061B6)
        var header = new Border { Background = new SolidColorBrush(Color.FromRgb(96, 97, 182)), Padding = new Thickness(18, 12, 18, 12) };
        var headerStack = new StackPanel();
        headerStack.Children.Add(new TextBlock { Text = "UMLFORGE WORKSPACE", Foreground = Fg, FontSize = 15, FontWeight = FontWeights.Bold });
        headerStack.Children.Add(new TextBlock { Text = "Select a package to open or initialize a new model.", Foreground = Fg, FontSize = 11.5, Opacity = 0.9, Margin = new Thickness(0, 2, 0, 0) });
        header.Child = headerStack;
        root.Children.Add(header);

        var sp = new StackPanel { Margin = new Thickness(18) };
        Project chosenProj = null;
        UmlPackage chosenPkg = null;
        string chosenFile = "";

        if (autosaved != null && autosaved.Packages.Count > 0)
        {
            sp.Children.Add(new TextBlock { Text = $"RECENT PROJECT: {autosaved.Name.ToUpperInvariant()}", Foreground = Muted, FontSize = 10.5, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 6) });
            var pkgList = new ListBox { MaxHeight = 165, Margin = new Thickness(0, 0, 0, 10) };
            foreach (var pkg in autosaved.Packages)
                pkgList.Items.Add(new ListBoxItem { Content = $"📦  {pkg.Name}   —   {pkg.Diagrams.Count} diagram(s)", Tag = pkg });
            pkgList.SelectedIndex = 0;
            sp.Children.Add(pkgList);

            var openRecent = new Button { Content = "Open Selected Package", HorizontalAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(0, 0, 0, 12), IsDefault = true };
            openRecent.Click += (_, _) =>
            {
                chosenProj = autosaved;
                chosenPkg = (pkgList.SelectedItem as ListBoxItem)?.Tag as UmlPackage;
                chosenFile = lastFile ?? "";
                w.DialogResult = true;
            };
            sp.Children.Add(openRecent);
        }

        sp.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Color.FromRgb(96, 97, 182)), Margin = new Thickness(0, 0, 0, 12) });

        var newBtn = new Button { Content = "+  Create New Project & Package…", HorizontalAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(0, 0, 0, 8) };
        newBtn.Click += (_, _) =>
        {
            var projName = Input(w, "New Project", "Project name:", "MyProject");
            if (string.IsNullOrWhiteSpace(projName)) return;
            var pkgName = Input(w, "Initial Package", "Package name:", "CoreModel");
            if (string.IsNullOrWhiteSpace(pkgName)) return;
            var p = new Project { Name = projName.Trim() };
            var pkg = new UmlPackage { Name = pkgName.Trim() };
            pkg.Diagrams.Add(new Diagram { Kind = DiagramKind.Class, Name = "Main" });
            p.Packages.Add(pkg);
            chosenProj = p; chosenPkg = pkg;
            w.DialogResult = true;
        };
        sp.Children.Add(newBtn);

        var openFileBtn = new Button { Content = "Open .uforge File from Disk…", HorizontalAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(0, 0, 0, 8) };
        openFileBtn.Click += (_, _) =>
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "UmlForge project (*.uforge)|*.uforge|JSON (*.json)|*.json|All files (*.*)|*.*" };
            if (dlg.ShowDialog() == true)
            {
                try
                {
                    chosenProj = ProjectStore.Load(dlg.FileName);
                    chosenPkg = chosenProj.Packages.FirstOrDefault();
                    chosenFile = dlg.FileName;
                    w.DialogResult = true;
                }
                catch (Exception ex) { MessageBox.Show("Could not open file:\n" + ex.Message, "UmlForge"); }
            }
        };
        sp.Children.Add(openFileBtn);

        var sampleBtn = new Button { Content = "Load Sample Project (Library Management System)", HorizontalAlignment = HorizontalAlignment.Stretch };
        sampleBtn.Click += (_, _) =>
        {
            chosenProj = Seed.Build();
            chosenPkg = chosenProj.Packages.FirstOrDefault();
            w.DialogResult = true;
        };
        sp.Children.Add(sampleBtn);

        root.Children.Add(sp);
        outer.Child = root;
        w.Content = outer;

        if (w.ShowDialog() == true && chosenProj != null)
            return (chosenProj, chosenPkg, chosenFile);

        return null;
    }

    public static (DiagramKind kind, string name)? NewDiagram(Window owner)
    {
        var w = new Window { Title = "New Diagram", Width = 640, Height = 520, WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = owner, Background = Bg };
        var grid = new Grid { Margin = new Thickness(16) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition());
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.Children.Add(new TextBlock { Text = "Choose a UML diagram type", Foreground = Fg, FontSize = 16, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 10) });
        var list = new ListBox { Background = Bg, BorderThickness = new Thickness(0) };
        foreach (var kv in Catalog.Diagrams)
        {
            var icon = new Path { Data = Catalog.Icon(kv.Key.ToString()), Stroke = new SolidColorBrush(Color.FromRgb(77, 141, 255)), StrokeThickness = 1.6, Width = 24, Height = 24, Stretch = Stretch.Uniform, Margin = new Thickness(0, 0, 10, 0) };
            var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(4) };
            sp.Children.Add(icon);
            var txt = new StackPanel();
            txt.Children.Add(new TextBlock { Text = kv.Value.Label, Foreground = Fg, FontWeight = FontWeights.SemiBold });
            txt.Children.Add(new TextBlock { Text = kv.Value.Desc, Foreground = Muted, FontSize = 11 });
            sp.Children.Add(txt);
            list.Items.Add(new ListBoxItem { Content = sp, Tag = kv.Key });
        }
        list.SelectedIndex = 0;
        Grid.SetRow(list, 1); grid.Children.Add(list);
        var nameRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
        nameRow.Children.Add(new TextBlock { Text = "Name:", Foreground = Muted, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
        var nameBox = new TextBox { Text = "New Diagram", Width = 300 };
        nameRow.Children.Add(nameBox);
        Grid.SetRow(nameRow, 2); grid.Children.Add(nameRow);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
        var create = new Button { Content = "Create", Width = 90, IsDefault = true, Margin = new Thickness(0, 0, 8, 0) };
        var cancel = new Button { Content = "Cancel", Width = 80, IsCancel = true };
        create.Click += (_, _) => { w.DialogResult = true; };
        buttons.Children.Add(create); buttons.Children.Add(cancel);
        Grid.SetRow(buttons, 3); grid.Children.Add(buttons);
        w.Content = grid;
        if (w.ShowDialog() == true && list.SelectedItem is ListBoxItem it)
            return ((DiagramKind)it.Tag, nameBox.Text.Trim().Length == 0 ? "Diagram" : nameBox.Text.Trim());
        return null;
    }

    public static void Help(Window owner)
    {
        var w = new Window { Title = "UmlForge Help", Width = 720, Height = 560, WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = owner, Background = Bg };
        var tabs = new TabControl { Margin = new Thickness(10) };
        // Shortcuts
        var sc = new StackPanel { Margin = new Thickness(12) };
        string[,] keys = {
            { "V / H", "Select / Pan tool" }, { "1 – 9", "Pick palette tool" }, { "F2", "Rename selected element" },
            { "Double-click", "Rename · double-click a class compartment to type “+ name : Type” or “+ op(args) : Ret”" },
            { "Drag from blue port", "Quick-connect with the diagram's default relationship" },
            { "Delete", "Delete selection" }, { "Ctrl+Z / Ctrl+Y", "Undo / Redo" }, { "Ctrl+C / V / X / D", "Copy / Paste / Cut / Duplicate" },
            { "Ctrl+A", "Select all" }, { "Ctrl+S / Ctrl+O", "Save / Open project" }, { "Ctrl+0 / Ctrl+1", "Fit / 100% zoom" },
            { "Mouse wheel", "Zoom at cursor" }, { "Space+drag / middle drag", "Pan" }, { "Arrows (+Shift)", "Nudge 1px (10px)" },
            { "Esc", "Cancel tool / selection" }
        };
        for (int i = 0; i < keys.GetLength(0); i++)
        {
            var row = new DockPanel { Margin = new Thickness(0, 3, 0, 3) };
            var k = new TextBlock { Text = keys[i, 0], Foreground = new SolidColorBrush(Color.FromRgb(43, 217, 232)), Width = 220, FontFamily = new FontFamily("Consolas") };
            DockPanel.SetDock(k, Dock.Left);
            row.Children.Add(k);
            row.Children.Add(new TextBlock { Text = keys[i, 1], Foreground = Fg, TextWrapping = TextWrapping.Wrap });
            sc.Children.Add(row);
        }
        tabs.Items.Add(new TabItem { Header = "Shortcuts", Content = new ScrollViewer { Content = sc } });
        // Notation
        var nt = new StackPanel { Margin = new Thickness(12) };
        void Note(string title, string body)
        {
            nt.Children.Add(new TextBlock { Text = title, Foreground = Fg, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 8, 0, 2) });
            nt.Children.Add(new TextBlock { Text = body, Foreground = Muted, TextWrapping = TextWrapping.Wrap, FontSize = 12 });
        }
        Note("Association", "Solid line. Open arrowheads only on navigable ends; role names and multiplicities sit near the ends.");
        Note("Aggregation / Composition", "Hollow / filled diamond at the WHOLE end. Drag whole → part. Composition = exclusive ownership (a part has at most one composite).");
        Note("Generalization", "Solid line + hollow triangle at the parent. Drag child → parent. Cycles are rejected.");
        Note("Realization", "Dashed line + hollow triangle at the interface. Drag classifier → interface.");
        Note("Dependency / Usage", "Dashed line + open arrow at the supplier. «use» for usage.");
        Note("Include / Extend", "Dashed open arrows between use cases. include: base → included. extend: extension → base.");
        Note("Class compartments", "Name (bold; italic when abstract), attributes, operations. Visibility: + public, - private, # protected, ~ package. Static features underlined. Attribute syntax: + name : Type [mult] = default {readOnly}.");
        Note("Sequence", "Sync = solid filled arrowhead (creates activation). Async = open. Reply = dashed open (closes activation). «create» targets the lifeline head; delete ends in an X.");
        Note("Activity", "Initial = filled circle; activity final = bullseye; flow final = circle with X. Decision/merge = diamond; fork/join = thick bar. Decision outputs carry [guards].");
        Note("State machine", "Rounded states with entry / exit / do compartment. Transitions: trigger [guard] / effect. Initial transition has no trigger/guard; final states have no outgoing transitions.");
        Note("Component / Deployment", "«component» box with icon; ports attach to borders; lollipop = provided, socket = required. Nodes are cubes; artifacts carry «artifact» and «deploy» dependencies.");
        tabs.Items.Add(new TabItem { Header = "UML Notation", Content = new ScrollViewer { Content = nt } });
        // About
        var ab = new StackPanel { Margin = new Thickness(16) };
        ab.Children.Add(new TextBlock { Text = "UmlForge 1.0", Foreground = Fg, FontSize = 18, FontWeight = FontWeights.SemiBold });
        ab.Children.Add(new TextBlock { Text = "A native UML 2.5 modeling studio for Windows.\nFully offline — your models never leave this PC.\n\nAutosave: %AppData%\\UmlForge\\autosave.uforge\nNative project format: .uforge (JSON)\nInterop: XMI 2.5 export, PNG export.", Foreground = Muted, Margin = new Thickness(0, 10, 0, 0), TextWrapping = TextWrapping.Wrap });
        tabs.Items.Add(new TabItem { Header = "About", Content = ab });
        w.Content = tabs;
        w.ShowDialog();
    }
}
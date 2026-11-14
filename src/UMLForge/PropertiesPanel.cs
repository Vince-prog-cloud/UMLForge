using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;

namespace UmlForge;

public class PropertiesPanel : ScrollViewer
{
    readonly Editor ed;
    readonly StackPanel root = new() { Margin = new Thickness(10) };
    public AppSettings Settings;
    public bool Editing;

    public PropertiesPanel(Editor ed, AppSettings settings)
    {
        this.ed = ed; Settings = settings;
        Content = root;
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        ed.SelectionChanged += Rebuild;
        ed.Changed += () => { if (!Editing) Rebuild(); };
        Rebuild();
    }

    readonly List<Action> cleanup = new();

    void Rebuild()
    {
        foreach (var dg in root.Children.OfType<DataGrid>())
        {
            dg.CommitEdit(DataGridEditingUnit.Cell, true);
            dg.CommitEdit(DataGridEditingUnit.Row, true);
            if (dg.Items is System.ComponentModel.IEditableCollectionView view && (view.IsEditingItem || view.IsAddingNew))
            {
                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, Rebuild);
                return;
            }
        }
        foreach (var dispose in cleanup) dispose();
        cleanup.Clear();

        root.Children.Clear();
        if (ed.Current == null) return;
        if (ed.SelNodes.Count == 1) NodeForm(ed.Current.Node(ed.SelNodes.First()));
        else if (ed.SelEdges.Count == 1) EdgeForm(ed.Current.Edge(ed.SelEdges.First()));
        else if (ed.SelNodes.Count > 1) MultiForm();
        else DiagramForm();
    }

    static Brush Muted => new SolidColorBrush(Color.FromRgb(143, 161, 192));
    static Brush Text => new SolidColorBrush(Color.FromRgb(232, 238, 249));

    void Header(string title, string sub)
    {
        root.Children.Add(new TextBlock { Text = title, FontSize = 15, FontWeight = FontWeights.SemiBold, Foreground = Text, Margin = new Thickness(0, 0, 0, 2) });
        if (sub != null) root.Children.Add(new TextBlock { Text = sub, FontSize = 11, Foreground = Muted, Margin = new Thickness(0, 0, 0, 10), TextWrapping = TextWrapping.Wrap });
    }
    void Label(string s) => root.Children.Add(new TextBlock { Text = s, FontSize = 11, Foreground = Muted, Margin = new Thickness(0, 8, 0, 3) });

    void TextField(string label, string value, Action<string> apply, bool multi = false)
    {
        Label(label);
        var tb = new TextBox { Text = value ?? "", AcceptsReturn = multi, MinHeight = multi ? 60 : 0, TextWrapping = multi ? TextWrapping.Wrap : TextWrapping.NoWrap };
        tb.GotFocus += (_, _) => Editing = true;
        tb.LostFocus += (_, _) => { Editing = false; if (tb.Text != (value ?? "")) ed.Mutate(() => apply(tb.Text), "Edit " + label); };
        tb.KeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Enter && !multi) { e.Handled = true; System.Windows.Input.Keyboard.ClearFocus(); } };
        root.Children.Add(tb);
    }
    void ComboField<T>(string label, T value, IEnumerable<T> options, Action<T> apply)
    {
        Label(label);
        var cb = new ComboBox { ItemsSource = options.ToList(), SelectedItem = value };
        cb.SelectionChanged += (_, _) => { if (cb.SelectedItem is T v && !EqualityComparer<T>.Default.Equals(v, value)) ed.Mutate(() => apply(v), "Edit " + label); };
        root.Children.Add(cb);
    }
    void CheckField(string label, bool value, Action<bool> apply)
    {
        var cb = new CheckBox { Content = label, IsChecked = value, Foreground = Text, Margin = new Thickness(0, 8, 0, 0) };
        cb.Checked += (_, _) => ed.Mutate(() => apply(true), label);
        cb.Unchecked += (_, _) => ed.Mutate(() => apply(false), label);
        root.Children.Add(cb);
    }
    void NumField(string label, double value, Action<double> apply)
    {
        Label(label);
        var tb = new TextBox { Text = value.ToString("0.##") };
        tb.GotFocus += (_, _) => Editing = true;
        tb.LostFocus += (_, _) => { Editing = false; if (double.TryParse(tb.Text, out var v)) ed.Mutate(() => apply(v), "Edit " + label); };
        root.Children.Add(tb);
    }
    void Button(string label, Action a)
    {
        var b = new Button { Content = label, Margin = new Thickness(0, 8, 0, 0), HorizontalAlignment = HorizontalAlignment.Left };
        b.Click += (_, _) => a();
        root.Children.Add(b);
    }

    void Grid<T>(string label, ObservableCollection<T> items, params (string header, string path, double width)[] cols) where T : class
    {
        Label(label);
        var dg = new DataGrid
        {
            ItemsSource = items,
            AutoGenerateColumns = false,
            CanUserAddRows = true,
            CanUserDeleteRows = true,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            MaxHeight = 220,
            Margin = new Thickness(0, 2, 0, 0)
        };
        foreach (var (h, p, w) in cols)
            dg.Columns.Add(new DataGridTextColumn { Header = h, Binding = new Binding(p), Width = w > 0 ? w : double.NaN });

        void CommitGrid()
        {
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, () =>
            {
                if (dg.Items is System.ComponentModel.IEditableCollectionView view && (view.IsEditingItem || view.IsAddingNew))
                    return;
                ed.Commit("Edit " + label);
                Editing = false;
            });
        }

        dg.BeginningEdit += (_, _) => { Editing = true; ed.Begin(); };
        dg.CellEditEnding += (_, _) => CommitGrid();
        dg.RowEditEnding += (_, _) => CommitGrid();
        dg.InitializingNewItem += (_, _) => { Editing = true; ed.Begin(); };
        dg.PreviewKeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Delete) ed.Begin(); };
        items.CollectionChanged += (_, _) =>
        {
            if (dg.Items is System.ComponentModel.IEditableCollectionView view && (view.IsEditingItem || view.IsAddingNew))
                return;
            ed.Commit("Edit " + label);
        };
        root.Children.Add(dg);
    }

    void StringList(string label, ObservableCollection<string> items)
    {
        Label(label);
        var sp = new StackPanel();
        void Rebuild()
        {
            sp.Children.Clear();
            foreach (var item in items.ToList())
            {
                var row = new DockPanel { Margin = new Thickness(0, 2, 0, 0) };
                var del = new Button { Content = "✕", Width = 26, Tag = item };
                del.Click += (_, _) => ed.Mutate(() => items.Remove(item), "Remove");
                DockPanel.SetDock(del, Dock.Right);
                var tb = new TextBox { Text = item };
                tb.GotFocus += (_, _) => Editing = true;
                tb.LostFocus += (_, _) => { Editing = false; int i = items.IndexOf(item); if (i >= 0 && tb.Text != item) ed.Mutate(() => items[i] = tb.Text, "Edit"); };
                row.Children.Add(del); row.Children.Add(tb);
                sp.Children.Add(row);
            }
            var add = new Button { Content = "+ Add", Margin = new Thickness(0, 4, 0, 0), HorizontalAlignment = HorizontalAlignment.Left };
            add.Click += (_, _) => ed.Mutate(() => items.Add("item"), "Add");
            sp.Children.Add(add);
        }
        Rebuild();
        root.Children.Add(sp);
    }

    void NodeForm(UmlNode n)
    {
        if (n == null) return;
        var def = Catalog.Nodes[n.Kind];
        Header(def.Label, def.Hint.Length > 0 ? def.Hint : null);
        bool named = n.Kind is not (NodeKind.Initial or NodeKind.StateInitial or NodeKind.ActivityFinal or NodeKind.StateFinal
            or NodeKind.FlowFinal or NodeKind.Junction or NodeKind.Terminate or NodeKind.Choice or NodeKind.Fork);
        if (named) TextField("Name", n.Name, v => n.Name = v, n.Kind == NodeKind.Note);
        if (n.Kind is NodeKind.Class or NodeKind.DataType) TextField("Stereotype", n.Stereotype, v => n.Stereotype = v);
        if (n.Kind is NodeKind.Class or NodeKind.Interface or NodeKind.DataType)
        {
            ComboField("Visibility", n.Visibility, Enum.GetValues<Visibility>(), v => n.Visibility = v);
            CheckField("Abstract (italic name)", n.IsAbstract, v => n.IsAbstract = v);
        }
        if (n.Kind is NodeKind.Class or NodeKind.Interface or NodeKind.DataType)
        {
            Grid("Attributes", n.Attributes,
                ("Vis", "Visibility", 60), ("Name", "Name", 90), ("Type", "Type", 70), ("Mult", "Multiplicity", 50), ("Default", "Default", 60), ("Static", "IsStatic", 44), ("RO", "IsReadOnly", 36));
            Grid("Operations", n.Operations,
                ("Vis", "Visibility", 60), ("Name", "Name", 90), ("Parameters", "Parameters", 110), ("Returns", "ReturnType", 70), ("Static", "IsStatic", 44), ("Abs", "IsAbstract", 40));
        }
        if (n.Kind == NodeKind.Enumeration) StringList("Literals", n.Literals);
        if (n.Kind == NodeKind.Object) { TextField("Classifier (Type)", n.Represents, v => n.Represents = v); StringList("Slots (name = value)", n.Slots); }
        if (n.Kind == NodeKind.UseCase) StringList("Extension points", n.ExtensionPoints);
        if (n.Kind == NodeKind.Lifeline) TextField("Represents (Type)", n.Represents, v => n.Represents = v);
        if (n.Kind == NodeKind.State)
        {
            TextField("entry /", n.Entry, v => n.Entry = v);
            TextField("exit /", n.Exit, v => n.Exit = v);
            TextField("do /", n.Do, v => n.Do = v);
            Grid("Internal transitions", n.Internals, ("Trigger", "Trigger", 90), ("Guard", "Guard", 70), ("Effect", "Effect", 90));
        }
        if (n.Kind == NodeKind.Fragment)
        {
            ComboField("Operator", n.Operator, new[] { "alt", "opt", "loop", "par", "break", "critical", "seq", "strict" }, v => n.Operator = v);
            Grid("Operands", n.Operands, ("Guard", "Guard", 160), ("Height", "Height", 70));
        }
        if (n.Kind == NodeKind.Partition)
        {
            ComboField("Orientation", n.Orientation, new[] { "Vertical", "Horizontal" }, v => n.Orientation = v);
            StringList("Lanes", n.Lanes);
        }
        if (n.Kind == NodeKind.Port) ComboField("Direction", "inout", new[] { "in", "out", "inout" }, _ => { });
        if (n.Kind == NodeKind.ObjectNode) TextField("Type", n.Represents, v => n.Represents = v);

        Label("Geometry");
        var gp = new WrapPanel();
        void Num(string l, double v, Action<double> a)
        {
            var tb = new TextBox { Text = v.ToString("0"), Width = 58, Margin = new Thickness(0, 0, 6, 0) };
            tb.GotFocus += (_, _) => Editing = true;
            tb.LostFocus += (_, _) => { Editing = false; if (double.TryParse(tb.Text, out var x)) ed.Mutate(() => a(x), "Geometry"); };
            gp.Children.Add(new TextBlock { Text = l, Foreground = Muted, FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 3, 0) });
            gp.Children.Add(tb);
        }
        Num("X", n.X, v => n.X = v); Num("Y", n.Y, v => n.Y = v);
        if (!Catalog.IsFixed(n.Kind)) { Num("W", n.W, v => n.W = v); Num("H", n.H, v => n.H = v); }
        root.Children.Add(gp);
        TextField("Fill override (#RRGGBB, empty = paper)", n.Fill, v => n.Fill = string.IsNullOrWhiteSpace(v) ? null : v);
        Button("Delete element", () => ed.DeleteSelection());
    }

    void EdgeForm(UmlEdge e)
    {
        if (e == null) return;
        var def = Catalog.Edges[e.Kind];
        Header(def.Label, def.Hint.Length > 0 ? def.Hint : null);
        ComboField("Relationship", e.Kind, Catalog.EdgeKindsFor(ed.Current.Kind), v =>
        {
            var s = ed.Current.Node(e.SourceId); var t = ed.Current.Node(e.TargetId);
            if (Catalog.CanConnect(v, s, t, ed.Current.Kind).ok) e.Kind = v;
        });
        if (e.Kind == EdgeKind.Transition)
        {
            TextField("Trigger", e.Trigger, v => e.Trigger = v);
            TextField("Guard", e.Guard, v => e.Guard = v);
            TextField("Effect", e.Effect, v => e.Effect = v);
        }
        else if (e.IsMessage)
        {
            TextField("Signature", e.Name, v => e.Name = v);
            ComboField("Sort", e.Kind, new[] { EdgeKind.MessageSync, EdgeKind.MessageAsync, EdgeKind.MessageReply, EdgeKind.MessageCreate, EdgeKind.MessageDelete }, v => e.Kind = v);
        }
        else if (e.Kind is EdgeKind.ControlFlow or EdgeKind.ObjectFlow)
        {
            TextField("Guard", e.Guard, v => e.Guard = v);
            TextField("Label", e.Name, v => e.Name = v);
        }
        else
        {
            TextField("Name", e.Name, v => e.Name = v);
            if (e.Kind == EdgeKind.Extend) TextField("Condition", e.Condition, v => e.Condition = v);
        }
        if (e.Kind is EdgeKind.Association or EdgeKind.Aggregation or EdgeKind.Composition or EdgeKind.Link or EdgeKind.CommunicationPath)
        {
            Label("Source end (drag origin)");
            TextField("Role", e.SourceRole, v => e.SourceRole = v);
            TextField("Multiplicity", e.SourceMultiplicity, v => e.SourceMultiplicity = v);
            Label("Target end");
            TextField("Role", e.TargetRole, v => e.TargetRole = v);
            TextField("Multiplicity", e.TargetMultiplicity, v => e.TargetMultiplicity = v);
            if (e.Kind == EdgeKind.Association)
            {
                CheckField("Navigable → source", e.SourceNavigable, v => e.SourceNavigable = v);
                CheckField("Navigable → target", e.TargetNavigable, v => e.TargetNavigable = v);
                CheckField("Show reading direction ▶", e.ShowDirection, v => e.ShowDirection = v);
            }
        }
        ComboField("Routing", e.Routing, Enum.GetValues<Routing>(), v => e.Routing = v);
        Button("Reverse direction", () => ed.Mutate(() => { (e.SourceId, e.TargetId) = (e.TargetId, e.SourceId); (e.SourceRole, e.TargetRole) = (e.TargetRole, e.SourceRole); (e.SourceMultiplicity, e.TargetMultiplicity) = (e.TargetMultiplicity, e.SourceMultiplicity); }, "Reverse"));
        Button("Delete relationship", () => ed.DeleteSelection());
    }

    void MultiForm()
    {
        Header($"{ed.SelNodes.Count} elements selected", "Align and distribute.");
        var wp = new WrapPanel();
        void B(string c, string tip, string how)
        {
            var b = new Button { Content = c, Width = 34, Margin = new Thickness(0, 0, 4, 4), ToolTip = tip };
            b.Click += (_, _) => ed.Align(how);
            wp.Children.Add(b);
        }
        B("⇤", "Align left", "left"); B("⇔", "Align centers", "center"); B("⇥", "Align right", "right");
        B("⇡", "Align top", "top"); B("⇕", "Align middles", "middle"); B("⇣", "Align bottom", "bottom");
        root.Children.Add(wp);
        Button("Duplicate selection", () => ed.DuplicateSelection());
        Button("Delete selection", () => ed.DeleteSelection());
    }

    void DiagramForm()
    {
        var d = ed.Current;
        Header(Catalog.Diagrams[d.Kind].Label, Catalog.Diagrams[d.Kind].Desc);
        TextField("Diagram name", d.Name, v => d.Name = v);
        TextField("Description", d.Description, v => d.Description = v, true);
        if (d.Kind == DiagramKind.Sequence) CheckField("Automatic message numbers", d.ShowSequenceNumbers, v => d.ShowSequenceNumbers = v);
        root.Children.Add(new TextBlock { Text = $"{d.Nodes.Count} elements · {d.Edges.Count} relationships", Foreground = Muted, FontSize = 11, Margin = new Thickness(0, 12, 0, 0) });
        root.Children.Add(new TextBlock { Text = "Select an element to edit its UML properties.", Foreground = Muted, FontSize = 11, Margin = new Thickness(0, 6, 0, 0), TextWrapping = TextWrapping.Wrap });
    }
}
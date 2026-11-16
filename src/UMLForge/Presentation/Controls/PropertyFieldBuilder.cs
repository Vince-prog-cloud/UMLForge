using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace UmlForge;

public class PropertyFieldBuilder
{
    readonly Editor ed;
    readonly StackPanel root;
    readonly Dispatcher dispatcher;
    readonly List<Action> cleanup;

    public bool Editing { get; set; }

    static Brush Muted => new SolidColorBrush(Color.FromRgb(143, 161, 192));
    static Brush TextBrush => new SolidColorBrush(Color.FromRgb(232, 238, 249));

    public PropertyFieldBuilder(Editor ed, StackPanel root, Dispatcher dispatcher, List<Action> cleanup)
    {
        this.ed = ed;
        this.root = root;
        this.dispatcher = dispatcher;
        this.cleanup = cleanup;
    }

    public void Header(string title, string sub)
    {
        root.Children.Add(new TextBlock { Text = title, FontSize = 15, FontWeight = FontWeights.SemiBold, Foreground = TextBrush, Margin = new Thickness(0, 0, 0, 2) });
        if (sub != null) root.Children.Add(new TextBlock { Text = sub, FontSize = 11, Foreground = Muted, Margin = new Thickness(0, 0, 0, 10), TextWrapping = TextWrapping.Wrap });
    }

    public void Label(string s) =>
        root.Children.Add(new TextBlock { Text = s, FontSize = 11, Foreground = Muted, Margin = new Thickness(0, 8, 0, 3) });

    public void Info(string s, double topMargin = 8) =>
        root.Children.Add(new TextBlock { Text = s, Foreground = Muted, FontSize = 11, Margin = new Thickness(0, topMargin, 0, 0), TextWrapping = TextWrapping.Wrap });

    public void TextField(string label, string value, Action<string> apply, bool multi = false)
    {
        Label(label);
        var tb = new TextBox { Text = value ?? "", AcceptsReturn = multi, MinHeight = multi ? 60 : 0, TextWrapping = multi ? TextWrapping.Wrap : TextWrapping.NoWrap };
        tb.GotFocus += (_, _) => Editing = true;
        tb.LostFocus += (_, _) => { Editing = false; if (tb.Text != (value ?? "")) ed.Mutate(() => apply(tb.Text), "Edit " + label); };
        tb.KeyDown += (_, e) => { if (e.Key == Key.Enter && !multi) { e.Handled = true; Keyboard.ClearFocus(); } };
        root.Children.Add(tb);
    }

    public void ComboField<T>(string label, T value, IEnumerable<T> options, Action<T> apply)
    {
        Label(label);
        var cb = new ComboBox { ItemsSource = options.ToList(), SelectedItem = value };
        cb.SelectionChanged += (_, _) => { if (cb.SelectedItem is T v && !EqualityComparer<T>.Default.Equals(v, value)) ed.Mutate(() => apply(v), "Edit " + label); };
        root.Children.Add(cb);
    }

    public void CheckField(string label, bool value, Action<bool> apply)
    {
        var cb = new CheckBox { Content = label, IsChecked = value, Foreground = TextBrush, Margin = new Thickness(0, 8, 0, 0) };
        cb.Checked += (_, _) => ed.Mutate(() => apply(true), label);
        cb.Unchecked += (_, _) => ed.Mutate(() => apply(false), label);
        root.Children.Add(cb);
    }

    public void Button(string label, Action a)
    {
        var b = new Button { Content = label, Margin = new Thickness(0, 8, 0, 0), HorizontalAlignment = HorizontalAlignment.Left };
        b.Click += (_, _) => a();
        root.Children.Add(b);
    }

    public void Grid<T>(string label, ObservableCollection<T> items, params (string header, string path, double width)[] cols) where T : class
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
            dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
            {
                if (dg.Items is IEditableCollectionView view && (view.IsEditingItem || view.IsAddingNew)) return;
                ed.Commit("Edit " + label);
                Editing = false;
            });
        }

        dg.BeginningEdit += (_, _) => { Editing = true; ed.Begin(); };
        dg.CellEditEnding += (_, _) => CommitGrid();
        dg.RowEditEnding += (_, _) => CommitGrid();
        dg.InitializingNewItem += (_, _) => { Editing = true; ed.Begin(); };
        dg.PreviewKeyDown += (_, e) => { if (e.Key == Key.Delete) ed.Begin(); };

        NotifyCollectionChangedEventHandler onColChanged = (_, _) =>
        {
            if (dg.Items is IEditableCollectionView view && (view.IsEditingItem || view.IsAddingNew)) return;
            ed.Commit("Edit " + label);
        };
        items.CollectionChanged += onColChanged;
        cleanup.Add(() => items.CollectionChanged -= onColChanged);
        root.Children.Add(dg);
    }

    public void StringList(string label, ObservableCollection<string> items)
    {
        Label(label);
        var sp = new StackPanel();
        for (int idx = 0; idx < items.Count; idx++)
        {
            int rowIndex = idx;
            string item = items[rowIndex];
            var row = new DockPanel { Margin = new Thickness(0, 2, 0, 0) };
            var del = new Button { Content = "✕", Width = 26 };
            del.Click += (_, _) => ed.Mutate(() => items.RemoveAt(rowIndex), "Remove");
            DockPanel.SetDock(del, Dock.Right);
            var tb = new TextBox { Text = item };
            tb.GotFocus += (_, _) => Editing = true;
            tb.LostFocus += (_, _) => { Editing = false; if (rowIndex < items.Count && tb.Text != items[rowIndex]) ed.Mutate(() => items[rowIndex] = tb.Text, "Edit"); };
            row.Children.Add(del);
            row.Children.Add(tb);
            sp.Children.Add(row);
        }
        var add = new Button { Content = "+ Add", Margin = new Thickness(0, 4, 0, 0), HorizontalAlignment = HorizontalAlignment.Left };
        add.Click += (_, _) => ed.Mutate(() => items.Add("item"), "Add");
        sp.Children.Add(add);
        root.Children.Add(sp);
    }

    public void GeometryRow(UmlNode n)
    {
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
        Num("X", n.X, v => n.X = v);
        Num("Y", n.Y, v => n.Y = v);
        if (!Catalog.IsFixed(n.Kind)) { Num("W", n.W, v => n.W = v); Num("H", n.H, v => n.H = v); }
        root.Children.Add(gp);
    }

    public void AlignmentRow()
    {
        var wp = new WrapPanel();
        void B(string c, string tip, string how)
        {
            var b = new Button { Content = c, Width = 34, Margin = new Thickness(0, 0, 4, 4), ToolTip = tip };
            b.Click += (_, _) => ed.Align(how);
            wp.Children.Add(b);
        }
        B("⇤", "Align left", "left"); B("⇔", "Align centers", "center"); B("⇥", "Align right", "right");
        B("⤒", "Align top", "top"); B("⇕", "Align middles", "middle"); B("⤓", "Align bottom", "bottom");
        root.Children.Add(wp);
    }
}
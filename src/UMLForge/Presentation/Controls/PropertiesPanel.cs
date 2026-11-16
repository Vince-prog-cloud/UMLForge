using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace UmlForge;

public class PropertiesPanel : ScrollViewer
{
    readonly Editor ed;
    readonly StackPanel root = new() { Margin = new Thickness(10) };
    readonly List<Action> cleanup = new();
    readonly PropertyFieldBuilder builder;

    public AppSettings Settings;
    public bool Editing { get => builder.Editing; set => builder.Editing = value; }

    public PropertiesPanel(Editor ed, AppSettings settings)
    {
        this.ed = ed;
        Settings = settings;
        builder = new PropertyFieldBuilder(ed, root, Dispatcher, cleanup);
        Content = root;
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        ed.SelectionChanged += Rebuild;
        ed.Changed += () => { if (!Editing) Rebuild(); };
        Rebuild();
    }

    void Rebuild()
    {
        foreach (var dg in root.Children.OfType<DataGrid>())
        {
            dg.CommitEdit(DataGridEditingUnit.Cell, true);
            dg.CommitEdit(DataGridEditingUnit.Row, true);
            if (dg.Items is IEditableCollectionView view && (view.IsEditingItem || view.IsAddingNew))
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Background, Rebuild);
                return;
            }
        }
        foreach (var dispose in cleanup) dispose();
        cleanup.Clear();
        root.Children.Clear();

        if (ed.Current == null) return;
        if (ed.SelNodes.Count == 1) ElementPropertyForms.BuildNodeForm(builder, ed, ed.Current.Node(ed.SelNodes.First()));
        else if (ed.SelEdges.Count == 1) ElementPropertyForms.BuildEdgeForm(builder, ed, ed.Current.Edge(ed.SelEdges.First()));
        else if (ed.SelNodes.Count > 1) ElementPropertyForms.BuildMultiForm(builder, ed);
        else ElementPropertyForms.BuildDiagramForm(builder, ed.Current);
    }
}
using System.Windows;
using System.Windows.Controls;

namespace UmlForge;

public static class CanvasContextMenu
{
    public static ContextMenu Build(Editor ed, DiagramCanvas canvas, UmlNode node, UmlEdge edge)
    {
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
            Item("Rename", () => canvas.RequestRename(node), "F2");
            if (node.Kind == NodeKind.Class)
            {
                Item("Add attribute", () => canvas.QuickAdd(node, "attrs"));
                Item("Add operation", () => canvas.QuickAdd(node, "ops"));
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
            Item("Edit label", () => canvas.RequestEdgeLabel(edge));
            Item("Reverse direction", () => ed.Mutate(() =>
            {
                (edge.SourceId, edge.TargetId) = (edge.TargetId, edge.SourceId);
                (edge.SourceRole, edge.TargetRole) = (edge.TargetRole, edge.SourceRole);
                (edge.SourceMultiplicity, edge.TargetMultiplicity) = (edge.TargetMultiplicity, edge.SourceMultiplicity);
            }, "Reverse"));
            Item(edge.Routing == Routing.Straight ? "Orthogonal routing" : "Straight routing",
                () => ed.Mutate(() => edge.Routing = edge.Routing == Routing.Straight ? Routing.Orthogonal : Routing.Straight, "Routing"));
            menu.Items.Add(new Separator());
            foreach (var k in Catalog.EdgeKindsFor(ed.Current.Kind).Where(k => k != edge.Kind))
            {
                var kk = k;
                Item("Change to " + Catalog.Edges[kk].Label, () =>
                {
                    var s = ed.Current.Node(edge.SourceId);
                    var t = ed.Current.Node(edge.TargetId);
                    var (ok, reason) = Catalog.CanConnect(kk, s, t, ed.Current.Kind);
                    if (ok) ed.Mutate(() => edge.Kind = kk, "Change type");
                    else canvas.Hint(reason);
                });
            }
            menu.Items.Add(new Separator());
            Item("Delete", () => ed.DeleteSelection(), "Del");
        }
        else
        {
            Item("Select all", () => { foreach (var n in ed.Current.Nodes) ed.SelNodes.Add(n.Id); ed.NotifySelection(); }, "Ctrl+A");
            Item("Fit diagram", canvas.Fit, "Ctrl+0");
            Item("Reset zoom", () => canvas.SetZoom(1), "Ctrl+1");
            menu.Items.Add(new Separator());
            Item(canvas.Settings.Grid ? "Hide grid" : "Show grid", () => { canvas.Settings.Grid = !canvas.Settings.Grid; canvas.InvalidateVisual(); });
            Item(canvas.Settings.Snap ? "Disable snap" : "Enable snap", () => { canvas.Settings.Snap = !canvas.Settings.Snap; });
        }
        return menu;
    }
}
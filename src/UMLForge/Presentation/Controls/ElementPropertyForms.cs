namespace UmlForge;

public static class ElementPropertyForms
{
    public static void BuildNodeForm(PropertyFieldBuilder ui, Editor ed, UmlNode n)
    {
        if (n == null) return;
        var def = Catalog.Nodes[n.Kind];
        ui.Header(def.Label, def.Hint.Length > 0 ? def.Hint : null);

        bool named = n.Kind is not (NodeKind.Initial or NodeKind.StateInitial or NodeKind.ActivityFinal or NodeKind.StateFinal
            or NodeKind.FlowFinal or NodeKind.Junction or NodeKind.Terminate or NodeKind.Choice or NodeKind.Fork or NodeKind.Fragment);
        if (named) ui.TextField("Name", n.Name, v => n.Name = v, n.Kind == NodeKind.Note);
        if (n.Kind is NodeKind.Class or NodeKind.DataType) ui.TextField("Stereotype", n.Stereotype, v => n.Stereotype = v);

        if (n.Kind is NodeKind.Class or NodeKind.Interface or NodeKind.DataType)
        {
            ui.ComboField("Visibility", n.Visibility, Enum.GetValues<Visibility>(), v => n.Visibility = v);
            ui.CheckField("Abstract (italic name)", n.IsAbstract, v => n.IsAbstract = v);
            ui.Grid("Attributes", n.Attributes,
                ("Vis", "Visibility", 60), ("Name", "Name", 90), ("Type", "Type", 70), ("Mult", "Multiplicity", 50), ("Default", "Default", 60), ("Static", "IsStatic", 44), ("RO", "IsReadOnly", 36));
            ui.Grid("Operations", n.Operations,
                ("Vis", "Visibility", 60), ("Name", "Name", 90), ("Parameters", "Parameters", 110), ("Returns", "ReturnType", 70), ("Static", "IsStatic", 44), ("Abs", "IsAbstract", 40));
        }

        if (n.Kind == NodeKind.Enumeration) ui.StringList("Literals", n.Literals);
        if (n.Kind == NodeKind.Object) { ui.TextField("Classifier (Type)", n.Represents, v => n.Represents = v); ui.StringList("Slots (name = value)", n.Slots); }
        if (n.Kind == NodeKind.UseCase) ui.StringList("Extension points", n.ExtensionPoints);
        if (n.Kind == NodeKind.Lifeline) ui.TextField("Represents (Type)", n.Represents, v => n.Represents = v);

        if (n.Kind == NodeKind.State)
        {
            ui.TextField("entry /", n.Entry, v => n.Entry = v);
            ui.TextField("exit /", n.Exit, v => n.Exit = v);
            ui.TextField("do /", n.Do, v => n.Do = v);
            ui.Grid("Internal transitions", n.Internals, ("Trigger", "Trigger", 90), ("Guard", "Guard", 70), ("Effect", "Effect", 90));
        }

        if (n.Kind == NodeKind.Fragment)
        {
            ui.ComboField("Operator", n.Operator, new[] { "alt", "opt", "loop", "par", "break", "critical", "seq", "strict" }, v => n.Operator = v);
            ui.Grid("Operands", n.Operands, ("Guard", "Guard", 160), ("Height", "Height", 70));
        }

        if (n.Kind == NodeKind.Partition)
        {
            ui.ComboField("Orientation", n.Orientation, new[] { "Vertical", "Horizontal" }, v => n.Orientation = v);
            ui.StringList("Lanes", n.Lanes);
        }

        if (n.Kind == NodeKind.ObjectNode) ui.TextField("Type", n.Represents, v => n.Represents = v);
        if (n.Kind == NodeKind.Fork)
        {
            ui.ComboField("Orientation", n.Orientation == "Horizontal" ? "Horizontal" : "Vertical", new[] { "Vertical", "Horizontal" }, v =>
            {
                if (n.Orientation == v) return;
                n.Orientation = v;
                (n.W, n.H) = (n.H, n.W);
            });
        }

        ui.GeometryRow(n);
        ui.TextField("Fill override (#RRGGBB, empty = paper)", n.Fill, v => n.Fill = string.IsNullOrWhiteSpace(v) ? null : v);
        ui.Button("Delete element", ed.DeleteSelection);
    }

    public static void BuildEdgeForm(PropertyFieldBuilder ui, Editor ed, UmlEdge e)
    {
        if (e == null) return;
        var def = Catalog.Edges[e.Kind];
        ui.Header(def.Label, def.Hint.Length > 0 ? def.Hint : null);

        ui.ComboField("Relationship", e.Kind, Catalog.EdgeKindsFor(ed.Current.Kind), v =>
        {
            var s = ed.Current.Node(e.SourceId);
            var t = ed.Current.Node(e.TargetId);
            if (Catalog.CanConnect(v, s, t, ed.Current.Kind).ok) e.Kind = v;
        });

        if (e.Kind == EdgeKind.Transition)
        {
            ui.TextField("Trigger", e.Trigger, v => e.Trigger = v);
            ui.TextField("Guard", e.Guard, v => e.Guard = v);
            ui.TextField("Effect", e.Effect, v => e.Effect = v);
        }
        else if (e.IsMessage)
        {
            ui.TextField("Signature", e.Name, v => e.Name = v);
            ui.ComboField("Sort", e.Kind, new[] { EdgeKind.MessageSync, EdgeKind.MessageAsync, EdgeKind.MessageReply, EdgeKind.MessageCreate, EdgeKind.MessageDelete }, v => e.Kind = v);
        }
        else if (e.Kind is EdgeKind.ControlFlow or EdgeKind.ObjectFlow)
        {
            ui.TextField("Guard", e.Guard, v => e.Guard = v);
            ui.TextField("Label", e.Name, v => e.Name = v);
        }
        else
        {
            ui.TextField("Name", e.Name, v => e.Name = v);
            if (e.Kind == EdgeKind.Extend) ui.TextField("Condition", e.Condition, v => e.Condition = v);
        }

        if (e.Kind is EdgeKind.Association or EdgeKind.Aggregation or EdgeKind.Composition or EdgeKind.Link or EdgeKind.CommunicationPath)
        {
            ui.Label("Source end (drag origin)");
            ui.TextField("Role", e.SourceRole, v => e.SourceRole = v);
            ui.TextField("Multiplicity", e.SourceMultiplicity, v => e.SourceMultiplicity = v);
            ui.Label("Target end");
            ui.TextField("Role", e.TargetRole, v => e.TargetRole = v);
            ui.TextField("Multiplicity", e.TargetMultiplicity, v => e.TargetMultiplicity = v);
            if (e.Kind is EdgeKind.Association or EdgeKind.Aggregation or EdgeKind.Composition)
            {
                if (e.Kind == EdgeKind.Association) ui.CheckField("Navigable → source", e.SourceNavigable, v => e.SourceNavigable = v);
                ui.CheckField("Navigable → target", e.TargetNavigable, v => e.TargetNavigable = v);
                ui.CheckField("Show reading direction ▶", e.ShowDirection, v => e.ShowDirection = v);
            }
        }

        ui.ComboField("Routing", e.Routing, Enum.GetValues<Routing>(), v => e.Routing = v);
        ui.Button("Reverse direction", () => ed.Mutate(() =>
        {
            (e.SourceId, e.TargetId) = (e.TargetId, e.SourceId);
            (e.SourceRole, e.TargetRole) = (e.TargetRole, e.SourceRole);
            (e.SourceMultiplicity, e.TargetMultiplicity) = (e.TargetMultiplicity, e.SourceMultiplicity);
        }, "Reverse"));
        ui.Button("Delete relationship", ed.DeleteSelection);
    }

    public static void BuildMultiForm(PropertyFieldBuilder ui, Editor ed)
    {
        ui.Header($"{ed.SelNodes.Count} elements selected", "Align and distribute.");
        ui.AlignmentRow();
        ui.Button("Duplicate selection", ed.DuplicateSelection);
        ui.Button("Delete selection", ed.DeleteSelection);
    }

    public static void BuildDiagramForm(PropertyFieldBuilder ui, Diagram d)
    {
        ui.Header(Catalog.Diagrams[d.Kind].Label, Catalog.Diagrams[d.Kind].Desc);
        ui.TextField("Diagram name", d.Name, v => d.Name = v);
        ui.TextField("Description", d.Description, v => d.Description = v, true);
        if (d.Kind == DiagramKind.Sequence) ui.CheckField("Automatic message numbers", d.ShowSequenceNumbers, v => d.ShowSequenceNumbers = v);
        ui.Info($"{d.Nodes.Count} elements · {d.Edges.Count} relationships", 12);
        ui.Info("Select an element to edit its UML properties.", 6);
    }
}
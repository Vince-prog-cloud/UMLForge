namespace UmlForge;

public record NodeDef(string Label, double W, double H, bool Fixed = false, bool Container = false, string Hint = "");

public static class NodeCatalog
{
    public static readonly Dictionary<NodeKind, NodeDef> Nodes = new()
    {
        [NodeKind.Class] = new("Class", 170, 90, Hint: "Three compartments: name, attributes, operations."),
        [NodeKind.Interface] = new("Interface", 170, 80, Hint: "«interface» — operations only by convention."),
        [NodeKind.Enumeration] = new("Enumeration", 150, 90, Hint: "«enumeration» — literals in the second compartment."),
        [NodeKind.DataType] = new("DataType", 150, 80),
        [NodeKind.Object] = new("Object", 170, 80, Hint: "Underlined name : Type, slots below."),
        [NodeKind.Actor] = new("Actor", 64, 96, Fixed: false, Hint: "Stick figure; name below."),
        [NodeKind.UseCase] = new("Use Case", 150, 72, Hint: "Ellipse; verb phrase inside."),
        [NodeKind.Boundary] = new("System Boundary", 380, 300, Container: true, Hint: "Subject name at top center."),
        [NodeKind.Lifeline] = new("Lifeline", 140, 340, Hint: "name : Type head, dashed timeline."),
        [NodeKind.Fragment] = new("Combined Fragment", 400, 180, Container: true, Hint: "alt / opt / loop / par / break / critical."),
        [NodeKind.InteractionUse] = new("Interaction Use", 200, 90, Hint: "ref pentagon."),
        [NodeKind.Initial] = new("Initial Node", 20, 20, Fixed: true),
        [NodeKind.ActivityFinal] = new("Activity Final", 26, 26, Fixed: true),
        [NodeKind.FlowFinal] = new("Flow Final", 26, 26, Fixed: true),
        [NodeKind.Action] = new("Action", 150, 52, Hint: "Rounded rectangle."),
        [NodeKind.Decision] = new("Decision / Merge", 46, 46, Fixed: true),
        [NodeKind.Fork] = new("Fork / Join", 8, 90),
        [NodeKind.SendSignal] = new("Send Signal", 140, 50),
        [NodeKind.AcceptSignal] = new("Accept Signal", 140, 50),
        [NodeKind.TimeEvent] = new("Time Event", 34, 44, Fixed: true),
        [NodeKind.ObjectNode] = new("Object Node", 140, 50),
        [NodeKind.Partition] = new("Swimlane", 320, 360, Container: true),
        [NodeKind.State] = new("State", 170, 70, Hint: "Rounded; entry / exit / do compartment."),
        [NodeKind.StateInitial] = new("Initial Pseudostate", 20, 20, Fixed: true),
        [NodeKind.StateFinal] = new("Final State", 26, 26, Fixed: true),
        [NodeKind.Choice] = new("Choice", 44, 44, Fixed: true),
        [NodeKind.Junction] = new("Junction", 16, 16, Fixed: true),
        [NodeKind.Terminate] = new("Terminate", 24, 24, Fixed: true),
        [NodeKind.HistoryShallow] = new("Shallow History", 28, 28, Fixed: true),
        [NodeKind.HistoryDeep] = new("Deep History", 28, 28, Fixed: true),
        [NodeKind.Component] = new("Component", 190, 90),
        [NodeKind.Port] = new("Port", 14, 14, Fixed: true, Hint: "Drag onto a component border to attach."),
        [NodeKind.ProvidedInterface] = new("Provided Interface", 22, 22, Fixed: true, Hint: "Lollipop — drag onto a component."),
        [NodeKind.RequiredInterface] = new("Required Interface", 22, 22, Fixed: true, Hint: "Socket — drag onto a component."),
        [NodeKind.DeviceNode] = new("Device Node", 190, 110, Container: true, Hint: "«device» cube."),
        [NodeKind.ExecEnvNode] = new("Execution Environment", 190, 110, Container: true, Hint: "«execution environment» cube."),
        [NodeKind.Artifact] = new("Artifact", 160, 80, Hint: "«artifact» with document icon."),
        [NodeKind.Package] = new("Package", 260, 180, Container: true, Hint: "Tabbed folder."),
        [NodeKind.Note] = new("Note", 180, 90, Hint: "Dog-eared comment."),
    };
}
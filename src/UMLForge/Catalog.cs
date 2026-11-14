using System.Windows;
using System.Windows.Media;

namespace UmlForge;

public record NodeDef(string Label, double W, double H, bool Fixed = false, bool Container = false, string Hint = "");
public record EdgeDef(string Label, bool Dashed, MarkerKind Start, MarkerKind End, string Stereo = "", string Hint = "");

public static class Catalog
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
        [NodeKind.Fork] = new("Fork / Join", 90, 8),
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

    public static readonly Dictionary<EdgeKind, EdgeDef> Edges = new()
    {
        [EdgeKind.Association] = new("Association", false, MarkerKind.None, MarkerKind.None, Hint: "Plain line; add role names / multiplicities in Properties."),
        [EdgeKind.Aggregation] = new("Aggregation", false, MarkerKind.DiamondHollow, MarkerKind.None, Hint: "Drag from whole → part. Hollow diamond at the whole."),
        [EdgeKind.Composition] = new("Composition", false, MarkerKind.DiamondFilled, MarkerKind.None, Hint: "Drag from whole → part. Filled diamond; exclusive ownership."),
        [EdgeKind.Generalization] = new("Generalization", false, MarkerKind.None, MarkerKind.Triangle, Hint: "Drag child → parent. Hollow triangle at parent."),
        [EdgeKind.Realization] = new("Realization", true, MarkerKind.None, MarkerKind.Triangle, Hint: "Drag classifier → interface. Dashed + hollow triangle."),
        [EdgeKind.Dependency] = new("Dependency", true, MarkerKind.None, MarkerKind.Open, Hint: "Drag client → supplier. Dashed + open arrow."),
        [EdgeKind.Usage] = new("Usage", true, MarkerKind.None, MarkerKind.Open, "use"),
        [EdgeKind.Include] = new("Include", true, MarkerKind.None, MarkerKind.Open, "include", "Base use case → included use case."),
        [EdgeKind.Extend] = new("Extend", true, MarkerKind.None, MarkerKind.Open, "extend", "Extension use case → base use case."),
        [EdgeKind.Link] = new("Link", false, MarkerKind.None, MarkerKind.None, Hint: "Instance-level association."),
        [EdgeKind.CommunicationAssociation] = new("Communication", false, MarkerKind.None, MarkerKind.None, Hint: "Actor ↔ use case, no arrowheads."),
        [EdgeKind.ControlFlow] = new("Control Flow", false, MarkerKind.None, MarkerKind.Open),
        [EdgeKind.ObjectFlow] = new("Object Flow", false, MarkerKind.None, MarkerKind.Open),
        [EdgeKind.Transition] = new("Transition", false, MarkerKind.None, MarkerKind.Open, Hint: "Label: trigger [guard] / effect."),
        [EdgeKind.MessageSync] = new("Synchronous Message", false, MarkerKind.None, MarkerKind.FilledTriangle, Hint: "Solid line, filled arrowhead. Creates an activation."),
        [EdgeKind.MessageAsync] = new("Asynchronous Message", false, MarkerKind.None, MarkerKind.Open),
        [EdgeKind.MessageReply] = new("Reply Message", true, MarkerKind.None, MarkerKind.Open, Hint: "Dashed, open arrowhead. Closes the activation."),
        [EdgeKind.MessageCreate] = new("Create Message", true, MarkerKind.None, MarkerKind.Open, "create", "Points at the created lifeline's head."),
        [EdgeKind.MessageDelete] = new("Delete Message", false, MarkerKind.None, MarkerKind.Cross, Hint: "X destroys the target lifeline."),
        [EdgeKind.CommunicationPath] = new("Communication Path", false, MarkerKind.None, MarkerKind.None, Hint: "Node ↔ node association."),
        [EdgeKind.Deployment] = new("Deployment", true, MarkerKind.None, MarkerKind.Open, "deploy", "Artifact → node."),
        [EdgeKind.Manifest] = new("Manifestation", true, MarkerKind.None, MarkerKind.Open, "manifest", "Artifact → component."),
        [EdgeKind.PackageImport] = new("Package Import", true, MarkerKind.None, MarkerKind.Open, "import"),
        [EdgeKind.PackageMerge] = new("Package Merge", true, MarkerKind.None, MarkerKind.Open, "merge"),
        [EdgeKind.NoteLink] = new("Note Link", true, MarkerKind.None, MarkerKind.None),
    };

    public static readonly Dictionary<DiagramKind, (string Label, string Desc, NodeKind[] Nodes, EdgeKind[] Edges, EdgeKind Quick)> Diagrams = new()
    {
        [DiagramKind.Class] = ("Class Diagram", "Structure: classifiers, features and relationships.",
            new[] { NodeKind.Class, NodeKind.Interface, NodeKind.Enumeration, NodeKind.DataType, NodeKind.Package, NodeKind.Note },
            new[] { EdgeKind.Association, EdgeKind.Aggregation, EdgeKind.Composition, EdgeKind.Generalization, EdgeKind.Realization, EdgeKind.Dependency, EdgeKind.Usage, EdgeKind.NoteLink },
            EdgeKind.Association),
        [DiagramKind.Object] = ("Object Diagram", "Instance snapshot: objects, slots and links.",
            new[] { NodeKind.Object, NodeKind.Note },
            new[] { EdgeKind.Link, EdgeKind.Dependency, EdgeKind.NoteLink }, EdgeKind.Link),
        [DiagramKind.UseCase] = ("Use Case Diagram", "Actors, use cases and system boundary.",
            new[] { NodeKind.Actor, NodeKind.UseCase, NodeKind.Boundary, NodeKind.Note },
            new[] { EdgeKind.CommunicationAssociation, EdgeKind.Include, EdgeKind.Extend, EdgeKind.Generalization, EdgeKind.Dependency, EdgeKind.NoteLink },
            EdgeKind.CommunicationAssociation),
        [DiagramKind.Sequence] = ("Sequence Diagram", "Lifelines, messages, activations and fragments.",
            new[] { NodeKind.Lifeline, NodeKind.Fragment, NodeKind.InteractionUse, NodeKind.Note },
            new[] { EdgeKind.MessageSync, EdgeKind.MessageAsync, EdgeKind.MessageReply, EdgeKind.MessageCreate, EdgeKind.MessageDelete, EdgeKind.NoteLink },
            EdgeKind.MessageSync),
        [DiagramKind.Activity] = ("Activity Diagram", "Control / object flow with decisions and concurrency.",
            new[] { NodeKind.Initial, NodeKind.Action, NodeKind.Decision, NodeKind.Fork, NodeKind.ObjectNode, NodeKind.SendSignal, NodeKind.AcceptSignal, NodeKind.TimeEvent, NodeKind.FlowFinal, NodeKind.ActivityFinal, NodeKind.Partition, NodeKind.Note },
            new[] { EdgeKind.ControlFlow, EdgeKind.ObjectFlow, EdgeKind.NoteLink }, EdgeKind.ControlFlow),
        [DiagramKind.StateMachine] = ("State Machine Diagram", "States, pseudostates and transitions.",
            new[] { NodeKind.StateInitial, NodeKind.State, NodeKind.StateFinal, NodeKind.Choice, NodeKind.Junction, NodeKind.Terminate, NodeKind.HistoryShallow, NodeKind.HistoryDeep, NodeKind.Note },
            new[] { EdgeKind.Transition, EdgeKind.NoteLink }, EdgeKind.Transition),
        [DiagramKind.Component] = ("Component Diagram", "Components, ports, provided / required interfaces.",
            new[] { NodeKind.Component, NodeKind.Port, NodeKind.ProvidedInterface, NodeKind.RequiredInterface, NodeKind.Class, NodeKind.Interface, NodeKind.Note },
            new[] { EdgeKind.Association, EdgeKind.Dependency, EdgeKind.Usage, EdgeKind.Realization, EdgeKind.Generalization, EdgeKind.NoteLink },
            EdgeKind.Dependency),
        [DiagramKind.Deployment] = ("Deployment Diagram", "Nodes, artifacts and communication paths.",
            new[] { NodeKind.DeviceNode, NodeKind.ExecEnvNode, NodeKind.Artifact, NodeKind.Component, NodeKind.Note },
            new[] { EdgeKind.CommunicationPath, EdgeKind.Deployment, EdgeKind.Manifest, EdgeKind.Dependency, EdgeKind.Generalization, EdgeKind.NoteLink },
            EdgeKind.CommunicationPath),
        [DiagramKind.Package] = ("Package Diagram", "Packages, imports and merges.",
            new[] { NodeKind.Package, NodeKind.Class, NodeKind.Interface, NodeKind.Note },
            new[] { EdgeKind.PackageImport, EdgeKind.PackageMerge, EdgeKind.Dependency, EdgeKind.Generalization, EdgeKind.Realization, EdgeKind.NoteLink },
            EdgeKind.PackageImport),
    };

    public static EdgeKind[] EdgeKindsFor(DiagramKind d) => Diagrams[d].Edges;
    public static NodeKind[] NodeKindsFor(DiagramKind d) => Diagrams[d].Nodes;

    public static string VisSymbol(Visibility v) => v switch
    { Visibility.Public => "+", Visibility.Private => "-", Visibility.Protected => "#", _ => "~" };

    public static bool IsFixed(NodeKind k) => Nodes[k].Fixed;
    public static bool IsContainer(NodeKind k) => Nodes[k].Container;

    // ---- Connection rules (enforced while drawing, so notation stays legal) ----
    public static (bool ok, string reason) CanConnect(EdgeKind kind, UmlNode s, UmlNode t, DiagramKind diagram)
    {
        if (s == null || t == null) return (false, "Drop on an element.");
        if (kind == EdgeKind.NoteLink)
            return (s.Kind == NodeKind.Note || t.Kind == NodeKind.Note) ? (true, "") : (false, "A note link must touch a note.");
        if (s.Kind == NodeKind.Note || t.Kind == NodeKind.Note) return (false, "Use the Note Link tool for notes.");

        switch (kind)
        {
            case EdgeKind.Generalization:
                if (s == t) return (false, "An element cannot generalize itself.");
                bool okG = (s.IsClassifier && t.IsClassifier) || (s.Kind == NodeKind.Actor && t.Kind == NodeKind.Actor)
                    || (s.Kind == NodeKind.UseCase && t.Kind == NodeKind.UseCase) || (s.Kind == NodeKind.Component && t.Kind == NodeKind.Component)
                    || (s.Kind == NodeKind.State && t.Kind == NodeKind.State)
                    || (s.Kind is NodeKind.DeviceNode or NodeKind.ExecEnvNode && t.Kind is NodeKind.DeviceNode or NodeKind.ExecEnvNode);
                return okG ? (true, "") : (false, "Generalization needs two classifiers of the same category (child → parent).");
            case EdgeKind.Realization:
                if (t.Kind == NodeKind.Interface && (s.Kind is NodeKind.Class or NodeKind.Component or NodeKind.DataType)) return (true, "");
                return (false, "Realization: classifier → interface.");
            case EdgeKind.Aggregation or EdgeKind.Composition:
                if (s == t) return (false, "Whole and part must differ.");
                return (s.IsClassifier && t.IsClassifier) ? (true, "") : (false, "Aggregation / composition connects classifiers (whole → part).");
            case EdgeKind.Association:
                if (diagram == DiagramKind.Component) return (true, "");
                return (s.IsClassifier && t.IsClassifier) ? (true, "") : (false, "Association connects classifiers.");
            case EdgeKind.Link:
                return (s.Kind == NodeKind.Object && t.Kind == NodeKind.Object) ? (true, "") : (false, "Links connect objects.");
            case EdgeKind.CommunicationAssociation:
                bool uc = (s.Kind == NodeKind.Actor && t.Kind == NodeKind.UseCase) || (s.Kind == NodeKind.UseCase && t.Kind == NodeKind.Actor);
                return uc ? (true, "") : (false, "Communication association: actor ↔ use case.");
            case EdgeKind.Include or EdgeKind.Extend:
                return (s.Kind == NodeKind.UseCase && t.Kind == NodeKind.UseCase && s != t) ? (true, "") : (false, $"{kind} connects two different use cases.");
            case EdgeKind.ControlFlow or EdgeKind.ObjectFlow:
                return (s.IsActivityish && t.IsActivityish) ? (true, "") : (false, "Flows connect activity nodes.");
            case EdgeKind.Transition:
                return (s.IsStateish && t.IsStateish) ? (true, "") : (false, "Transitions connect states / pseudostates.");
            case EdgeKind.MessageSync or EdgeKind.MessageAsync or EdgeKind.MessageReply or EdgeKind.MessageCreate or EdgeKind.MessageDelete:
                if (s.Kind != NodeKind.Lifeline || t.Kind != NodeKind.Lifeline) return (false, "Messages connect lifelines.");
                if (kind == EdgeKind.MessageCreate && s == t) return (false, "Create must target another lifeline.");
                return (true, "");
            case EdgeKind.CommunicationPath:
                return (s.Kind is NodeKind.DeviceNode or NodeKind.ExecEnvNode && t.Kind is NodeKind.DeviceNode or NodeKind.ExecEnvNode)
                    ? (true, "") : (false, "Communication path connects deployment nodes.");
            case EdgeKind.Deployment:
                return (s.Kind is NodeKind.Artifact or NodeKind.Component && t.Kind is NodeKind.DeviceNode or NodeKind.ExecEnvNode)
                    ? (true, "") : (false, "«deploy»: artifact / component → node.");
            case EdgeKind.Manifest:
                return (s.Kind == NodeKind.Artifact && t.Kind == NodeKind.Component) ? (true, "") : (false, "«manifest»: artifact → component.");
            case EdgeKind.PackageImport or EdgeKind.PackageMerge:
                return (s.Kind == NodeKind.Package && t.Kind == NodeKind.Package && s != t) ? (true, "") : (false, "Import / merge connects two packages.");
            default:
                return (true, "");
        }
    }

    // ---- Palette / tree icons (mini geometry, 24×24 box) ----
    public static Geometry Icon(string key) => Geometry.Parse(key switch
    {
        "Class" => "M3,4 H21 V20 H3 Z M3,9 H21 M3,14 H21",
        "Interface" => "M12,4 a8,8 0 1,0 0.1,0 Z M12,12 V21",
        "Enumeration" => "M3,4 H21 V20 H3 Z M3,9 H21 M6,12 H14 M6,16 H12",
        "DataType" => "M3,4 H21 V20 H3 Z M3,9 H21",
        "Object" => "M3,5 H21 V19 H3 Z M3,10 H21",
        "Actor" => "M12,3 a3,3 0 1,0 0.1,0 Z M12,9 V16 M6,12 H18 M12,16 L7,22 M12,16 L17,22",
        "UseCase" => "M3,12 a9,5.5 0 1,0 18,0 a9,5.5 0 1,0 -18,0 Z",
        "Boundary" => "M3,4 H21 V20 H3 Z M8,4 V20",
        "Lifeline" => "M5,3 H19 V8 H5 Z M12,8 V22",
        "Fragment" => "M3,4 H21 V20 H3 Z M3,4 H10 L12,7 H3 Z M3,13 H21",
        "InteractionUse" => "M3,4 H21 V20 H3 Z M3,4 H9 L11,7 H3 Z",
        "Initial" => "M12,6 a6,6 0 1,0 0.1,0 Z",
        "ActivityFinal" => "M12,3 a9,9 0 1,0 0.1,0 Z M12,8 a4,4 0 1,0 0.1,0 Z",
        "FlowFinal" => "M12,3 a9,9 0 1,0 0.1,0 Z M7,7 L17,17 M17,7 L7,17",
        "Action" => "M6,5 H18 a4,4 0 0 1 4,4 V15 a4,4 0 0 1 -4,4 H6 a4,4 0 0 1 -4,-4 V9 a4,4 0 0 1 4,-4 Z",
        "Decision" => "M12,3 L21,12 L12,21 L3,12 Z",
        "Fork" => "M3,10 H21 V14 H3 Z",
        "SendSignal" => "M3,6 H17 L21,12 L17,18 H3 Z",
        "AcceptSignal" => "M7,6 H21 V18 H7 L3,12 Z",
        "TimeEvent" => "M5,4 H19 L5,20 H19 Z",
        "ObjectNode" => "M3,6 H21 V18 H3 Z",
        "Partition" => "M3,4 H21 V20 H3 Z M3,8 H21 M12,8 V20",
        "State" => "M7,5 H17 a4,4 0 0 1 4,4 V15 a4,4 0 0 1 -4,4 H7 a4,4 0 0 1 -4,-4 V9 a4,4 0 0 1 4,-4 Z M3,11 H21",
        "StateInitial" => "M12,6 a6,6 0 1,0 0.1,0 Z",
        "StateFinal" => "M12,3 a9,9 0 1,0 0.1,0 Z M12,8 a4,4 0 1,0 0.1,0 Z",
        "Choice" => "M12,3 L21,12 L12,21 L3,12 Z",
        "Junction" => "M12,8 a4,4 0 1,0 0.1,0 Z",
        "Terminate" => "M5,5 L19,19 M19,5 L5,19",
        "HistoryShallow" => "M12,3 a9,9 0 1,0 0.1,0 Z M9,8 V16 M15,8 V16 M9,12 H15",
        "HistoryDeep" => "M12,3 a9,9 0 1,0 0.1,0 Z M8,8 V16 M13,8 V16 M8,12 H13 M17,8 L20,11 M20,8 L17,11",
        "Component" => "M6,4 H21 V20 H6 Z M3,7 H8 V10 H3 Z M3,14 H8 V17 H3 Z",
        "Port" => "M8,8 H16 V16 H8 Z",
        "ProvidedInterface" => "M12,4 a7,7 0 1,0 0.1,0 Z M12,18 V22",
        "RequiredInterface" => "M6,11 a6,6 0 0 1 12,0 M12,11 V22",
        "DeviceNode" => "M3,8 L8,3 H21 L16,8 Z M3,8 H16 V21 H3 Z M16,8 L21,3 V16 L16,21",
        "ExecEnvNode" => "M3,8 L8,3 H21 L16,8 Z M3,8 H16 V21 H3 Z M16,8 L21,3 V16 L16,21",
        "Artifact" => "M5,3 H15 L19,7 V21 H5 Z M15,3 V7 H19",
        "Package" => "M3,6 H10 V9 H21 V20 H3 Z",
        "Note" => "M4,3 H16 L20,7 V21 H4 Z M16,3 V7 H20",
        "Association" => "M3,18 L21,6",
        "Aggregation" => "M8,15 L21,6 M3,18 L6,13 L11,12 L8,17 Z",
        "Composition" => "M8,15 L21,6 M3,18 L6,13 L11,12 L8,17 Z",
        "Generalization" => "M3,18 L14,11 M14,11 L21,6 L17,14 Z",
        "Realization" => "M3,18 L7,15 M9,14 L13,11 M14,11 L21,6 L17,14 Z",
        "Dependency" => "M3,18 L7,15 M9,14 L13,11 M15,10 L19,7 M19,7 L15,7 M19,7 L18,11",
        "Usage" => "M3,18 L7,15 M9,14 L13,11 M15,10 L19,7 M19,7 L15,7 M19,7 L18,11",
        "Include" => "M3,18 L7,15 M9,14 L13,11 M15,10 L19,7 M19,7 L15,7 M19,7 L18,11",
        "Extend" => "M3,18 L7,15 M9,14 L13,11 M15,10 L19,7 M19,7 L15,7 M19,7 L18,11",
        "Link" => "M3,18 L21,6",
        "CommunicationAssociation" => "M3,18 L21,6",
        "ControlFlow" => "M3,18 L19,7 M19,7 L14,7 M19,7 L18,12",
        "ObjectFlow" => "M3,18 L19,7 M19,7 L14,7 M19,7 L18,12",
        "Transition" => "M3,18 L19,7 M19,7 L14,7 M19,7 L18,12",
        "MessageSync" => "M3,12 H19 M19,12 L13,8 V16 Z",
        "MessageAsync" => "M3,12 H19 M19,12 L13,8 M19,12 L13,16",
        "MessageReply" => "M3,12 H7 M9,12 H13 M15,12 H19 M19,12 L13,8 M19,12 L13,16",
        "MessageCreate" => "M3,12 H7 M9,12 H13 M15,12 H19 M19,12 L13,8 M19,12 L13,16",
        "MessageDelete" => "M3,12 H17 M17,8 L21,16 M21,8 L17,16",
        "CommunicationPath" => "M3,18 L21,6",
        "Deployment" => "M3,18 L7,15 M9,14 L13,11 M15,10 L19,7 M19,7 L15,7 M19,7 L18,11",
        "Manifest" => "M3,18 L7,15 M9,14 L13,11 M15,10 L19,7 M19,7 L15,7 M19,7 L18,11",
        "PackageImport" => "M3,18 L7,15 M9,14 L13,11 M15,10 L19,7 M19,7 L15,7 M19,7 L18,11",
        "PackageMerge" => "M3,18 L7,15 M9,14 L13,11 M15,10 L19,7 M19,7 L15,7 M19,7 L18,11",
        "NoteLink" => "M3,18 L7,15 M9,14 L13,11 M15,10 L19,7",
        "Select" => "M6,3 L18,13 L12,14 L15,20 L12,21 L9,15 L6,18 Z",
        "Pan" => "M12,3 V21 M3,12 H21 M12,3 L9,6 M12,3 L15,6 M12,21 L9,18 M12,21 L15,18 M3,12 L6,9 M3,12 L6,15 M21,12 L18,9 M21,12 L18,15",
        _ => "M4,4 H20 V20 H4 Z"
    });
}
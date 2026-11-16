using System.Windows.Media;

namespace UmlForge;

public static class Catalog
{
    public static Dictionary<NodeKind, NodeDef> Nodes => NodeCatalog.Nodes;
    public static Dictionary<EdgeKind, EdgeDef> Edges => EdgeCatalog.Edges;

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

    public static (bool ok, string reason) CanConnect(EdgeKind kind, UmlNode s, UmlNode t, DiagramKind diagram) =>
        ConnectionRules.CanConnect(kind, s, t, diagram);

    public static Geometry Icon(string key) => UmlIcons.Get(key);
}
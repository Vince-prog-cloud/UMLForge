using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace UmlForge;

public enum DiagramKind { Class, Object, UseCase, Sequence, Activity, StateMachine, Component, Deployment, Package }
public enum Visibility { Public, Private, Protected, Package }
public enum Routing { Straight, Orthogonal }
public enum PaperMode { White, Cream, Blueprint }
public enum Severity { Info, Warning, Error }

public enum NodeKind
{
    Class, Interface, Enumeration, DataType, Object,
    Actor, UseCase, Boundary,
    Lifeline, Fragment, InteractionUse,
    Initial, ActivityFinal, FlowFinal, Action, Decision, Fork, SendSignal, AcceptSignal, TimeEvent, ObjectNode, Partition,
    State, StateInitial, StateFinal, Choice, Junction, Terminate, HistoryShallow, HistoryDeep,
    Component, Port, ProvidedInterface, RequiredInterface,
    DeviceNode, ExecEnvNode, Artifact, Package, Note
}

public enum EdgeKind
{
    Association, Aggregation, Composition, Generalization, Realization, Dependency, Usage,
    Include, Extend, Link, CommunicationAssociation,
    ControlFlow, ObjectFlow, Transition,
    MessageSync, MessageAsync, MessageReply, MessageCreate, MessageDelete,
    CommunicationPath, Deployment, Manifest, PackageImport, PackageMerge, NoteLink
}

public enum MarkerKind { None, Open, Triangle, FilledTriangle, DiamondHollow, DiamondFilled, Cross }

public abstract class Observable
{
    public event PropertyChangedEventHandler PropertyChanged;
    protected bool Set<T>(ref T field, T value, [CallerMemberName] string name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        return true;
    }
    protected void Raise([CallerMemberName] string name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class UmlAttribute : Observable
{
    Visibility _vis = Visibility.Public; string _name = "attribute", _type = "String", _mult = "", _def = "";
    bool _static, _readOnly, _derived;
    public Visibility Visibility { get => _vis; set => Set(ref _vis, value); }
    public string Name { get => _name; set => Set(ref _name, value); }
    public string Type { get => _type; set => Set(ref _type, value); }
    public string Multiplicity { get => _mult; set => Set(ref _mult, value); }
    public string Default { get => _def; set => Set(ref _def, value); }
    public bool IsStatic { get => _static; set => Set(ref _static, value); }
    public bool IsReadOnly { get => _readOnly; set => Set(ref _readOnly, value); }
    public bool IsDerived { get => _derived; set => Set(ref _derived, value); }
}

public class UmlOperation : Observable
{
    Visibility _vis = Visibility.Public; string _name = "operation", _params = "", _ret = "";
    bool _static, _abstract;
    public Visibility Visibility { get => _vis; set => Set(ref _vis, value); }
    public string Name { get => _name; set => Set(ref _name, value); }
    public string Parameters { get => _params; set => Set(ref _params, value); }
    public string ReturnType { get => _ret; set => Set(ref _ret, value); }
    public bool IsStatic { get => _static; set => Set(ref _static, value); }
    public bool IsAbstract { get => _abstract; set => Set(ref _abstract, value); }
}

public class InternalTransition : Observable
{
    string _trigger = "", _guard = "", _effect = "";
    public string Trigger { get => _trigger; set => Set(ref _trigger, value); }
    public string Guard { get => _guard; set => Set(ref _guard, value); }
    public string Effect { get => _effect; set => Set(ref _effect, value); }
}

public class FragmentOperand : Observable
{
    string _guard = ""; double _height = 90;
    public string Guard { get => _guard; set => Set(ref _guard, value); }
    public double Height { get => _height; set => Set(ref _height, Math.Max(34, value)); }
}

public class Activation : Observable
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..10];
    double _start, _end;
    int _depth;
    public double Start { get => _start; set => Set(ref _start, value); }
    public double End { get => _end; set => Set(ref _end, value); }
    public int Depth { get => _depth; set => Set(ref _depth, value); }
    public string MessageId { get; set; } = "";
}

public class UmlNode : Observable
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..10];
    NodeKind _kind; double _x, _y, _w = 150, _h = 90;
    string _name = "", _stereo = "", _represents = "", _entry = "", _exit = "", _do = "", _operator = "alt", _fill;
    Visibility _vis = Visibility.Public; bool _abstract;
    string _orientation = "Horizontal";

    public NodeKind Kind { get => _kind; set => Set(ref _kind, value); }
    public double X { get => _x; set => Set(ref _x, value); }
    public double Y { get => _y; set => Set(ref _y, value); }
    public double W { get => _w; set => Set(ref _w, Math.Max(10, value)); }
    public double H { get => _h; set => Set(ref _h, Math.Max(10, value)); }
    public string Name { get => _name; set => Set(ref _name, value); }
    public string Stereotype { get => _stereo; set => Set(ref _stereo, value); }
    public string Represents { get => _represents; set => Set(ref _represents, value); }
    public string Entry { get => _entry; set => Set(ref _entry, value); }
    public string Exit { get => _exit; set => Set(ref _exit, value); }
    public string Do { get => _do; set => Set(ref _do, value); }
    public string Operator { get => _operator; set => Set(ref _operator, value); }
    public string Orientation { get => _orientation; set => Set(ref _orientation, value); }
    public string Fill { get => _fill; set => Set(ref _fill, value); }
    public Visibility Visibility { get => _vis; set => Set(ref _vis, value); }
    public bool IsAbstract { get => _abstract; set => Set(ref _abstract, value); }

    public ObservableCollection<UmlAttribute> Attributes { get; set; } = new();
    public ObservableCollection<UmlOperation> Operations { get; set; } = new();
    public ObservableCollection<string> Literals { get; set; } = new();
    public ObservableCollection<string> Slots { get; set; } = new();
    public ObservableCollection<string> ExtensionPoints { get; set; } = new();
    public ObservableCollection<string> Lanes { get; set; } = new();
    public ObservableCollection<InternalTransition> Internals { get; set; } = new();
    public ObservableCollection<FragmentOperand> Operands { get; set; } = new();
    public ObservableCollection<Activation> Activations { get; set; } = new();

    // Attachment (ports / lollipops on components)
    public string AttachParentId { get; set; }
    public string AttachSide { get; set; } = "right";
    public double AttachT { get; set; } = 0.5;

    public bool IsClassifier => Kind is NodeKind.Class or NodeKind.Interface or NodeKind.Enumeration or NodeKind.DataType;
    public bool IsStateish => Kind is NodeKind.State or NodeKind.StateInitial or NodeKind.StateFinal or NodeKind.Choice
        or NodeKind.Junction or NodeKind.Terminate or NodeKind.HistoryShallow or NodeKind.HistoryDeep;
    public bool IsActivityish => Kind is NodeKind.Initial or NodeKind.ActivityFinal or NodeKind.FlowFinal or NodeKind.Action
        or NodeKind.Decision or NodeKind.Fork or NodeKind.SendSignal or NodeKind.AcceptSignal or NodeKind.TimeEvent or NodeKind.ObjectNode;
}

public class UmlEdge : Observable
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..10];
    EdgeKind _kind; string _src = "", _tgt = "", _name = "";
    string _srcRole = "", _tgtRole = "", _srcMult = "", _tgtMult = "";
    bool _srcNav, _tgtNav = true, _showDir, _selfReply;
    Routing _routing = Routing.Straight; double _bend, _ldx, _ldy, _y;
    string _guard = "", _trigger = "", _effect = "", _condition = "";

    public EdgeKind Kind { get => _kind; set => Set(ref _kind, value); }
    public string SourceId { get => _src; set => Set(ref _src, value); }
    public string TargetId { get => _tgt; set => Set(ref _tgt, value); }
    public string Name { get => _name; set => Set(ref _name, value); }
    public string SourceRole { get => _srcRole; set => Set(ref _srcRole, value); }
    public string TargetRole { get => _tgtRole; set => Set(ref _tgtRole, value); }
    public string SourceMultiplicity { get => _srcMult; set => Set(ref _srcMult, value); }
    public string TargetMultiplicity { get => _tgtMult; set => Set(ref _tgtMult, value); }
    public bool SourceNavigable { get => _srcNav; set => Set(ref _srcNav, value); }
    public bool TargetNavigable { get => _tgtNav; set => Set(ref _tgtNav, value); }
    public bool ShowDirection { get => _showDir; set => Set(ref _showDir, value); }
    public bool IsSelfReply { get => _selfReply; set => Set(ref _selfReply, value); }
    public Routing Routing { get => _routing; set => Set(ref _routing, value); }
    public double BendOffset { get => _bend; set => Set(ref _bend, value); }
    public double LabelDx { get => _ldx; set => Set(ref _ldx, value); }
    public double LabelDy { get => _ldy; set => Set(ref _ldy, value); }
    public double Y { get => _y; set => Set(ref _y, value); }
    public string Guard { get => _guard; set => Set(ref _guard, value); }
    public string Trigger { get => _trigger; set => Set(ref _trigger, value); }
    public string Effect { get => _effect; set => Set(ref _effect, value); }
    public string Condition { get => _condition; set => Set(ref _condition, value); }

    public bool IsMessage => Kind is EdgeKind.MessageSync or EdgeKind.MessageAsync or EdgeKind.MessageReply
        or EdgeKind.MessageCreate or EdgeKind.MessageDelete;
}

public class Diagram : Observable
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..10];
    DiagramKind _kind; string _name = "Diagram", _desc = ""; bool _seqNumbers = true;
    public DiagramKind Kind { get => _kind; set => Set(ref _kind, value); }
    public string Name { get => _name; set => Set(ref _name, value); }
    public string Description { get => _desc; set => Set(ref _desc, value); }
    public bool ShowSequenceNumbers { get => _seqNumbers; set => Set(ref _seqNumbers, value); }
    public ObservableCollection<UmlNode> Nodes { get; set; } = new();
    public ObservableCollection<UmlEdge> Edges { get; set; } = new();
    public UmlNode Node(string id) => Nodes.FirstOrDefault(n => n.Id == id);
    public UmlEdge Edge(string id) => Edges.FirstOrDefault(e => e.Id == id);
}

public class UmlPackage : Observable
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..10];
    string _name = "Package";
    public string Name { get => _name; set => Set(ref _name, value); }
    public ObservableCollection<Diagram> Diagrams { get; set; } = new();
}

public class Project : Observable
{
    public int SchemaVersion { get; set; } = 1;
    string _name = "Untitled Project";
    public string Name { get => _name; set => Set(ref _name, value); }
    public ObservableCollection<UmlPackage> Packages { get; set; } = new();
    public IEnumerable<Diagram> AllDiagrams => Packages.SelectMany(p => p.Diagrams);
    public Diagram FindDiagram(string id) => AllDiagrams.FirstOrDefault(d => d.Id == id);
    public UmlPackage PackageOf(Diagram d) => Packages.FirstOrDefault(p => p.Diagrams.Contains(d));
}

public class Issue
{
    public Severity Severity { get; set; }
    public string Message { get; set; } = "";
    public string ElementId { get; set; } = "";
}

public class AppSettings
{
    public PaperMode Paper { get; set; } = PaperMode.White;
    public bool Grid { get; set; } = true;
    public bool Snap { get; set; } = true;
    public bool Guides { get; set; } = true;
    public bool Shadows { get; set; } = true;
    public double GridSize { get; set; } = 20;
    public double FontSize { get; set; } = 13;
    public Routing DefaultRouting { get; set; } = Routing.Straight;
    public string LastFile { get; set; } = "";
}

public struct ToolSpec
{
    public ToolMode Mode; public NodeKind Node; public EdgeKind Edge;
    public static ToolSpec Select => new() { Mode = ToolMode.Select };
    public static ToolSpec Pan => new() { Mode = ToolMode.Pan };
    public static ToolSpec Place(NodeKind k) => new() { Mode = ToolMode.Place, Node = k };
    public static ToolSpec Connect(EdgeKind k) => new() { Mode = ToolMode.Connect, Edge = k };
}
public enum ToolMode { Select, Pan, Place, Connect }
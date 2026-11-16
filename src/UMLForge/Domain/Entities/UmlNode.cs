using System.Collections.ObjectModel;

namespace UmlForge;

public class InternalTransition : Observable
{
    string _trigger = "", _guard = "", _effect = "";
    public string Trigger { get => _trigger; set => Set(ref _trigger, value); }
    public string Guard { get => _guard; set => Set(ref _guard, value); }
    public string Effect { get => _effect; set => Set(ref _effect, value); }
}

public class FragmentOperand : Observable
{
    string _guard = "";
    double _height = 90;
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
    NodeKind _kind;
    double _x, _y, _w = 150, _h = 90;
    string _name = "", _stereo = "", _represents = "", _entry = "", _exit = "", _do = "", _operator = "alt", _fill;
    Visibility _vis = Visibility.Public;
    bool _abstract;
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

    public string AttachParentId { get; set; }
    public string AttachSide { get; set; } = "right";
    public double AttachT { get; set; } = 0.5;

    public bool IsClassifier => Kind is NodeKind.Class or NodeKind.Interface or NodeKind.Enumeration or NodeKind.DataType;
    public bool IsStateish => Kind is NodeKind.State or NodeKind.StateInitial or NodeKind.StateFinal or NodeKind.Choice
        or NodeKind.Junction or NodeKind.Terminate or NodeKind.HistoryShallow or NodeKind.HistoryDeep;
    public bool IsActivityish => Kind is NodeKind.Initial or NodeKind.ActivityFinal or NodeKind.FlowFinal or NodeKind.Action
        or NodeKind.Decision or NodeKind.Fork or NodeKind.SendSignal or NodeKind.AcceptSignal or NodeKind.TimeEvent or NodeKind.ObjectNode;
}
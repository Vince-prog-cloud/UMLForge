namespace UmlForge;

public class UmlEdge : Observable
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..10];
    EdgeKind _kind;
    string _src = "", _tgt = "", _name = "";
    string _srcRole = "", _tgtRole = "", _srcMult = "", _tgtMult = "";
    bool _srcNav, _tgtNav, _showDir, _selfReply;
    Routing _routing = Routing.Straight;
    double _bend, _ldx, _ldy, _y;
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
namespace UmlForge;

public class UmlAttribute : Observable
{
    Visibility _vis = Visibility.Public;
    string _name = "attribute", _type = "String", _mult = "", _def = "";
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
    Visibility _vis = Visibility.Public;
    string _name = "operation", _params = "", _ret = "";
    bool _static, _abstract;

    public Visibility Visibility { get => _vis; set => Set(ref _vis, value); }
    public string Name { get => _name; set => Set(ref _name, value); }
    public string Parameters { get => _params; set => Set(ref _params, value); }
    public string ReturnType { get => _ret; set => Set(ref _ret, value); }
    public bool IsStatic { get => _static; set => Set(ref _static, value); }
    public bool IsAbstract { get => _abstract; set => Set(ref _abstract, value); }
}
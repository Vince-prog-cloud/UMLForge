using System.Collections.ObjectModel;

namespace UmlForge;

public static class Seed
{
    static UmlNode N(NodeKind k, double x, double y, double w, double h, string name, Action<UmlNode> extra = null)
    {
        var n = new UmlNode { Kind = k, X = x, Y = y, W = w, H = h, Name = name };
        extra?.Invoke(n); return n;
    }
    static UmlEdge E(EdgeKind k, UmlNode s, UmlNode t, Action<UmlEdge> extra = null)
    {
        var e = new UmlEdge { Kind = k, SourceId = s.Id, TargetId = t.Id };
        extra?.Invoke(e); return e;
    }
    static UmlAttribute A(string name, string type, Visibility v = Visibility.Private) => new() { Name = name, Type = type, Visibility = v };
    static UmlOperation O(string name, string ret, string ps = "", Visibility v = Visibility.Public) => new() { Name = name, ReturnType = ret, Parameters = ps, Visibility = v };

    public static Project Build()
    {
        var p = new Project { Name = "Library Management System" };
        var pkg = new UmlPackage { Name = "LibraryModel" };
        p.Packages.Add(pkg);

        // ---------- Class ----------
        var cd = new Diagram { Kind = DiagramKind.Class, Name = "Domain Model" };
        var searchable = N(NodeKind.Interface, 60, 60, 200, 80, "Searchable", n => n.Operations.Add(O("search", "Book[*]", "query : String")));
        var catalog = N(NodeKind.Class, 60, 220, 230, 110, "Catalog", n => { n.Operations.Add(O("search", "Book[*]", "title : String")); n.Operations.Add(O("addBook", "", "b : Book")); });
        var library = N(NodeKind.Class, 60, 470, 230, 130, "Library", n => { n.Attributes.Add(A("name", "String")); n.Attributes.Add(A("address", "String")); n.Operations.Add(O("registerMember", "Member", "name : String")); });
        var book = N(NodeKind.Class, 450, 110, 230, 140, "Book", n => { n.Attributes.Add(A("title", "String")); n.Attributes.Add(A("ISBN", "String")); n.Attributes.Add(A("publicationYear", "Integer")); n.Operations.Add(O("isAvailable", "Boolean")); });
        var ebook = N(NodeKind.Class, 380, 420, 200, 100, "EBook", n => { n.Attributes.Add(A("fileFormat", "String")); n.Operations.Add(O("download", "Boolean")); });
        var printed = N(NodeKind.Class, 620, 420, 210, 100, "PrintedBook", n => { n.Attributes.Add(A("shelfLocation", "String")); n.Attributes.Add(A("pageCount", "Integer")); });
        var member = N(NodeKind.Class, 920, 110, 240, 140, "Member", n => { n.IsAbstract = true; n.Attributes.Add(A("name", "String")); n.Attributes.Add(A("memberId", "String")); n.Operations.Add(new UmlOperation { Name = "maxLoans", ReturnType = "Integer", IsAbstract = true }); });
        var student = N(NodeKind.Class, 850, 420, 210, 100, "StudentMember", n => { n.Attributes.Add(A("university", "String")); n.Operations.Add(O("maxLoans", "Integer")); });
        var staff = N(NodeKind.Class, 1100, 420, 210, 100, "StaffMember", n => { n.Attributes.Add(A("department", "String")); n.Operations.Add(O("maxLoans", "Integer")); });
        var loan = N(NodeKind.Class, 450, 660, 240, 140, "Loan", n => { n.Attributes.Add(A("loanDate", "Date")); n.Attributes.Add(A("dueDate", "Date")); n.Attributes.Add(A("returnDate", "Date")); n.Operations.Add(O("renew", "Boolean")); });
        var reservation = N(NodeKind.Class, 780, 660, 230, 110, "Reservation", n => { n.Attributes.Add(A("reservedDate", "Date")); n.Attributes.Add(A("expiryDate", "Date")); });
        var status = N(NodeKind.Enumeration, 1100, 660, 170, 110, "LoanStatus", n => { n.Literals.Add("ACTIVE"); n.Literals.Add("OVERDUE"); n.Literals.Add("RETURNED"); });
        var note = N(NodeKind.Note, 1100, 840, 240, 80, "A loan becomes OVERDUE when dueDate < today.");
        cd.Nodes.AddRange(searchable, catalog, library, book, ebook, printed, member, student, staff, loan, reservation, status, note);
        cd.Edges.AddRange(
            E(EdgeKind.Realization, catalog, searchable),
            E(EdgeKind.Composition, library, catalog, e => { e.SourceMultiplicity = "1"; e.TargetMultiplicity = "1"; }),
            E(EdgeKind.Composition, library, book, e => { e.SourceMultiplicity = "1"; e.TargetMultiplicity = "0..*"; e.TargetRole = "holdings"; }),
            E(EdgeKind.Association, catalog, book, e => { e.Name = "indexes"; e.SourceMultiplicity = "1"; e.TargetMultiplicity = "0..*"; }),
            E(EdgeKind.Composition, library, member, e => { e.SourceMultiplicity = "1"; e.TargetMultiplicity = "0..*"; e.TargetRole = "members"; }),
            E(EdgeKind.Association, member, loan, e => { e.SourceMultiplicity = "1"; e.TargetMultiplicity = "0..*"; e.TargetRole = "loans"; }),
            E(EdgeKind.Association, loan, book, e => { e.SourceMultiplicity = "0..*"; e.TargetMultiplicity = "1"; }),
            E(EdgeKind.Association, member, reservation, e => { e.SourceMultiplicity = "1"; e.TargetMultiplicity = "0..*"; }),
            E(EdgeKind.Association, reservation, book, e => { e.SourceMultiplicity = "0..*"; e.TargetMultiplicity = "1"; }),
            E(EdgeKind.Generalization, ebook, book),
            E(EdgeKind.Generalization, printed, book),
            E(EdgeKind.Generalization, student, member),
            E(EdgeKind.Generalization, staff, member),
            E(EdgeKind.Usage, loan, status),
            E(EdgeKind.NoteLink, note, loan));
        pkg.Diagrams.Add(cd);

        // ---------- Use case ----------
        var ud = new Diagram { Kind = DiagramKind.UseCase, Name = "Borrowing Services" };
        var boundary = N(NodeKind.Boundary, 250, 50, 720, 560, "Library System");
        var mActor = N(NodeKind.Actor, 70, 240, 64, 96, "Member");
        var lActor = N(NodeKind.Actor, 1060, 260, 64, 96, "Librarian");
        var search = N(NodeKind.UseCase, 320, 120, 160, 72, "Search Catalog");
        var borrow = N(NodeKind.UseCase, 320, 270, 160, 72, "Borrow Book");
        var ret = N(NodeKind.UseCase, 320, 420, 160, 72, "Return Book");
        var reserve = N(NodeKind.UseCase, 570, 120, 160, 72, "Reserve Book");
        var verify = N(NodeKind.UseCase, 570, 270, 170, 72, "Verify Availability");
        var fine = N(NodeKind.UseCase, 570, 420, 160, 72, "Pay Fine");
        var manage = N(NodeKind.UseCase, 790, 180, 160, 72, "Manage Members");
        var notice = N(NodeKind.UseCase, 790, 350, 170, 72, "Send Overdue Notice");
        ud.Nodes.AddRange(boundary, mActor, lActor, search, borrow, ret, reserve, verify, fine, manage, notice);
        ud.Edges.AddRange(
            E(EdgeKind.CommunicationAssociation, mActor, search), E(EdgeKind.CommunicationAssociation, mActor, borrow),
            E(EdgeKind.CommunicationAssociation, mActor, ret), E(EdgeKind.CommunicationAssociation, mActor, reserve),
            E(EdgeKind.CommunicationAssociation, lActor, manage), E(EdgeKind.CommunicationAssociation, lActor, notice),
            E(EdgeKind.Include, borrow, verify), E(EdgeKind.Include, reserve, search), E(EdgeKind.Extend, fine, ret));
        pkg.Diagrams.Add(ud);

        // ---------- Sequence ----------
        var sd = new Diagram { Kind = DiagramKind.Sequence, Name = "borrowBook Flow" };
        var lm = N(NodeKind.Lifeline, 60, 60, 130, 560, "m", n => n.Represents = "Member");
        var lui = N(NodeKind.Lifeline, 260, 60, 140, 560, "ui", n => n.Represents = "LoanScreen");
        var lc = N(NodeKind.Lifeline, 470, 60, 160, 560, "ctrl", n => n.Represents = "LoanController");
        var lcat = N(NodeKind.Lifeline, 700, 60, 140, 560, "cat", n => n.Represents = "Catalog");
        var lb = N(NodeKind.Lifeline, 910, 60, 130, 560, "b", n => n.Represents = "Book");
        var ll = N(NodeKind.Lifeline, 1120, 300, 130, 320, "loan", n => n.Represents = "Loan");
        var frag = N(NodeKind.Fragment, 430, 330, 760, 250, "", n => { n.Operator = "alt"; n.Operands.Add(new FragmentOperand { Guard = "book available", Height = 150 }); n.Operands.Add(new FragmentOperand { Guard = "else", Height = 100 }); });
        sd.Nodes.AddRange(frag, lm, lui, lc, lcat, lb, ll);
        void Msg(EdgeKind k, UmlNode s, UmlNode t, double y, string name)
        {
            var e = E(k, s, t, x => { x.Y = y; x.Name = name; });
            sd.Edges.Add(e);
            if (k == EdgeKind.MessageSync && s != t) t.Activations.Add(new Activation { Start = y, End = y + 44, MessageId = e.Id });
        }
        Msg(EdgeKind.MessageSync, lm, lui, 140, "borrowBook(ISBN)");
        Msg(EdgeKind.MessageSync, lui, lc, 185, "borrow(m, ISBN)");
        Msg(EdgeKind.MessageSync, lc, lcat, 230, "findBook(ISBN)");
        Msg(EdgeKind.MessageReply, lcat, lc, 270, "book");
        Msg(EdgeKind.MessageCreate, lc, ll, 318, "new Loan(m, b)");
        Msg(EdgeKind.MessageSync, lc, lb, 380, "markOnLoan()");
        Msg(EdgeKind.MessageReply, lb, lc, 415, "ok");
        Msg(EdgeKind.MessageReply, lc, lui, 450, "confirm");
        Msg(EdgeKind.MessageReply, lui, lm, 485, "receipt");
        Msg(EdgeKind.MessageReply, lc, lui, 545, "unavailable");
        pkg.Diagrams.Add(sd);

        // ---------- Activity ----------
        var ad = new Diagram { Kind = DiagramKind.Activity, Name = "Return Book" };
        var init = N(NodeKind.Initial, 70, 205, 20, 20, "");
        var accept = N(NodeKind.Action, 140, 190, 150, 52, "Accept Return");
        var dec = N(NodeKind.Decision, 350, 193, 46, 46, "Overdue?");
        var calc = N(NodeKind.Action, 460, 100, 150, 52, "Calculate Fine");
        var pay = N(NodeKind.Action, 660, 100, 150, 52, "Accept Payment");
        var merge = N(NodeKind.Decision, 860, 193, 46, 46, "");
        var shelve = N(NodeKind.Action, 960, 190, 150, 52, "Shelve Book");
        var fork = N(NodeKind.Fork, 1160, 171, 8, 90, "", n => n.Orientation = "Vertical");
        var upd = N(NodeKind.Action, 1230, 90, 160, 52, "Update Catalog");
        var notify = N(NodeKind.Action, 1230, 300, 160, 52, "Notify Member");
        var join = N(NodeKind.Fork, 1450, 171, 8, 90, "", n => n.Orientation = "Vertical");
        var final = N(NodeKind.ActivityFinal, 1520, 203, 26, 26, "");
        ad.Nodes.AddRange(init, accept, dec, calc, pay, merge, shelve, fork, upd, notify, join, final);
        ad.Edges.AddRange(
            E(EdgeKind.ControlFlow, init, accept), E(EdgeKind.ControlFlow, accept, dec),
            E(EdgeKind.ControlFlow, dec, calc, e => e.Guard = "yes"), E(EdgeKind.ControlFlow, calc, pay),
            E(EdgeKind.ControlFlow, pay, merge), E(EdgeKind.ControlFlow, dec, merge, e => e.Guard = "no"),
            E(EdgeKind.ControlFlow, merge, shelve), E(EdgeKind.ControlFlow, shelve, fork),
            E(EdgeKind.ControlFlow, fork, upd), E(EdgeKind.ControlFlow, fork, notify),
            E(EdgeKind.ControlFlow, upd, join), E(EdgeKind.ControlFlow, notify, join),
            E(EdgeKind.ControlFlow, join, final));
        pkg.Diagrams.Add(ad);

        // ---------- State machine ----------
        var sm = new Diagram { Kind = DiagramKind.StateMachine, Name = "Book Lifecycle" };
        var si = N(NodeKind.StateInitial, 70, 165, 20, 20, "");
        var avail = N(NodeKind.State, 150, 140, 160, 64, "Available");
        var onloan = N(NodeKind.State, 430, 120, 210, 110, "OnLoan", n => { n.Entry = "startTimer"; n.Do = "trackDueDate"; n.Exit = "stopTimer"; });
        var reserved = N(NodeKind.State, 430, 340, 160, 64, "Reserved");
        var lost = N(NodeKind.State, 760, 140, 150, 64, "Lost");
        var sf = N(NodeKind.StateFinal, 990, 160, 26, 26, "");
        sm.Nodes.AddRange(si, avail, onloan, reserved, lost, sf);
        sm.Edges.AddRange(
            E(EdgeKind.Transition, si, avail),
            E(EdgeKind.Transition, avail, onloan, e => { e.Trigger = "borrow"; e.Effect = "markOnLoan"; }),
            E(EdgeKind.Transition, onloan, avail, e => { e.Trigger = "return"; e.Effect = "markAvailable"; e.Routing = Routing.Orthogonal; e.BendOffset = -40; }),
            E(EdgeKind.Transition, onloan, onloan, e => { e.Trigger = "renew"; e.Guard = "renewals < 2"; e.Effect = "extendDueDate"; }),
            E(EdgeKind.Transition, avail, reserved, e => { e.Trigger = "reserve"; e.Routing = Routing.Orthogonal; }),
            E(EdgeKind.Transition, reserved, onloan, e => { e.Trigger = "borrow"; }),
            E(EdgeKind.Transition, reserved, avail, e => { e.Trigger = "cancel"; e.Routing = Routing.Orthogonal; e.BendOffset = 40; }),
            E(EdgeKind.Transition, onloan, lost, e => { e.Trigger = "reportLost"; }),
            E(EdgeKind.Transition, lost, avail, e => { e.Trigger = "found"; e.Routing = Routing.Orthogonal; e.BendOffset = -70; }),
            E(EdgeKind.Transition, lost, sf, e => { e.Trigger = "discard"; }));
        pkg.Diagrams.Add(sm);

        return p;
    }
}

public static class CollectionExtensions
{
    public static void AddRange<T>(this ObservableCollection<T> c, params T[] items)
    { foreach (var i in items) c.Add(i); }
}
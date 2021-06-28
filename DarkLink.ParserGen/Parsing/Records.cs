using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DarkLink.ParserGen.Parsing
{
    internal abstract record Symbol(string Name);

    internal record TerminalSymbol(string Name) : Symbol(Name)
    {
        public override string ToString()
            => $"#{Name}";
    }

    internal record NonTerminalSymbol(string Name) : Symbol(Name)
    {
        public override string ToString()
            => Name;
    }

    internal record DerivedNonTerminalSymbol(NonTerminalSymbol Base) : NonTerminalSymbol(Base.Name)
    {
        public override string ToString()
            => $"{Name}'";
    }

    internal record Production(NonTerminalSymbol Left, Symbol[] Right)
    {
        public override string ToString()
            => $"{Left} -> {(Right.Length == 0 ? "ε" : string.Join(" ", Right.AsEnumerable()))}";
    }

    internal record Grammar(ISet<NonTerminalSymbol> Variables, ISet<TerminalSymbol> Alphabet, ISet<Production> Productions, NonTerminalSymbol Start);
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DarkLink.ParserGen.Parsing
{
    partial class Parser<T>
    {
        private abstract record NodeLabel(object? S, int Start, int End);

        private record TerminalNodeLabel(TerminalSymbol? Symbol, int Start, int End) : NodeLabel(Symbol, Start, End);

        private record NonTerminalNodeLabel(NonTerminalSymbol LR0Item, int Start, int End) : NodeLabel(LR0Item, Start, End);

        private record IntermediateNodeLabel(LR0Item LR0Item, int Start, int End) : NodeLabel(LR0Item, Start, End);

        private abstract record Node();

        private abstract record SymbolNode() : Node;

        private abstract record BranchNode() : SymbolNode
        {
            public List<PackNode> Children { get; } = new();
        }

        private record TerminalNode(TerminalNodeLabel Label) : SymbolNode;

        private record NonTerminalNode(NonTerminalNodeLabel Label) : BranchNode;

        private record IntermediateNode(IntermediateNodeLabel Label) : BranchNode;

        private record PackNode(BranchNode Parent, Production Production, SymbolNode? Left, SymbolNode? Right) : Node
        {
            public IEnumerable<SymbolNode> Children
                => new[] { Left, Right }
                    .Where(o => o is not null)
                    .Cast<SymbolNode>();
        }

        private record LR0Item(Production Production, int Position)
        {
            public override string ToString()
                => $"{Production.Left} -> {(Production.Right.Length == 0 ? "ε" : string.Join(" ", Production.Right.Symbols.Select((s, i) => (Position == i ? "•" : string.Empty) + s.ToString())))}" + (Position == Production.Right.Length ? "•" : string.Empty);

            public Symbol Current => Production.Right[Position];

            public bool IsFinished => Production.Right.Length == Position;

            public LR0Item Step() => this with { Position = Position + 1 };
        }

        private record EarleyItem(LR0Item LR0, int Start, SymbolNode? Node);
    }
}
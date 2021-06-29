using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DarkLink.ParserGen.Parsing
{
    internal static partial class Earley
    {
        private abstract record NodeLabel(object? S, int Start, int End);

        private record TerminalNodeLabel(TerminalSymbol? Symbol, int Start, int End) : NodeLabel(Symbol, Start, End);

        private record NonTerminalNodeLabel(NonTerminalSymbol LR0Item, int Start, int End) : NodeLabel(LR0Item, Start, End);

        private record IntermediateNodeLabel(LR0Item LR0Item, int Start, int End) : NodeLabel(LR0Item, Start, End);

        public abstract record Node();

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

        public class Parser
        {
            private readonly Grammar grammar;

            public Parser(Grammar grammar)
            {
                this.grammar = grammar;
            }

            public IReadOnlyList<Node> Parse(IReadOnlyList<TerminalSymbol> tokens)
            {
                var n = tokens.Count;
                var E = Enumerable.Repeat(0, tokens.Count + 1)
                    .Select(_ => new HashSet<EarleyItem>())
                    .ToArray();
                var Q_ = new HashSet<EarleyItem>();
                var V = new Dictionary<NodeLabel, BranchNode>();
                BranchNode? v;

                foreach (var production in grammar.Productions.Where(p => p.Left == grammar.Start))
                {
                    if (IsInSigmaIndexN(production.Right))
                        E[0].Add(new(new(production, 0), 0, null));
                    if (production.Right.Length > 0 && production.Right[0] == tokens[0])
                        Q_.Add(new(new(production, 0), 0, null));
                }

                for (var i = 0; i <= n; i++)
                {
                    var H = new Dictionary<Symbol, BranchNode>();
                    var R = new HashSet<EarleyItem>(E[i]);
                    var Q = Q_;
                    Q_ = new();

                    while (!R.IsEmpty())
                    {
                        var A = R.Remove();
                        var h = A.Start;
                        var w = A.Node;

                        if (!A.LR0.IsFinished)
                        {
                            foreach (var production in grammar.Productions.Where(p => p.Left == A.LR0.Current))
                            {
                                var item = new EarleyItem(new(production, 0), i, null);
                                if (IsInSigmaIndexN(production.Right) && !E[i].Contains(item))
                                {
                                    E[i].Add(item);
                                    R.Add(item);
                                }
                                if (production.Right.Length > 0 && tokens.Count > i && production.Right[0] == tokens[i])
                                {
                                    Q.Add(item);
                                }
                            }

                            if (H.TryGetValue(A.LR0.Current, out v))
                            {
                                var lr0 = A.LR0 with { Position = A.LR0.Position + 1 };
                                var y = (BranchNode)MakeNode(lr0, h, i, w, v, V);
                                var beta = lr0.Production.Right.Symbols.Skip(lr0.Position).ToArray();
                                var item = new EarleyItem(lr0, h, y);
                                if (IsInSigmaIndexN(beta) && !E[i].Contains(item))
                                {
                                    E[i].Add(item);
                                    R.Add(item);
                                }
                                if (beta.Length > 0 && beta[0] == tokens[i])
                                {
                                    Q.Add(item);
                                }
                            }
                        }
                        else
                        {
                            if (w is null)
                            {
                                var label = new NonTerminalNodeLabel(A.LR0.Production.Left, i, i);
                                if (!V.TryGetValue(label, out v))
                                {
                                    v = new NonTerminalNode(label);
                                    V[label] = v;
                                }

                                var w2 = v;

                                var packNode = new PackNode(w2, A.LR0.Production, null, null);
                                if (!w2.Children.Contains(packNode))
                                {
                                    w2.Children.Add(packNode);
                                }

                                w = v;
                            }

                            if (h == i)
                            {
                                H.Add(A.LR0.Production.Left, (BranchNode)w);
                            }

                            foreach (var item in E[h].Where(i => !i.LR0.IsFinished && i.LR0.Current == A.LR0.Production.Left))
                            {
                                var y = (BranchNode)MakeNode(item.LR0.Step(), item.Start, i, item.Node, w, V);
                                var delta = item.LR0.Production.Right.Symbols.Skip(item.LR0.Position + 1).ToArray();
                                var newItem = new EarleyItem(item.LR0.Step(), item.Start, y);
                                if (IsInSigmaIndexN(delta) && !E[i].Contains(newItem))
                                {
                                    E[i].Add(newItem);
                                    R.Add(newItem);
                                }
                                if (delta.Length > 0 && tokens.Count > i && delta[0] == tokens[i])
                                {
                                    Q.Add(newItem);
                                }
                            }
                        }
                    }

                    V.Clear();
                    var token = tokens.Count > i ? tokens[i] : null;
                    var vLabel = new TerminalNodeLabel(token, i, i + 1);
                    var v2 = new TerminalNode(vLabel);

                    while (!Q.IsEmpty())
                    {
                        var A = Q.Remove(item => item.LR0.Current == tokens[i]);
                        var h = A.Start;
                        var w = A.Node;

                        var y = MakeNode(A.LR0.Step(), h, i + 1, w, v2, V);

                        var beta = A.LR0.Production.Right.Symbols.Skip(A.LR0.Position + 1).ToArray();

                        if (IsInSigmaIndexN(beta))
                        {
                            E[i + 1].Add(new(A.LR0.Step(), h, y));
                        }

                        if (beta.Length > 0 && tokens.Count > i + 1 && beta[0] == tokens[i + 1])
                        {
                            Q_.Add(new(A.LR0.Step(), h, y));
                        }
                    }
                }

                var lastSet = E.Last();
                var completedNodes = lastSet
                    .Where(i => i.LR0.IsFinished && i.LR0.Production.Left == grammar.Start && i.Start == 0)
                    .Select(i => i.Node)
                    .ToList();
                return completedNodes;
            }

            private bool IsInSigmaIndexN(Word word) => word.IsEmpty || word[0] is NonTerminalSymbol;

            private SymbolNode MakeNode(LR0Item lr0, int j, int i, SymbolNode? w, SymbolNode v, Dictionary<NodeLabel, BranchNode> V)
            {
                if (lr0.Position == 1 && !lr0.IsFinished)
                {
                    return v;
                }
                else
                {
                    var label = lr0.IsFinished
                        ? (NodeLabel)new NonTerminalNodeLabel(lr0.Production.Left, j, i)
                        : new IntermediateNodeLabel(lr0, j, i);

                    if (!V.TryGetValue(label, out var y))
                    {
                        y = label is NonTerminalNodeLabel nonTerminalNodeLabel
                            ? new NonTerminalNode(nonTerminalNodeLabel)
                            : new IntermediateNode((IntermediateNodeLabel)label);
                        V[label] = y;
                    }

                    var packNode = new PackNode(y, lr0.Production, w, v);
                    if (w is null && !y.Children.Contains(packNode))
                    {
                        y.Children.Add(packNode);
                    }

                    if (w is not null && !y.Children.Contains(packNode))
                    {
                        y.Children.Add(packNode);
                    }

                    return y;
                }
            }
        }
    }
}
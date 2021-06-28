using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DarkLink.ParserGen.Parsing
{
    internal static class Earley2
    {
        private record NodeLabel(object? S, int Start, int End);

        private record Node(NodeLabel Label)
        {
            public List<PackNode> Children { get; } = new();
        }

        private record PackNode(Node? Left, Node? Right);

        private record LR0Item(Production Production, int Position)
        {
            public override string ToString()
                => $"{Production.Left} -> {(Production.Right.Length == 0 ? "ε" : string.Join(" ", Production.Right.Select((s, i) => (Position == i ? "•" : string.Empty) + s.ToString())))}" + (Position == Production.Right.Length ? "•" : string.Empty);

            public Symbol Current => Production.Right[Position];

            public bool IsFinished => Production.Right.Length == Position;

            public LR0Item Step() => this with { Position = Position + 1 };
        }

        private record EarleyItem(LR0Item LR0, int Start, Node? Node);

        public class Parser
        {
            private readonly Grammar grammar;

            public Parser(Grammar grammar)
            {
                this.grammar = grammar;
            }

            public object Parse(IReadOnlyList<TerminalSymbol> tokens)
            {
                var n = tokens.Count;
                var E = Enumerable.Repeat(0, tokens.Count + 1)
                    .Select(_ => new Set<EarleyItem>())
                    .ToArray();
                var R = new Set<EarleyItem>();
                var Q_ = new Set<EarleyItem>();
                var V = new Dictionary<NodeLabel, Node>();
                Node? v;

                foreach (var production in grammar.Productions.Where(p => p.Left == grammar.Start))
                {
                    if (IsInSigmaIndexN(production.Right))
                        E[0].Add(new(new(production, 0), 0, null));
                    if (production.Right.Length > 0 && production.Right[0] == tokens[0])
                        Q_.Add(new(new(production, 0), 0, null));
                }

                for (var i = 0; i <= n; i++)
                {
                    var H = new Dictionary<Symbol, Node>();
                    R = E[i].Clone();
                    var Q = Q_;
                    Q_ = new();

                    while (!R.IsEmpty)
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
                                var y = MakeNode(lr0, h, i, w, v, V);
                                var beta = lr0.Production.Right.Skip(lr0.Position).ToArray();
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
                                var label = new NodeLabel(A.LR0.Production.Left, i, i);
                                if (V.TryGetValue(label, out v))
                                {
                                    v = new Node(label);
                                    V[label] = v;
                                }

                                w = v;

                                var packNode = new PackNode(null, null);
                                if (!w.Children.Contains(packNode))
                                {
                                    w.Children.Add(packNode);
                                }
                            }

                            if (h == i)
                            {
                                H.Add(A.LR0.Production.Left, w);
                            }

                            foreach (var item in E[h].Where(i => !i.LR0.IsFinished && i.LR0.Current == A.LR0.Production.Left))
                            {
                                var y = MakeNode(item.LR0.Step(), item.Start, i, item.Node, w, V);
                                var delta = item.LR0.Production.Right.Skip(item.LR0.Position + 1).ToArray();
                                var newItem = new EarleyItem(item.LR0.Step(), item.Start, y);
                                if (IsInSigmaIndexN(delta) && !E[i].Contains(newItem))
                                {
                                    E[i].Add(newItem);
                                    R.Add(newItem);
                                }
                                if (delta.Length > 0 && tokens.Count > i && delta[0] == tokens[i])
                                {
                                    Q.Add(item);
                                }
                            }
                        }
                    }

                    V.Clear();
                    var token = tokens.Count > i ? tokens[i] : null;
                    var vLabel = new NodeLabel(token, i, i + 1);
                    v = new Node(vLabel);

                    while (!Q.IsEmpty)
                    {
                        var A = Q.Remove(item => item.LR0.Current == tokens[i]);
                        var h = A.Start;
                        var w = A.Node;

                        var y = MakeNode(A.LR0.Step(), h, i + 1, w, v, V);

                        var beta = A.LR0.Production.Right.Skip(A.LR0.Position + 1).ToArray();

                        if (IsInSigmaIndexN(beta))
                        {
                            E[i + 1].Add(new(A.LR0.Step(), h, y));
                        }

                        if (beta.Length > 0 && beta[0] == tokens[i + 1])
                        {
                            Q_.Add(new(A.LR0.Step(), h, y));
                        }
                    }
                }

                return E.Last()
                    .Where(i => i.LR0.IsFinished && i.LR0.Production.Left == grammar.Start && i.Start == 0)
                    .Select(i => i.Node);
            }

            private bool IsInSigmaIndexN(Symbol[] word) => word.Length == 0 || word[0] is NonTerminalSymbol;

            private Node MakeNode(LR0Item lr0, int j, int i, Node? w, Node v, Dictionary<NodeLabel, Node> V)
            {
                var s = lr0.IsFinished ? (object)lr0.Production.Left : lr0;
                if (lr0.Position == 1 && !lr0.IsFinished)
                {
                    return v;
                }
                else
                {
                    var label = new NodeLabel(s, j, i);
                    if (!V.TryGetValue(label, out var y))
                    {
                        y = new Node(label);
                        V[label] = y;
                    }

                    var packNode = new PackNode(w, v);
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
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace DarkLink.ParserGen.Parsing
{
    internal static class Earley
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

        private record PackNode(BranchNode Parent, Production Production, SymbolNode? Left, SymbolNode? Right) : Node;

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

            public object Parse(IReadOnlyList<TerminalSymbol> tokens)
            {
                var n = tokens.Count;
                var E = Enumerable.Repeat(0, tokens.Count + 1)
                    .Select(_ => new Set<EarleyItem>())
                    .ToArray();
                var R = new Set<EarleyItem>();
                var Q_ = new Set<EarleyItem>();
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

                    while (!Q.IsEmpty)
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
                var completedNodes = lastSet.Where(i => i.LR0.IsFinished && i.LR0.Production.Left == grammar.Start && i.Start == 0)
                    .Select(i => i.Node)
                    .ToList();

                var visitor = new ForestToParseTree(new()
                {
                    { new Production(new("S"), new(new TerminalSymbol("a"), new NonTerminalSymbol("S"), new TerminalSymbol("a"))), list => list },
                    { new Production(new("S"), new(new TerminalSymbol("b"), new NonTerminalSymbol("S"), new TerminalSymbol("b"))), list => list },
                    { new Production(new("S"), Word.Empty), list => list },
                });
                var transform = visitor.Transform(completedNodes.First());

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

        private class ForestToParseTree : ForestTransformer
        {
            private readonly Dictionary<Production, Func<List<object>, object>> callbacks;

            private Node? cycleNode = null;

            private bool onCycleRetreat = false;

            private HashSet<Node> successfulVisits = new();

            public ForestToParseTree(Dictionary<Production, Func<List<object>, object>> callbacks)
            {
                this.callbacks = callbacks;
            }

            protected override void OnCycle(Node node, IReadOnlyList<Node> path)
            {
                cycleNode = node;
                onCycleRetreat = true;
            }

            protected override (bool, object?) TransformPackNode(PackNode node, List<object> data)
            {
                CheckCycle(node);
                if (successfulVisits.Contains(node.Parent))
                    return (false, default);

                var children = new List<object>();
                Debug.Assert(data.Count <= 2);
                var packData = new PackData(node, data);
                if (packData.Left != PackData.NoData)
                {
                    if (node.Left is IntermediateNode && packData.Left is IEnumerable list)
                    {
                        children.AddRange(list.Cast<object>());
                    }
                    else
                    {
                        children.Add(packData.Left);
                    }
                }
                if (packData.Right != PackData.NoData)
                {
                    children.Add(packData.Right);
                }
                if (node.Parent is IntermediateNode)
                    return (true, children);
                return CallRuleFunc(node, children);
            }

            protected override (bool, object?) TransformSymbolNode(SymbolNode node, List<object> data)
            {
                if (!successfulVisits.Contains(node))
                    return (false, default);

                CheckCycle(node);
                successfulVisits.Remove(node);

                if (node is not IntermediateNode)
                {
                    data = CollapseAmbiguity(data);
                    return CallAmbiguityFunc(node, data);
                }
                else
                {
                    if (data.Count > 1)
                    {
                        throw new NotImplementedException();
                    }
                    return (true, data[0]);
                }
            }

            protected override IEnumerable<Node> VisitNonTerminalNodeIn(NonTerminalNode node)
            {
                var children = base.VisitNonTerminalNodeIn(node);
                if (onCycleRetreat)
                    return Enumerable.Empty<Node>();
                return children;
            }

            protected override IEnumerable<Node> VisitPackNodeIn(PackNode node)
            {
                onCycleRetreat = false;
                var children = base.VisitPackNodeIn(node);
                if (!successfulVisits.Contains(node.Parent))
                    return children;
                return Enumerable.Empty<Node>();
            }

            protected override void VisitPackNodeOut(PackNode node)
            {
                base.VisitPackNodeOut(node);
                if (!onCycleRetreat)
                    successfulVisits.Add(node.Parent);
            }

            private (bool, object?) CallAmbiguityFunc(Node node, List<object> data)
            {
                if (data.Count > 1)
                    throw new NotImplementedException();
                else if (data.Count == 1)
                    return (true, data[0]);
                else
                    return (false, default);
            }

            private (bool, object?) CallRuleFunc(PackNode node, List<object> children)
            {
                return (true, callbacks[node.Production](children));
            }

            private bool CheckCycle(Node node)
            {
                if (onCycleRetreat)
                {
                    if (node == cycleNode || successfulVisits.Contains(node))
                    {
                        cycleNode = node;
                        onCycleRetreat = false;
                        return true;
                    }
                    return false;
                }
                return true;
            }

            private List<object> CollapseAmbiguity(List<object> children)
            {
                var newChildren = new List<object>();
                foreach (var child in children)
                {
                    // Check if _ambig
                    newChildren.Add(child);
                }
                return newChildren;
            }

            private class PackData
            {
                public PackData(PackNode node, List<object> data)
                {
                    Left = NoData;
                    Right = NoData;

                    if (data.Count > 0)
                    {
                        if (node.Left is not null)
                        {
                            Left = data[0];
                            if (data.Count > 1)
                                Right = data[1];
                        }
                        else
                        {
                            Right = data[0];
                        }
                    }
                }

                public static object NoData { get; } = new _NoData();

                public object? Left { get; }

                public object? Right { get; }

                private class _NoData { }
            }
        }

        private class ForestTransformer : ForestVisitor
        {
            private record ResultNode() : Node;

            private readonly Dictionary<object, List<object>> data = new();

            private readonly Stack<Node> nodeStack = new();

            public object? Transform(Node root)
            {
                var resultNode = new ResultNode();
                nodeStack.Push(resultNode);
                data[resultNode] = new List<object>();
                Visit(root);
                Debug.Assert(data[resultNode].Count <= 1);
                if (data[resultNode].Count > 0)
                    return data[resultNode][0];
                return null;
            }

            protected virtual (bool, object?) TransformPackNode(PackNode node, List<object> data)
                => (true, node);

            protected virtual (bool, object?) TransformSymbolNode(SymbolNode node, List<object> data)
                => (true, node);

            protected virtual (bool, object?) TransformTerminalNode(TerminalNode node, List<object> data)
                => (true, node);

            protected override IEnumerable<Node> VisitNonTerminalNodeIn(NonTerminalNode node)
            {
                nodeStack.Push(node);
                data[node] = new();
                return node.Children;
            }

            protected override void VisitNonTerminalNodeOut(NonTerminalNode node)
            {
                nodeStack.Pop();
                var (keep, transformed) = TransformSymbolNode(node, data[node]);
                if (keep)
                    data[nodeStack.Peek()].Add(transformed);
                data.Remove(node);
            }

            protected override IEnumerable<Node> VisitPackNodeIn(PackNode node)
            {
                nodeStack.Push(node);
                data[node] = new();
                return new[] { node.Left, node.Right }.Where(o => o is not null).Cast<Node>();
            }

            protected override void VisitPackNodeOut(PackNode node)
            {
                nodeStack.Pop();
                var (keep, transformed) = TransformPackNode(node, data[node]);
                if (keep)
                    data[nodeStack.Peek()].Add(transformed);
                data.Remove(node);
            }
        }

        private abstract class ForestVisitor
        {
            private readonly bool onlyVisitOnce;

            public ForestVisitor(bool onlyVisitOnce = false)
            {
                this.onlyVisitOnce = onlyVisitOnce;
            }

            public void Visit(Node node)
            {
                Visit(node, new(), new(), new());
            }

            protected virtual void OnCycle(Node node, IReadOnlyList<Node> path)
            {
            }

            protected abstract IEnumerable<Node> VisitNonTerminalNodeIn(NonTerminalNode node);

            protected virtual void VisitNonTerminalNodeOut(NonTerminalNode node)
            {
            }

            protected abstract IEnumerable<Node> VisitPackNodeIn(PackNode node);

            protected virtual void VisitPackNodeOut(PackNode node)
            {
            }

            protected void VisitTerminalNode(TerminalNode node)
            {
            }

            private void Visit(Node node, HashSet<Node> visited, HashSet<Node> visiting, Stack<Node> path)
            {
                if (node is TerminalNode terminalNode)
                {
                    VisitTerminalNode(terminalNode);
                    return;
                }

                if (onlyVisitOnce && visited.Contains(node))
                    return;

                if (visiting.Contains(node))
                {
                    OnCycle(node, path.ToList());
                    return;
                }

                visiting.Add(node);
                path.Push(node);

                var nextNodes = node switch
                {
                    NonTerminalNode nonTerminalNode => VisitNonTerminalNodeIn(nonTerminalNode),
                    PackNode packNode => VisitPackNodeIn(packNode),
                    _ => Enumerable.Empty<Node>(),
                };

                foreach (var nextNode in nextNodes)
                    Visit(nextNode, visited, visiting, path);

                switch (node)
                {
                    case NonTerminalNode nonTerminalNode:
                        VisitNonTerminalNodeOut(nonTerminalNode);
                        break;

                    case PackNode packNode:
                        VisitPackNodeOut(packNode);
                        break;
                }

                path.Pop();
                visiting.Remove(node);
                visited.Add(node);
            }
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DarkLink.ParserGen.Parsing
{
    partial class Parser<T, TNT, TT>
    {
        private class EarleyParser
        {
            private readonly Grammar<TNT, TT> grammar;

            public EarleyParser(Grammar<TNT, TT> grammar)
            {
                this.grammar = grammar;
            }

            public Node? Parse(IReadOnlyList<Token<TT>> tokens)
            {
                var n = tokens.Count;
                var E = Enumerable.Repeat(0, tokens.Count + 1)
                    .Select(_ => new OrderedSet<EarleyItem>())
                    .ToArray();
                var Q_ = new HashSet<EarleyItem>();
                var V = new Dictionary<NodeLabel, BranchNode>();
                BranchNode? v;

                foreach (var production in grammar.Productions.Where(p => p.Left == grammar.Start))
                {
                    CheckWordAndItem(production.Right, 0, new(new(production, 0), 0, null), E[0], null, Q_);
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
                                CheckWordAndItem(production.Right, i, item, E[i], R, Q);
                            }

                            if (H.TryGetValue(A.LR0.Current, out v))
                            {
                                var lr0 = A.LR0.Step();
                                var y = (BranchNode)MakeNode(lr0, h, i, w, v, V);
                                var beta = lr0.Production.Right.Symbols.Skip(lr0.Position).ToArray();
                                var item = new EarleyItem(lr0, h, y);
                                CheckWordAndItem(beta, i, item, E[i], R, Q);
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

                            if (h == i && H.Contains(new(A.LR0.Production.Left, (BranchNode)w)))
                            {
                                H.Add(A.LR0.Production.Left, (BranchNode)w);
                            }

                            foreach (var item in E[h])
                            {
                                if (!(!item.LR0.IsFinished && item.LR0.Current == A.LR0.Production.Left))
                                    continue;

                                var y = (BranchNode)MakeNode(item.LR0.Step(), item.Start, i, item.Node, w, V);
                                var delta = item.LR0.Production.Right.Symbols.Skip(item.LR0.Position + 1).ToArray();
                                var newItem = new EarleyItem(item.LR0.Step(), item.Start, y);
                                CheckWordAndItem(delta, i, newItem, E[i], R, Q);
                            }
                        }
                    }

                    V.Clear();
                    var token = tokens.Count > i ? tokens[i] : null;
                    var vLabel = new TerminalNodeLabel(token, i, i + 1);
                    var v2 = new TerminalNode(vLabel);

                    while (!Q.IsEmpty())
                    {
                        var A = Q.Remove(item => item.LR0.Current == tokens[i].Symbol);
                        var h = A.Start;
                        var w = A.Node;

                        var y = MakeNode(A.LR0.Step(), h, i + 1, w, v2, V);

                        var beta = A.LR0.Production.Right.Symbols.Skip(A.LR0.Position + 1).ToArray();

                        CheckWordAndItem(beta, i + 1, new(A.LR0.Step(), h, y), E[i + 1], null, Q_);
                    }
                }

                var lastSet = E.Last();
                var node = lastSet
                    .Where(i => i.LR0.IsFinished && i.LR0.Production.Left == grammar.Start && i.Start == 0)
                    .Select(i => i.Node)
                    .WhereNotNull()
                    .FirstOrDefault();
                return node;

                void CheckWordAndItem(Word word, int i, EarleyItem item, OrderedSet<EarleyItem> itemSet, HashSet<EarleyItem>? R, HashSet<EarleyItem> Q)
                {
                    if (IsInSigmaIndexN(word) && (R is null || !itemSet.Contains(item)))
                    {
                        itemSet.Add(item);
                        if (R is not null)
                            R.Add(item);
                    }
                    if (word.Length > 0 && tokens.Count > i && word[0] == tokens[i].Symbol)
                    {
                        Q.Add(item);
                    }
                }
            }

            private bool IsInSigmaIndexN(Word word) => word.IsEmpty || word[0] is NonTerminalSymbol<TNT>;

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
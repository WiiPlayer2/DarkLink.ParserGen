using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace DarkLink.ParserGen.Parsing
{
    partial class Parser<T, TNT, TT>
    {
        // Adapted from SPPF-Style Parsing From Earley Recognisers
        // by Elizabeth Scott
        // https://www.sciencedirect.com/science/article/pii/S1571066108001497
        private class EarleyParser
        {
            private readonly Grammar<TNT, TT> grammar;

            public EarleyParser(Grammar<TNT, TT> grammar)
            {
                this.grammar = grammar;
            }

            public Either<Node, IEnumerable<SyntaxError<TT>>> Parse(IReadOnlyList<Token<TT>> tokens, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var inputLength = tokens.Count;
                var itemSets = Enumerable.Repeat(0, inputLength + 1)
                    .Select(_ => new OrderedSet<EarleyItem>())
                    .ToArray();
                var Q_ = new HashSet<EarleyItem>();
                var nodeCache = new Dictionary<NodeLabel, BranchNode>();
                var syntaxErrors = new List<(int Index, SyntaxError<TT> Error)>();

                foreach (var production in grammar.Productions.Where(p => p.Left == grammar.Start))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    CheckWordAndItem(production.Right, 0, new(new(production, 0), 0, null), itemSets[0], null, Q_);
                }

                for (var i = 0; i <= inputLength; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var H = new Dictionary<Symbol, BranchNode>();
                    var R = new HashSet<EarleyItem>(itemSets[i]);
                    var Q = Q_;
                    Q_ = new();

                    while (!R.IsEmpty())
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var item = R.Remove();

                        if (!item.LR0.IsFinished)
                        {
                            Predict(i, H, R, Q, item);
                        }
                        else
                        {
                            Complete(i, H, R, Q, item);
                        }
                    }

                    nodeCache.Clear();
                    Scan(i, Q);
                }

                var lastSet = itemSets.Last();
                var node = lastSet
                    .Where(i => i.LR0.IsFinished && i.LR0.Production.Left == grammar.Start && i.Start == 0)
                    .Select(i => i.Node)
                    .WhereNotNull()
                    .FirstOrDefault();

                if (node is not null)
                    return node;

                var lastRelevantErrors = syntaxErrors
                    .Distinct()
                    .ToLookup(o => o.Index)
                    .Last(o => itemSets[o.Key].Any())
                    .Select(o => o.Error);
                return Either.Right(lastRelevantErrors.ToList().AsEnumerable());

                void CheckWordAndItem(Word word, int i, EarleyItem item, OrderedSet<EarleyItem> itemSet, HashSet<EarleyItem>? R, HashSet<EarleyItem> Q)
                {
                    if (IsInSigmaIndexN(word))
                    {
                        if ((R is null || !itemSet.Contains(item)))
                        {
                            itemSet.Add(item);
                            if (R is not null)
                                R.Add(item);
                        }
                    }
                    else if (word.Length > 0 && tokens.Count > i && word[0] == tokens[i].Symbol)
                    {
                        Q.Add(item);
                    }
                    else if (word.Length > 0 && R is not null)
                    {
                        syntaxErrors.Add((i, new(word[0], i < inputLength ? tokens[i] : default)));
                    }
                }

                void Predict(int i, Dictionary<Symbol, BranchNode> H, HashSet<EarleyItem> R, HashSet<EarleyItem> Q, EarleyItem item)
                {
                    foreach (var production in grammar.Productions.Where(p => p.Left == item.LR0.Current))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var newItem = new EarleyItem(new(production, 0), i, null);
                        CheckWordAndItem(production.Right, i, newItem, itemSets[i], R, Q);
                    }

                    if (H.TryGetValue(item.LR0.Current, out var v))
                    {
                        var lr0 = item.LR0.Step();
                        var y = (Parser<T, TNT, TT>.BranchNode)MakeNode(lr0, item.Start, i, item.Node, v, nodeCache);
                        var beta = lr0.Production.Right.Symbols.Skip(lr0.Position).ToArray();
                        var newItem = new Parser<T, TNT, TT>.EarleyItem(lr0, item.Start, y);
                        CheckWordAndItem(beta, i, newItem, itemSets[i], R, Q);
                    }
                }

                void Complete(int i, Dictionary<Symbol, BranchNode> H, HashSet<EarleyItem> R, HashSet<EarleyItem> Q, EarleyItem item)
                {
                    var itemNode = item.Node;

                    if (itemNode is null)
                    {
                        var label = new NonTerminalNodeLabel(item.LR0.Production.Left, i, i);
                        if (!nodeCache.TryGetValue(label, out var v))
                        {
                            v = new NonTerminalNode(label);
                            nodeCache[label] = v;
                        }

                        var w2 = v;

                        var packNode = new PackNode(w2, item.LR0.Production, null, null);
                        if (!w2.Children.Contains(packNode))
                        {
                            w2.Children.Add(packNode);
                        }

                        itemNode = v;
                    }

                    if (item.Start == i && !H.Contains(new(item.LR0.Production.Left, (BranchNode)itemNode))) // checking H may be unnecessary
                    {
                        H.Add(item.LR0.Production.Left, (BranchNode)itemNode);
                    }

                    foreach (var otherItem in itemSets[item.Start].Where(o => (!o.LR0.IsFinished && o.LR0.Current == item.LR0.Production.Left)))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var y = (BranchNode)MakeNode(otherItem.LR0.Step(), otherItem.Start, i, otherItem.Node, itemNode, nodeCache);
                        var delta = otherItem.LR0.Production.Right.Symbols.Skip(otherItem.LR0.Position + 1).ToArray();
                        var newItem = new EarleyItem(otherItem.LR0.Step(), otherItem.Start, y);
                        CheckWordAndItem(delta, i, newItem, itemSets[i], R, Q);
                    }
                }

                void Scan(int i, HashSet<EarleyItem> Q)
                {
                    var token = tokens.Count > i ? tokens[i] : null;
                    var vLabel = new TerminalNodeLabel(token, i, i + 1);
                    var v = new TerminalNode(vLabel);

                    while (!Q.IsEmpty())
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var item = Q.Remove(o => o.LR0.Current == tokens[i].Symbol);
                        var y = MakeNode(item.LR0.Step(), item.Start, i + 1, item.Node, v, nodeCache);
                        var beta = item.LR0.Production.Right.Symbols.Skip(item.LR0.Position + 1).ToArray();
                        CheckWordAndItem(beta, i + 1, new(item.LR0.Step(), item.Start, y), itemSets[i + 1], null, Q_);
                    }
                }
            }

            private bool IsInSigmaIndexN(Word word) => word.IsEmpty || word[0] is NonTerminalSymbol<TNT>;

            private SymbolNode MakeNode(LR0Item lr0, int j, int i, SymbolNode? w, SymbolNode v, Dictionary<NodeLabel, BranchNode> V)
            {
                var alpha = (Word)lr0.Production.Right.Symbols.Take(lr0.Position - 1).ToArray();
                var beta = (Word)lr0.Production.Right.Symbols.Skip(lr0.Position).ToArray();

                if (alpha.IsEmpty && !beta.IsEmpty)
                {
                    return v;
                }
                else
                {
                    var label = beta.IsEmpty
                        ? (NodeLabel)new NonTerminalNodeLabel(lr0.Production.Left, j, i)
                        : new IntermediateNodeLabel(lr0, j, i);

                    if (!V.TryGetValue(label, out var y))
                    {
                        y = label switch
                        {
                            NonTerminalNodeLabel nonTerminalNodeLabel => new NonTerminalNode(nonTerminalNodeLabel),
                            IntermediateNodeLabel intermediateNodeLabel => new IntermediateNode(intermediateNodeLabel),
                            _ => throw new NotImplementedException(),
                        };
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
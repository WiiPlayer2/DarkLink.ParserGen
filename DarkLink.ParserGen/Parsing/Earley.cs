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
        private record SPPFNodeLabel(object SymbolOrProductionState, int Start, int End);

        private abstract record SPPFNode();

        private record SPPFSymbolNode(SPPFNodeLabel Label, List<SPPFNode> Children) : SPPFNode();

        private record SPPFPackedNode(SPPFNode Parent, ProductionState ProductionState, SPPFNode? Left, object? Right) : SPPFNode();

        private record ProductionState(Production Production, int ProductionPosition)
        {
            public override string ToString()
                => $"{Production.Left} -> {(Production.Right.Length == 0 ? "ε" : string.Join(" ", Production.Right.Select((s, i) => (ProductionPosition == i ? "•" : string.Empty) + s.ToString())))}" + (ProductionPosition == Production.Right.Length ? "•" : string.Empty);
        }

        public class Parser
        {
            private readonly Grammar grammar;

            private readonly Production startProduction;

            public Parser(Grammar grammar)
            {
                this.grammar = grammar;
                var newStart = new DerivedNonTerminalSymbol(grammar.Start);
                startProduction = new Production(newStart, new[] { grammar.Start });
            }

            public object Parse(IEnumerable<TerminalSymbol> terminalSymbols)
            {
                var parsing = new Parsing(terminalSymbols.ToList(), grammar, startProduction);
                var lastSet = (OrderedSet<State>)parsing.Parse();
                var states = lastSet.Where(o => o.Node is not null && o.IsFinished && o.OriginPosition == 0).ToList();
                return states;
            }

            private class Parsing
            {
                private readonly Grammar grammar;

                private readonly Dictionary<SPPFNodeLabel, SPPFSymbolNode> nodes = new();

                private readonly OrderedSet<State>[] sets;

                private readonly IReadOnlyList<TerminalSymbol> terminalSymbols;

                public Parsing(IReadOnlyList<TerminalSymbol> terminalSymbols, Grammar grammar, Production startProduction)
                {
                    sets = new OrderedSet<State>[terminalSymbols.Count + 1];
                    for (var i = 0; i < sets.Length; i++)
                    {
                        sets[i] = new OrderedSet<State>();
                    }

                    this.terminalSymbols = terminalSymbols;

                    sets[0].Add(new(new(startProduction, 0), 0, null));
                    this.grammar = grammar;
                }

                public object Parse()
                {
                    for (var k = 0; k < sets.Length; k++)
                    {
                        foreach (var state in sets[k])
                        {
                            if (!state.IsFinished)
                            {
                                if (state.Current is NonTerminalSymbol)
                                    Predict(state, k);
                                else
                                    Scan(state, k, terminalSymbols);
                            }
                            else
                            {
                                Complete(state, k);
                            }
                        }
                    }

                    return sets.Last();
                }

                private void Complete(State state, int k)
                {
                    foreach (var pastState in sets[state.OriginPosition]
                        .Where(s => !s.IsFinished && s.Current == state.Production.Production.Left))
                    {
                        var label = new SPPFNodeLabel(pastState.Production, pastState.OriginPosition, k);
                        var newNode = GetOrCreateNode(label);
                        var newState = new State(
                            pastState.Production with
                            {
                                ProductionPosition = pastState.Production.ProductionPosition + 1
                            },
                            pastState.OriginPosition,
                            newNode);

                        newNode.Children.Add(new SPPFPackedNode(newNode, pastState.Production, state.Node, pastState.Production.Production));

                        sets[k].Add(newState);
                    }
                }

                private SPPFSymbolNode GetOrCreateNode(SPPFNodeLabel label)
                {
                    if (!nodes.TryGetValue(label, out var node))
                    {
                        node = new SPPFSymbolNode(label, new());
                        nodes[label] = node;
                    }

                    return node;
                }

                private void Predict(State state, int k)
                {
                    var currentSymbol = state.Current;
                    foreach (var production in grammar.Productions.Where(p => p.Left == currentSymbol))
                    {
                        sets[k].Add(new(new(production, 0), k, null));
                    }
                }

                private void Scan(State state, int k, IReadOnlyList<TerminalSymbol> terminalSymbols)
                {
                    var currentSymbol = state.Current;
                    if (k < terminalSymbols.Count && terminalSymbols[k] == currentSymbol)
                    {
                        var label = new SPPFNodeLabel(currentSymbol, k, k + 1);
                        var newNode = GetOrCreateNode(label);
                        var newState = new State(
                            state.Production with
                            {
                                ProductionPosition = state.Production.ProductionPosition + 1,
                            },
                            state.OriginPosition,
                            newNode);

                        newNode.Children.Add(new SPPFPackedNode(newNode, state.Production, state.Node, terminalSymbols[k]));

                        sets[k + 1].Add(newState);
                    };
                }
            }
        }

        [DebuggerDisplay("Count = {Count,nq}")]
        private class OrderedSet<T> : IEnumerable<T>
        {
            private readonly List<T> list = new();

            public int Count => list.Count;

            public T this[int index] => list[index];

            public bool Add(T item)
            {
                if (list.Contains(item))
                    return false;
                list.Add(item);
                return true;
            }

            public IEnumerator<T> GetEnumerator() => new Enumerator(this);

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

            private class Enumerator : IEnumerator<T>
            {
                public readonly OrderedSet<T> set;

                private int nextIndex = 0;

                public Enumerator(OrderedSet<T> set)
                {
                    this.set = set;
                }

                public T Current => set.list[nextIndex - 1];

                object? IEnumerator.Current => Current;

                public void Dispose()
                {
                }

                public bool MoveNext()
                {
                    if (nextIndex >= set.list.Count)
                        return false;

                    nextIndex++;
                    return true;
                }

                public void Reset() => nextIndex = 0;
            }
        }

        private class State
        {
            public State(ProductionState production, int originPosition, SPPFSymbolNode? node)
            {
                this.Production = production;
                this.OriginPosition = originPosition;
                this.Node = node;
            }

            public Symbol Current => Production.Production.Right[Production.ProductionPosition];

            public bool IsFinished => Production.ProductionPosition == Production.Production.Right.Length;

            public SPPFSymbolNode? Node { get; set; }

            public int OriginPosition { get; }

            public ProductionState Production { get; }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(this, obj))
                    return true;

                if (obj is not State other)
                    return false;

                return Production == other.Production
                    && OriginPosition == other.OriginPosition;
            }

            public override int GetHashCode()
                => Production.GetHashCode()
                    ^ OriginPosition.GetHashCode();

            public override string ToString()
                => $"({Production}, {OriginPosition})";
        }
    }
}
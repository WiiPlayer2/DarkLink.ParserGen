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
        private record ProductionState(Production Production, int ProductionPosition)
        {
            public override string ToString()
                => $"{Production.Left} -> {(Production.Right.Length == 0 ? "ε" : string.Join(" ", Production.Right.Select((s, i) => (ProductionPosition == i ? "•" : string.Empty) + s.Name)))}" + (ProductionPosition == Production.Right.Length ? "•" : string.Empty);
        }

        private record State(ProductionState Production, int OriginPosition)
        {
            public override string ToString()
                => $"({Production}, {OriginPosition})";

            public bool IsFinished => Production.ProductionPosition == Production.Production.Right.Length;

            public Symbol Current => Production.Production.Right[Production.ProductionPosition];
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
                var terminalSymbolList = terminalSymbols.ToList();
                var sets = Init(terminalSymbolList);
                sets[0].Add(new(new(startProduction, 0), 0));

                for (var k = 0; k < sets.Length; k++)
                {
                    for (var i = 0; i < sets[k].Count; i++)
                    {
                        var state = sets[k][i];
                        if (!state.IsFinished)
                        {
                            if (state.Current is NonTerminalSymbol)
                                Predict(state, k, sets[k]);
                            else
                                Scan(state, k, terminalSymbolList, sets[k + 1]);
                        }
                        else
                        {
                            Complete(state, k, sets);
                        }
                    }
                }

                return sets.Last();
            }

            private void Complete(State state, int k, OrderedSet<State>[] sets)
            {
                foreach (var pastState in sets[state.OriginPosition]
                    .Where(s => !s.IsFinished && s.Current == state.Production.Production.Left))
                {
                    sets[k].Add(pastState with
                    {
                        Production = pastState.Production with
                        {
                            ProductionPosition = pastState.Production.ProductionPosition + 1
                        }
                    });
                }
            }

            private OrderedSet<State>[] Init(IReadOnlyList<TerminalSymbol> terminalSymbols)
            {
                var sets = new OrderedSet<State>[terminalSymbols.Count + 1];
                for (var i = 0; i < sets.Length; i++)
                {
                    sets[i] = new OrderedSet<State>();
                }
                return sets;
            }

            private void Predict(State state, int k, OrderedSet<State> set)
            {
                var currentSymbol = state.Current;
                foreach (var production in grammar.Productions.Where(p => p.Left == currentSymbol))
                {
                    set.Add(new(new(production, 0), k));
                }
            }

            private void Scan(State state, int k, IReadOnlyList<TerminalSymbol> terminalSymbols, OrderedSet<State> nextSet)
            {
                var currentSymbol = state.Current;
                if (k < terminalSymbols.Count && terminalSymbols[k] == currentSymbol)
                    nextSet.Add(state with
                    {
                        Production = state.Production with
                        {
                            ProductionPosition = state.Production.ProductionPosition + 1
                        }
                    });
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
    }
}
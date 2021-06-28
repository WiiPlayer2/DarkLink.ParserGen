using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DarkLink.ParserGen.Parsing
{
    internal static class Earley
    {
        private record State(Production Production, int ProductionPosition, int OrginPosition);

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
                sets[0].Add(new(startProduction, 0, 0));

                for (var k = 0; k < sets.Length; k++)
                {
                    for (var i = 0; i < sets[k].Count; i++)
                    {
                        var state = sets[k][i];
                        if (!Finished(state))
                        {
                            if (NextElementOf(state) is NonTerminalSymbol)
                                Predictor(state, k, sets);
                            else
                                Scanner(state, k, terminalSymbolList, sets);
                        }
                        else
                        {
                            Completer(state, k, sets);
                        }
                    }
                }

                return sets;
            }

            private void Completer(State state, int k, OrderedSet<State>[] sets)
            {
                foreach (var pastState in sets[state.OrginPosition]
                    .Where(s => s.Production.Right.Length > s.ProductionPosition && s.Production.Right[s.ProductionPosition] == state.Production.Left))
                {
                    sets[k].Add(pastState with { ProductionPosition = pastState.ProductionPosition + 1 });
                }
            }

            private bool Finished(State state) => state.ProductionPosition == state.Production.Right.Length;

            private OrderedSet<State>[] Init(IReadOnlyList<TerminalSymbol> terminalSymbols)
            {
                var sets = new OrderedSet<State>[terminalSymbols.Count + 1];
                for (var i = 0; i < sets.Length; i++)
                {
                    sets[i] = new OrderedSet<State>();
                }
                return sets;
            }

            private Symbol NextElementOf(State state) => state.Production.Right[state.ProductionPosition];

            private void Predictor(State state, int k, OrderedSet<State>[] sets)
            {
                var currentSymbol = state.Production.Right[state.ProductionPosition];
                foreach (var production in grammar.Productions.Where(p => p.Left == currentSymbol))
                {
                    sets[k].Add(new(production, 0, k));
                }
            }

            private void Scanner(State state, int k, IReadOnlyList<TerminalSymbol> terminalSymbols, OrderedSet<State>[] sets)
            {
                var currentSymbol = NextElementOf(state);
                if (k < terminalSymbols.Count && terminalSymbols[k] == currentSymbol)
                    sets[k + 1].Add(state with { ProductionPosition = state.ProductionPosition + 1 });
            }
        }

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
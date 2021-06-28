using System;
using System.Collections.Immutable;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DarkLink.ParserGen.Parsing
{
    internal static class GLR
    {
        private record PDA(
            ISet<TerminalSymbol> InputAlphabet,
            ISet<Symbol> StackAlphabet,
            ISet<PDATransition> Transitions,
            Symbol StartStackSymbol);

        private record PDATransition(TerminalSymbol? TapeSymbol, Symbol StackSymbol, Symbol[] PushStackSymbols)
        {
            public bool IsApplicableTo(PDAState state)
            {
                if (state.Stack.IsEmpty)
                    return false;

                state.Stack.Pop(out var stackSymbol);

                if (stackSymbol != StackSymbol)
                    return false;

                if (TapeSymbol is null)
                    return true;

                if (state.Tape.IsEmpty)
                    return false;

                state.Tape.Dequeue(out var inputSymbol);
                return TapeSymbol == inputSymbol;
            }
        }

        public record PDAResult(
            bool Success,
            PDAState State);

        public record PDAState(
            ImmutableQueue<TerminalSymbol> Tape,
            ImmutableStack<Symbol> Stack,
            int MatchCount = 0,
            bool Failure = false);

        public class Parser
        {
            private readonly PDA pda;

            public Parser(Grammar grammar)
            {
                var gnfGrammar = ConvertToGNF(grammar);
                var transitions = new HashSet<PDATransition>();
                foreach (var production in gnfGrammar.Productions)
                {
                    transitions.Add(new(default, production.Left, production.Right));
                }
                foreach (var terminalSymbol in gnfGrammar.Alphabet)
                {
                    transitions.Add(new(terminalSymbol, terminalSymbol, Array.Empty<Symbol>()));
                }

                pda = new PDA(
                    gnfGrammar.Alphabet,
                    new HashSet<Symbol>(gnfGrammar.Alphabet.Cast<Symbol>().Concat(gnfGrammar.Variables)),
                    transitions,
                    gnfGrammar.Start);
            }

            public IEnumerable<PDAResult> Parse(IEnumerable<TerminalSymbol> terminalSymbols)
            {
                var terminalSymbolList = terminalSymbols.ToList();

                var results = new HashSet<PDAResult>();
                var states = new HashSet<PDAState>();

                states.Add(new(ImmutableQueue.CreateRange(terminalSymbolList), ImmutableStack.Create(pda.StartStackSymbol)));

                while (states.Count > 0)
                {
                    var newStates = new HashSet<PDAState>();

                    foreach (var state in states)
                    {
                        var nextStates = GetNextStates(state).ToList();
                        if (nextStates.Count == 0)
                        {
                            results.Add(new(false, state));
                            continue;
                        }

                        foreach (var nextState in nextStates)
                        {
                            if (nextState.Failure
                                || (nextState.Tape.IsEmpty && nextState.Stack.IsEmpty))
                                results.Add(new(!nextState.Failure, nextState));
                            else
                                newStates.Add(nextState);
                        }
                    }

                    states = newStates;
                }

                return results;
            }

            private static PDAState ApplyTransition(PDAState state, PDATransition transition)
            {
                var failed = false;
                var failure = false;

                var newMatchCount = state.MatchCount;

                var newTape = state.Tape;
                if (transition.TapeSymbol is not null)
                {
                    if (newTape.IsEmpty)
                    {
                        failed = true;
                    }
                    else
                    {
                        newTape = newTape.Dequeue();
                        newMatchCount++;
                    }
                }

                var newStack = state.Stack;
                if (newStack.IsEmpty)
                    failed = true;
                else
                    newStack = newStack.Pop();

                if (!failed)
                {
                    foreach (var symbol in transition.PushStackSymbols)
                        newStack = newStack.Push(symbol);
                }
                else
                {
                    newTape = state.Tape;
                    newStack = state.Stack;
                }

                if (failed
                    || newTape.IsEmpty && !newStack.IsEmpty
                    || newTape.Count() < newStack.Count())
                {
                    failure = true;
                }

                return new PDAState(newTape, newStack, newMatchCount, failure);
            }

            private Grammar ConvertToGNF(Grammar grammar)
            {
                var newGrammar = ReplaceStartOnRightSide(grammar);
                newGrammar = RemoveNullProductions(newGrammar);
                newGrammar = RemoveUnitProductions(newGrammar);
                newGrammar = RemoveLeftRecursion(newGrammar);
                newGrammar = CreateProperSubstitutions(newGrammar);

                return newGrammar;

                Grammar ReplaceStartOnRightSide(Grammar g)
                {
                    if (!g.Productions.Any(o => o.Right.Contains(g.Start)))
                        return g;

                    var newStart = new DerivedNonTerminalSymbol(g.Start);
                    var newProduction = new Production(newStart, new[] { g.Start });
                    return g with
                    {
                        Start = newStart,
                        Productions = new HashSet<Production>(g.Productions.Concat(new[] { newProduction })),
                    };
                }

                Grammar RemoveNullProductions(Grammar g)
                {
                    do
                    {
                        var nullProduction = g.Productions
                            .Where(o => o.Left != g.Start && o.Right.Length == 0)
                            .FirstOrDefault();
                        if (nullProduction is null)
                            return g;
                    }
                    while (true);
                }

                Grammar RemoveUnitProductions(Grammar g)
                {
                    do
                    {
                        var unitProduction = g.Productions
                            .Where(o => o.Right.Length == 1 && o.Right[0] is NonTerminalSymbol)
                            .FirstOrDefault();
                        if (unitProduction is null)
                            return g;

                        var right = (NonTerminalSymbol)unitProduction.Right[0];

                        var newProductions = g.Productions
                            .Where(p => p.Left == right && (p.Right.Length == 1 && p.Right[0] is TerminalSymbol terminalSymbol || p.Right.Length == 0))
                            .Select(p => new Production(unitProduction.Left, p.Right));

                        g = g with
                        {
                            Productions = new HashSet<Production>(g.Productions.Except(new[] { unitProduction }).Concat(newProductions)),
                        };
                    }
                    while (true);
                }

                Grammar RemoveLeftRecursion(Grammar g)
                {
                    throw new NotImplementedException();
                }

                Grammar CreateProperSubstitutions(Grammar g)
                {
                    throw new NotImplementedException();
                }
            }

            private IEnumerable<PDAState> GetNextStates(PDAState state)
                => pda.Transitions
                    .Where(t => t.IsApplicableTo(state))
                    .Select(t => ApplyTransition(state, t));
        }
    }
}
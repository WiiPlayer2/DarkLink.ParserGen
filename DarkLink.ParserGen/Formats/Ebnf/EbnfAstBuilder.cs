using DarkLink.ParserGen.Parsing;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace DarkLink.ParserGen.Formats.Ebnf
{
    internal record EbnfNode();

    internal record EbnfString(string S) : EbnfNode;

    internal record EbnfRule(string Left, EbnfExpression Right) : EbnfNode;

    internal abstract record EbnfExpression() : EbnfNode;

    internal record EbnfRuleRef(string Identifier) : EbnfExpression;

    internal record EbnfTerminal(string Literal) : EbnfExpression;

    internal record EbnfAnd(EbnfExpression Left, EbnfExpression Right) : EbnfExpression;

    internal record EbnfOption(EbnfExpression Expression) : EbnfExpression;

    internal record EbnfRepeat(EbnfExpression Expression) : EbnfExpression;

    internal record EbnfGroup(EbnfExpression Expression) : EbnfExpression;

    internal record EbnfOr(EbnfExpression Left, EbnfExpression Right) : EbnfExpression;

    internal record EbnfGrammar(ImmutableList<EbnfRule> Rules) : EbnfNode;

    internal record EbnfConfig(EbnfGrammar Grammar) : EbnfNode;

    internal class EbnfAstBuilder : AstBuilder<EbnfNode, NTs>
    {
        public EbnfAstBuilder()
        {
            R(G.P(NTs.Letter, G.T(Ts.Letter)), nameof(CharString));
            R(G.P(NTs.Digit, G.T(Ts.Digit)), nameof(CharString));
            R(G.P(NTs.Symbol, G.T(Ts.Symbol)), nameof(CharString));
            R(G.P(NTs.Symbol, G.T(Ts.LeftSquareBracket)), nameof(CharString));
            R(G.P(NTs.Symbol, G.T(Ts.RightSquareBracket)), nameof(CharString));
            R(G.P(NTs.Symbol, G.T(Ts.LeftCurlyBracket)), nameof(CharString));
            R(G.P(NTs.Symbol, G.T(Ts.RightCurlyBracket)), nameof(CharString));
            R(G.P(NTs.Symbol, G.T(Ts.LeftRoundBracket)), nameof(CharString));
            R(G.P(NTs.Symbol, G.T(Ts.RightRoundBracket)), nameof(CharString));
            //R(G.P(NTs.Symbol, G.T(Ts.SingleQuote)), nameof(CharString));
            //R(G.P(NTs.Symbol, G.T(Ts.DoubleQuote)), nameof(CharString));
            R(G.P(NTs.Symbol, G.T(Ts.Equals)), nameof(CharString));
            R(G.P(NTs.Symbol, G.T(Ts.Pipe)), nameof(CharString));
            R(G.P(NTs.Symbol, G.T(Ts.Comma)), nameof(CharString));
            R(G.P(NTs.Symbol, G.T(Ts.Semicolon)), nameof(CharString));

            R(G.P(NTs.Character, G.NT(NTs.Letter)), PASS);
            R(G.P(NTs.Character, G.NT(NTs.Digit)), PASS);
            R(G.P(NTs.Character, G.NT(NTs.Symbol)), PASS);
            R(G.P(NTs.Character, G.T(Ts.Underscore)), PASS);

            R(G.P(NTs.Identifier, G.NT(NTs.Letter), G.NT(NTs.IdentifierCont)), nameof(Concat));
            R(G.P(NTs.IdentifierCont), nameof(EmptyString));
            R(G.P(NTs.IdentifierCont, G.NT(NTs.Letter), G.NT(NTs.IdentifierCont)), nameof(Concat));
            R(G.P(NTs.IdentifierCont, G.NT(NTs.Digit), G.NT(NTs.IdentifierCont)), nameof(Concat));
            R(G.P(NTs.IdentifierCont, G.T(Ts.Underscore), G.NT(NTs.IdentifierCont)), nameof(PrependChar));

            R(G.P(NTs.Terminal, G.T(Ts.SingleQuote), G.NT(NTs.Character), G.NT(NTs.TerminalCont), G.T(Ts.SingleQuote)), MAP(nameof(Concat), 1, 2));
            R(G.P(NTs.Terminal, G.T(Ts.DoubleQuote), G.NT(NTs.Character), G.NT(NTs.TerminalCont), G.T(Ts.DoubleQuote)), MAP(nameof(Concat), 1, 2));
            R(G.P(NTs.TerminalCont), nameof(EmptyString));
            R(G.P(NTs.TerminalCont, G.NT(NTs.Character), G.NT(NTs.TerminalCont)), nameof(Concat));

            R(G.P(NTs.Lhs, G.NT(NTs.Identifier)), PASS);

            R(G.P(NTs.Rhs, G.NT(NTs.Identifier)), nameof(RuleRef));
            R(G.P(NTs.Rhs, G.NT(NTs.Terminal)), nameof(Terminal));
            R(G.P(NTs.Rhs, G.T(Ts.LeftSquareBracket), G.NT(NTs.Rhs), G.T(Ts.RightSquareBracket)), MAP(nameof(Option), 1));
            R(G.P(NTs.Rhs, G.T(Ts.LeftCurlyBracket), G.NT(NTs.Rhs), G.T(Ts.RightCurlyBracket)), MAP(nameof(Repeat), 1));
            R(G.P(NTs.Rhs, G.T(Ts.LeftRoundBracket), G.NT(NTs.Rhs), G.T(Ts.RightRoundBracket)), MAP(nameof(Group), 1));
            R(G.P(NTs.Rhs, G.NT(NTs.Rhs), G.T(Ts.Pipe), G.NT(NTs.Rhs)), MAP(nameof(Or), 0, 2));
            R(G.P(NTs.Rhs, G.NT(NTs.Rhs), G.T(Ts.Comma), G.NT(NTs.Rhs)), MAP(nameof(And), 0, 2));

            R(G.P(NTs.Rule, G.NT(NTs.Lhs), G.T(Ts.Equals), G.NT(NTs.Rhs), G.T(Ts.Semicolon)), MAP(nameof(Rule), 0, 2));

            R(G.P(NTs.Grammar), nameof(EmptyGrammar));
            R(G.P(NTs.Grammar, G.NT(NTs.Rule), G.NT(NTs.Grammar)), nameof(ExtendGrammar));

            R(G.P(NTs.Config, G.NT(NTs.Grammar)), nameof(Config));
        }

        private EbnfAnd And(EbnfExpression left, EbnfExpression right)
            => new EbnfAnd(left, right);

        private EbnfString CharString(Token<Ts> token)
            => new EbnfString(token.Value);

        private EbnfString Concat(EbnfString s1, EbnfString s2)
            => new EbnfString(s1.S + s2.S);

        private EbnfConfig Config(EbnfGrammar grammar)
            => new EbnfConfig(grammar);

        private EbnfGrammar EmptyGrammar()
            => new EbnfGrammar(ImmutableList.Create<EbnfRule>());

        private EbnfString EmptyString()
            => new EbnfString(string.Empty);

        private EbnfGrammar ExtendGrammar(EbnfRule rule, EbnfGrammar grammar)
            => new EbnfGrammar(grammar.Rules.Insert(0, rule));

        private EbnfGroup Group(EbnfExpression expression)
            => new EbnfGroup(expression);

        private EbnfOption Option(EbnfExpression expression)
            => new EbnfOption(expression);

        private EbnfOr Or(EbnfExpression left, EbnfExpression right)
            => new EbnfOr(left, right);

        private EbnfString PrependChar(Token<Ts> token, EbnfString str)
            => new EbnfString(token.Value + str.S);

        private EbnfRepeat Repeat(EbnfExpression expression)
            => new EbnfRepeat(expression);

        private EbnfRule Rule(EbnfString left, EbnfExpression right)
            => new EbnfRule(left.S, right);

        private EbnfRuleRef RuleRef(EbnfString identifier)
            => new EbnfRuleRef(identifier.S);

        private EbnfTerminal Terminal(EbnfString literal)
            => new EbnfTerminal(literal.S);
    }
}
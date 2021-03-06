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

    internal record EbnfTerminal() : EbnfExpression;

    internal record EbnfLiteral(string Literal) : EbnfTerminal;

    internal record EbnfAnd(EbnfExpression Left, EbnfExpression Right) : EbnfExpression;

    internal record EbnfOption(EbnfExpression Expression) : EbnfExpression;

    internal record EbnfRepeat(EbnfExpression Expression) : EbnfExpression;

    internal record EbnfGroup(EbnfExpression Expression) : EbnfExpression;

    internal record EbnfOr(EbnfExpression Left, EbnfExpression Right) : EbnfExpression;

    internal record EbnfGrammar(ImmutableList<EbnfRule> Rules) : EbnfNode;

    internal record EbnfConfig(EbnfMeta Meta, EbnfGrammar Grammar) : EbnfNode;

    internal record EbnfMeta(ImmutableDictionary<string, string> Entries) : EbnfNode;

    internal record EbnfMetaEntry(string Key, string Value) : EbnfNode;

    internal record EbnfSpecialText(string Text) : EbnfTerminal;

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
            R(G.P(NTs.Symbol, G.T(Ts.Dollar)), nameof(CharString));
            R(G.P(NTs.Symbol, G.T(Ts.Underscore)), nameof(CharString));

            R(G.P(NTs.Character, G.NT(NTs.Letter)), PASS);
            R(G.P(NTs.Character, G.NT(NTs.Digit)), PASS);
            R(G.P(NTs.Character, G.NT(NTs.Symbol)), PASS);

            R(G.P(NTs.Identifier, G.NT(NTs.Letter), G.NT(NTs.IdentifierCont)), nameof(Concat));
            R(G.P(NTs.IdentifierCont), nameof(EmptyString));
            R(G.P(NTs.IdentifierCont, G.NT(NTs.Letter), G.NT(NTs.IdentifierCont)), nameof(Concat));
            R(G.P(NTs.IdentifierCont, G.NT(NTs.Digit), G.NT(NTs.IdentifierCont)), nameof(Concat));
            R(G.P(NTs.IdentifierCont, G.T(Ts.Underscore), G.NT(NTs.IdentifierCont)), nameof(PrependChar));

            R(G.P(NTs.Literal, G.T(Ts.SingleQuote), G.NT(NTs.Character), G.NT(NTs.LiteralCont), G.T(Ts.SingleQuote)), MAP(nameof(Literal), 1, 2));
            R(G.P(NTs.Literal, G.T(Ts.DoubleQuote), G.NT(NTs.Character), G.NT(NTs.LiteralCont), G.T(Ts.DoubleQuote)), MAP(nameof(Literal), 1, 2));
            R(G.P(NTs.LiteralCont, G.NT(NTs.Character), G.NT(NTs.LiteralCont)), nameof(Concat));
            R(G.P(NTs.LiteralCont), nameof(EmptyString));

            R(G.P(NTs.SpecialText), nameof(EmptyString));
            R(G.P(NTs.SpecialText, G.NT(NTs.Character), G.NT(NTs.SpecialText)), nameof(Concat));

            R(G.P(NTs.Special, G.T(Ts.QuestionMark), G.NT(NTs.SpecialText), G.T(Ts.QuestionMark)), MAP(nameof(SpecialText), 1));

            R(G.P(NTs.Terminal, G.NT(NTs.Literal)), PASS);
            R(G.P(NTs.Terminal, G.NT(NTs.Special)), PASS);

            R(G.P(NTs.Lhs, G.NT(NTs.Identifier)), PASS);

            R(G.P(NTs.Rhs, G.NT(NTs.Identifier)), nameof(RuleRef));
            R(G.P(NTs.Rhs, G.NT(NTs.Terminal)), PASS);
            R(G.P(NTs.Rhs, G.T(Ts.LeftSquareBracket), G.NT(NTs.Rhs), G.T(Ts.RightSquareBracket)), MAP(nameof(Option), 1));
            R(G.P(NTs.Rhs, G.T(Ts.LeftCurlyBracket), G.NT(NTs.Rhs), G.T(Ts.RightCurlyBracket)), MAP(nameof(Repeat), 1));
            R(G.P(NTs.Rhs, G.T(Ts.LeftRoundBracket), G.NT(NTs.Rhs), G.T(Ts.RightRoundBracket)), MAP(nameof(Group), 1));
            R(G.P(NTs.Rhs, G.NT(NTs.Rhs), G.T(Ts.Pipe), G.NT(NTs.Rhs)), MAP(nameof(Or), 0, 2));
            R(G.P(NTs.Rhs, G.NT(NTs.Rhs), G.T(Ts.Comma), G.NT(NTs.Rhs)), MAP(nameof(And), 0, 2));

            R(G.P(NTs.Rule, G.NT(NTs.Lhs), G.T(Ts.Equals), G.NT(NTs.Rhs), G.T(Ts.Semicolon)), MAP(nameof(Rule), 0, 2));

            R(G.P(NTs.Grammar), nameof(EmptyGrammar));
            R(G.P(NTs.Grammar, G.NT(NTs.Rule), G.NT(NTs.Grammar)), nameof(ExtendGrammar));

            R(G.P(NTs.MetaEntry, G.T(Ts.Dollar), G.NT(NTs.Identifier), G.T(Ts.Equals), G.NT(NTs.Terminal), G.T(Ts.Semicolon)), MAP(nameof(MetaEntry), 1, 3));

            R(G.P(NTs.Meta), nameof(EmptyMeta));
            R(G.P(NTs.Meta, G.NT(NTs.MetaEntry), G.NT(NTs.Meta)), nameof(AddMetaEntry));

            R(G.P(NTs.Config, G.NT(NTs.Meta), G.NT(NTs.Grammar)), nameof(Config));
        }

        private EbnfMeta AddMetaEntry(EbnfMetaEntry entry, EbnfMeta meta)
            => new EbnfMeta(meta.Entries.Add(entry.Key, entry.Value));

        private EbnfAnd And(EbnfExpression left, EbnfExpression right)
            => new EbnfAnd(left, right);

        private EbnfString CharString(Token<Ts> token)
            => new EbnfString(token.Value);

        private EbnfString Concat(EbnfString s1, EbnfString s2)
            => new EbnfString(s1.S + s2.S);

        private EbnfConfig Config(EbnfMeta meta, EbnfGrammar grammar)
            => new EbnfConfig(meta, grammar);

        private EbnfGrammar EmptyGrammar()
            => new EbnfGrammar(ImmutableList.Create<EbnfRule>());

        private EbnfMeta EmptyMeta()
            => new EbnfMeta(ImmutableDictionary.Create<string, string>());

        private EbnfString EmptyString()
            => new EbnfString(string.Empty);

        private EbnfGrammar ExtendGrammar(EbnfRule rule, EbnfGrammar grammar)
            => new EbnfGrammar(grammar.Rules.Insert(0, rule));

        private EbnfGroup Group(EbnfExpression expression)
            => new EbnfGroup(expression);

        private EbnfLiteral Literal(EbnfString first, EbnfString rest)
            => new EbnfLiteral(first.S + rest.S);

        private EbnfMetaEntry MetaEntry(EbnfString key, EbnfLiteral value)
            => new EbnfMetaEntry(key.S, value.Literal);

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

        private EbnfSpecialText SpecialText(EbnfString text)
            => new EbnfSpecialText(text.S);
    }
}
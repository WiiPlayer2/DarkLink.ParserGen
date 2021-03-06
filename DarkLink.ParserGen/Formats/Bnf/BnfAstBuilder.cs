using DarkLink.ParserGen.Parsing;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace DarkLink.ParserGen.Formats.Bnf
{
    internal record BnfNode();

    internal record BnfRule(string Name, BnfExpression Expression) : BnfNode;

    internal record BnfConfig(BnfMeta Meta, BnfSyntax Syntax) : BnfNode;

    internal record BnfMetaEntry(string Config, string Value) : BnfNode;

    internal record BnfMeta(ImmutableDictionary<string, string> Entries) : BnfNode;

    internal record BnfSyntax(ImmutableList<BnfRule> Rules) : BnfNode;

    internal record BnfEmpty() : BnfNode;

    internal record BnfChar(char C) : BnfNode;

    internal record BnfString(string S) : BnfNode;

    internal record BnfExpression(ImmutableList<BnfTerms> TermLists) : BnfNode;

    internal record BnfTerms(ImmutableList<BnfTerm> Terms) : BnfNode;

    internal abstract record BnfTerm() : BnfNode;

    internal record BnfLiteralTerm(string Literal) : BnfTerm;

    internal record BnfRuleTerm(string Rule) : BnfTerm;

    internal record BnfRegexTerm(string Regex) : BnfTerm;

    internal class BnfAstBuilder : AstBuilder<BnfNode, NTs>
    {
        public BnfAstBuilder()
        {
            R(G.P(NTs.Config, G.NT(NTs.Meta), G.NT(NTs.Syntax)), nameof(CreateConfig));

            R(G.P(NTs.Meta, G.NT(NTs.MetaEntry)), nameof(SingleEntryMeta));
            R(G.P(NTs.Meta, G.NT(NTs.MetaEntry), G.NT(NTs.Meta)), nameof(MultipleEntryMeta));
            R(G.P(NTs.MetaEntry, G.T(Ts.Sharp), G.NT(NTs.OptWhitespace), G.NT(NTs.ConfigName), G.NT(NTs.OptWhitespace), G.NT(NTs.ConfigValue), G.NT(NTs.LineEnd)), nameof(CreateMetaEntry));
            R(G.P(NTs.ConfigName, G.NT(NTs.Letter)), nameof(StringFromChar));
            R(G.P(NTs.ConfigName, G.NT(NTs.Letter), G.NT(NTs.ConfigName)), nameof(PrependCharToString));
            R(G.P(NTs.ConfigValue, G.NT(NTs.Character)), nameof(StringFromChar));
            R(G.P(NTs.ConfigValue, G.NT(NTs.Character), G.NT(NTs.ConfigValue)), nameof(PrependCharToString));

            R(G.P(NTs.Syntax, G.NT(NTs.Rule)), nameof(SyntaxFromSingleRule));
            R(G.P(NTs.Syntax, G.NT(NTs.Rule), G.NT(NTs.Syntax)), nameof(SyntaxFromMultipleRules));
            R(G.P(NTs.Rule, G.NT(NTs.OptWhitespace), G.T(Ts.LeftBracket), G.NT(NTs.RuleName), G.T(Ts.RightBracket), G.NT(NTs.OptWhitespace), G.T(Ts.Definition), G.NT(NTs.OptWhitespace), G.NT(NTs.Expression), G.NT(NTs.LineEnd)), nameof(CreateRule));
            R(G.P(NTs.OptWhitespace, G.T(Ts.Space), G.NT(NTs.OptWhitespace)), IGNORE);
            R(G.P(NTs.OptWhitespace), IGNORE);
            R(G.P(NTs.Expression, G.NT(NTs.List)), nameof(SingleListExpression));
            R(G.P(NTs.Expression, G.NT(NTs.List), G.NT(NTs.OptWhitespace), G.T(Ts.Pipe), G.NT(NTs.OptWhitespace), G.NT(NTs.Expression)), nameof(MultipleTermsExpression));
            R(G.P(NTs.LineEnd, G.NT(NTs.OptWhitespace), G.T(Ts.EOL)), IGNORE);
            R(G.P(NTs.LineEnd, G.NT(NTs.LineEnd), G.NT(NTs.LineEnd)), IGNORE);
            R(G.P(NTs.List, G.NT(NTs.Term)), nameof(SingleTermList));
            R(G.P(NTs.List, G.NT(NTs.Term), G.NT(NTs.OptWhitespace), G.NT(NTs.List)), nameof(MultipleTermList));

            R(G.P(NTs.Term, G.NT(NTs.Literal)), nameof(LiteralTerm));
            R(G.P(NTs.Term, G.T(Ts.LeftBracket), G.NT(NTs.RuleName), G.T(Ts.RightBracket)), nameof(RuleTerm));
            R(G.P(NTs.Term, G.T(Ts.Slash), G.NT(NTs.Regex), G.T(Ts.Slash)), MAP(nameof(RegexTerm), 1));

            R(G.P(NTs.Regex, G.NT(NTs.Character)), nameof(StringFromChar));
            R(G.P(NTs.Regex, G.NT(NTs.Character), G.NT(NTs.Regex)), nameof(PrependCharToString));

            R(G.P(NTs.Literal, G.T(Ts.DoubleQuote), G.NT(NTs.Text1), G.T(Ts.DoubleQuote)), SELECT(1));
            R(G.P(NTs.Literal, G.T(Ts.SingleQuote), G.NT(NTs.Text2), G.T(Ts.SingleQuote)), SELECT(1));
            R(G.P(NTs.Text1), nameof(EmptyString));
            R(G.P(NTs.Text1, G.NT(NTs.Character1), G.NT(NTs.Text1)), nameof(PrependCharToString));
            R(G.P(NTs.Text2), nameof(EmptyString));
            R(G.P(NTs.Text2, G.NT(NTs.Character2), G.NT(NTs.Text2)), nameof(PrependCharToString));
            R(G.P(NTs.Character, G.NT(NTs.Letter)), nameof(Pass));
            R(G.P(NTs.Character, G.NT(NTs.Digit)), nameof(Pass));
            R(G.P(NTs.Character, G.NT(NTs.Symbol)), nameof(Pass));

            R(G.P(NTs.Letter, G.T(Ts.Letter)), nameof(CreateChar));
            R(G.P(NTs.Digit, G.T(Ts.Digit)), nameof(CreateChar));
            R(G.P(NTs.Symbol, G.T(Ts.Symbol)), nameof(CreateChar));
            R(G.P(NTs.Symbol, G.T(Ts.LeftBracket)), nameof(CreateChar));
            R(G.P(NTs.Symbol, G.T(Ts.RightBracket)), nameof(CreateChar));
            R(G.P(NTs.Symbol, G.T(Ts.Space)), nameof(CreateChar));
            R(G.P(NTs.Symbol, G.T(Ts.Pipe)), nameof(CreateChar));
            R(G.P(NTs.Symbol, G.T(Ts.Dash)), nameof(CreateChar));
            R(G.P(NTs.Symbol, G.T(Ts.Sharp)), nameof(CreateChar));
            R(G.P(NTs.Symbol, G.T(Ts.Slash)), nameof(CreateChar));

            R(G.P(NTs.Character1, G.NT(NTs.Character)), nameof(Pass));
            R(G.P(NTs.Character1, G.T(Ts.SingleQuote)), nameof(CreateChar));
            R(G.P(NTs.Character2, G.NT(NTs.Character)), nameof(Pass));
            R(G.P(NTs.Character2, G.T(Ts.DoubleQuote)), nameof(CreateChar));
            R(G.P(NTs.RuleName, G.NT(NTs.Letter)), nameof(StringFromChar));
            R(G.P(NTs.RuleName, G.NT(NTs.RuleName), G.NT(NTs.RuleChar)), nameof(AppendCharToString));
            R(G.P(NTs.RuleChar, G.NT(NTs.Letter)), nameof(Pass));
            R(G.P(NTs.RuleChar, G.NT(NTs.Digit)), nameof(Pass));
            R(G.P(NTs.RuleChar, G.T(Ts.Dash)), nameof(CreateChar));
        }

        private BnfString AppendCharToString(BnfString s, BnfChar c)
            => new BnfString(s.S + c.C);

        private BnfChar CreateChar(Token<Ts> charToken)
            => new BnfChar(charToken.Value[0]);

        private BnfConfig CreateConfig(BnfMeta meta, BnfSyntax syntax)
            => new BnfConfig(meta, syntax);

        private BnfMetaEntry CreateMetaEntry(Token<Ts> _0, BnfNode _1, BnfString configName, BnfNode _2, BnfString configValue, BnfNode _3)
            => new BnfMetaEntry(configName.S, configValue.S);

        private BnfRule CreateRule(BnfEmpty _0, Token<Ts> _1, BnfString ruleName, Token<Ts> _2, BnfEmpty _3, Token<Ts> _4, BnfEmpty _5, BnfExpression expression, BnfEmpty _6)
            => new BnfRule(ruleName.S, expression);

        private BnfString EmptyString()
            => new BnfString(string.Empty);

        private BnfLiteralTerm LiteralTerm(BnfString literal)
            => new BnfLiteralTerm(literal.S);

        private BnfMeta MultipleEntryMeta(BnfMetaEntry entry, BnfMeta meta)
            => new BnfMeta(meta.Entries.Add(entry.Config, entry.Value));

        private BnfTerms MultipleTermList(BnfTerm term, BnfEmpty _, BnfTerms terms)
            => new BnfTerms(terms.Terms.Insert(0, term));

        private BnfExpression MultipleTermsExpression(BnfTerms terms, BnfEmpty _0, Token<Ts> _1, BnfEmpty _2, BnfExpression expression)
            => new BnfExpression(expression.TermLists.Insert(0, terms));

        private BnfNode Pass(BnfNode node) => node;

        private BnfString PrependCharToString(BnfChar c, BnfString s)
            => new BnfString(c.C + s.S);

        private BnfRegexTerm RegexTerm(BnfString regex)
            => new BnfRegexTerm(regex.S);

        private BnfRuleTerm RuleTerm(Token<Ts> _0, BnfString ruleName, Token<Ts> _1)
            => new BnfRuleTerm(ruleName.S);

        private BnfMeta SingleEntryMeta(BnfMetaEntry entry)
            => new BnfMeta(ImmutableDictionary.CreateRange(new[] { new KeyValuePair<string, string>(entry.Config, entry.Value) }));

        private BnfExpression SingleListExpression(BnfTerms terms)
            => new BnfExpression(ImmutableList.Create(terms));

        private BnfTerms SingleTermList(BnfTerm term)
            => new BnfTerms(ImmutableList.Create(term));

        private BnfString StringFromChar(BnfChar c)
            => new BnfString(c.C.ToString());

        private BnfSyntax SyntaxFromMultipleRules(BnfRule rule, BnfSyntax syntax)
            => new BnfSyntax(syntax.Rules.Insert(0, rule));

        private BnfSyntax SyntaxFromSingleRule(BnfRule rule)
            => new BnfSyntax(ImmutableList.Create(rule));
    }
}
using DarkLink.ParserGen.Parsing;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace DarkLink.ParserGen.Formats.Bnf
{
    internal static class BnfParser
    {
        private const string START = "START";

        private static readonly Encoding encoding = new UTF8Encoding(false);

        public static Config? Parse(GeneratorExecutionContext context, AdditionalText additionalText)
        {
            var astBuilder = new BnfAstBuilder();
            var grammar = G.Create<NTs, Ts>(new HashSet<Production<NTs>>(astBuilder.Callbacks.Keys), NTs.Syntax);
            var lexerRules = new Dictionary<Ts, Lexer<Ts>.Rule>()
            {
                { Ts.LeftBracket, new Lexer<Ts>.LiteralRule("<") },
                { Ts.RightBracket, new Lexer<Ts>.LiteralRule(">") },
                { Ts.Definition, new Lexer<Ts>.LiteralRule("::=") },
                { Ts.Space, new Lexer<Ts>.LiteralRule(" ") },
                { Ts.Pipe, new Lexer<Ts>.LiteralRule("|") },
                { Ts.DoubleQuote, new Lexer<Ts>.LiteralRule("\"") },
                { Ts.SingleQuote, new Lexer<Ts>.LiteralRule("'") },
                { Ts.Dash, new Lexer<Ts>.LiteralRule("-") },
                { Ts.EOL, new Lexer<Ts>.RegexRule(new Regex("\\r?\\n")) },
                { Ts.Letter, new Lexer<Ts>.RegexRule(new Regex("[A-Za-z]")) },
                { Ts.Digit, new Lexer<Ts>.RegexRule(new Regex("[0-9]")) },
                { Ts.Symbol, new Lexer<Ts>.RegexRule(new Regex(".")) },
            };
            var lexer = new Lexer<Ts>(lexerRules);
            var parser = new Parser<BnfNode, NTs, Ts>(grammar, astBuilder.Callbacks);

            var sourceText = additionalText.GetText(context.CancellationToken);
            if (sourceText is null)
                return null;

            var tokens = lexer.Lex(sourceText.ToString()).ToList();
            var syntax = parser.Parse(tokens).ToList();
            if (syntax.IsEmpty())
                return null;

            var (parsedGrammar, literals) = CreateGrammar((BnfSyntax)syntax.First());
            return CreateConfig(parsedGrammar, literals);
        }

        private static Config CreateConfig(Grammar<string, string> grammar, Dictionary<string, string> literals)
        {
            var typeInfo = new TypeInfo("TestingNS", "TestingC", "internal");
            var lexerInfo = new LexerInfo(literals.Select(CreateTokenInfo).ToList());
            var parserInfo = new ParserInfo(START, null, grammar.Productions.Select(CreateRule).ToList());
            return new Config(typeInfo, lexerInfo, parserInfo);

            TokenInfo CreateTokenInfo(KeyValuePair<string, string> pair)
                => new TokenInfo(pair.Key, new LiteralRule(pair.Value));

            ParserRule CreateRule(Production<string> production)
                => new ParserRule(production.Left.Value, production.Right.Symbols.Select(CreateTarget).ToList());

            ParserRuleTarget CreateTarget(Symbol symbol)
                => symbol switch
                {
                    TerminalSymbol<string> terminal => new ParserRuleTarget(terminal.Value, true),
                    NonTerminalSymbol<string> nonTerminal => new ParserRuleTarget(nonTerminal.Value, false),
                    _ => throw new NotSupportedException(),
                };
        }

        private static (Grammar<string, string>, Dictionary<string, string>) CreateGrammar(BnfSyntax syntax)
        {
            var literals = new Dictionary<string, string>();
            var nonTerminals = new HashSet<NonTerminalSymbol<string>>(syntax.Rules.Select(o => G.NT(o.Name)));
            var productions = new HashSet<Production<string>>(syntax.Rules.SelectMany(CreateProductions));
            var terminals = new HashSet<TerminalSymbol<string>>(productions.SelectMany(o => o.Right.Symbols).OfType<TerminalSymbol<string>>());

            var grammar = new Grammar<string, string>(nonTerminals, terminals, productions, G.NT(START));

            return (grammar, literals);

            IEnumerable<Production<string>> CreateProductions(BnfRule rule)
                => rule.Expression.TermLists.Select(l => CreateProduction(rule, l));

            Production<string> CreateProduction(BnfRule rule, BnfTerms terms)
                => G.P(rule.Name, terms.Terms.Select(CreateSymbol).WhereNotNull().ToArray());

            Symbol? CreateSymbol(BnfTerm term)
            {
                var symbol = term switch
                {
                    BnfLiteralTerm { Literal: "" } => (Symbol?)null,
                    BnfLiteralTerm literalTerm => G.T(GetTerminalName(literalTerm.Literal)),
                    BnfRuleTerm ruleTerm => G.NT(ruleTerm.Rule),
                    _ => throw new NotSupportedException(),
                };
                if (symbol is TerminalSymbol<string> terminalSymbol && term is BnfLiteralTerm lt)
                    literals[terminalSymbol.Value] = lt.Literal;
                return symbol;
            }

            string GetTerminalName(string literal)
                => $"_{string.Concat(literal.Select(GetTerminalChar))}";

            string GetTerminalChar(char c)
            {
                var s = c.ToString();
                if (Regex.IsMatch(s, "[A-Za-z0-9_]"))
                    return s;
                return string.Concat(encoding.GetBytes(s).Select(b => $"_x{b:X2}"));
            }
        }
    }
}
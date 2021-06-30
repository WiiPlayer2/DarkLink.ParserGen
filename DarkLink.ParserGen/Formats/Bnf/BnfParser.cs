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
        private const string NAMESPACE = "namespace";

        private const string START = "start";

        private static readonly Encoding encoding = new UTF8Encoding(false);

        public static Config? Parse(GeneratorExecutionContext context, AdditionalText additionalText, string className)
        {
            var astBuilder = new BnfAstBuilder();
            var grammar = G.Create<NTs, Ts>(new HashSet<Production<NTs>>(astBuilder.Callbacks.Keys), NTs.Config);
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
                { Ts.Sharp, new Lexer<Ts>.LiteralRule("#") },
                { Ts.Symbol, new Lexer<Ts>.RegexRule(new Regex(".")) },
            };
            var lexer = new Lexer<Ts>(lexerRules);
            var parser = new Parser<BnfNode, NTs, Ts>(grammar, astBuilder.Callbacks);

            var sourceText = additionalText.GetText(context.CancellationToken);
            if (sourceText is null)
            {
                context.ReportDiagnostic(Diagnostic.Create(Diagnostics.FailedToOpenFile, Location.Create(additionalText.Path, default, default), additionalText.Path));
                return null;
            }

            var tokens = lexer.Lex(sourceText.ToString()).ToList();
            var syntax = parser.Parse(tokens).ToList();
            if (syntax.IsEmpty())
            {
                context.ReportDiagnostic(Diagnostic.Create(Diagnostics.FailedToParse, Location.Create(additionalText.Path, default, default), additionalText.Path));
                return null;
            }

            var config = (BnfConfig)syntax.First();
            var (parsedGrammar, literals) = CreateGrammar(config);
            return CreateConfig(config.Meta, className, parsedGrammar, literals);
        }

        private static Config CreateConfig(BnfMeta meta, string className, Grammar<string, string> grammar, Dictionary<string, string> literals)
        {
            var @namespace = meta.Entries[NAMESPACE];
            var typeInfo = new TypeInfo(@namespace, className, "internal");
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

        private static (Grammar<string, string>, Dictionary<string, string>) CreateGrammar(BnfConfig config)
        {
            var syntax = config.Syntax;
            var literals = new Dictionary<string, string>();
            var nonTerminals = new HashSet<NonTerminalSymbol<string>>(syntax.Rules.Select(o => G.NT(GetNonTerminalName(o.Name))));
            var productions = new HashSet<Production<string>>(syntax.Rules.SelectMany(CreateProductions));
            var terminals = new HashSet<TerminalSymbol<string>>(productions.SelectMany(o => o.Right.Symbols).OfType<TerminalSymbol<string>>());

            var grammar = new Grammar<string, string>(nonTerminals, terminals, productions, G.NT(config.Meta.Entries[START]));

            return (grammar, literals);

            IEnumerable<Production<string>> CreateProductions(BnfRule rule)
                => rule.Expression.TermLists.Select(l => CreateProduction(rule, l));

            Production<string> CreateProduction(BnfRule rule, BnfTerms terms)
                => G.P(GetNonTerminalName(rule.Name), terms.Terms.Select(CreateSymbol).WhereNotNull().ToArray());

            Symbol? CreateSymbol(BnfTerm term)
            {
                var symbol = term switch
                {
                    BnfLiteralTerm { Literal: "" } => (Symbol?)null,
                    BnfLiteralTerm literalTerm => G.T(GetTerminalName(literalTerm.Literal)),
                    BnfRuleTerm ruleTerm => G.NT(GetNonTerminalName(ruleTerm.Rule)),
                    _ => throw new NotSupportedException(),
                };
                if (symbol is TerminalSymbol<string> terminalSymbol && term is BnfLiteralTerm lt)
                    literals[terminalSymbol.Value] = lt.Literal;
                return symbol;
            }

            string GetNonTerminalName(string name)
                => name.Replace('-', '_');

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
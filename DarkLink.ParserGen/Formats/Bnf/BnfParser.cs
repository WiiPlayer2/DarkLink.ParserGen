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
                { Ts.Slash, new Lexer<Ts>.LiteralRule("/") },
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
            var result = parser.Parse(tokens);
            return result.Match(syntax =>
            {
                if (syntax is null)
                {
                    context.ReportDiagnostic(Diagnostic.Create(Diagnostics.FailedToParse, Location.Create(additionalText.Path, default, default), additionalText.Path));
                    return null;
                }

                var config = (BnfConfig)syntax;
                var (parsedGrammar, literals) = CreateGrammar(config);
                return CreateConfig(config.Meta, className, parsedGrammar, literals);
            },
            errors =>
            {
                foreach (var error in errors)
                {
                    context.ReportDiagnostic(Diagnostic.Create(Diagnostics.SyntaxError, Location.Create(additionalText.Path, default, default), error));
                }
                return null;
            });
        }

        private static Config CreateConfig(BnfMeta meta, string className, Grammar<string, string> grammar, Dictionary<string, TokenRule> literalRules)
        {
            var @namespace = meta.Entries[NAMESPACE];
            var start = meta.Entries[START];

            var typeInfo = new TypeInfo(@namespace, className, "internal");
            var lexerInfo = new LexerInfo(literalRules.Select(CreateTokenInfo).ToList());
            return new Config(typeInfo, lexerInfo, grammar);

            TokenInfo CreateTokenInfo(KeyValuePair<string, TokenRule> pair)
                => new TokenInfo(pair.Key, pair.Value);
        }

        private static (Grammar<string, string>, Dictionary<string, TokenRule>) CreateGrammar(BnfConfig config)
        {
            var syntax = config.Syntax;
            var literalRules = new Dictionary<string, TokenRule>();
            var nonTerminals = new HashSet<NonTerminalSymbol<string>>(syntax.Rules.Select(o => G.NT(GetNonTerminalName(o.Name))));
            var productions = new HashSet<Production<string>>(syntax.Rules.SelectMany(CreateProductions));
            var terminals = new HashSet<TerminalSymbol<string>>(productions.SelectMany(o => o.Right.Symbols).OfType<TerminalSymbol<string>>());

            var grammar = new Grammar<string, string>(nonTerminals, terminals, productions, G.NT(config.Meta.Entries[START]));

            return (grammar, literalRules);

            IEnumerable<Production<string>> CreateProductions(BnfRule rule)
                => rule.Expression.TermLists.Select(l => CreateProduction(rule, l));

            Production<string> CreateProduction(BnfRule rule, BnfTerms terms)
                => G.P(GetNonTerminalName(rule.Name), terms.Terms.Select(CreateSymbol).WhereNotNull().ToArray());

            Symbol? CreateSymbol(BnfTerm term)
            {
                var symbol = term switch
                {
                    BnfLiteralTerm { Literal: "" } => (Symbol?)null,
                    BnfLiteralTerm literalTerm => G.T(GetTerminalName(literalTerm)),
                    BnfRegexTerm regexTerm => G.T(GetTerminalName(regexTerm)),
                    BnfRuleTerm ruleTerm => G.NT(GetNonTerminalName(ruleTerm.Rule)),
                    _ => throw new NotSupportedException(),
                };
                if (symbol is TerminalSymbol<string> terminalSymbol)
                    if (term is BnfLiteralTerm lt)
                        literalRules[terminalSymbol.Value] = new LiteralRule(lt.Literal);
                    else if (term is BnfRegexTerm rt)
                        literalRules[terminalSymbol.Value] = new RegexRule(rt.Regex);
                return symbol;
            }

            string GetNonTerminalName(string name)
                => name.Replace('-', '_');

            string GetTerminalName(BnfTerm term)
                => term switch
                {
                    BnfLiteralTerm literalTerm => $"_{string.Concat(literalTerm.Literal.Select(GetTerminalChar))}",
                    BnfRegexTerm regexTerm => $"R_{string.Concat(regexTerm.Regex.Select(c => Regex.IsMatch(c.ToString(), "[A-Za-z0-9]") ? c : '_'))}_{regexTerm.Regex.Length}",
                    _ => throw new NotSupportedException(),
                };

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
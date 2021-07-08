using DarkLink.ParserGen.Parsing;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace DarkLink.ParserGen.Formats.Ebnf
{
    internal static class EbnfParser
    {
        private const string NAMESPACE = "namespace";

        private const string START = "start";

        private static Encoding encoding = new UTF8Encoding(false);

        internal record UnrolledAnd(List<EbnfExpression> Expressions);

        internal record UnrolledOr(List<EbnfExpression> Expressions);

        public static Config? Parse(GeneratorExecutionContext context, AdditionalText additionalText, string className)
        {
            var astBuilder = new EbnfAstBuilder();
            var grammar = G.Create<NTs, Ts>(new HashSet<Production<NTs>>(astBuilder.Callbacks.Keys), NTs.Config);
            var lexerRules = new Dictionary<Ts, Lexer<Ts>.Rule>()
            {
                { Ts.Sharp, new Lexer<Ts>.LiteralRule("#") },
                { Ts.Letter, new Lexer<Ts>.RegexRule(new Regex("[A-Za-z]")) },
                { Ts.Digit, new Lexer<Ts>.RegexRule(new Regex("[0-9]")) },
                { Ts.Underscore, new Lexer<Ts>.LiteralRule("_") },
                { Ts.DoubleQuote, new Lexer<Ts>.LiteralRule("\"") },
                { Ts.SingleQuote, new Lexer<Ts>.LiteralRule("'") },
                { Ts.LeftSquareBracket, new Lexer<Ts>.LiteralRule("[") },
                { Ts.RightSquareBracket, new Lexer<Ts>.LiteralRule("]") },
                { Ts.LeftCurlyBracket, new Lexer<Ts>.LiteralRule("{") },
                { Ts.RightCurlyBracket, new Lexer<Ts>.LiteralRule("}") },
                { Ts.LeftRoundBracket, new Lexer<Ts>.LiteralRule("(") },
                { Ts.RightRoundBracket, new Lexer<Ts>.LiteralRule(")") },
                { Ts.Pipe, new Lexer<Ts>.LiteralRule("|") },
                { Ts.Comma, new Lexer<Ts>.LiteralRule(",") },
                { Ts.Equals, new Lexer<Ts>.LiteralRule("=") },
                { Ts.Semicolon, new Lexer<Ts>.LiteralRule(";") },
                { Ts.Dollar, new Lexer<Ts>.LiteralRule("$") },
                { Ts.QuestionMark, new Lexer<Ts>.LiteralRule("?") },
                { Ts.Symbol, new Lexer<Ts>.RegexRule(new Regex(@"[<>\.\-/]")) },
            };
            var lexer = new Lexer<Ts>(lexerRules);
            var parser = new Parser<EbnfNode, NTs, Ts>(grammar, astBuilder.Callbacks);

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

            var config = (EbnfConfig)syntax.First();
            config = Cleanup(config);

            var literalRules = new Dictionary<TerminalSymbol<string>, TokenRule>();
            var parsedGrammar = CreateGrammar(config, literalRules);
            return CreateConfig(config.Meta.Entries, className, parsedGrammar, literalRules);
        }

        private static EbnfConfig Cleanup(EbnfConfig config)
        {
            return config with
            {
                Grammar = config.Grammar with
                {
                    Rules = ImmutableList.CreateRange(config.Grammar.Rules.Select(CleanupRule)),
                }
            };

            EbnfRule CleanupRule(EbnfRule rule)
                => rule with { Right = CleanupExpression(rule.Right) };

            EbnfExpression CleanupExpression(EbnfExpression expression)
            {
                switch (expression)
                {
                    case EbnfRuleRef ruleRef:
                        return ruleRef;

                    case EbnfTerminal terminal:
                        return terminal;

                    case EbnfGroup group:
                        return group with { Expression = CleanupExpression(group.Expression) };

                    case EbnfOption option:
                        return option with { Expression = CleanupExpression(option.Expression) };

                    case EbnfRepeat repeat:
                        return repeat with { Expression = CleanupExpression(repeat.Expression) };

                    case EbnfAnd and:
                        {
                            if (and.Left is EbnfAnd other)
                                return CleanupExpression(new EbnfAnd(
                                    CleanupExpression(other.Left),
                                    new EbnfAnd(
                                        CleanupExpression(other.Right),
                                        CleanupExpression(and.Right))));
                            if (and.Left is EbnfOr or)
                                return new EbnfOr(
                                    CleanupExpression(or.Left),
                                    new EbnfAnd(
                                        CleanupExpression(or.Right),
                                        CleanupExpression(and.Right)));
                            return and with
                            {
                                Left = CleanupExpression(and.Left),
                                Right = CleanupExpression(and.Right),
                            };
                        }

                    case EbnfOr or:
                        return or with
                        {
                            Left = CleanupExpression(or.Left),
                            Right = CleanupExpression(or.Right),
                        };

                    default:
                        throw new NotSupportedException();
                }
            }
        }

        private static Config CreateConfig(ImmutableDictionary<string, string> entries, string className, Grammar<string, string> grammar, Dictionary<TerminalSymbol<string>, TokenRule> literalRules)
        {
            var @namespace = entries[NAMESPACE];

            var typeInfo = new TypeInfo(@namespace, className, "internal");
            var lexerInfo = new LexerInfo(literalRules.Select(CreateTokenInfo).ToList());
            return new Config(typeInfo, lexerInfo, grammar);

            TokenInfo CreateTokenInfo(KeyValuePair<TerminalSymbol<string>, TokenRule> pair)
                => new TokenInfo(pair.Key.Value, pair.Value);
        }

        private static Grammar<string, string> CreateGrammar(EbnfConfig config, Dictionary<TerminalSymbol<string>, TokenRule> literalRules)
        {
            var derivedSymbols = new HashSet<NonTerminalSymbol<string>>();

            var productions = config.Grammar.Rules.SelectMany(r => CreateProduction(r, derivedSymbols, literalRules)).ToSet();
            var variables = productions.Select(o => o.Left).ToSet();
            var alphabet = literalRules.Keys.ToSet();
            var start = G.NT(config.Meta.Entries[START]);
            return new(
                variables,
                alphabet,
                productions,
                start);
        }

        private static IEnumerable<Production<string>> CreateProduction(EbnfRule rule, HashSet<NonTerminalSymbol<string>> derivedSymbols, Dictionary<TerminalSymbol<string>, TokenRule> literalRules)
        {
            var left = G.NT(rule.Left);
            switch (rule.Right)
            {
                case EbnfRuleRef ruleRef:
                    return G.P(rule.Left, G.NT(ruleRef.Identifier)).Yield();

                case EbnfTerminal terminal:
                    return G.P(rule.Left, GetSymbol(terminal).Symbol).Yield();

                case EbnfOr or:
                    var unrolledOr = Unroll(or);
                    return unrolledOr.Expressions.SelectMany(e => CreateProduction(new EbnfRule(rule.Left, e), derivedSymbols, literalRules));

                case EbnfAnd and:
                    {
                        var unrolledAnd = Unroll(and);
                        var symbolsAndProductions = unrolledAnd.Expressions.Select(GetSymbol).ToList();
                        var production = G.P(rule.Left, symbolsAndProductions.Select(o => o.Symbol).ToArray());
                        return production.Yield().Concat(symbolsAndProductions.SelectMany(o => o.Productions));
                    }

                case EbnfOption option:
                    {
                        var nullProduction = G.P(rule.Left);
                        return nullProduction.Yield().Concat(CreateProduction(new EbnfRule(rule.Left, option.Expression), derivedSymbols, literalRules));
                    }

                case EbnfRepeat repeat:
                    {
                        var newNonTerminal = GetNewDerivedSymbol(left, derivedSymbols);
                        var nullProduction = G.P(rule.Left);
                        var multiProduction = G.P(rule.Left, newNonTerminal, left);
                        return new[] { nullProduction, multiProduction }.Concat(CreateProduction(new EbnfRule(newNonTerminal.Value, repeat.Expression), derivedSymbols, literalRules));
                    }

                default:
                    throw new NotImplementedException();
            }

            (Symbol Symbol, IEnumerable<Production<string>> Productions) GetSymbol(EbnfExpression expression)
            {
                var empty = Enumerable.Empty<Production<string>>();
                switch (expression)
                {
                    case EbnfRuleRef ruleRef:
                        return (G.NT(ruleRef.Identifier), empty);

                    case EbnfTerminal terminal:
                        {
                            var symbol = G.T(GetTerminalName(terminal));
                            literalRules[symbol] = terminal switch
                            {
                                EbnfLiteral literal => new LiteralRule(literal.Literal),
                                EbnfSpecialText specialText => FromSpecialText(specialText.Text),
                                _ => throw new NotImplementedException(),
                            };
                            return (symbol, empty);
                        }

                    case EbnfGroup group:
                        {
                            var newNonTerminal = GetNewDerivedSymbol(left, derivedSymbols);
                            return (newNonTerminal, CreateProduction(new EbnfRule(newNonTerminal.Value, group.Expression), derivedSymbols, literalRules));
                        }

                    case EbnfOption option:
                        {
                            var newNonTerminal = GetNewDerivedSymbol(left, derivedSymbols);
                            return (newNonTerminal, CreateProduction(new EbnfRule(newNonTerminal.Value, option), derivedSymbols, literalRules));
                        }

                    case EbnfRepeat repeat:
                        {
                            var newNonTerminal = GetNewDerivedSymbol(left, derivedSymbols);
                            return (newNonTerminal, CreateProduction(new EbnfRule(newNonTerminal.Value, repeat), derivedSymbols, literalRules));
                        }

                    default:
                        throw new NotImplementedException();
                }
            }
        }

        private static TokenRule FromSpecialText(string text)
        {
            Match match;
            if ((match = Regex.Match(text, @"^/(?<regex>.*)/$")).Success)
                return new RegexRule(match.Groups["regex"].Value);

            if ((match = Regex.Match(text, @"((?<enc>\w+) )?0x(?<hex>([0-9A-Fa-f]{2})+)")).Success)
            {
                var hex = match.Groups["hex"].Value;
                var enc = GetEncoding();
                var length = hex.Length / 2;
                var bytes = Enumerable.Range(0, length)
                    .Select(i => byte.Parse(hex.Substring(i * 2, 2), System.Globalization.NumberStyles.HexNumber))
                    .ToArray();
                var str = enc.GetString(bytes);
                return new LiteralRule(str);

                Encoding GetEncoding()
                {
                    if (match.Groups["enc"].Success)
                    {
                        var enc = match.Groups["enc"].Value;
                        if (int.TryParse(enc, out var codepage))
                            return Encoding.GetEncoding(codepage);
                        else
                            return Encoding.GetEncoding(enc);
                    }
                    else
                    {
                        return encoding;
                    }
                }
            }

            throw new NotImplementedException();
        }

        private static NonTerminalSymbol<string> GetNewDerivedSymbol(NonTerminalSymbol<string> baseSymbol, HashSet<NonTerminalSymbol<string>> allSymbols)
        {
            var derivedSymbol = baseSymbol;
            do
            {
                derivedSymbol = G.NT($"{derivedSymbol.Value}_");
            }
            while (allSymbols.Contains(derivedSymbol));
            return derivedSymbol;
        }

        private static string GetTerminalChar(char c)
        {
            var s = c.ToString();
            if (Regex.IsMatch(s, "[A-Za-z0-9_]"))
                return s;
            return string.Concat(encoding.GetBytes(s).Select(b => $"_x{b:X2}"));
        }

        private static string GetTerminalName(EbnfTerminal term)
            => term switch
            {
                EbnfLiteral literal => $"_{string.Concat(literal.Literal.Select(GetTerminalChar))}",
                EbnfSpecialText specialText => $"S_{string.Concat(specialText.Text.Where(o => Regex.IsMatch(o.ToString(), "[A-Za-z0-9_]")))}_{specialText.Text.Length}",
                _ => throw new NotSupportedException(),
            };

        private static UnrolledOr Unroll(EbnfOr or)
        {
            var unrolled = new UnrolledOr(new());
            Recurse(or);
            return unrolled;

            void Recurse(EbnfOr or)
            {
                unrolled.Expressions.Add(or.Left);

                if (or.Right is not EbnfOr other)
                {
                    unrolled.Expressions.Add(or.Right);
                    return;
                }

                Recurse(other);
            }
        }

        private static UnrolledAnd Unroll(EbnfAnd and)
        {
            var unrolled = new UnrolledAnd(new());
            Recurse(and);
            return unrolled;

            void Recurse(EbnfAnd and)
            {
                unrolled.Expressions.Add(and.Left);

                if (and.Right is not EbnfAnd other)
                {
                    unrolled.Expressions.Add(and.Right);
                    return;
                }

                Recurse(other);
            }
        }
    }
}
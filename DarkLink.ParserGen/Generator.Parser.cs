using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace DarkLink.ParserGen
{
    partial class Generator
    {
        private List<(string Symbol, string Token, IReadOnlyList<ParserRuleTarget> Targets)> ConstructLLParsingTable(Config config)
        {
            // k=1 for now

            var firsts = new Dictionary<ParserRuleTarget, HashSet<string>>();
            foreach (var token in config.Lexer.Tokens)
                firsts[new(token.Name, true)] = new(new[] { token.Name });
            foreach (var rule in config.Parser.Rules)
            {
                var key = new ParserRuleTarget(rule.Name, false);
                if (!firsts.TryGetValue(key, out var set))
                {
                    set = new();
                    firsts[key] = set;
                }

                if (rule.Targets.Count == 1 && rule.Targets[0] is { IsToken: true, Name: "EMPTY" })
                {
                    set.Add("EMPTY");
                }
            }
            firsts[new("EMPTY", true)] = new(new[] { "EMPTY" });

            bool changed;
            do
            {
                changed = false;

                foreach (var rule in config.Parser.Rules)
                {
                    var key = new ParserRuleTarget(rule.Name, false);
                    for (var i = 0; i < rule.Targets.Count; i++)
                    {
                        var hasEmpty = true;
                        for (var j = 0; j < i; j++)
                        {
                            if (!firsts[rule.Targets[j]].Contains("EMPTY"))
                            {
                                hasEmpty = false;
                                break;
                            }
                        }

                        if (!hasEmpty)
                            continue;

                        foreach (var token in firsts[rule.Targets[i]])
                        {
                            changed |= firsts[key].Add(token);
                        }
                    }

                    if (rule.Targets.All(target => firsts[target].Contains("EMPTY")))
                        changed |= firsts[key].Add("EMPTY");
                }
            }
            while (changed);

            var follows = new Dictionary<string, HashSet<string>>();
            foreach (var rule in config.Parser.Rules)
                follows[rule.Name] = new();

            follows[config.Parser.Start].Add("END");

            do
            {
                changed = false;
                foreach (var rule in config.Parser.Rules)
                {
                    for (var i = 0; i < rule.Targets.Count; i++)
                    {
                        if (rule.Targets[i].IsToken)
                            continue;

                        if (i < rule.Targets.Count - 1)
                        {
                            foreach (var token in firsts[rule.Targets[i + 1]])
                            {
                                if (token == "EMPTY")
                                    continue;
                                changed |= follows[rule.Targets[i].Name].Add(token);
                            }

                            if (firsts[rule.Targets[i + 1]].Contains("EMPTY"))
                            {
                                foreach (var token in follows[rule.Name])
                                    changed |= follows[rule.Targets[i].Name].Add(token);
                            }
                        }
                        else
                        {
                            foreach (var token in follows[rule.Name])
                                changed |= follows[rule.Targets[i].Name].Add(token);
                        }
                    }
                }
            }
            while (changed);

            var map = new List<(string Symbol, string Token, IReadOnlyList<ParserRuleTarget> Targets)>();
            foreach (var rule in config.Parser.Rules)
            {
                foreach (var token in firsts[rule.Targets[0]])
                {
                    if (token == "EMPTY")
                    {
                        foreach (var followToken in follows[rule.Name])
                        {
                            map.Add((rule.Name, followToken, rule.Targets));
                        }
                    }
                    else
                    {
                        map.Add((rule.Name, token, rule.Targets));
                    }
                }
            }

            return map;
        }

        private void GenerateParser(TextWriter writer, Config config)
        {
            var map = ConstructLLParsingTable(config);

            writer.WriteLine($@"
        public class Parser
        {{
            public record Symbol();

            public record RuleSymbol(SymbolType Type) : Symbol;

            public record TokenSymbol(TokenType Type) : Symbol;

            public record Rule(SymbolType Type, IReadOnlyList<Symbol> Symbols);

            class RuleTable
            {{
                private readonly Dictionary<SymbolType, Dictionary<TokenType[], Rule?>> rules = new();

                public Rule? this[SymbolType symbolType, TokenType[] tokens]
                {{
                    get
                    {{
                        if (!rules.ContainsKey(symbolType))
                            return null;
                        var row = rules[symbolType];

                        if (!row.ContainsKey(tokens))
                            return null;
                        return row[tokens];
                    }}
                    set
                    {{
                        rules.TryAdd(symbolType, new(TokensComparer.Instance));
                        rules[symbolType][tokens] = value;
                    }}
                }}
            }}

            class TokensComparer : IEqualityComparer<TokenType[]>
            {{
                public static TokensComparer Instance {{ get; }} = new TokensComparer();

                private TokensComparer() {{ }}

                public bool Equals(TokenType[]? x, TokenType[]? y)
                    => x?.SequenceEqual(y) ?? false;

                public int GetHashCode(TokenType[]? obj)
                    => obj?.Aggregate(0, (acc, curr) => acc ^ curr.GetHashCode()) ?? 0;
            }}

            private readonly RuleTable ruleTable = new();

            public Parser()
            {{");

            foreach (var (symbol, token, targets) in map)
            {
                writer.WriteLine($"ruleTable[SymbolType.{symbol}, new[]{{ TokenType.{token} }}] = new(SymbolType.{symbol}, new Symbol[] {{ {string.Join(", ", targets.Select(o => o.IsToken ? $"new TokenSymbol(TokenType.{o.Name})" : $"new RuleSymbol(SymbolType.{o.Name})"))} }});");
            }

            writer.WriteLine($@"
            }}

            public SymbolNode Parse(string input)
                => Parse(Lexer.Lex(input));

            public SymbolNode Parse(TextReader reader)
                => Parse(Lexer.Lex(reader));

            public SymbolNode Parse(Stream stream)
                => Parse(Lexer.Lex(stream));

            public SymbolNode Parse(IEnumerable<Token> tokens)
            {{
                IEnumerable SyntacticAnalysis()
                {{
                    var endSymbol = new TokenSymbol(TokenType.END);
                    var stack = new Stack<Symbol>();
                    stack.Push(endSymbol);
                    stack.Push(new RuleSymbol(SymbolType.{config.Parser.Start}));

                    var tokenList = tokens
                        .Concat(new []{{ new Token(TokenType.END, string.Empty, -1) }})
                        .ToArray();

                    var position = 0;

                    while (stack.Count > 0)
                    {{
                        var symbol = stack.Pop();
                        var token = tokenList[position];

                        if (symbol is TokenSymbol {{ Type: TokenType.EMPTY }})
                        {{
                            yield return new Token(TokenType.EMPTY, string.Empty, token.Index);
                        }}
                        else if (symbol is TokenSymbol tokenSymbol)
                        {{
                            if (tokenSymbol.Type == token.Type)
                            {{
                                position++;
                                if (tokenSymbol == endSymbol)
                                    yield break;
                                else
                                    yield return token;
                            }}
                            else
                            {{
                                throw new Exception();
                            }}
                        }}
                        else if (symbol is RuleSymbol ruleSymbol)
                        {{
                            var lookAheadTokens = tokenList
                                .Select(o => o.Type)
                                .ToArray()[position..(position + 1)];
                            var rule = ruleTable[ruleSymbol.Type, lookAheadTokens];
                            if (rule is null)
                                throw new Exception();

                            foreach(var s in rule.Symbols.Reverse())
                                stack.Push(s);

                            yield return rule;
                        }}
                    }}
                }}

                var stack = new Stack<(Rule Rule, List<Node> Segments)>();
                var parseStack = new Stack<object>(SyntacticAnalysis().Cast<object>().Reverse());

                do
                {{
                    var obj = parseStack.Pop();
                    if (obj is Token token)
                        obj = new TokenNode(token);

                    Console.WriteLine(obj);

                    if (obj is Rule rule)
                    {{
                        stack.Push((rule, new()));
                    }}
                    else if (obj is Node dataNode)
                    {{
                        var node = stack.Peek();
                        node.Segments.Add(dataNode);
                        if (node.Segments.Count == node.Rule.Symbols.Count)
                        {{
                            stack.Pop();
                            parseStack.Push(new SymbolNode(node.Rule.Type, node.Rule.Symbols.Zip(node.Segments, (l, r) => (l, r)).ToList()));
                        }}
                    }}
                }}
                while (stack.Count > 0);

                var rootNode = (SymbolNode)parseStack.Pop();
                return rootNode;
            }}

            public record Node();

            public record TokenNode(Token Token) : Node;

            public record SymbolNode(SymbolType Type, IReadOnlyList<(Symbol Node, Node Segment)> Segments) : Node;
        }}
");
        }

        private void GenerateSymbolType(TextWriter writer, Config config)
        {
            writer.WriteLine($@"
        public enum SymbolType
        {{");

            foreach (var symbol in config.Parser.Rules
                .Select(o => o.Name)
                .Distinct())
                writer.WriteLine($"{symbol},");

            writer.WriteLine($@"
        }}
");
        }
    }
}
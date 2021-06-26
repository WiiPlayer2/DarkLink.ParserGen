using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DarkLink.ParserGen
{
    partial class Generator
    {
        private void GenerateParser(TextWriter writer, Config config)
        {
            writer.WriteLine($@"
        public class Parser
        {{
            record Symbol();

            record RuleSymbol(SymbolType Type) : Symbol;

            record TokenSymbol(TokenType Type) : Symbol;

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

            record Rule(SymbolType OfType, IReadOnlyList<Symbol> Symbols);

            private readonly RuleTable ruleTable = new();

            public IEnumerable Parse(IEnumerable<Token> tokens)
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

                    if (symbol is TokenSymbol tokenSymbol)
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
        }}
");
        }

        private void GenerateSymbolType(TextWriter writer, Config config)
        {
            writer.WriteLine($@"
        public enum SymbolType
        {{");

            foreach (var rule in config.Parser.Rules)
                writer.WriteLine($"{rule.Name},");

            writer.WriteLine($@"
        }}
");
        }
    }
}
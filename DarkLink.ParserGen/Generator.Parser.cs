using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DarkLink.ParserGen
{
    partial class Generator
    {
        private List<(string Symbol, string[] Tokens, IReadOnlyList<ParserRuleTarget> Targets)> ConstructLLParsingTable(Config config)
        {
            var k = config.Parser.K ?? 1;

            var firsts = new SetOf2<ParserRuleTargets, string>(k);
            var follows = new SetOf2<string, string>(k);
            var wordSet = new SpecialSet<ParserRuleTarget>(k);

            firsts[ParserRuleTargets.Empty].Add(VarArr<string>.Empty);
            foreach (var a in config.Lexer.Tokens)
            {
                var target = new ParserRuleTarget(a.Name, true);
                firsts[new(target)].Add(new(a.Name));
                wordSet.Add(new(target));
            }
            foreach (var rule in config.Parser.Rules)
            {
                wordSet.Add(new(new ParserRuleTarget(rule.Name, false)));
            }

            bool changed;

            do
            {
                changed = false;

                foreach (var rule in config.Parser.Rules)
                {
                    var key = new ParserRuleTargets(new ParserRuleTarget(rule.Name, false));
                    var w = new ParserRuleTargets(rule.Targets).Limit(k);
                    changed |= firsts[key].AddRange(firsts[w]);
                }

                if (k > 1)
                {
                    var words = Enumerable.Range(1, k).Select(i => GetWords(i).ToArray()).ToArray();
                    HandleWords(1, new ParserRuleTargets[k]);

                    void HandleWords(int length, ParserRuleTargets[] currentWords)
                    {
                        var index = length - 1;
                        foreach (var w in words[index])
                        {
                            currentWords[index] = w.Limit(length);

                            if (length < currentWords.Length)
                            {
                                HandleWords(length + 1, currentWords);
                            }
                            else
                            {
                                var key = currentWords.Aggregate(ParserRuleTargets.Empty, (acc, curr) => acc + curr).Limit(length);
                                var setWithEmpty = new SpecialSet<string>(k);
                                setWithEmpty.Add(VarArr<string>.Empty);
                                var combinations = currentWords.Aggregate(setWithEmpty, (acc, curr) => acc + firsts[curr]);
                                changed |= firsts[key].AddRange(combinations);
                            }
                        }
                    }

                    IEnumerable<ParserRuleTargets> GetWords(int k)
                    {
                        for (var i = 1; i <= k; i++)
                        {
                            foreach (var word in wordSet * k)
                            {
                                yield return new ParserRuleTargets(word.Limit(i).Targets);
                            }
                        }
                    }
                }
            }
            while (changed);

            follows[config.Parser.Start].Add(new VarArr<string>(Enumerable.Repeat("END", k)));

            do
            {
                changed = false;

                foreach (var rule in config.Parser.Rules)
                {
                    for (var i = 0; i < rule.Targets.Count; i++)
                    {
                        if (rule.Targets[i].IsToken)
                            continue;

                        var w = new ParserRuleTargets(rule.Targets.ToArray()[(i + 1)..]).Limit(k);
                        changed |= follows[rule.Targets[i].Name].AddRange(firsts[w] + follows[rule.Name]);
                    }
                }
            }
            while (changed);

            var map = new List<(string Symbol, string[] Tokens, IReadOnlyList<ParserRuleTarget> Targets)>();
            foreach (var rule in config.Parser.Rules)
            {
                var w = new ParserRuleTargets(rule.Targets).Limit(k);
                if (firsts[w].Contains(VarArr<string>.Empty))
                {
                    foreach (var tokens in follows[rule.Name])
                    {
                        map.Add((rule.Name, tokens.Targets, rule.Targets));
                    }
                }
                else
                {
                    foreach (var tokens in firsts[w])
                    {
                        var paddedTokens = tokens.Targets;
                        if (paddedTokens.Length < k)
                            paddedTokens = paddedTokens.Concat(Enumerable.Repeat("EMPTY", k - paddedTokens.Length)).ToArray();
                        map.Add((rule.Name, paddedTokens, rule.Targets));
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
                        rules[symbolType].Add(tokens, value);
                    }}
                }}
            }}

            class TokensComparer : IEqualityComparer<TokenType[]>
            {{
                public static TokensComparer Instance {{ get; }} = new TokensComparer();

                private TokensComparer() {{ }}

                public bool Equals(TokenType[]? x, TokenType[]? y)
                {{
                    if (ReferenceEquals(x, y))
                        return true;

                    if(x is null || y is null)
                        return false;

                    var zip = x.Zip(y, (l, r) => (l, r));
                    return zip.All(pair => pair.l == pair.r || pair.l == TokenType.EMPTY || pair.r == TokenType.EMPTY);
                }}

                public int GetHashCode(TokenType[]? obj)
                    => obj is null ? -1 : 0;
            }}

            private static readonly RuleTable ruleTable = new();

            private const int K = {config.Parser.K ?? 1};

            static Parser()
            {{");

            foreach (var (symbol, tokens, targets) in map)
            {
                writer.WriteLine($"ruleTable[SymbolType.{symbol}, new[]{{ {string.Join(", ", tokens.Select(o => $"TokenType.{o}"))} }}] = new(SymbolType.{symbol}, new Symbol[] {{ {string.Join(", ", targets.Select(o => o.IsToken ? $"new TokenSymbol(TokenType.{o.Name})" : $"new RuleSymbol(SymbolType.{o.Name})"))} }});");
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
                        .Concat(Enumerable.Repeat(new Token(TokenType.END, string.Empty, -1), K))
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
                                .ToArray()[position..(position + K)];
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

        public class SetOf<TKey, TValue>
        {
            private readonly Dictionary<TKey, HashSet<TValue>> sets = new();

            public ISet<TValue> this[TKey key]
            {
                get
                {
                    if (!sets.TryGetValue(key, out var set))
                    {
                        set = new();
                        sets.Add(key, set);
                    }

                    return set;
                }
            }
        }

        private class ParserRuleTargets : VarArr<ParserRuleTarget>
        {
            public ParserRuleTargets(params ParserRuleTarget[] targets)
                : base(targets)
            {
            }

            public ParserRuleTargets(IEnumerable<ParserRuleTarget> targets)
                : base(targets) { }

            public new static ParserRuleTargets Empty { get; } = new();

            public static ParserRuleTargets operator +(ParserRuleTargets arr1, ParserRuleTargets arr2)
                            => new((arr1 + (VarArr<ParserRuleTarget>)arr2).Targets);

            public new ParserRuleTargets Limit(int length)
                => new(base.Limit(length).Targets);

            protected override string ItemToString(ParserRuleTarget item)
                => $"{(item.IsToken ? "#" : string.Empty)}{item.Name}";
        }

        private class SetOf2<TKey, TValue>
        {
            private readonly int limit;

            private readonly Dictionary<TKey, SpecialSet<TValue>> sets = new();

            public SetOf2(int limit)
            {
                this.limit = limit;
            }

            public SpecialSet<TValue> this[TKey key]
            {
                get
                {
                    if (!sets.TryGetValue(key, out var set))
                    {
                        set = new(limit);
                        sets.Add(key, set);
                    }

                    return set;
                }
            }
        }

        private class SpecialSet<T> : HashSet<VarArr<T>>
        {
            private readonly int limit;

            public SpecialSet(int limit)
            {
                this.limit = limit;
            }

            public static SpecialSet<T> operator *(SpecialSet<T> set, int power)
            {
                var acc = new SpecialSet<T>(set.limit);
                if (power == 0)
                    return acc;

                acc.Add(VarArr<T>.Empty);
                for (var i = 0; i < power; i++)
                {
                    acc += set;
                }
                return acc;
            }

            public static SpecialSet<T> operator +(SpecialSet<T> set1, SpecialSet<T> set2)
            {
                var result = new SpecialSet<T>(Math.Min(set1.limit, set2.limit));
                foreach (var item1 in set1)
                    foreach (var item2 in set2)
                        result.Add(item1 + item2);
                return result;
            }

            public new bool Add(VarArr<T> item)
                => base.Add(item.Limit(limit));

            public bool AddRange(IEnumerable<VarArr<T>> items)
            {
                var changed = false;
                foreach (var item in items)
                    changed |= Add(item);
                return changed;
            }
        }

        private class VarArr<T>
        {
            public VarArr(params T[] targets)
            {
                Targets = targets;
            }

            public VarArr(IEnumerable<T> targets)
                : this(targets.ToArray()) { }

            public static VarArr<T> Empty { get; } = new();

            public T[] Targets { get; }

            public static VarArr<T> operator +(VarArr<T> arr1, VarArr<T> arr2)
                => new VarArr<T>(arr1.Targets.Concat(arr2.Targets));

            public override bool Equals(object obj)
            {
                if (obj is not VarArr<T> other)
                    return false;
                return Targets.SequenceEqual(other.Targets);
            }

            public override int GetHashCode()
                => Targets.Aggregate(0, (acc, curr) => acc ^ curr?.GetHashCode() ?? 0);

            public VarArr<T> Limit(int length)
                => new VarArr<T>(Targets.Take(length));

            public override string ToString()
                => string.Join(" ", Targets.Select(ItemToString));

            protected virtual string ItemToString(T item)
                => item?.ToString() ?? string.Empty;
        }
    }
}
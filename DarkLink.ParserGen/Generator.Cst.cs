using DarkLink.ParserGen.Parsing;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace DarkLink.ParserGen
{
    partial class Generator
    {
        private enum ProductionType
        {
            Empty,

            Mixed,

            OnlyTerminals,
        }

        private enum SymbolType
        {
            Single,

            OnlyTerminals,

            Multiple,
        }

        private void GenerateCst(TextWriter writer, Config config)
        {
            writer.WriteLine($@"
        public static class Cst
        {{");

            GenerateCstNodes(writer, config);
            GenerateCstBuilder(writer, config);
            GenerateCstParse(writer, config);

            writer.WriteLine($@"
        }}");
        }

        private void GenerateCstBuilder(TextWriter writer, Config config)
        {
            var symbolProductions = GetProductionLookup(config.Grammar.Productions);

            writer.WriteLine($@"
            public class CstBuilder : AstBuilder<Node, NonTerminals>
            {{
                public CstBuilder()
                {{");

            foreach (var production in config.Grammar.Productions)
            {
                var name = GetProductionName(production);
                var productionType = GetProductionType(production);
                var symbolType = GetSymbolType(production.Left, symbolProductions);

                var names = production.Right.Symbols.Select(o => o switch
                {
                    TerminalSymbol<string> ts => ts.Value,
                    NonTerminalSymbol<string> nts => nts.Value,
                    _ => throw new NotSupportedException(),
                });
                var suffix = string.Join("_", names);
                var symbolClassName = symbolType switch
                {
                    SymbolType.Single => $"{production.Left.Value}Node",
                    SymbolType.OnlyTerminals => $"{production.Left.Value}Node",
                    SymbolType.Multiple => $"{production.Left.Value}__{suffix}__Node",
                    _ => throw new NotSupportedException(),
                };
                var args = production.Right.Symbols.Select((s, i) => s switch
                {
                    TerminalSymbol<string> ts => $"(Token<Terminals>)args[{i}]",
                    NonTerminalSymbol<string> nts => $"({nts.Value}Node)args[{i}]",
                    _ => throw new NotSupportedException(),
                });
                var code = productionType switch
                {
                    ProductionType.Empty when (symbolType == SymbolType.OnlyTerminals) => $"new {symbolClassName}(Array.Empty<Token<Terminals>>())",
                    ProductionType.Empty => $"new {symbolClassName}()",
                    ProductionType.OnlyTerminals => $"new {symbolClassName}(args.Cast<Token<Terminals>>().ToList())",
                    ProductionType.Mixed => $"new {symbolClassName}({string.Join(", ", args)})",
                    _ => throw new NotSupportedException(),
                };

                writer.WriteLine($"R(Productions.{name}, args => {code});");
            }

            writer.WriteLine($@"
                }}
            }}");
        }

        private void GenerateCstNodes(TextWriter writer, Config config)
        {
            var symbolProductions = GetProductionLookup(config.Grammar.Productions);

            writer.WriteLine($@"
            /// <summary>Base node class.</summary>
            public record Node();");

            foreach (var symbol in config.Grammar.Variables)
            {
                switch (GetSymbolType(symbol, symbolProductions))
                {
                    case SymbolType.Single:
                        {
                            var production = symbolProductions[symbol].Single();
                            WriteNode(production.Production, production.Type, string.Empty, "Node");
                        }
                        break;

                    case SymbolType.OnlyTerminals:
                        WriteOnlyTerminalsNode(symbol, string.Empty, "Node");
                        break;

                    case SymbolType.Multiple:
                        {
                            var baseClass = $"{symbol.Value}Node";
                            writer.WriteLine($@"
            ///<summary>A node representing {symbol}.</summary>
            public abstract record {baseClass}() : Node;");
                            foreach (var production in symbolProductions[symbol])
                            {
                                var names = production.Production.Right.Symbols.Select(o => o switch
                                {
                                    TerminalSymbol<string> ts => ts.Value,
                                    NonTerminalSymbol<string> nts => nts.Value,
                                    _ => throw new NotSupportedException(),
                                });
                                var suffix = string.Join("_", names);
                                WriteNode(production.Production, production.Type, $"__{suffix}__", baseClass);
                            }
                        }
                        break;
                }

                void WriteNode(Production<string> production, ProductionType type, string suffix, string baseClass)
                {
                    switch (type)
                    {
                        case ProductionType.Empty:
                            WriteEmptyNode(production.Left, suffix, baseClass);
                            break;

                        case ProductionType.OnlyTerminals:
                            WriteOnlyTerminalsNode(production.Left, suffix, baseClass);
                            break;

                        case ProductionType.Mixed:
                            WriteMixedNode(production, suffix, baseClass);
                            break;
                    }
                }

                void WriteEmptyNode(NonTerminalSymbol<string> symbol, string suffix, string baseClass)
                {
                    writer.WriteLine($@"
            ///<summary>A node representing the symbol {symbol}.</summary>
            public record {symbol.Value}{suffix}Node() : {baseClass}
            {{
                public override string ToString() => string.Empty;
            }}");
                }

                void WriteOnlyTerminalsNode(NonTerminalSymbol<string> symbol, string suffix, string baseClass)
                {
                    writer.WriteLine($@"
            ///<summary>A node representing the symbol {symbol}.</summary>
            public record {symbol.Value}{suffix}Node(IReadOnlyList<Token<Terminals>> Tokens) : {baseClass}
            {{
                public override string ToString() => string.Concat(Tokens.Select(o => o.Value));
            }}");
                }

                void WriteMixedNode(Production<string> production, string suffix, string baseClass)
                {
                    var args = production.Right.Symbols.Select((s, i) => s switch
                    {
                        TerminalSymbol<string> ts => $"Token<Terminals> Token{i}",
                        NonTerminalSymbol<string> nts => $"{nts.Value}Node {nts.Value}{i}",
                        _ => throw new NotImplementedException(),
                    });
                    var props = production.Right.Symbols.Select((s, i) => s switch
                    {
                        TerminalSymbol<string> ts => $"Token{i}.Value",
                        NonTerminalSymbol<string> nts => $"{nts.Value}{i}",
                        _ => throw new NotImplementedException(),
                    });
                    writer.WriteLine($@"
            ///<summary>A node representing the production {production}.</summary>
            public record {production.Left.Value}{suffix}Node({string.Join(", ", args)}) : {baseClass}
            {{
                public override string ToString() => $""{string.Concat(props.Select(o => $"{{{o}}}"))}"";
            }}");
                }
            }
        }

        private void GenerateCstParse(TextWriter writer, Config config)
        {
            var startClass = $"{config.Grammar.Start.Value}Node";
            writer.WriteLine($@"
            private static IEnumerable<{startClass}> Parse(Func<Parser<Node>, IEnumerable<Node>> parse)
                => parse(new Parser<Node>(new CstBuilder().Callbacks)).Cast<{startClass}>();

            public static IEnumerable<{startClass}> Parse(string input)
                => Parse(p => p.Parse(input));

            public static IEnumerable<{startClass}> Parse(Stream stream)
                => Parse(p => p.Parse(stream));

            public static IEnumerable<{startClass}> Parse(TextReader reader)
                => Parse(p => p.Parse(reader));
");
        }

        private ILookup<NonTerminalSymbol<string>, (ProductionType Type, Production<string> Production)> GetProductionLookup(IEnumerable<Production<string>> productions)
                    => productions.Select(p => (Type: GetProductionType(p), Production: p)).ToLookup(o => o.Production.Left);

        private ProductionType GetProductionType(Production<string> production)
        {
            if (production.Right.IsEmpty)
                return ProductionType.Empty;

            if (production.Right.Symbols.All(o => o is TerminalSymbol<string>))
                return ProductionType.OnlyTerminals;

            return ProductionType.Mixed;
        }

        private SymbolType GetSymbolType(NonTerminalSymbol<string> symbol, ILookup<NonTerminalSymbol<string>, (ProductionType Type, Production<string> Production)> symbolProductions)
        {
            if (symbolProductions[symbol].Count() == 1)
                return SymbolType.Single;

            if (symbolProductions[symbol].All(o => o.Type == ProductionType.OnlyTerminals || o.Type == ProductionType.Empty))
                return SymbolType.OnlyTerminals;

            return SymbolType.Multiple;
        }
    }
}
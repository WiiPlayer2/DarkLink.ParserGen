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
        public class Cst
        {{");

            GenerateCstNodes(writer, config);

            writer.WriteLine($@"
        }}");
        }

        private void GenerateCstNodes(TextWriter writer, Config config)
        {
            var symbolProductions = config.Grammar.Productions.Select(p => (Type: GetProductionType(p), Production: p)).ToLookup(o => o.Production.Left);

            writer.WriteLine($@"
            public record Node();");

            foreach (var symbol in config.Grammar.Variables)
            {
                switch (GetSymbolType(symbol))
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
                            writer.WriteLine($"public abstract record {baseClass}() : Node;");
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

                SymbolType GetSymbolType(NonTerminalSymbol<string> symbol)
                {
                    if (symbolProductions[symbol].Count() == 1)
                        return SymbolType.Single;

                    if (symbolProductions[symbol].All(o => o.Type == ProductionType.OnlyTerminals || o.Type == ProductionType.Empty))
                        return SymbolType.OnlyTerminals;

                    return SymbolType.Multiple;
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
                    writer.WriteLine($"public record {symbol.Value}{suffix}Node() : {baseClass};");
                }

                void WriteOnlyTerminalsNode(NonTerminalSymbol<string> symbol, string suffix, string baseClass)
                {
                    writer.WriteLine($"public record {symbol.Value}{suffix}Node(IReadOnlyList<Token<Terminals>> Tokens) : {baseClass};");
                }

                void WriteMixedNode(Production<string> production, string suffix, string baseClass)
                {
                    var args = production.Right.Symbols.Select((s, i) => s switch
                    {
                        TerminalSymbol<string> ts => $"Token<Terminals> token{i}",
                        NonTerminalSymbol<string> nts => $"{nts.Value}Node {nts.Value.Decapitalize()}{i}",
                        _ => throw new NotImplementedException(),
                    });
                    writer.WriteLine($"public record {production.Left.Value}{suffix}Node({string.Join(", ", args)}) : {baseClass};");
                }
            }

            ProductionType GetProductionType(Production<string> production)
            {
                if (production.Right.IsEmpty)
                    return ProductionType.Empty;

                if (production.Right.Symbols.All(o => o is TerminalSymbol<string>))
                    return ProductionType.OnlyTerminals;

                return ProductionType.Mixed;
            }
        }
    }
}
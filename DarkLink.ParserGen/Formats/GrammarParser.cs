using DarkLink.ParserGen.Parsing;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DarkLink.ParserGen.Formats
{
    internal abstract class GrammarParser<TNT, TT, TNode, TRootNode, TAstBuilder> : IParser
        where TNT : Enum
        where TT : Enum
        where TRootNode : TNode
        where TAstBuilder : AstBuilder<TNode, TNT>, new()
    {
        private readonly Lexer<TT> lexer;

        private readonly Parser<TNode, TNT, TT> parser;

        protected GrammarParser(TNT startSymbol)
        {
            var astBuilder = new TAstBuilder();
            var grammar = G.Create<TNT, TT>(astBuilder.Callbacks.Keys.ToSet(), startSymbol);
            lexer = CreateLexer();
            parser = new Parser<TNode, TNT, TT>(grammar, astBuilder.Callbacks);
        }

        public Config? Parse(GeneratorExecutionContext context, AdditionalText additionalText, string className)
        {
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

                var rootNode = (TRootNode)syntax;
                return CreateConfig(rootNode, className);
            }, errors =>
            {
                foreach (var error in errors)
                {
                    context.ReportDiagnostic(Diagnostic.Create(Diagnostics.SyntaxError, Location.Create(additionalText.Path, default, default), error));
                }
                return null;
            });
        }

        protected abstract Config? CreateConfig(TRootNode rootNode, string className);

        protected abstract Lexer<TT> CreateLexer();
    }
}
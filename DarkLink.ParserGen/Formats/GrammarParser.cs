using DarkLink.ParserGen.Parsing;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
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

            var text = sourceText.ToString();
            var tokens = lexer.Lex(text, context.CancellationToken).ToList();
            var result = parser.Parse(tokens, context.CancellationToken);
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
                    var linePositionSpan = GetLinePositionSpan(text, error.Got);
                    var location = Location.Create(additionalText.Path, default, linePositionSpan);
                    context.ReportDiagnostic(Diagnostic.Create(Diagnostics.SyntaxError, location, error.Expected, error.Got?.Value, error.Got?.Symbol));
                }
                return null;
            });
        }

        protected abstract Config? CreateConfig(TRootNode rootNode, string className);

        protected abstract Lexer<TT> CreateLexer();

        private static LinePosition GetLinePosition(string text, int index)
        {
            var line = 0;
            var character = 0;
            for (var i = 0; i < text.Length; i++)
            {
                if (i == index)
                    return new(line, character);

                character++;
                if (text[i] == '\n')
                {
                    line++;
                    character = 0;
                }
            }

            return new(line, character);
        }

        private static LinePositionSpan GetLinePositionSpan(string text, Token<TT>? token)
        {
            if (token is null)
            {
                var startAndEnd = GetLinePosition(text, text.Length);
                return new(startAndEnd, startAndEnd);
            }

            var start = GetLinePosition(text, token.Index);
            var end = GetLinePosition(text, token.Index + token.Value.Length);
            return new(start, end);
        }
    }
}
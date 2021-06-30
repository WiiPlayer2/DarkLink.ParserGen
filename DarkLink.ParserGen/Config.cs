using DarkLink.ParserGen.Parsing;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Text;

namespace DarkLink.ParserGen
{
    record Config(TypeInfo Type, LexerInfo Lexer, Grammar<string, string> Grammar);

    record TypeInfo(string Namespace, string Name, string Modifier);

    record LexerInfo(IReadOnlyList<TokenInfo> Tokens);

    record TokenInfo(string Name, TokenRule Rule);

    record TokenRule();

    record RegexRule(string Regex) : TokenRule;

    record LiteralRule(string Literal) : TokenRule;
}

namespace System.Runtime.CompilerServices
{
    internal class IsExternalInit
    { }
}
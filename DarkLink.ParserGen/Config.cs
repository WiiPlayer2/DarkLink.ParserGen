using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Text;

namespace DarkLink.ParserGen
{
    record Config(TypeInfo Type, LexerInfo Lexer, ParserInfo Parser);

    record TypeInfo(string Namespace, string Name, string Modifier);

    record LexerInfo(IReadOnlyList<TokenInfo> Tokens);

    record TokenInfo(string Name, TokenRule Rule);

    record TokenRule();

    record RegexRule(string Regex) : TokenRule;

    record LiteralRule(string Literal) : TokenRule;

    record ParserInfo(string Start, IReadOnlyList<ParserRule> Rules);

    record ParserRuleTarget(string Name, bool IsToken);

    record ParserRule(string Name, IReadOnlyList<ParserRuleTarget> Targets);
}

namespace System.Runtime.CompilerServices
{
    internal class IsExternalInit
    { }
}
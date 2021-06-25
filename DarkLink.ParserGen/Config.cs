using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Text;

namespace DarkLink.ParserGen
{
    record Config(TypeInfo Type, LexerInfo Lexer);

    record TypeInfo(string Namespace, string Name, string Modifier);

    record LexerInfo(IReadOnlyList<TokenInfo> Tokens);

    record TokenInfo(string Name, string Regex);
}

namespace System.Runtime.CompilerServices
{
    internal class IsExternalInit
    { }
}
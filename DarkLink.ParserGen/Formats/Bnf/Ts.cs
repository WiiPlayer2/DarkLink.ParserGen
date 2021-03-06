using DarkLink.ParserGen.Parsing;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace DarkLink.ParserGen.Formats.Bnf
{
    internal enum Ts
    {
        LeftBracket,

        RightBracket,

        Definition,

        Space,

        Pipe,

        Slash,

        DoubleQuote,

        SingleQuote,

        EOL,

        Letter,

        Digit,

        Dash,

        Sharp,

        Symbol,
    }
}
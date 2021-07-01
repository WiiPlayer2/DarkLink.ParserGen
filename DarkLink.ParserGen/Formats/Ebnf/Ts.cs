using System;
using System.Collections.Generic;
using System.Text;

namespace DarkLink.ParserGen.Formats.Ebnf
{
    internal enum Ts
    {
        Sharp,

        Letter,

        Digit,

        Symbol,

        Underscore,

        SingleQuote,

        DoubleQuote,

        LeftSquareBracket,

        RightSquareBracket,

        LeftCurlyBracket,

        RightCurlyBracket,

        LeftRoundBracket,

        RightRoundBracket,

        Pipe,

        Comma,

        Equals,

        Semicolon,

        Dollar,
    }
}
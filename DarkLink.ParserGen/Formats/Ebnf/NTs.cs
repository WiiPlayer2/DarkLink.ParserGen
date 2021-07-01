using System;
using System.Collections.Generic;
using System.Text;

namespace DarkLink.ParserGen.Formats.Ebnf
{
    internal enum NTs
    {
        Letter,

        Digit,

        Symbol,

        Character,

        Identifier,

        IdentifierCont,

        Terminal,

        LiteralCont,

        Lhs,

        Rhs,

        Rule,

        Grammar,

        Config,
        Meta,
        MetaEntry,
        Literal,
    }
}
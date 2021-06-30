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
    internal enum NTs
    {
        Config,

        Meta,

        MetaEntry,

        Syntax,

        Rule,

        OptWhitespace,

        Expression,

        LineEnd,

        List,

        Term,

        Literal,

        Regex,

        Text1,

        Text2,

        Character,

        Letter,

        Digit,

        Symbol,

        Character1,

        Character2,

        RuleName,

        RuleChar,

        ConfigName,

        ConfigValue,
    }
}
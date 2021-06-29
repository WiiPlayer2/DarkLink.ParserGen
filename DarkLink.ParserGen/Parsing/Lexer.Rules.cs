using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace DarkLink.ParserGen.Parsing
{
    partial class Lexer<TT>
    {
        public record RuleMatch(bool Success, int Index, string Value)
        {
            public int Length => Value.Length;
        }

        public class LiteralRule : Rule
        {
            private readonly string literal;

            public LiteralRule(string literal)
            {
                this.literal = literal;
            }

            public override RuleMatch Match(string input, int startAt)
            {
                var index = input.IndexOf(literal, startAt);

                if (index == -1)
                    return new(false, index, string.Empty);
                else
                    return new(true, index, literal);
            }
        }

        public class RegexRule : Rule
        {
            private readonly Regex regex;

            public RegexRule(Regex regex)
            {
                this.regex = regex;
            }

            public override RuleMatch Match(string input, int startAt)
            {
                var match = regex.Match(input, startAt);
                return new(match.Success, match.Index, match.Value);
            }
        }

        public abstract class Rule
        {
            public abstract RuleMatch Match(string input, int startAt);
        }
    }
}
using Markdig.Helpers;
using Markdig.Parsers;
using Markdig.Syntax.Inlines;
using System;
using System.Collections.Generic;
using System.Text;

namespace ScriptureRenderingPipeline.Helpers.MarkdigExtensions
{
    public class RCLinksParser : InlineParser
    {
        private static readonly char[] _openingCharacters = { '[' };
        public RCLinksParser()
        {
            this.OpeningCharacters = _openingCharacters;
        }
        public override bool Match(InlineProcessor processor, ref StringSlice slice)
        {
            var current = slice.CurrentChar;

            if(current != '[')
            {
                return false;
            }

            if (slice.PeekChar() != '[')
            {
                return false;
            }

            // Increment by two to skip the [[
            slice.NextChar();
            slice.NextChar();

            var startLink = slice.Start;
            var endLink = slice.Start;
            while(current != ']')
            {
                endLink = slice.Start;
                current = slice.NextChar();
            }

            // get rid of the ]] at the end
            slice.NextChar();
            slice.NextChar();

            var link = new StringSlice(slice.Text, startLink, endLink);
            processor.Inline = new RCLink() { Link = link };
            return true;
        }
    }
}
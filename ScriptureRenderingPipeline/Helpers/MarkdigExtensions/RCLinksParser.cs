using Markdig.Helpers;
using Markdig.Parsers;
using System;
using System.Collections.Generic;
using System.Text;

namespace ScriptureRenderingPipeline.Helpers.MarkdigExtensions
{
    public class RCLinksParser : InlineParser
    {
        public override bool Match(InlineProcessor processor, ref StringSlice slice)
        {
            var previousChar = slice.PeekCharExtra(-1);
            if (!previousChar.IsWhiteSpaceOrZero())
            {
                return false;
            }
            var current = slice.CurrentChar;

            if(current != '[' && slice.PeekCharExtra(1) != '[')
            {
                return false;
            }
            if (slice.PeekChar() != '[')
            {
                return false;
            }

            var startLink = slice.Start;
            var endLink = slice.Start;
            while(current != ']')
            {
                endLink = slice.Start;
                current = slice.NextChar();
            }

            var link = new StringSlice(slice.Text, startLink, endLink);
            return true;
        }
    }
}

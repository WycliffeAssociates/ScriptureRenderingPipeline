using DotLiquid;
using System;
using System.Collections.Generic;
using System.Text;

namespace ScriptureRenderingPipeline.Models
{
    public class NavigationChapter: ILiquidizable
    {
        public string title { get; set; }
        public string id { get; set; }

        public object ToLiquid()
        {
            return new
            {
                title,
                id,
            };
        }
    }
}

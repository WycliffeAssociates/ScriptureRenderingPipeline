﻿using DotLiquid;

namespace ScriptureRenderingPipelineWorker.Models
{
    public class NavigationBook: ILiquidizable
    {
        public string title { get; set; }
        public string abbreviation { get; set; }
        public string file { get; set; }
        public List<NavigationChapter> chapters { get; set; }

        public NavigationBook()
        {
            chapters = new List<NavigationChapter>();
        }

        public object ToLiquid()
        {
            return new
            {
                title,
                abbreviation,
                chapters,
                file
            };
        }
    }
}

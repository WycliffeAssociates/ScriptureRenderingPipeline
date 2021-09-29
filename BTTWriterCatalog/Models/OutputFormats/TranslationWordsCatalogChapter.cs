﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace BTTWriterCatalog.Models.OutputFormats
{
    public class TranslationWordsCatalogChapter
    {
        [JsonProperty("frames")]
        public List<TranslationWordCatalogFrame> Frames { get; set; }
        [JsonProperty("id")]
        public string Id { get; set; }
        public TranslationWordsCatalogChapter(string id)
        {
            Id = id;
            Frames = new List<TranslationWordCatalogFrame>();
        }
        public TranslationWordsCatalogChapter()
        {
            Frames = new List<TranslationWordCatalogFrame>();
        }
    }
}
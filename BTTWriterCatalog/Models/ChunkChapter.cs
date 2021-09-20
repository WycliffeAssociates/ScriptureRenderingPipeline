using System;
using System.Collections.Generic;
using System.Text;

namespace BTTWriterCatalog.Models
{
    public class ChunkChapter
    {
        public int Number { get;set;  }
        public List<VerseChunk> Chunks {  get; set; }
        public ChunkChapter(int number)
        {
            Number = number;
            Chunks = new List<VerseChunk>();
        }
        public ChunkChapter()
        {
            Chunks = new List<VerseChunk>();
        }
    }
}

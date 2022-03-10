using NUnit.Framework;
using System;
using System.Collections.Generic;
using BTTWriterCatalog.Models;
using System.Text;
using BTTWriterCatalog.Helpers;

namespace SRPTests
{
    internal class ConvertHelpersTests
    {
        [Test]
        public void TestD43ChunkConversion()
        {
            var chunks = new List<Door43Chunk>()
            {
                new Door43Chunk() { Chapter = "1", FirstVerse = "01"},
                new Door43Chunk() { Chapter = "1", FirstVerse = "04"}
            };
            var convertedChunks = ConversionUtils.ConvertChunks(chunks);
            Assert.AreEqual(1, convertedChunks.Count);
            Assert.AreEqual(1, convertedChunks[1][0].StartingVerse);
            Assert.AreEqual(3, convertedChunks[1][0].EndingVerse);
        }
    }
}

using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using BTTWriterCatalog.Models;

namespace BTTWriterCatalog.Helpers
{
    /// <summary>
    /// Reads and writes a VerseChunk as a compact [startingVerse, endingVerse] pair
    /// </summary>
    /// <remarks>
    /// This keeps a repository's chunks.json small since the property names would otherwise
    /// be several times larger than the two numbers they describe. Register this via
    /// JsonSerializerOptions rather than an attribute on VerseChunk so that the chunk
    /// definitions in storage, which use the full property names, still deserialize.
    /// </remarks>
    public class VerseChunkArrayConverter : JsonConverter<VerseChunk>
    {
        public override bool HandleNull => true;

        public override VerseChunk Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartArray)
            {
                throw new JsonException("Expected a [startingVerse, endingVerse] array for a verse chunk");
            }
            var startingVerse = ReadVerseNumber(ref reader, "starting");
            var endingVerse = ReadVerseNumber(ref reader, "ending");
            reader.Read();
            if (reader.TokenType != JsonTokenType.EndArray)
            {
                throw new JsonException("A verse chunk must contain exactly two numbers");
            }
            return new VerseChunk(startingVerse, endingVerse);
        }

        /// <summary>
        /// Read a single verse number out of a chunk's array
        /// </summary>
        /// <param name="reader">The reader, positioned just before the number</param>
        /// <param name="which">Which of the two verse numbers this is, used for the error message</param>
        /// <returns>The verse number</returns>
        private static int ReadVerseNumber(ref Utf8JsonReader reader, string which)
        {
            reader.Read();
            if (reader.TokenType != JsonTokenType.Number)
            {
                throw new JsonException($"The {which} verse of a chunk must be a number");
            }
            var verse = reader.GetInt32();
            if (verse < 0)
            {
                throw new JsonException($"The {which} verse of a chunk cannot be negative but was {verse}");
            }
            return verse;
        }

        public override void Write(Utf8JsonWriter writer, VerseChunk value, JsonSerializerOptions options)
        {
            writer.WriteStartArray();
            writer.WriteNumberValue(value.StartingVerse);
            writer.WriteNumberValue(value.EndingVerse);
            writer.WriteEndArray();
        }
    }
}

using Soundpacks_Framework.Serialization.Json;

namespace Soundpacks_Framework.Tests
{
    public static class JsonTests
    {
        public static void Register(TestRunner runner)
        {
            runner.Add("Json.RoundTrip.Primitives", RoundTripPrimitives);
            runner.Add("Json.RoundTrip.NestedObjectsAndArrays", RoundTripNested);
            runner.Add("Json.Canonical.SortsKeys", CanonicalSortsKeys);
            runner.Add("Json.Canonical.IsIdempotent", CanonicalIsIdempotent);
            runner.Add("Json.Parser.RejectsTrailingContent", RejectsTrailingContent);
            runner.Add("Json.Parser.RejectsTrailingComma", RejectsTrailingComma);
            runner.Add("Json.Parser.HandlesEscapes", HandlesEscapes);
            runner.Add("Json.Parser.RejectsUnterminatedString", RejectsUnterminatedString);
        }

        private static void RoundTripPrimitives()
        {
            var obj = JsonValue.NewObject();
            obj.Set("s", JsonValue.Of("hello"));
            obj.Set("n", JsonValue.Of(42));
            obj.Set("f", JsonValue.Of(1.5));
            obj.Set("b", JsonValue.Of(true));
            obj.Set("nil", JsonValue.Null);

            string text = JsonWriter.Write(obj);
            var reparsed = JsonParser.Parse(text);

            Assert.Equal("hello", reparsed.Get("s").AsString(), "string field");
            Assert.Equal(42, reparsed.Get("n").AsInt(), "int field");
            Assert.Equal(1.5, reparsed.Get("f").AsNumber(), "float field");
            Assert.True(reparsed.Get("b").AsBool(), "bool field");
            Assert.True(reparsed.Get("nil").IsNull, "null field");
        }

        private static void RoundTripNested()
        {
            const string text = "{\"mappings\":[{\"files\":[{\"path\":\"Audio/a.mp3\"},{\"path\":\"Audio/b.mp3\"}]}]}";
            var value = JsonParser.Parse(text);
            var files = value.Get("mappings").ArrayItems[0].Get("files");
            Assert.Equal(2, files.ArrayItems.Count, "file count");
            Assert.Equal("Audio/a.mp3", files.ArrayItems[0].Get("path").AsString(), "first file path");
        }

        private static void CanonicalSortsKeys()
        {
            var obj = JsonValue.NewObject();
            obj.Set("zeta", JsonValue.Of(1));
            obj.Set("alpha", JsonValue.Of(2));
            string text = JsonWriter.Write(obj, canonical: true, indent: false);
            Assert.True(text.IndexOf("alpha") < text.IndexOf("zeta"), "alpha should sort before zeta: " + text);
        }

        private static void CanonicalIsIdempotent()
        {
            var obj = JsonValue.NewObject();
            obj.Set("b", JsonValue.Of("2"));
            obj.Set("a", JsonValue.Of("1"));
            string first = JsonWriter.Write(obj, canonical: true, indent: true);
            var reparsed = JsonParser.Parse(first);
            string second = JsonWriter.Write(reparsed, canonical: true, indent: true);
            Assert.Equal(first, second, "canonical output must be stable across a reparse");
        }

        private static void RejectsTrailingContent()
        {
            Assert.Throws(() => JsonParser.Parse("{}garbage"), "trailing content after root value");
        }

        private static void RejectsTrailingComma()
        {
            Assert.Throws(() => JsonParser.Parse("{\"a\":1,}"), "trailing comma in object");
            Assert.Throws(() => JsonParser.Parse("[1,]"), "trailing comma in array");
        }

        private static void HandlesEscapes()
        {
            var value = JsonParser.Parse("\"line1\\nline2\\t\\u0041\"");
            Assert.Equal("line1\nline2\tA", value.AsString(), "escape sequences");
        }

        private static void RejectsUnterminatedString()
        {
            Assert.Throws(() => JsonParser.Parse("\"unterminated"), "unterminated string literal");
        }
    }
}

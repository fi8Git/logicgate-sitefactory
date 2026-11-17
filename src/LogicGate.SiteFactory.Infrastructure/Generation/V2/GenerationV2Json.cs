using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LogicGate.SiteFactory.Application.Generation;

namespace LogicGate.SiteFactory.Infrastructure.Generation.V2;

/// <summary>Memory-only canonical controls. This policy is deliberately separate from the frozen V1 codecs.</summary>
internal static class GenerationV2Json
{
    internal const int MaximumStateLength = 256 * 1024;
    internal const int MaximumLockLength = 64 * 1024;
    internal const int MaximumDepth = 16;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    internal static void RequireText(string value, int maximumBytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        try
        {
            if (value.Any(char.IsControl) || StrictUtf8.GetByteCount(value) > maximumBytes)
                throw new ArgumentException("V2 metadata must be bounded text without control characters.");
        }
        catch (EncoderFallbackException)
        {
            throw new ArgumentException("V2 metadata must contain valid Unicode.");
        }
    }

    internal static byte[] Write(int maximumLength, Action<Utf8JsonWriter> write)
    {
        using LimitedStream stream = new(maximumLength);
        using (Utf8JsonWriter writer = new(stream, new JsonWriterOptions
        {
            Indented = true,
            IndentCharacter = ' ',
            IndentSize = 2,
            NewLine = "\n",
        }))
        {
            write(writer);
            writer.Flush();
        }
        stream.WriteByte((byte)'\n');
        return stream.ToArray();
    }

    internal static JsonDocument? Parse(byte[] bytes, int maximumLength)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        if (bytes.Length == 0 || bytes.Length > maximumLength || bytes.AsSpan().StartsWith("\uFEFF"u8)) return null;
        try
        {
            _ = StrictUtf8.GetCharCount(bytes);
            return JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = MaximumDepth });
        }
        catch (Exception exception) when (exception is JsonException or DecoderFallbackException)
        {
            return null;
        }
    }

    internal static void RequireProperties(JsonElement element, string[] names)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.EnumerateObject().Select(static property => property.Name).SequenceEqual(names, StringComparer.Ordinal))
            throw new FormatException("Invalid V2 object shape.");
    }

    internal static string Text(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out JsonElement property) || property.ValueKind != JsonValueKind.String)
            throw new FormatException("Invalid V2 text field.");
        return property.GetString()!;
    }

    internal static void RequireVersion(JsonElement element, string name, int expected)
    {
        if (!element.TryGetProperty(name, out JsonElement property) || !property.TryGetInt32(out int actual) || actual != expected)
            throw new FormatException("Unsupported V2 control version.");
    }

    internal static GeneratedContentHash Hash(JsonElement element, string name)
    {
        string value = Text(element, name);
        if (value.Length != 64 || value.Any(static character => character is not (>= '0' and <= '9') and not (>= 'A' and <= 'F')))
            throw new FormatException("Invalid canonical V2 SHA-256.");
        return new(value);
    }

    internal static GeneratedContentHash ComputeHash(ReadOnlySpan<byte> bytes) => new(Convert.ToHexString(SHA256.HashData(bytes)));

    // Enforce the actual encoded byte limit, including the final LF, before returning any document.
    private sealed class LimitedStream(int maximumLength) : MemoryStream
    {
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            Check(buffer.Length);
            base.Write(buffer);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            Check(count);
            base.Write(buffer, offset, count);
        }

        public override void WriteByte(byte value)
        {
            Check(1);
            base.WriteByte(value);
        }

        private void Check(int count)
        {
            if (Position + count > maximumLength)
                throw new ArgumentException("V2 control document exceeds its byte limit.");
        }
    }
}

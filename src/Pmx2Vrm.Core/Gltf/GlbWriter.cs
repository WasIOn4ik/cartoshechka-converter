using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Pmx2Vrm.Core.Gltf;

/// <summary>
/// Serialises a <see cref="GltfRoot"/> plus its binary buffer into a binary glTF
/// (.glb / .vrm) container.
/// </summary>
public static class GlbWriter
{
    private const uint Magic = 0x46546C67;   // "glTF"
    private const uint JsonChunk = 0x4E4F534A; // "JSON"
    private const uint BinChunk = 0x004E4942;  // "BIN\0"

    public static readonly JsonSerializerOptions JsonOptions = CreateOptions();

    public static byte[] Write(GltfRoot root, byte[] binary)
    {
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(root, JsonOptions);
        json = Pad(json, 0x20);              // JSON chunk padded with spaces
        byte[] bin = Pad(binary, 0x00);      // BIN chunk padded with zeros

        int total = 12 + 8 + json.Length + 8 + bin.Length;
        using var ms = new MemoryStream(total);
        using var w = new BinaryWriter(ms);

        w.Write(Magic);
        w.Write((uint)2);
        w.Write((uint)total);

        w.Write((uint)json.Length);
        w.Write(JsonChunk);
        w.Write(json);

        w.Write((uint)bin.Length);
        w.Write(BinChunk);
        w.Write(bin);

        w.Flush();
        return ms.ToArray();
    }

    public static void WriteToFile(GltfRoot root, byte[] binary, string path) =>
        File.WriteAllBytes(path, Write(root, binary));

    private static byte[] Pad(byte[] data, byte padByte)
    {
        int rem = data.Length % 4;
        if (rem == 0) return data;
        var padded = new byte[data.Length + (4 - rem)];
        Array.Copy(data, padded, data.Length);
        for (int i = data.Length; i < padded.Length; i++) padded[i] = padByte;
        return padded;
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var opts = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };
        // Omit empty arrays/dictionaries — glTF rejects empty array properties.
        opts.TypeInfoResolver = new DefaultJsonTypeInfoResolver
        {
            Modifiers = { DropEmptyCollections },
        };
        return opts;
    }

    private static void DropEmptyCollections(JsonTypeInfo info)
    {
        foreach (var prop in info.Properties)
        {
            var t = prop.PropertyType;
            if (t == typeof(string)) continue;
            if (!typeof(System.Collections.IEnumerable).IsAssignableFrom(t)) continue;

            var inner = prop.ShouldSerialize;
            prop.ShouldSerialize = (obj, value) =>
            {
                if (inner is not null && !inner(obj, value)) return false;
                if (value is null) return false;
                if (value is System.Collections.ICollection c) return c.Count > 0;
                if (value is System.Collections.IEnumerable e) return e.GetEnumerator().MoveNext();
                return true;
            };
        }
    }
}

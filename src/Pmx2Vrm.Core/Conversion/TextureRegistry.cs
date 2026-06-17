using Pmx2Vrm.Core.Gltf;
using SixLabors.ImageSharp;

namespace Pmx2Vrm.Core.Conversion;

/// <summary>
/// Resolves PMX texture indices to glTF texture indices, building image,
/// sampler and texture entries on demand. Images are ALWAYS embedded into the
/// binary buffer: PNG/JPEG verbatim, other formats transcoded to PNG, and
/// missing/undecodable textures replaced by a 1x1 placeholder. External URIs are
/// never emitted — UniGLTF/UniVRM only accepts embedded (bufferView or data-URI)
/// images and throws on any other URI, which would make the .vrm fail to load.
/// </summary>
public sealed class TextureRegistry
{
    private readonly GltfRoot _root;
    private readonly BufferBuilder _buffer;
    private readonly string? _baseDir;
    private readonly IReadOnlyList<string> _paths;
    private readonly Action<string>? _warn;
    private readonly Dictionary<int, int> _cache = new();
    private int _samplerIndex = -1;
    private readonly Dictionary<int, bool> _hasAlpha = new();

    public TextureRegistry(GltfRoot root, BufferBuilder buffer, string? baseDir,
        IReadOnlyList<string> texturePaths, Action<string>? warn = null)
    {
        _root = root;
        _buffer = buffer;
        _baseDir = baseDir;
        _paths = texturePaths;
        _warn = warn;
    }

    /// <summary>Returns a glTF texture index for the PMX texture, or -1 if absent.</summary>
    public int GetOrAdd(int pmxTextureIndex)
    {
        if (pmxTextureIndex < 0 || pmxTextureIndex >= _paths.Count) return -1;
        if (_cache.TryGetValue(pmxTextureIndex, out var existing)) return existing;

        string rel = _paths[pmxTextureIndex].Replace('\\', '/');
        var image = BuildImage(rel);
        _root.Images.Add(image);
        int imageIndex = _root.Images.Count - 1;

        _root.Textures.Add(new GltfTexture { Source = imageIndex, Sampler = EnsureSampler(), Name = rel });
        int texIndex = _root.Textures.Count - 1;

        _cache[pmxTextureIndex] = texIndex;
        return texIndex;
    }

    /// <summary>
    /// True if the PMX texture has a non-trivial alpha channel (some pixels not
    /// fully opaque), used to decide cutout (MASK) rendering. Computed once per
    /// texture; defaults to false for missing files / JPEG.
    /// </summary>
    public bool HasAlpha(int pmxTextureIndex)
    {
        if (pmxTextureIndex < 0 || pmxTextureIndex >= _paths.Count) return false;
        if (_hasAlpha.TryGetValue(pmxTextureIndex, out var known)) return known;

        bool result = false;
        try
        {
            string rel = _paths[pmxTextureIndex].Replace('\\', '/');
            string? full = _baseDir is null ? null : Path.Combine(_baseDir, rel);
            if (full is not null && File.Exists(full) &&
                Path.GetExtension(rel).ToLowerInvariant() is not (".jpg" or ".jpeg"))
            {
                using var image = SixLabors.ImageSharp.Image.Load<SixLabors.ImageSharp.PixelFormats.Rgba32>(full);
                result = ImageHasTransparency(image);
            }
        }
        catch { result = false; }

        _hasAlpha[pmxTextureIndex] = result;
        return result;
    }

    private static bool ImageHasTransparency(SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32> image)
    {
        bool found = false;
        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < accessor.Height && !found; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (int x = 0; x < row.Length; x++)
                    if (row[x].A < 250) { found = true; break; }
            }
        });
        return found;
    }

    private GltfImage BuildImage(string rel)
    {
        string? full = _baseDir is null ? null : Path.Combine(_baseDir, rel);

        if (full is null || !File.Exists(full))
        {
            _warn?.Invoke($"Texture '{rel}' not found on disk; embedding a placeholder (VRM must be self-contained).");
            return EmbedPlaceholder(rel);
        }

        // glTF/VRM only embeds PNG and JPEG. Those pass through verbatim;
        // anything else (BMP, TGA, GIF, …) is transcoded to PNG.
        if (MimeOf(rel) is { } mime)
        {
            int view = _buffer.AddRawBufferView(File.ReadAllBytes(full));
            return new GltfImage { Name = rel, MimeType = mime, BufferView = view };
        }

        try
        {
            using var image = SixLabors.ImageSharp.Image.Load(full);
            using var ms = new MemoryStream();
            image.SaveAsPng(ms);
            int view = _buffer.AddRawBufferView(ms.ToArray());
            return new GltfImage { Name = Path.ChangeExtension(rel, ".png"), MimeType = "image/png", BufferView = view };
        }
        catch (Exception ex)
        {
            _warn?.Invoke($"Texture '{rel}' could not be transcoded to PNG ({ex.Message}); embedding a placeholder.");
            return EmbedPlaceholder(rel);
        }
    }

    /// <summary>Embed a 1x1 opaque-white PNG so the image is always self-contained.</summary>
    private GltfImage EmbedPlaceholder(string rel)
    {
        int view = _buffer.AddRawBufferView(PlaceholderPng);
        return new GltfImage { Name = Path.ChangeExtension(rel, ".png"), MimeType = "image/png", BufferView = view };
    }

    // 1x1 white PNG.
    private static readonly byte[] PlaceholderPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAAC0lEQVR42mP8/5+hHgAHggJ/PchI7wAAAABJRU5ErkJggg==");

    private int EnsureSampler()
    {
        if (_samplerIndex >= 0) return _samplerIndex;
        _root.Samplers.Add(new GltfSampler
        {
            MagFilter = 9729, // LINEAR
            MinFilter = 9987, // LINEAR_MIPMAP_LINEAR
            WrapS = 10497,     // REPEAT
            WrapT = 10497,
        });
        _samplerIndex = _root.Samplers.Count - 1;
        return _samplerIndex;
    }

    private static string? MimeOf(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        _ => null,
    };
}

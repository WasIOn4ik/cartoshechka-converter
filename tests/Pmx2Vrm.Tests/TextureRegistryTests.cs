using Pmx2Vrm.Core.Conversion;
using Pmx2Vrm.Core.Gltf;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace Pmx2Vrm.Tests;

public class TextureRegistryTests
{
    private static string WriteTempTextures(Action<string> writer)
    {
        var dir = Path.Combine(Path.GetTempPath(), "pmx2vrm_tex_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "tex"));
        writer(dir);
        return dir;
    }

    [Theory]
    [InlineData("tex/skin.bmp")]
    [InlineData("tex/cheek.tga")]
    public void Non_png_jpeg_is_transcoded_and_embedded_as_png(string rel)
    {
        var dir = WriteTempTextures(d =>
        {
            using var img = new Image<Rgba32>(2, 2, new Rgba32(10, 20, 30, 255));
            var path = Path.Combine(d, rel);
            if (rel.EndsWith(".bmp")) img.SaveAsBmp(path);
            else img.SaveAsTga(path);
        });
        try
        {
            var root = new GltfRoot();
            var buffer = new BufferBuilder(root);
            var reg = new TextureRegistry(root, buffer, dir, new[] { rel });

            int tex = reg.GetOrAdd(0);
            buffer.Build();

            Assert.True(tex >= 0);
            var image = Assert.Single(root.Images);
            Assert.Equal("image/png", image.MimeType);
            Assert.NotNull(image.BufferView);
            Assert.Null(image.Uri);
            Assert.EndsWith(".png", image.Name);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void Png_passes_through_verbatim()
    {
        var dir = WriteTempTextures(d =>
        {
            using var img = new Image<Rgba32>(1, 1);
            img.SaveAsPng(Path.Combine(d, "tex/body.png"));
        });
        try
        {
            var root = new GltfRoot();
            var buffer = new BufferBuilder(root);
            var reg = new TextureRegistry(root, buffer, dir, new[] { "tex/body.png" });
            reg.GetOrAdd(0);
            buffer.Build();

            var image = Assert.Single(root.Images);
            Assert.Equal("image/png", image.MimeType);
            Assert.Equal("tex/body.png", image.Name);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void HasAlpha_detects_transparency()
    {
        var dir = WriteTempTextures(d =>
        {
            using var opaque = new Image<Rgba32>(2, 2, new Rgba32(255, 0, 0, 255));
            opaque.SaveAsPng(Path.Combine(d, "tex/opaque.png"));
            using var cut = new Image<Rgba32>(2, 2, new Rgba32(0, 255, 0, 0)); // fully transparent
            cut.SaveAsPng(Path.Combine(d, "tex/cut.png"));
        });
        try
        {
            var root = new GltfRoot();
            var reg = new TextureRegistry(root, new BufferBuilder(root), dir, new[] { "tex/opaque.png", "tex/cut.png" });
            Assert.False(reg.HasAlpha(0));
            Assert.True(reg.HasAlpha(1));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void Missing_file_embeds_placeholder_not_uri()
    {
        var root = new GltfRoot();
        var buffer = new BufferBuilder(root);
        var reg = new TextureRegistry(root, buffer, baseDir: null, new[] { "tex/missing.png" });
        reg.GetOrAdd(0);
        buffer.Build();

        var image = Assert.Single(root.Images);
        // Never an external URI — UniGLTF/UniVRM throws on those.
        Assert.Null(image.Uri);
        Assert.NotNull(image.BufferView);
        Assert.Equal("image/png", image.MimeType);
    }

    [Fact]
    public void No_image_ever_uses_an_external_uri()
    {
        var root = new GltfRoot();
        var buffer = new BufferBuilder(root);
        var reg = new TextureRegistry(root, buffer, baseDir: null, new[] { "a.bmp", "b.tga", "c.png" });
        for (int i = 0; i < 3; i++) reg.GetOrAdd(i);
        buffer.Build();

        Assert.All(root.Images, img => Assert.Null(img.Uri));
        Assert.All(root.Images, img => Assert.NotNull(img.BufferView));
    }
}

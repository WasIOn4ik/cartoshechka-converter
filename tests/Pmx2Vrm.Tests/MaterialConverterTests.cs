using Pmx2Vrm.Core.Conversion;
using Pmx2Vrm.Core.Gltf;
using Pmx2Vrm.Core.Pmx;
using Pmx2Vrm.Core.Vrm;
using Pmx2Vrm.Tests.TestSupport;
using Xunit;

namespace Pmx2Vrm.Tests;

public class MaterialConverterTests
{
    private static (GltfRoot root, IReadOnlyList<MToonMaterialDef> defs) Convert()
    {
        var model = PmxReader.Read(new MemoryStream(SyntheticPmx.Build()));
        var root = new GltfRoot();
        var buffer = new BufferBuilder(root);
        var coords = new CoordinateConverter();
        var textures = new TextureRegistry(root, buffer, baseDir: null, model.TexturePaths);
        var defs = new MaterialConverter(textures, coords).Convert(model, root);
        return (root, defs);
    }

    [Fact]
    public void Emits_one_gltf_material_and_def_per_pmx_material()
    {
        var (root, defs) = Convert();
        Assert.Single(root.Materials);
        Assert.Single(defs);
    }

    [Fact]
    public void Base_color_comes_from_diffuse()
    {
        var (_, defs) = Convert();
        Assert.Equal(0.8f, defs[0].BaseColor.X, 3);
        Assert.Equal(1f, defs[0].BaseColor.W, 3);
    }

    [Fact]
    public void Edge_flag_becomes_world_outline()
    {
        var (_, defs) = Convert();
        Assert.Equal(MToonOutlineMode.World, defs[0].OutlineMode);
        Assert.True(defs[0].OutlineWidth > 0f);
    }

    [Fact]
    public void Missing_texture_embeds_placeholder()
    {
        var (root, _) = Convert();
        // tex/body.png does not exist on disk (baseDir null). The image must be
        // an embedded placeholder, never an external URI (UniVRM rejects those).
        var image = Assert.Single(root.Images);
        Assert.Null(image.Uri);
        Assert.NotNull(image.BufferView);
    }

    [Fact]
    public void Opaque_material_has_no_blend_mode()
    {
        var (root, _) = Convert();
        Assert.Null(root.Materials[0].AlphaMode);
    }
}

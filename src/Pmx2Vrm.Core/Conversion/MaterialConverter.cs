using System.Numerics;
using Pmx2Vrm.Core.Gltf;
using Pmx2Vrm.Core.Pmx;
using Pmx2Vrm.Core.Vrm;

namespace Pmx2Vrm.Core.Conversion;

/// <summary>
/// Converts PMX materials into glTF PBR materials plus a version-agnostic MToon
/// description per material. The glTF material is a sensible PBR fallback for
/// viewers that ignore the VRM MToon extension; the <see cref="MToonMaterialDef"/>
/// carries the toon-specific data the VRM writer needs.
/// </summary>
public sealed class MaterialConverter
{
    private readonly TextureRegistry _textures;
    private readonly CoordinateConverter _coords;

    public MaterialConverter(TextureRegistry textures, CoordinateConverter coords)
    {
        _textures = textures;
        _coords = coords;
    }

    public IReadOnlyList<MToonMaterialDef> Convert(PmxModel model, GltfRoot root)
    {
        var defs = new List<MToonMaterialDef>(model.Materials.Count);

        foreach (var m in model.Materials)
        {
            int baseTex = _textures.GetOrAdd(m.TextureIndex);

            bool doubleSided = m.Flags.HasFlag(PmxMaterialFlags.NoCull);
            // Alpha: explicit diffuse alpha -> blend; otherwise a texture with a
            // real alpha channel -> cutout (MASK). MMD has no cutout flag, so
            // decor with transparent textures rendered as opaque white before.
            bool hasTextureAlpha = baseTex >= 0 && _textures.HasAlpha(m.TextureIndex);
            var alphaMode = m.Diffuse.W < 0.999f ? MToonAlphaMode.Blend
                : hasTextureAlpha ? MToonAlphaMode.Mask
                : MToonAlphaMode.Opaque;

            root.Materials.Add(BuildGltfMaterial(m, baseTex, doubleSided, alphaMode));

            defs.Add(new MToonMaterialDef
            {
                Name = MaterialName(m),
                BaseColor = m.Diffuse,
                ShadeColor = new Vector3(m.Diffuse.X, m.Diffuse.Y, m.Diffuse.Z) * 0.85f,
                // MMD "ambient" is ambient *reflectance*, NOT emission. Mapping it
                // to emissiveFactor made every material glow and washed textures
                // out to white. VRM has no equivalent, so emit no emission.
                EmissiveColor = Vector3.Zero,
                BaseColorTexture = baseTex,
                // Shade samples the SAME base texture (just tinted darker by
                // ShadeColor). Using a flat shade colour made shadowed/raised
                // hair turn grey instead of keeping its texture colour.
                ShadeMultiplyTexture = baseTex,
                // MMD sphere/"hi"/"spa" maps applied as an additive MToon matcap
                // blew hair and decor out to white from most angles. Skip them so
                // the real texture colour shows; faithful matcap support is TODO.
                SphereTexture = -1,
                SphereMode = MToonSphereMode.None,
                AlphaMode = alphaMode,
                DoubleSided = doubleSided,
                OutlineMode = m.Flags.HasFlag(PmxMaterialFlags.HasEdge) ? MToonOutlineMode.World : MToonOutlineMode.None,
                // MToon worldCoordinates outline width is in METRES. An MMD edge
                // size of ~1.0 should be a thin ~1 mm line, not metres — scaling
                // by the model unit (0.08) gave 8 cm shells. Map ~1 mm per edge
                // unit and clamp so a stray large value can't explode the mesh.
                OutlineWidth = m.Flags.HasFlag(PmxMaterialFlags.HasEdge)
                    ? Math.Clamp(m.EdgeScale * 0.001f, 0f, 0.01f)
                    : 0f,
                OutlineColor = new Vector3(m.EdgeColor.X, m.EdgeColor.Y, m.EdgeColor.Z),
            });
        }

        return defs;
    }

    private static GltfMaterial BuildGltfMaterial(PmxMaterial m, int baseTex, bool doubleSided, MToonAlphaMode alpha)
    {
        var pbr = new GltfPbr
        {
            BaseColorFactor = new[] { m.Diffuse.X, m.Diffuse.Y, m.Diffuse.Z, m.Diffuse.W },
            MetallicFactor = 0f,
            RoughnessFactor = 0.9f,
        };
        if (baseTex >= 0)
            pbr.BaseColorTexture = new GltfTextureRef { Index = baseTex };

        return new GltfMaterial
        {
            Name = MaterialName(m),
            PbrMetallicRoughness = pbr,
            DoubleSided = doubleSided,
            AlphaMode = alpha switch
            {
                MToonAlphaMode.Blend => "BLEND",
                MToonAlphaMode.Mask => "MASK",
                _ => null,
            },
            AlphaCutoff = alpha == MToonAlphaMode.Mask ? 0.5f : null,
            // No emissive: MMD ambient is not emission (see MToonMaterialDef).
        };
    }

    private static string MaterialName(PmxMaterial m) =>
        !string.IsNullOrWhiteSpace(m.NameUniversal) ? m.NameUniversal :
        !string.IsNullOrWhiteSpace(m.NameLocal) ? m.NameLocal : "material";
}

using System.Numerics;

namespace Pmx2Vrm.Core.Vrm;

public enum MToonAlphaMode { Opaque, Mask, Blend }

public enum MToonOutlineMode { None, World, Screen }

/// <summary>How a PMX sphere (environment) map is approximated in MToon.</summary>
public enum MToonSphereMode { None, AdditiveMatcap, MultiplyRim }

/// <summary>
/// Version-agnostic description of a toon material. The VRM writer turns this
/// into either a <c>VRMC_materials_mtoon</c> block (VRM 1.0) or a
/// <c>materialProperties</c> entry (VRM 0.x).
/// </summary>
public sealed class MToonMaterialDef
{
    public required string Name { get; init; }

    public Vector4 BaseColor { get; init; } = Vector4.One;
    public Vector3 ShadeColor { get; init; } = new(0.6f, 0.6f, 0.6f);
    public Vector3 EmissiveColor { get; init; } = Vector3.Zero;

    public int BaseColorTexture { get; init; } = -1;
    public int ShadeMultiplyTexture { get; init; } = -1; // PMX toon ramp
    public int SphereTexture { get; init; } = -1;

    public MToonSphereMode SphereMode { get; init; } = MToonSphereMode.None;
    public MToonAlphaMode AlphaMode { get; init; } = MToonAlphaMode.Opaque;
    public float AlphaCutoff { get; init; } = 0.5f;
    public bool DoubleSided { get; init; }

    public MToonOutlineMode OutlineMode { get; init; } = MToonOutlineMode.None;
    public float OutlineWidth { get; init; }        // metres (world mode)
    public Vector3 OutlineColor { get; init; } = Vector3.Zero;
}

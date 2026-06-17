namespace Pmx2Vrm.Core.Vrm;

public enum VrmVersion { Vrm0, Vrm1 }

/// <summary>
/// Author / licensing metadata written into the VRM meta block. Defaults are
/// deliberately conservative (author-only, non-commercial, redistribution
/// prohibited) so a converted model never claims permissions it was not granted.
/// </summary>
public sealed class VrmMeta
{
    public string Title { get; set; } = "Converted Model";
    public string Author { get; set; } = "Unknown";
    public string Version { get; set; } = "1.0";
    public string? ContactInformation { get; set; }
    public string? Reference { get; set; }

    /// <summary>onlyAuthor / onlySeparatelyLicensedPerson / everyone.</summary>
    public string AvatarPermission { get; set; } = "onlyAuthor";
    public bool AllowExcessivelyViolentUsage { get; set; }
    public bool AllowExcessivelySexualUsage { get; set; }
    /// <summary>personalNonProfit / personalProfit / corporation.</summary>
    public string CommercialUsage { get; set; } = "personalNonProfit";
    /// <summary>SPDX-style or custom; default disallows redistribution.</summary>
    public string CreditNotation { get; set; } = "required";
    public bool AllowRedistribution { get; set; }
    public string Modification { get; set; } = "prohibited";
}

public sealed class ConversionOptions
{
    public VrmVersion Version { get; set; } = VrmVersion.Vrm0;
    public float Scale { get; set; } = Conversion.CoordinateConverter.DefaultScale;
    public VrmMeta Meta { get; set; } = new();
    /// <summary>Directory used to resolve and embed external texture files.</summary>
    public string? TextureBaseDir { get; set; }
    /// <summary>
    /// Emit a curated set of body-sized spring-bone colliders (torso/limb
    /// capsules + head sphere) so hair and skirt drape over the body instead of
    /// clipping through it. On by default: unlike the old "every MMD rigid body
    /// becomes a collider" approach (which made cloth jitter), the curated body
    /// capsules mirror what stable VRoid exports ship. Disable with --no-colliders.
    /// </summary>
    public bool SpringColliders { get; set; } = true;
    /// <summary>Re-pose the A-pose MMD skeleton into the VRM 1.0 T-pose. Default on.</summary>
    public bool TPose { get; set; } = true;
    public Action<string>? Warn { get; set; }
}

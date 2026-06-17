namespace Pmx2Vrm.Core.Vrm;

/// <summary>
/// The VRM humanoid bone set (VRM 1.0 naming). The writer maps these to the
/// version-specific JSON keys ("leftThumbMetacarpal" for 1.0,
/// "leftThumbProximal" for 0.x, etc.).
/// </summary>
public enum VrmHumanBone
{
    Hips,
    Spine,
    Chest,
    UpperChest,
    Neck,
    Head,
    LeftEye,
    RightEye,
    Jaw,

    LeftUpperLeg, LeftLowerLeg, LeftFoot, LeftToes,
    RightUpperLeg, RightLowerLeg, RightFoot, RightToes,

    LeftShoulder, LeftUpperArm, LeftLowerArm, LeftHand,
    RightShoulder, RightUpperArm, RightLowerArm, RightHand,

    LeftThumbMetacarpal, LeftThumbProximal, LeftThumbDistal,
    LeftIndexProximal, LeftIndexIntermediate, LeftIndexDistal,
    LeftMiddleProximal, LeftMiddleIntermediate, LeftMiddleDistal,
    LeftRingProximal, LeftRingIntermediate, LeftRingDistal,
    LeftLittleProximal, LeftLittleIntermediate, LeftLittleDistal,

    RightThumbMetacarpal, RightThumbProximal, RightThumbDistal,
    RightIndexProximal, RightIndexIntermediate, RightIndexDistal,
    RightMiddleProximal, RightMiddleIntermediate, RightMiddleDistal,
    RightRingProximal, RightRingIntermediate, RightRingDistal,
    RightLittleProximal, RightLittleIntermediate, RightLittleDistal,
}

public static class VrmHumanBoneExtensions
{
    /// <summary>The five bones VRM requires (besides hips→head spine chain).</summary>
    public static readonly VrmHumanBone[] Required =
    {
        VrmHumanBone.Hips, VrmHumanBone.Spine, VrmHumanBone.Head,
        VrmHumanBone.LeftUpperArm, VrmHumanBone.LeftLowerArm, VrmHumanBone.LeftHand,
        VrmHumanBone.RightUpperArm, VrmHumanBone.RightLowerArm, VrmHumanBone.RightHand,
        VrmHumanBone.LeftUpperLeg, VrmHumanBone.LeftLowerLeg, VrmHumanBone.LeftFoot,
        VrmHumanBone.RightUpperLeg, VrmHumanBone.RightLowerLeg, VrmHumanBone.RightFoot,
    };

    /// <summary>camelCase key used in VRM 1.0 (VRMC_vrm.humanoid.humanBones).</summary>
    public static string ToVrm10Key(this VrmHumanBone bone)
    {
        var s = bone.ToString();
        return char.ToLowerInvariant(s[0]) + s[1..];
    }

    /// <summary>
    /// camelCase key used in VRM 0.x (humanoid.humanBones[].bone). Thumb naming
    /// differs: VRM 0.x uses Proximal/Intermediate/Distal instead of
    /// Metacarpal/Proximal/Distal.
    /// </summary>
    public static string ToVrm0Key(this VrmHumanBone bone) => bone switch
    {
        VrmHumanBone.LeftThumbMetacarpal => "leftThumbProximal",
        VrmHumanBone.LeftThumbProximal => "leftThumbIntermediate",
        VrmHumanBone.LeftThumbDistal => "leftThumbDistal",
        VrmHumanBone.RightThumbMetacarpal => "rightThumbProximal",
        VrmHumanBone.RightThumbProximal => "rightThumbIntermediate",
        VrmHumanBone.RightThumbDistal => "rightThumbDistal",
        _ => bone.ToVrm10Key(),
    };
}

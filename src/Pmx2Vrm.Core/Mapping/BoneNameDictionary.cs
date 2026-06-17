using Pmx2Vrm.Core.Vrm;

namespace Pmx2Vrm.Core.Mapping;

/// <summary>
/// Maps standard / semi-standard MMD bone names (準標準ボーン) to VRM humanoid
/// bones. Both the original Japanese names and common English universal names
/// are recognised.
/// </summary>
public static class BoneNameDictionary
{
    private static readonly Dictionary<string, VrmHumanBone> Map = Build();

    /// <summary>Look up a humanoid bone by an MMD bone name (JP or EN).</summary>
    public static bool TryGet(string name, out VrmHumanBone bone) =>
        Map.TryGetValue(name.Trim(), out bone);

    private static Dictionary<string, VrmHumanBone> Build()
    {
        var m = new Dictionary<string, VrmHumanBone>(StringComparer.Ordinal);

        void Add(VrmHumanBone bone, params string[] names)
        {
            foreach (var n in names) m[n] = bone;
        }

        // Spine chain. MMD's センター is the animation centre near the pelvis and
        // is the common ancestor of upper/lower body, so it maps to hips.
        Add(VrmHumanBone.Hips, "センター", "center", "下半身", "lower body");
        Add(VrmHumanBone.Spine, "上半身", "upper body");
        Add(VrmHumanBone.Chest, "上半身2", "upper body 2");
        Add(VrmHumanBone.Neck, "首", "neck");
        Add(VrmHumanBone.Head, "頭", "head");
        Add(VrmHumanBone.LeftEye, "左目", "eye_L");
        Add(VrmHumanBone.RightEye, "右目", "eye_R");

        // Arms
        Add(VrmHumanBone.LeftShoulder, "左肩", "shoulder_L");
        Add(VrmHumanBone.LeftUpperArm, "左腕", "arm_L");
        Add(VrmHumanBone.LeftLowerArm, "左ひじ", "elbow_L");
        Add(VrmHumanBone.LeftHand, "左手首", "wrist_L");
        Add(VrmHumanBone.RightShoulder, "右肩", "shoulder_R");
        Add(VrmHumanBone.RightUpperArm, "右腕", "arm_R");
        Add(VrmHumanBone.RightLowerArm, "右ひじ", "elbow_R");
        Add(VrmHumanBone.RightHand, "右手首", "wrist_R");

        // Legs
        Add(VrmHumanBone.LeftUpperLeg, "左足", "leg_L");
        Add(VrmHumanBone.LeftLowerLeg, "左ひざ", "knee_L");
        Add(VrmHumanBone.LeftFoot, "左足首", "ankle_L");
        Add(VrmHumanBone.LeftToes, "左つま先", "左足先EX", "toe_L");
        Add(VrmHumanBone.RightUpperLeg, "右足", "leg_R");
        Add(VrmHumanBone.RightLowerLeg, "右ひざ", "knee_R");
        Add(VrmHumanBone.RightFoot, "右足首", "ankle_R");
        Add(VrmHumanBone.RightToes, "右つま先", "右足先EX", "toe_R");

        // Fingers (left then right)
        AddFingers(Add, side: "左");
        AddFingers(Add, side: "右");

        return m;
    }

    private static void AddFingers(Action<VrmHumanBone, string[]> add, string side)
    {
        bool left = side == "左";

        VrmHumanBone B(VrmHumanBone l, VrmHumanBone r) => left ? l : r;

        // Thumb: MMD 親指０/１/２  ->  VRM Metacarpal/Proximal/Distal
        add(B(VrmHumanBone.LeftThumbMetacarpal, VrmHumanBone.RightThumbMetacarpal), new[] { side + "親指０", side + "親指0" });
        add(B(VrmHumanBone.LeftThumbProximal, VrmHumanBone.RightThumbProximal), new[] { side + "親指１", side + "親指1" });
        add(B(VrmHumanBone.LeftThumbDistal, VrmHumanBone.RightThumbDistal), new[] { side + "親指２", side + "親指2" });

        AddDigit(add, side, "人指", left ? VrmHumanBone.LeftIndexProximal : VrmHumanBone.RightIndexProximal);
        AddDigit(add, side, "中指", left ? VrmHumanBone.LeftMiddleProximal : VrmHumanBone.RightMiddleProximal);
        AddDigit(add, side, "薬指", left ? VrmHumanBone.LeftRingProximal : VrmHumanBone.RightRingProximal);
        AddDigit(add, side, "小指", left ? VrmHumanBone.LeftLittleProximal : VrmHumanBone.RightLittleProximal);
    }

    /// <summary>A four-finger digit is three consecutive enum values (Proximal, Intermediate, Distal).</summary>
    private static void AddDigit(Action<VrmHumanBone, string[]> add, string side, string jp, VrmHumanBone proximal)
    {
        add(proximal, new[] { side + jp + "１", side + jp + "1" });
        add(proximal + 1, new[] { side + jp + "２", side + jp + "2" });
        add(proximal + 2, new[] { side + jp + "３", side + jp + "3" });
    }
}

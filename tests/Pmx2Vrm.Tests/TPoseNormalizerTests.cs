using System.Numerics;
using Pmx2Vrm.Core.Conversion;
using Pmx2Vrm.Core.Vrm;
using Xunit;

namespace Pmx2Vrm.Tests;

public class TPoseNormalizerTests
{
    private static ConvertedSkeleton ArmSkeleton()
    {
        // A-pose left arm hanging down-and-out. In glTF space (Z-reflected from
        // MMD) the left side sits on +X.
        var nodes = new List<SkeletonNode>
        {
            new() { Name = "hips",     ParentIndex = -1, LocalTranslation = Vector3.Zero, WorldPosition = new Vector3(0f, 1.0f, 0f) },
            new() { Name = "upperArm", ParentIndex = 0,  LocalTranslation = Vector3.Zero, WorldPosition = new Vector3(0.1f, 1.3f, 0f) },
            new() { Name = "lowerArm", ParentIndex = 1,  LocalTranslation = Vector3.Zero, WorldPosition = new Vector3(0.25f, 1.1f, 0f) },
            new() { Name = "hand",     ParentIndex = 2,  LocalTranslation = Vector3.Zero, WorldPosition = new Vector3(0.4f, 0.9f, 0f) },
        };
        var humanoid = new Dictionary<VrmHumanBone, int>
        {
            [VrmHumanBone.Hips] = 0,
            [VrmHumanBone.LeftUpperArm] = 1,
            [VrmHumanBone.LeftLowerArm] = 2,
            [VrmHumanBone.LeftHand] = 3,
        };
        return new ConvertedSkeleton { Nodes = nodes, Humanoid = humanoid };
    }

    [Fact]
    public void Straightens_left_arm_horizontal_along_plus_x()
    {
        var t = new TPoseNormalizer(ArmSkeleton(), enabled: true);

        // After T-pose, upper->lower->hand all sit at the upper arm's height.
        float armY = t.NewWorldPos[1].Y;
        Assert.Equal(armY, t.NewWorldPos[2].Y, 4);
        Assert.Equal(armY, t.NewWorldPos[3].Y, 4);

        // and extend toward +X (left side in glTF space), keeping segment lengths.
        Assert.True(t.NewWorldPos[3].X > t.NewWorldPos[2].X);
        Assert.True(t.NewWorldPos[2].X > t.NewWorldPos[1].X);
    }

    [Fact]
    public void Segment_lengths_are_preserved()
    {
        var skel = ArmSkeleton();
        var t = new TPoseNormalizer(skel, enabled: true);

        float origLen = Vector3.Distance(skel.Nodes[2].WorldPosition, skel.Nodes[3].WorldPosition);
        float newLen = Vector3.Distance(t.NewWorldPos[2], t.NewWorldPos[3]);
        Assert.Equal(origLen, newLen, 4);
    }

    [Fact]
    public void Vrm1_knee_leans_forward_plus_z()
    {
        var nodes = new List<SkeletonNode>
        {
            new() { Name = "hips",     ParentIndex = -1, LocalTranslation = Vector3.Zero, WorldPosition = new Vector3(0f, 1.0f, 0f) },
            new() { Name = "upperLeg", ParentIndex = 0,  LocalTranslation = Vector3.Zero, WorldPosition = new Vector3(0.1f, 0.9f, 0f) },
            new() { Name = "lowerLeg", ParentIndex = 1,  LocalTranslation = Vector3.Zero, WorldPosition = new Vector3(0.15f, 0.5f, 0.05f) },
            new() { Name = "foot",     ParentIndex = 2,  LocalTranslation = Vector3.Zero, WorldPosition = new Vector3(0.15f, 0.1f, 0.0f) },
        };
        var humanoid = new Dictionary<VrmHumanBone, int>
        {
            [VrmHumanBone.LeftUpperLeg] = 1,
            [VrmHumanBone.LeftLowerLeg] = 2,
            [VrmHumanBone.LeftFoot] = 3,
        };
        var t = new TPoseNormalizer(new ConvertedSkeleton { Nodes = nodes, Humanoid = humanoid }, enabled: true, version: VrmVersion.Vrm1);

        Assert.True(t.NewWorldPos[2].Z > t.NewWorldPos[1].Z,
            "Vrm1: knee Z > upperLeg Z (+Z in glTF, stays +Z after ReverseX)");
    }

    [Fact]
    public void Vrm0_knee_leans_backward_minus_z()
    {
        var nodes = new List<SkeletonNode>
        {
            new() { Name = "hips",     ParentIndex = -1, LocalTranslation = Vector3.Zero, WorldPosition = new Vector3(0f, 1.0f, 0f) },
            new() { Name = "upperLeg", ParentIndex = 0,  LocalTranslation = Vector3.Zero, WorldPosition = new Vector3(0.1f, 0.9f, 0f) },
            new() { Name = "lowerLeg", ParentIndex = 1,  LocalTranslation = Vector3.Zero, WorldPosition = new Vector3(0.15f, 0.5f, 0.05f) },
            new() { Name = "foot",     ParentIndex = 2,  LocalTranslation = Vector3.Zero, WorldPosition = new Vector3(0.15f, 0.1f, 0.0f) },
        };
        var humanoid = new Dictionary<VrmHumanBone, int>
        {
            [VrmHumanBone.LeftUpperLeg] = 1,
            [VrmHumanBone.LeftLowerLeg] = 2,
            [VrmHumanBone.LeftFoot] = 3,
        };
        var t = new TPoseNormalizer(new ConvertedSkeleton { Nodes = nodes, Humanoid = humanoid }, enabled: true, version: VrmVersion.Vrm0);

        Assert.True(t.NewWorldPos[2].Z < t.NewWorldPos[1].Z,
            "Vrm0: knee Z < upperLeg Z (-Z in glTF, becomes +Z after ReverseZ)");
    }

    [Fact]
    public void Leg_gets_a_slight_forward_knee_with_ankle_under_hip()
    {
        // Perfectly vertical A-pose leg; after T-pose it should gain a gentle
        // forward knee (for IK) while the ankle stays roughly under the hip.
        var nodes = new List<SkeletonNode>
        {
            new() { Name = "hips",     ParentIndex = -1, LocalTranslation = Vector3.Zero, WorldPosition = new Vector3(0f, 1.0f, 0f) },
            new() { Name = "upperLeg", ParentIndex = 0,  LocalTranslation = Vector3.Zero, WorldPosition = new Vector3(0.1f, 0.9f, 0f) },
            new() { Name = "lowerLeg", ParentIndex = 1,  LocalTranslation = Vector3.Zero, WorldPosition = new Vector3(0.1f, 0.5f, 0f) },
            new() { Name = "foot",     ParentIndex = 2,  LocalTranslation = Vector3.Zero, WorldPosition = new Vector3(0.1f, 0.1f, 0f) },
        };
        var humanoid = new Dictionary<VrmHumanBone, int>
        {
            [VrmHumanBone.LeftUpperLeg] = 1,
            [VrmHumanBone.LeftLowerLeg] = 2,
            [VrmHumanBone.LeftFoot] = 3,
        };
        var skel = new ConvertedSkeleton { Nodes = nodes, Humanoid = humanoid };
        var t = new TPoseNormalizer(skel, enabled: true, VrmVersion.Vrm1);

        float thighLen = Vector3.Distance(nodes[1].WorldPosition, nodes[2].WorldPosition);
        // knee pushed forward (+Z for VRM 1.0), by a small fraction of the thigh.
        float kneeZ = t.NewWorldPos[2].Z;
        Assert.True(kneeZ > 0.02f, $"knee should bend forward, was {kneeZ}");
        Assert.True(kneeZ < 0.3f * thighLen, $"knee bend should be slight, was {kneeZ}");
        // ankle returns under the hip (small |Z|), not thrown forward.
        Assert.True(MathF.Abs(t.NewWorldPos[3].Z) < 0.02f, $"ankle Z should be ~0, was {t.NewWorldPos[3].Z}");
    }

    [Fact]
    public void Disabled_is_identity()
    {
        var t = new TPoseNormalizer(ArmSkeleton(), enabled: false);
        for (int i = 0; i < t.AccRot.Length; i++)
        {
            Assert.Equal(Quaternion.Identity, t.AccRot[i]);
            Assert.Equal(t.OrigWorldPos[i], t.NewWorldPos[i]);
        }
    }

    [Fact]
    public void Hips_and_unrelated_bones_keep_identity_rotation()
    {
        var t = new TPoseNormalizer(ArmSkeleton(), enabled: true);
        Assert.Equal(Quaternion.Identity, t.AccRot[0]); // hips not in an arm chain
    }
}

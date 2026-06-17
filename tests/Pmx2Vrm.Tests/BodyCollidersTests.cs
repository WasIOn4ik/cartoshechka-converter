using System.Numerics;
using Pmx2Vrm.Core.Conversion;
using Pmx2Vrm.Core.Vrm;
using Xunit;

namespace Pmx2Vrm.Tests;

public class BodyCollidersTests
{
    // A minimal humanoid: hips/spine/chest/neck/head torso, one arm, one leg.
    private static (Dictionary<VrmHumanBone, int> map, Vector3[] pos) Rig()
    {
        var map = new Dictionary<VrmHumanBone, int>
        {
            [VrmHumanBone.Hips] = 0,
            [VrmHumanBone.Spine] = 1,
            [VrmHumanBone.Chest] = 2,
            [VrmHumanBone.Neck] = 3,
            [VrmHumanBone.Head] = 4,
            [VrmHumanBone.LeftUpperArm] = 5,
            [VrmHumanBone.LeftLowerArm] = 6,
            [VrmHumanBone.LeftHand] = 7,
            [VrmHumanBone.LeftUpperLeg] = 8,
            [VrmHumanBone.LeftLowerLeg] = 9,
            [VrmHumanBone.LeftFoot] = 10,
        };
        var pos = new[]
        {
            new Vector3(0f, 0.9f, 0f),   // hips
            new Vector3(0f, 1.05f, 0f),  // spine
            new Vector3(0f, 1.2f, 0f),   // chest
            new Vector3(0f, 1.4f, 0f),   // neck
            new Vector3(0f, 1.5f, 0f),   // head
            new Vector3(0.15f, 1.3f, 0f),// upperArm
            new Vector3(0.4f, 1.3f, 0f), // lowerArm
            new Vector3(0.65f, 1.3f, 0f),// hand
            new Vector3(0.1f, 0.9f, 0f), // upperLeg
            new Vector3(0.1f, 0.5f, 0f), // lowerLeg
            new Vector3(0.1f, 0.05f, 0f),// foot
        };
        return (map, pos);
    }

    [Fact]
    public void Builds_capsules_along_limbs_and_a_head_sphere()
    {
        var (map, pos) = Rig();
        var colliders = BodyColliders.Build(map, pos);

        // Head is a sphere; every other collider is a capsule.
        Assert.Single(colliders, c => c.Shape == SpringColliderShape.Sphere && c.NodeIndex == map[VrmHumanBone.Head]);
        Assert.Contains(colliders, c => c.Shape == SpringColliderShape.Capsule && c.NodeIndex == map[VrmHumanBone.LeftUpperArm]);
        Assert.Contains(colliders, c => c.Shape == SpringColliderShape.Capsule && c.NodeIndex == map[VrmHumanBone.LeftUpperLeg]);
    }

    [Fact]
    public void Capsule_tail_points_to_the_child_bone()
    {
        var (map, pos) = Rig();
        var colliders = BodyColliders.Build(map, pos);

        var lowerLegCapsule = colliders.Single(c =>
            c.Shape == SpringColliderShape.Capsule && c.NodeIndex == map[VrmHumanBone.LeftLowerLeg]);

        // Offset is at the bone (zero); tail reaches the foot in node-local space.
        Assert.Equal(Vector3.Zero, lowerLegCapsule.Offset);
        var expectedTail = pos[map[VrmHumanBone.LeftFoot]] - pos[map[VrmHumanBone.LeftLowerLeg]];
        Assert.True(Vector3.Distance(lowerLegCapsule.TailOffset, expectedTail) < 1e-5f);
    }

    [Fact]
    public void Radii_scale_with_model_height()
    {
        var (map, pos) = Rig();
        var baseColliders = BodyColliders.Build(map, pos);

        // Double the height: radii should roughly double too.
        var tall = pos.Select(p => p * 2f).ToArray();
        var tallColliders = BodyColliders.Build(map, tall);

        float baseR = baseColliders.First(c => c.NodeIndex == map[VrmHumanBone.LeftUpperLeg]).Radius;
        float tallR = tallColliders.First(c => c.NodeIndex == map[VrmHumanBone.LeftUpperLeg]).Radius;
        Assert.Equal(2f, tallR / baseR, 2);
    }

    [Fact]
    public void Hips_capsule_is_nudged_toward_the_model_front()
    {
        var (map, pos) = Rig();
        var hipsNode = map[VrmHumanBone.Hips];

        // forwardZ = +1 shifts the hips capsule toward +Z (and -1 toward -Z),
        // so its back surface clears the skirt hanging behind the hip bone.
        var fwd = BodyColliders.Build(map, pos, forwardZ: 1f)
            .Single(c => c.NodeIndex == hipsNode && c.Shape == SpringColliderShape.Capsule);
        var none = BodyColliders.Build(map, pos, forwardZ: 0f)
            .Single(c => c.NodeIndex == hipsNode && c.Shape == SpringColliderShape.Capsule);

        Assert.True(fwd.Offset.Z > 0.001f, $"hips capsule should shift +Z, was {fwd.Offset.Z}");
        Assert.Equal(0f, none.Offset.Z, 5);
        // The shift moves the whole capsule (offset and tail) by the same amount,
        // so its length/orientation are preserved.
        Assert.Equal(fwd.Offset.Z, fwd.TailOffset.Z - none.TailOffset.Z, 4);
    }

    [Fact]
    public void Empty_humanoid_yields_no_colliders()
    {
        Assert.Empty(BodyColliders.Build(new Dictionary<VrmHumanBone, int>(), Array.Empty<Vector3>()));
    }

    [Fact]
    public void Only_maps_bones_that_exist()
    {
        // Just hips + spine: a single torso capsule, no limbs or head.
        var map = new Dictionary<VrmHumanBone, int> { [VrmHumanBone.Hips] = 0, [VrmHumanBone.Spine] = 1 };
        var pos = new[] { new Vector3(0, 0.9f, 0), new Vector3(0, 1.05f, 0) };

        var colliders = BodyColliders.Build(map, pos);
        var c = Assert.Single(colliders);
        Assert.Equal(SpringColliderShape.Capsule, c.Shape);
        Assert.Equal(0, c.NodeIndex);
    }
}

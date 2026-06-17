using System.Numerics;
using Pmx2Vrm.Core.Conversion;
using Pmx2Vrm.Core.Pmx;
using Pmx2Vrm.Tests.TestSupport;
using Xunit;

namespace Pmx2Vrm.Tests;

public class PhysicsConverterTests
{
    private static PhysicsConverter Make() =>
        new(new CoordinateConverter(scale: 1f), boneIndex => boneIndex);

    [Fact]
    public void Dynamic_body_becomes_a_spring_chain()
    {
        // Synthetic model: one dynamic capsule rigid body on bone 2 (hair, parent 1).
        var model = PmxReader.Read(new MemoryStream(SyntheticPmx.Build()));
        var physics = Make().Convert(model);

        var chain = Assert.Single(physics.Chains);
        // Anchor (parent bone 1) + dynamic bone 2.
        Assert.Equal(2, chain.Joints.Count);
        Assert.Equal(1, chain.Joints[0].NodeIndex);
        Assert.Equal(2, chain.Joints[1].NodeIndex);
        Assert.Empty(physics.Colliders); // the only body is dynamic
    }

    [Fact]
    public void Static_body_becomes_a_collider()
    {
        var model = new PmxModel();
        model.Bones.Add(new PmxBone { NameUniversal = "head", Position = new Vector3(0, 16, 0) });
        model.RigidBodies.Add(new PmxRigidBody
        {
            NameUniversal = "headCollider",
            BoneIndex = 0,
            Shape = PmxRigidBodyShape.Sphere,
            Size = new Vector3(1.0f, 0, 0),
            Position = new Vector3(0, 16, 0),
            PhysicsMode = PmxPhysicsMode.FollowBone,
        });

        var physics = Make().Convert(model);
        Assert.Empty(physics.Chains);
        var col = Assert.Single(physics.Colliders);
        Assert.Equal(SpringColliderShape.Sphere, col.Shape);
        Assert.Equal(1.0f, col.Radius, 3);
        Assert.Equal(0, col.NodeIndex);
    }

    [Fact]
    public void Colliders_are_omitted_when_disabled()
    {
        var model = new PmxModel();
        model.Bones.Add(new PmxBone { NameUniversal = "head" });
        model.RigidBodies.Add(new PmxRigidBody
        {
            BoneIndex = 0,
            Shape = PmxRigidBodyShape.Sphere,
            Size = Vector3.One,
            PhysicsMode = PmxPhysicsMode.FollowBone,
        });

        var physics = Make().Convert(model, humanoidBones: null, centerNode: -1, includeColliders: false);
        Assert.Empty(physics.Colliders);
    }

    [Fact]
    public void Branching_chain_produces_one_spring_per_leaf()
    {
        // bone0 (root dynamic) -> bone1, bone2 (both dynamic children)
        var model = new PmxModel();
        model.Bones.Add(new PmxBone { NameUniversal = "root", ParentIndex = -1 });
        model.Bones.Add(new PmxBone { NameUniversal = "a", ParentIndex = 0 });
        model.Bones.Add(new PmxBone { NameUniversal = "b", ParentIndex = 0 });
        for (int i = 0; i < 3; i++)
            model.RigidBodies.Add(new PmxRigidBody { BoneIndex = i, PhysicsMode = PmxPhysicsMode.Physics, Size = Vector3.One });

        var physics = Make().Convert(model);
        Assert.Equal(2, physics.Chains.Count); // root->a, root->b
    }

    [Fact]
    public void Humanoid_bones_are_excluded_from_spring_chains()
    {
        // bone 0 = leg (humanoid), bone 1 = skirt (non-humanoid), both with
        // dynamic bodies. Only the skirt may become a spring; the leg must not.
        var model = new PmxModel();
        model.Bones.Add(new PmxBone { NameUniversal = "leg", ParentIndex = -1 });
        model.Bones.Add(new PmxBone { NameUniversal = "skirt", ParentIndex = 0 });
        for (int i = 0; i < 2; i++)
            model.RigidBodies.Add(new PmxRigidBody { BoneIndex = i, PhysicsMode = PmxPhysicsMode.Physics, Size = Vector3.One });

        var physics = Make().Convert(model, humanoidBones: new[] { 0 }); // bone 0 is humanoid

        var chain = Assert.Single(physics.Chains);
        Assert.Equal(1, chain.FirstDynamicNode);                 // swaying starts at the skirt
        Assert.Equal(1, chain.Joints[^1].NodeIndex);             // leaf is the skirt, not the leg
        Assert.Contains(physics.Colliders, c => c.NodeIndex == 0); // leg body -> collider instead
    }

    [Fact]
    public void Spring_params_are_well_behaved_defaults()
    {
        var model = new PmxModel();
        model.Bones.Add(new PmxBone { ParentIndex = -1 });
        model.RigidBodies.Add(new PmxRigidBody
        {
            BoneIndex = 0,
            PhysicsMode = PmxPhysicsMode.Physics,
            AngularDamping = 0.7f,
            LinearDamping = 0.99f, // typical MMD value; must NOT collapse stiffness
            Size = Vector3.One,
        });

        var chain = Assert.Single(Make().Convert(model).Chains);
        Assert.Equal(1.0f, chain.Stiffness, 3);               // restoring force kept
        Assert.InRange(chain.DragForce, 0.6f, 0.95f);         // heavy damping
        Assert.Equal(0.845f, chain.DragForce, 3);             // 0.6 + 0.7*0.35
        Assert.Equal(0.3f, chain.GravityPower, 3);            // gravity pull so hair/skirt hangs
    }

    [Fact]
    public void Stiffness_stays_high_so_hair_does_not_sag_into_colliders()
    {
        // A long single hair strand: root + 7 dynamic children in a line.
        // Softening long chains made hair sag into the body colliders and jitter
        // worse, so stiffness must stay at the full restoring value.
        var model = new PmxModel();
        model.Bones.Add(new PmxBone { NameUniversal = "hairRoot", ParentIndex = -1 });
        for (int i = 1; i <= 7; i++)
            model.Bones.Add(new PmxBone { NameUniversal = $"hair{i}", ParentIndex = i - 1 });
        for (int i = 0; i < 8; i++)
            model.RigidBodies.Add(new PmxRigidBody { BoneIndex = i, PhysicsMode = PmxPhysicsMode.Physics, Size = Vector3.One });

        var chain = Assert.Single(Make().Convert(model).Chains);
        Assert.True(chain.Joints.Count >= 6, $"expected a long chain, got {chain.Joints.Count}");
        Assert.Equal(1.0f, chain.Stiffness, 3);
    }
}

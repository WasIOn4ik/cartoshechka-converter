using System.Numerics;
using Pmx2Vrm.Core.Pmx;

namespace Pmx2Vrm.Core.Conversion;

public enum SpringColliderShape { Sphere, Capsule }

public sealed class SpringColliderDef
{
    public required int NodeIndex { get; init; }
    public required SpringColliderShape Shape { get; init; }
    public Vector3 Offset { get; init; }
    public float Radius { get; init; }
    public Vector3 TailOffset { get; init; } // capsule only
}

public sealed class SpringJointDef
{
    public required int NodeIndex { get; init; }
    public float HitRadius { get; init; }
}

public sealed class SpringChainDef
{
    public string Name { get; init; } = "spring";
    public List<SpringJointDef> Joints { get; init; } = new();
    /// <summary>Node of the first swaying bone (VRM 0.x lists chain roots, not the anchor).</summary>
    public int FirstDynamicNode { get; init; }
    /// <summary>True if this chain starts at a top-level swaying bone (its parent is
    /// not dynamic). VRM 0.x must list only these as boneGroup roots; the engine
    /// grows the rest of each swaying tree from them.</summary>
    public bool TreeRoot { get; init; }
    public float Stiffness { get; init; } = 1.0f;
    public float DragForce { get; init; } = 0.4f;
    public float GravityPower { get; init; }
    public Vector3 GravityDir { get; init; } = new(0, -1, 0);
    /// <summary>Node whose motion the spring ignores (local inertia); -1 = world space.</summary>
    public int Center { get; init; } = -1;
}

public sealed class ConvertedPhysics
{
    public required IReadOnlyList<SpringColliderDef> Colliders { get; init; }
    /// <summary>Disjoint linear spring chains for VRM 1.0 (each bone swings once).</summary>
    public required IReadOnlyList<SpringChainDef> Chains { get; init; }
    /// <summary>Top-level swaying-tree roots for VRM 0.x; the engine grows each into
    /// its full child tree, so branches must NOT be listed separately.</summary>
    public required IReadOnlyList<SpringChainDef> Roots { get; init; }
}

/// <summary>
/// Approximates PMX rigid-body / joint physics with VRM spring bones.
///
/// Dynamic rigid bodies (physics mode Physics or PhysicsAndBone) sway, so the
/// bones they are bound to are gathered into root-to-leaf chains and emitted as
/// spring joints. Static rigid bodies (FollowBone) become colliders. The full
/// Bullet rigid-body simulation is NOT reproduced — this is a best-effort
/// mapping onto VRM's simpler verlet spring model.
/// </summary>
public sealed class PhysicsConverter
{
    private readonly CoordinateConverter _coords;
    private readonly Func<int, int> _boneToNode;
    private int _centerNode = -1;

    /// <param name="boneToNode">Maps a PMX bone index to its glTF node index.</param>
    public PhysicsConverter(CoordinateConverter coords, Func<int, int> boneToNode)
    {
        _coords = coords;
        _boneToNode = boneToNode;
    }

    /// <param name="humanoidBones">
    /// Bone indices that fill VRM humanoid slots. These must never become spring
    /// joints (a spring rotating a leg/hip bone makes the body twitch); they may
    /// still carry colliders (the body capsules cloth collides against).
    /// </param>
    public ConvertedPhysics Convert(PmxModel model, IEnumerable<int>? humanoidBones = null,
        int centerNode = -1, bool includeColliders = true)
    {
        _centerNode = centerNode;
        var humanoid = humanoidBones is null ? new HashSet<int>() : new HashSet<int>(humanoidBones);
        var colliders = new List<SpringColliderDef>();
        var chains = new List<SpringChainDef>();

        // First dynamic rigid body per bone (for chain joints) and static ones (colliders).
        // Colliders are off by default: approximating MMD's collision groups as
        // "every spring collides with every body capsule" makes cloth fight the
        // colliders and jitter. Known-good VRMs (e.g. VRoid exports) ship hair/
        // skirt springs with no colliders, which is stable.
        var dynamicByBone = new Dictionary<int, PmxRigidBody>();
        foreach (var rb in model.RigidBodies)
        {
            if (rb.BoneIndex < 0)
            {
                if (includeColliders && rb.PhysicsMode == PmxPhysicsMode.FollowBone) AddCollider(colliders, model, rb);
                continue;
            }

            // A humanoid bone with a dynamic body must not become a spring.
            if (rb.PhysicsMode == PmxPhysicsMode.FollowBone || humanoid.Contains(rb.BoneIndex))
            {
                if (includeColliders) AddCollider(colliders, model, rb);
            }
            else
                dynamicByBone.TryAdd(rb.BoneIndex, rb);
        }

        BuildChains(model, dynamicByBone, chains, out var roots);
        return new ConvertedPhysics { Colliders = colliders, Chains = chains, Roots = roots };
    }

    private void AddCollider(List<SpringColliderDef> colliders, PmxModel model, PmxRigidBody rb)
    {
        if (rb.BoneIndex < 0 || rb.BoneIndex >= model.Bones.Count) return;

        var bonePos = _coords.Position(model.Bones[rb.BoneIndex].Position);
        var rbPos = _coords.Position(rb.Position);
        var offset = rbPos - bonePos;
        float s = _coords.Scale;

        if (rb.Shape == PmxRigidBodyShape.Capsule)
        {
            // A MMD capsule's long axis is its local Y, rotated by the body's
            // euler rotation. Ignoring that rotation (always vertical) put body
            // and limb colliders at the wrong angle and ejected nearby cloth.
            float half = rb.Size.Y * 0.5f * s;
            var axis = Vector3.Transform(Vector3.UnitY, _coords.EulerToQuaternion(rb.Rotation));
            colliders.Add(new SpringColliderDef
            {
                NodeIndex = _boneToNode(rb.BoneIndex),
                Shape = SpringColliderShape.Capsule,
                Offset = offset - axis * half,
                Radius = rb.Size.X * s,
                TailOffset = offset + axis * half,
            });
        }
        else // sphere, or box approximated as a sphere
        {
            float radius = rb.Shape == PmxRigidBodyShape.Sphere
                ? rb.Size.X * s
                : Math.Max(rb.Size.X, Math.Max(rb.Size.Y, rb.Size.Z)) * 0.5f * s;
            colliders.Add(new SpringColliderDef
            {
                NodeIndex = _boneToNode(rb.BoneIndex),
                Shape = SpringColliderShape.Sphere,
                Offset = offset,
                Radius = radius,
            });
        }
    }

    private void BuildChains(PmxModel model, Dictionary<int, PmxRigidBody> dynamicByBone,
        List<SpringChainDef> chains, out List<SpringChainDef> roots)
    {
        roots = new List<SpringChainDef>();
        // Dynamic children of each dynamic bone (chain continuation).
        var children = new Dictionary<int, List<int>>();
        foreach (var boneIndex in dynamicByBone.Keys)
        {
            int parent = model.Bones[boneIndex].ParentIndex;
            if (parent >= 0 && dynamicByBone.ContainsKey(parent))
                (children.TryGetValue(parent, out var l) ? l : children[parent] = new List<int>()).Add(boneIndex);
        }

        // A bone may swing in at most ONE spring. Springs are maximal
        // non-branching paths of dynamic bones; at a branch point the current
        // spring ends and each branch starts its own spring whose head is the
        // branch point (heads may be shared across springs; only swing joints
        // must be unique, otherwise FastSpringBone drives one bone twice and
        // the cloth jitters).
        var claimed = new HashSet<int>();
        var dynamicRoots = new HashSet<int>();
        foreach (int b in dynamicByBone.Keys)
        {
            int p = model.Bones[b].ParentIndex;
            if (p < 0 || !dynamicByBone.ContainsKey(p)) dynamicRoots.Add(b);
        }

        // VRM 0.x: one entry per top-level swaying root; the engine grows each
        // into its full child tree (branches included), so only the root is listed.
        foreach (int r in dynamicRoots.OrderBy(i => i))
            roots.Add(MakeRoot(model, dynamicByBone, r));

        // Spring starts: (headBone, firstSwingBone). headBone < 0 means "use
        // firstSwingBone itself as the fixed head" (a dynamic root with no
        // non-dynamic parent).
        var starts = new Queue<(int head, int first)>();

        foreach (int root in dynamicByBone.Keys.OrderBy(i => i))
        {
            int parent = model.Bones[root].ParentIndex;
            bool hasAnchor = parent >= 0 && !dynamicByBone.ContainsKey(parent);
            if (hasAnchor)
                starts.Enqueue((parent, root));
            else if (!dynamicByBone.ContainsKey(parent)) // root with no parent
            {
                // Root is its own fixed head; each dynamic child starts a spring.
                if (children.TryGetValue(root, out var kids))
                    foreach (int k in kids.OrderBy(i => i)) starts.Enqueue((root, k));
                else
                    starts.Enqueue((root, root)); // lone dynamic bone -> degenerate single-joint spring
            }
        }

        while (starts.Count > 0)
        {
            var (head, first) = starts.Dequeue();
            if (claimed.Contains(first)) continue;
            chains.Add(MakeChain(model, dynamicByBone, head, first, children, claimed, starts));
        }
    }

    /// <summary>A VRM 0.x swaying-tree root entry (params + the root bone).</summary>
    private SpringChainDef MakeRoot(PmxModel model, Dictionary<int, PmxRigidBody> dynamicByBone, int root)
    {
        var body = dynamicByBone[root];
        return new SpringChainDef
        {
            Name = model.Bones[root].NameUniversal is { Length: > 0 } n ? n : "spring",
            DragForce = Math.Clamp(0.6f + body.AngularDamping * 0.35f, 0.6f, 0.95f),
            Stiffness = 1.0f,
            GravityPower = 0.3f,
            Center = _centerNode,
            FirstDynamicNode = _boneToNode(root),
            TreeRoot = true,
            Joints = new List<SpringJointDef>
            {
                new() { NodeIndex = _boneToNode(root), HitRadius = MathF.Max(body.Size.X * _coords.Scale, 0.01f) },
            },
        };
    }

    private SpringChainDef MakeChain(PmxModel model, Dictionary<int, PmxRigidBody> dynamicByBone,
        int head, int first, Dictionary<int, List<int>> children,
        HashSet<int> claimed, Queue<(int head, int first)> starts)
    {
        var joints = new List<SpringJointDef>();
        // Head (fixed anchor) — emit unless it coincides with the first swing
        // bone (lone-bone case), where the bone is both head and only joint.
        if (head != first)
            joints.Add(new SpringJointDef { NodeIndex = _boneToNode(head), HitRadius = 0.02f });

        int cur = first;
        claimed.Add(cur);
        joints.Add(new SpringJointDef
        {
            NodeIndex = _boneToNode(cur),
            HitRadius = MathF.Max(dynamicByBone[cur].Size.X * _coords.Scale, 0.01f),
        });

        // Extend down the single-child path; branch points spawn new springs.
        while (true)
        {
            var kids = children.TryGetValue(cur, out var ks) ? ks.Where(k => !claimed.Contains(k)).OrderBy(i => i).ToList() : new();
            if (kids.Count == 0) break;
            if (kids.Count == 1)
            {
                cur = kids[0];
                claimed.Add(cur);
                joints.Add(new SpringJointDef
                {
                    NodeIndex = _boneToNode(cur),
                    HitRadius = MathF.Max(dynamicByBone[cur].Size.X * _coords.Scale, 0.01f),
                });
                continue;
            }
            foreach (int k in kids) starts.Enqueue((cur, k));
            break;
        }

        var rootBody = dynamicByBone[first];
        return new SpringChainDef
        {
            Name = model.Bones[first].NameUniversal is { Length: > 0 } n ? n : "spring",
            // Bullet damping does NOT map linearly to VRM's verlet spring; use
            // well-behaved defaults nudged by the body's angular damping. Keep
            // stiffness high: softening it lets long hair sag INTO the body
            // colliders, which then eject it every frame and jitter worse.
            DragForce = Math.Clamp(0.6f + rootBody.AngularDamping * 0.35f, 0.6f, 0.95f),
            Stiffness = 1.0f,
            GravityPower = 0.3f,
            Center = _centerNode,
            FirstDynamicNode = _boneToNode(first),
            Joints = joints,
        };
    }
}

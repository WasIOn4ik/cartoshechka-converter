using System.Numerics;
using Pmx2Vrm.Core.Gltf;
using Pmx2Vrm.Core.Pmx;
using Pmx2Vrm.Core.Vrm;

namespace Pmx2Vrm.Core.Conversion;

/// <summary>
/// End-to-end orchestrator: turns a parsed <see cref="PmxModel"/> into VRM
/// (.vrm / glb) bytes. Bone nodes occupy node indices 0..N-1 so that PMX bone
/// indices map directly to glTF node and skin-joint indices; the skinned mesh
/// node follows.
/// </summary>
public sealed class VrmConverter
{
    public byte[] Convert(PmxModel model, ConversionOptions options)
    {
        var coords = new CoordinateConverter(options.Scale, options.Version);
        var root = new GltfRoot { Asset = { Generator = "Pmx2Vrm" } };
        var buffer = new BufferBuilder(root);

        var skeleton = new SkeletonConverter(coords).Convert(model);
        // Make hips the skeleton root: MMD rigs sit hips under control bones
        // (master/groove/waist) and add stray roots (view cnt), but VRM/Unity
        // humanoid retargeting expects hips at the top of the bone hierarchy.
        // Node indices are preserved (skinning maps bone index == node index);
        // only parent links move, with local translations recomputed so world
        // rest positions are unchanged.
        ReparentToHips(skeleton);
        // NOTE: do NOT flatten the humanoid chain or strip non-humanoid
        // intermediates. MMD models need their original bone hierarchy for
        // Unity's humanoid retargeting to drive legs/ IK correctly. Known-good
        // MMD-to-VRM conversions preserve the full hierarchy (上半身→下半身→
        // 腰キャンセル→足等). Flattening breaks animation because Unity walks
        // the transform chain from hips to feet at runtime.
        // Re-pose A-pose -> T-pose (VRM expects T-pose); baked into rest.
        var tpose = new TPoseNormalizer(skeleton, enabled: options.TPose, version: options.Version);
        // Place feet on the ground (Y=0). Unity's AvatarBuilder and runtime
        // IK both assume the model stands at the origin. After conversion the
        // feet may float or sink depending on the PMX origin; shifting all
        // T-pose world positions down by the lowest foot Y fixes this without
        // altering local bone offsets (only the root hip translation changes).
        GroundToFloor(skeleton, tpose);
        BuildBoneNodes(root, skeleton, tpose);

        // Geometry, materials, morphs (all append accessors to the buffer).
        var mesh = new MeshConverter(coords).Convert(model, buffer, tpose);
        var textures = new TextureRegistry(root, buffer, options.TextureBaseDir, model.TexturePaths, options.Warn);
        var materials = new MaterialConverter(textures, coords).Convert(model, root);
        var morphs = new MorphConverter(coords, options.Warn).Convert(model, mesh, buffer);

        int meshIndex = AddMesh(root, mesh);
        int skinIndex = BuildSkin(root, buffer, skeleton, tpose);
        int meshNodeIndex = AddMeshNode(root, model, meshIndex, skinIndex);

        BuildScene(root, skeleton, meshNodeIndex);

        // Spring inertia is measured relative to the hips ("center" node) rather
        // than world space. With world-space inertia, an animation that moves the
        // whole body fast (a bow, a lean, root translation) hits the springs with
        // a big impulse and flings the skirt back up and the hair forward. Anchoring
        // the reference frame to the hips subtracts that body-wide motion, so only
        // local bone rotation drives the sway — hair still swings when the head
        // turns, but the skirt no longer flies during animation.
        int centerNode = skeleton.Humanoid.TryGetValue(VrmHumanBone.Hips, out int hipsNode)
            ? hipsNode : -1;

        // Colliders are a curated set of body-sized capsules built from the final
        // humanoid skeleton (see BodyColliders), NOT the raw MMD rigid bodies:
        // dumping every MMD body made cloth jitter, while a handful of body
        // capsules let hair/skirt drape cleanly. Spring chains are still built
        // from the MMD dynamic bodies; we just swap in the curated colliders.
        var springs = new PhysicsConverter(coords, b => b)
            .Convert(model, skeleton.Humanoid.Values, centerNode, includeColliders: false);
        // Facing direction in glTF +Z: VRM 0.x (X-reflected) faces +Z, VRM 1.0
        // (Z-reflected) faces -Z. The hips collider is nudged this way.
        float forwardZ = options.Version == VrmVersion.Vrm1 ? -1f : 1f;
        var colliders = options.SpringColliders
            ? BodyColliders.Build(skeleton.Humanoid, tpose.NewWorldPos, forwardZ, mesh.SkinVertices)
            : new List<SpringColliderDef>();
        if (options.Log != null)
            BodyColliders.Report(colliders, idx => root.Nodes[idx].Name ?? idx.ToString(), options.Log);
        var physics = new ConvertedPhysics
        {
            Colliders = colliders,
            Chains = springs.Chains,
            Roots = springs.Roots,
        };

        new VrmExtensionBuilder().Apply(root, options.Version, new VrmExtensionBuilder.Inputs
        {
            Meta = options.Meta,
            Humanoid = skeleton.Humanoid,
            Expressions = morphs.Expressions,
            Physics = physics,
            Materials = materials,
            MeshNodeIndex = meshNodeIndex,
            MeshIndex = meshIndex,
        });

        var binary = buffer.Build();
        return GlbWriter.Write(root, binary);
    }

    public void ConvertFile(string pmxPath, string vrmPath, ConversionOptions options)
    {
        options.TextureBaseDir ??= Path.GetDirectoryName(Path.GetFullPath(pmxPath));
        var model = PmxReader.ReadFromFile(pmxPath);
        File.WriteAllBytes(vrmPath, Convert(model, options));
    }

    private static void BuildBoneNodes(GltfRoot root, ConvertedSkeleton skeleton, TPoseNormalizer tpose)
    {
        for (int i = 0; i < skeleton.Nodes.Count; i++)
        {
            var (trans, rot) = tpose.LocalTransform(i);
            root.Nodes.Add(new GltfNode
            {
                Name = skeleton.Nodes[i].Name,
                Translation = NonZero(trans),
                Rotation = NonIdentity(rot),
            });
        }

        for (int i = 0; i < skeleton.Nodes.Count; i++)
        {
            int parent = skeleton.Nodes[i].ParentIndex;
            if (parent < 0) continue;
            (root.Nodes[parent].Children ??= new List<int>()).Add(i);
        }
    }

    private static int AddMesh(GltfRoot root, ConvertedMesh mesh)
    {
        root.Meshes.Add(mesh.Mesh);
        return root.Meshes.Count - 1;
    }

    private static int BuildSkin(GltfRoot root, BufferBuilder buffer, ConvertedSkeleton skeleton, TPoseNormalizer tpose)
    {
        if (skeleton.Nodes.Count == 0) return -1;

        var ibms = new Matrix4x4[skeleton.Nodes.Count];
        for (int i = 0; i < skeleton.Nodes.Count; i++)
            ibms[i] = tpose.InverseBind(i);

        int ibmAccessor = buffer.AddMatrices(ibms);
        int firstRoot = 0;
        for (int i = 0; i < skeleton.Nodes.Count; i++)
            if (skeleton.Nodes[i].ParentIndex < 0) { firstRoot = i; break; }

        root.Skins.Add(new GltfSkin
        {
            InverseBindMatrices = ibmAccessor,
            Skeleton = firstRoot,
            Joints = Enumerable.Range(0, skeleton.Nodes.Count).ToList(),
        });
        return root.Skins.Count - 1;
    }

    private static int AddMeshNode(GltfRoot root, PmxModel model, int meshIndex, int skinIndex)
    {
        var node = new GltfNode
        {
            Name = string.IsNullOrEmpty(model.NameUniversal) ? "mesh" : model.NameUniversal,
            Mesh = meshIndex,
        };
        if (skinIndex >= 0) node.Skin = skinIndex;
        root.Nodes.Add(node);
        return root.Nodes.Count - 1;
    }

    private static void BuildScene(GltfRoot root, ConvertedSkeleton skeleton, int meshNodeIndex)
    {
        var scene = new GltfScene();
        for (int i = 0; i < skeleton.Nodes.Count; i++)
            if (skeleton.Nodes[i].ParentIndex < 0) scene.Nodes.Add(i);
        scene.Nodes.Add(meshNodeIndex);

        root.Scenes.Add(scene);
        root.Scene = 0;
    }

    /// <summary>Omit a translation that is exactly zero to keep the JSON tidy.</summary>
    private static float[]? NonZero(Vector3 v) =>
        v == Vector3.Zero ? null : new[] { v.X, v.Y, v.Z };

    /// <summary>
    /// Restructure the node hierarchy so the humanoid hips is the single bone
    /// root, without changing node indices or world rest positions. The former
    /// ancestor chain of hips (master/groove/waist in MMD rigs) is reversed to
    /// hang below hips, and any stray roots (view cnt, etc.) are reparented
    /// under hips. This matches the structure of known-good VRM exports and
    /// lets Unity's humanoid retargeting drive the legs.
    /// </summary>
    private static void ReparentToHips(ConvertedSkeleton skeleton)
    {
        if (!skeleton.Humanoid.TryGetValue(VrmHumanBone.Hips, out int hips)) return;
        var nodes = skeleton.Nodes;
        if (hips < 0 || hips >= nodes.Count) return;

        // Ancestor chain of hips: [hips, parent, ..., root].
        var chain = new List<int>();
        for (int n = hips; n >= 0; n = nodes[n].ParentIndex) chain.Add(n);

        // Reverse the chain: hips becomes root, each former ancestor points at
        // the next one down.
        nodes[hips].ParentIndex = -1;
        for (int i = 1; i < chain.Count; i++)
            nodes[chain[i]].ParentIndex = chain[i - 1];

        // Recompute local translations along the reparented chain (world pos
        // unchanged; rest rotations are identity at this stage).
        foreach (int i in chain)
        {
            int p = nodes[i].ParentIndex;
            nodes[i].LocalTranslation = p < 0
                ? nodes[i].WorldPosition
                : nodes[i].WorldPosition - nodes[p].WorldPosition;
        }

        // Fold any remaining stray roots under hips so the scene has one bone root.
        for (int i = 0; i < nodes.Count; i++)
        {
            if (i == hips || nodes[i].ParentIndex != -1) continue;
            nodes[i].ParentIndex = hips;
            nodes[i].LocalTranslation = nodes[i].WorldPosition - nodes[hips].WorldPosition;
        }
    }

    /// <summary>
    /// Make every humanoid bone a direct child of its nearest humanoid ancestor,
    /// preserving world rest positions. Removes MMD intermediate helper bones
    /// (leg-attach, etc.) from the humanoid chain so Unity's AvatarBuilder sees
    /// a clean hips->upperLeg->lowerLeg->foot->toes path with non-zero segments.
    /// </summary>
    private static void FlattenHumanoidChain(ConvertedSkeleton skeleton)
    {
        var nodes = skeleton.Nodes;
        var humanoidNodes = skeleton.Humanoid.Values.ToHashSet();

        foreach (var (bone, nodeIdx) in skeleton.Humanoid)
        {
            if (bone == VrmHumanBone.Hips) continue;
            if (nodeIdx < 0 || nodeIdx >= nodes.Count) continue;

            // Walk up to the nearest humanoid ancestor.
            int ancestor = -1;
            for (int p = nodes[nodeIdx].ParentIndex; p >= 0; p = nodes[p].ParentIndex)
            {
                if (humanoidNodes.Contains(p)) { ancestor = p; break; }
            }
            if (ancestor < 0 || ancestor == nodes[nodeIdx].ParentIndex) continue;

            nodes[nodeIdx].ParentIndex = ancestor;
            nodes[nodeIdx].LocalTranslation = nodes[nodeIdx].WorldPosition - nodes[ancestor].WorldPosition;
        }
    }

    private static void FlattenNonHumanoid(ConvertedSkeleton skeleton, PmxModel model)
    {
        var nodes = skeleton.Nodes;
        var humanoidNodes = skeleton.Humanoid.Values.ToHashSet();

        // Identify dynamic bones (spring bone joints) from PMX rigid bodies.
        // These must NOT be flattened — their internal chain structure is the
        // physics chain and must stay intact.
        var dynamicBones = new HashSet<int>();
        foreach (var rb in model.RigidBodies)
        {
            if (rb.BoneIndex < 0 || rb.BoneIndex >= nodes.Count) continue;
            if (rb.PhysicsMode == PmxPhysicsMode.Physics || rb.PhysicsMode == PmxPhysicsMode.PhysicsAndBone)
                if (!humanoidNodes.Contains(rb.BoneIndex))
                    dynamicBones.Add(rb.BoneIndex);
        }

        // Process in reverse depth order (deepest first) so that when we
        // reparent a bone's children, those children have already been processed.
        var depth = new int[nodes.Count];
        for (int i = 0; i < nodes.Count; i++)
        {
            int d = 0;
            for (int p = nodes[i].ParentIndex; p >= 0; p = nodes[p].ParentIndex) d++;
            depth[i] = d;
        }
        var order = Enumerable.Range(0, nodes.Count).OrderByDescending(i => depth[i]);

        foreach (int i in order)
        {
            if (humanoidNodes.Contains(i)) continue;
            if (dynamicBones.Contains(i)) continue;

            // Find nearest humanoid ancestor (walk up, skipping non-humanoid).
            int humanoidAncestor = -1;
            for (int p = nodes[i].ParentIndex; p >= 0; p = nodes[p].ParentIndex)
            {
                if (humanoidNodes.Contains(p)) { humanoidAncestor = p; break; }
            }
            if (humanoidAncestor < 0) continue;

            // Collect current children.
            var children = new List<int>();
            for (int j = 0; j < nodes.Count; j++)
                if (nodes[j].ParentIndex == i) children.Add(j);

            if (children.Count == 0) continue;

            // Reparent children to the nearest humanoid ancestor.
            foreach (int c in children)
            {
                nodes[c].ParentIndex = humanoidAncestor;
                nodes[c].LocalTranslation = nodes[c].WorldPosition - nodes[humanoidAncestor].WorldPosition;
            }
        }
    }

    private static void GroundToFloor(ConvertedSkeleton skeleton, TPoseNormalizer tpose)
    {
        var footBones = new[]
        {
            VrmHumanBone.LeftFoot, VrmHumanBone.LeftToes,
            VrmHumanBone.RightFoot, VrmHumanBone.RightToes,
        };

        float minY = float.MaxValue;
        foreach (var fb in footBones)
        {
            if (skeleton.Humanoid.TryGetValue(fb, out int idx) && idx >= 0 && idx < skeleton.Nodes.Count)
                minY = MathF.Min(minY, tpose.NewWorldPos[idx].Y);
        }

        if (minY == float.MaxValue)
        {
            for (int i = 0; i < skeleton.Nodes.Count; i++)
                minY = MathF.Min(minY, tpose.NewWorldPos[i].Y);
        }

        if (MathF.Abs(minY) < 1e-6f) return;

        var offset = new Vector3(0, -minY, 0);
        for (int i = 0; i < tpose.NewWorldPos.Length; i++)
            tpose.NewWorldPos[i] += offset;
    }

    /// <summary>Omit a rotation that is (near) identity.</summary>
    private static float[]? NonIdentity(Quaternion q) =>
        MathF.Abs(q.X) < 1e-6f && MathF.Abs(q.Y) < 1e-6f && MathF.Abs(q.Z) < 1e-6f
            ? null : new[] { q.X, q.Y, q.Z, q.W };
}

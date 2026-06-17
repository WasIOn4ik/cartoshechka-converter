using System.Numerics;
using Pmx2Vrm.Core.Pmx;
using Pmx2Vrm.Core.Vrm;

namespace Pmx2Vrm.Core.Conversion;

/// <summary>A single converted skeleton node, ready to become a glTF node.</summary>
public sealed class SkeletonNode
{
    public required string Name { get; init; }
    /// <summary>Index of the parent node, or -1 for a root.</summary>
    public int ParentIndex { get; set; }
    /// <summary>Translation relative to the parent node (glTF space).</summary>
    public Vector3 LocalTranslation { get; set; }
    /// <summary>World-space rest position (glTF space) — used for inverse bind matrices.</summary>
    public required Vector3 WorldPosition { get; init; }
}

public sealed class ConvertedSkeleton
{
    public required IReadOnlyList<SkeletonNode> Nodes { get; init; }
    public required IReadOnlyDictionary<VrmHumanBone, int> Humanoid { get; init; }
}

/// <summary>
/// Converts PMX bones into a node hierarchy in glTF space. PMX stores bones in
/// world space with an identity rest rotation, so each node carries only a
/// translation (parent-relative); rotations stay identity.
/// </summary>
public sealed class SkeletonConverter
{
    private readonly CoordinateConverter _coords;

    public SkeletonConverter(CoordinateConverter coords) => _coords = coords;

    public ConvertedSkeleton Convert(PmxModel model)
    {
        var worldPositions = new Vector3[model.Bones.Count];
        for (int i = 0; i < model.Bones.Count; i++)
            worldPositions[i] = _coords.Position(model.Bones[i].Position);

        var nodes = new List<SkeletonNode>(model.Bones.Count);
        var usedNames = new HashSet<string>();
        for (int i = 0; i < model.Bones.Count; i++)
        {
            var bone = model.Bones[i];
            int parent = bone.ParentIndex;
            var world = worldPositions[i];
            var local = parent >= 0 ? world - worldPositions[parent] : world;

            nodes.Add(new SkeletonNode
            {
                Name = UniqueName(PickName(bone, i), i, usedNames),
                ParentIndex = parent,
                LocalTranslation = local,
                WorldPosition = world,
            });
        }

        return new ConvertedSkeleton
        {
            Nodes = nodes,
            Humanoid = HumanoidMapper.Map(model),
        };
    }

    private static string PickName(PmxBone bone, int index)
    {
        if (!string.IsNullOrWhiteSpace(bone.NameUniversal)) return bone.NameUniversal;
        if (!string.IsNullOrWhiteSpace(bone.NameLocal)) return bone.NameLocal;
        return $"bone_{index}";
    }

    /// <summary>
    /// Unity's AvatarBuilder requires uniquely-named Transforms for the bones it
    /// maps. MMD rigs routinely reuse universal names across chain copies
    /// (Skirt_0_1 etc.) or leave names blank, so disambiguate by appending the
    /// bone index on collision.
    /// </summary>
    private static string UniqueName(string name, int index, HashSet<string> used)
    {
        if (used.Add(name)) return name;
        var deduped = $"{name}_{index}";
        while (!used.Add(deduped)) deduped = $"{deduped}_{index}";
        return deduped;
    }
}

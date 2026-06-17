using Pmx2Vrm.Core.Mapping;
using Pmx2Vrm.Core.Pmx;
using Pmx2Vrm.Core.Vrm;

namespace Pmx2Vrm.Core.Conversion;

/// <summary>
/// Resolves which PMX bones fill the VRM humanoid slots, matching by Japanese
/// (local) name first, then by universal name.
/// </summary>
public static class HumanoidMapper
{
    /// <summary>
    /// Build a humanoid-bone -> PMX bone-index map. When several bones resolve
    /// to the same humanoid slot, the earliest bone in the file wins (PMX lists
    /// センター before 下半身, giving the more correct hips).
    /// </summary>
    public static Dictionary<VrmHumanBone, int> Map(PmxModel model)
    {
        var result = new Dictionary<VrmHumanBone, int>();

        for (int i = 0; i < model.Bones.Count; i++)
        {
            var bone = model.Bones[i];
            if (TryResolve(bone, out var human) && !result.ContainsKey(human))
                result[human] = i;
        }

        RefineHips(model, result);
        return result;
    }

    /// <summary>
    /// Re-anchor hips to the pelvis: the lowest bone that is a common ancestor of
    /// both upper legs and the spine. MMD's name-matched hips (センター) sits high
    /// up the rig (above groove/waist), giving the legs a long lever so they
    /// swing too much under animation. The lowest common ancestor is the real
    /// pelvis (like a VRoid export's HipMaster), keeping legs planted.
    /// </summary>
    private static void RefineHips(PmxModel model, Dictionary<VrmHumanBone, int> map)
    {
        if (!map.TryGetValue(VrmHumanBone.LeftUpperLeg, out var l) ||
            !map.TryGetValue(VrmHumanBone.RightUpperLeg, out var r) ||
            !map.TryGetValue(VrmHumanBone.Spine, out var s))
            return;

        int lca = LowestCommonAncestor(model, l, r, s);
        // Only adopt it if it is a distinct, non-humanoid bone (don't collapse
        // hips onto a leg/spine slot).
        if (lca >= 0 && lca != l && lca != r && lca != s && !map.Values.Contains(lca))
            map[VrmHumanBone.Hips] = lca;
    }

    private static int LowestCommonAncestor(PmxModel model, params int[] nodes)
    {
        // Walk up from the first node; the first ancestor (self included) that is
        // an ancestor-or-self of every node is the deepest common ancestor.
        for (int a = nodes[0]; a >= 0; a = model.Bones[a].ParentIndex)
        {
            bool common = true;
            foreach (var n in nodes)
                if (!IsAncestorOrSelf(model, a, n)) { common = false; break; }
            if (common) return a;
        }
        return -1;
    }

    private static bool IsAncestorOrSelf(PmxModel model, int ancestor, int node)
    {
        for (int n = node; n >= 0; n = model.Bones[n].ParentIndex)
            if (n == ancestor) return true;
        return false;
    }

    private static bool TryResolve(PmxBone bone, out VrmHumanBone human)
    {
        if (!string.IsNullOrEmpty(bone.NameLocal) && BoneNameDictionary.TryGet(bone.NameLocal, out human))
            return true;
        if (!string.IsNullOrEmpty(bone.NameUniversal) && BoneNameDictionary.TryGet(bone.NameUniversal, out human))
            return true;
        human = default;
        return false;
    }

    /// <summary>Required humanoid bones that the model did not provide.</summary>
    public static IReadOnlyList<VrmHumanBone> MissingRequired(IReadOnlyDictionary<VrmHumanBone, int> mapping) =>
        VrmHumanBoneExtensions.Required.Where(b => !mapping.ContainsKey(b)).ToList();
}

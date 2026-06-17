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
        RemapToDeformBones(model, result);
        return result;
    }

    /// <summary>
    /// Re-target humanoid slots onto the bone that actually carries the mesh.
    /// MMD rigs often skin the body to "D" deform bones that copy an FK control
    /// bone via append/grant (付与) rotation, leaving the FK bone (the one matched
    /// by name) with zero weights. Mapping humanoid to the empty FK bone means
    /// Unity animates a bone with no mesh, so e.g. the legs never deform. For
    /// each humanoid bone whose matched bone has no weights, switch to the
    /// mesh-bearing append-copy (and, for leaf bones like toes, a mesh-bearing
    /// descendant of the already-remapped parent).
    /// </summary>
    private static void RemapToDeformBones(PmxModel model, Dictionary<VrmHumanBone, int> map)
    {
        var vtx = new int[model.Bones.Count];
        foreach (var v in model.Vertices)
            foreach (var w in v.Weights)
                if (w.Weight > 0f && (uint)w.BoneIndex < vtx.Length) vtx[w.BoneIndex]++;

        // d copies bone i's rotation via append/grant at (near) full weight.
        var twins = new Dictionary<int, List<int>>();
        for (int d = 0; d < model.Bones.Count; d++)
        {
            var b = model.Bones[d];
            if (b.HasFlag(PmxBoneFlags.InheritRotation) && b.InheritParentIndex >= 0 && b.InheritWeight > 0.5f)
                (twins.TryGetValue(b.InheritParentIndex, out var l) ? l : twins[b.InheritParentIndex] = new()).Add(d);
        }

        // Pass 1: FK control bone -> its mesh-bearing append twin.
        foreach (var h in map.Keys.ToList())
        {
            int fk = map[h];
            if (!twins.TryGetValue(fk, out var cands)) continue;
            int best = fk, bestVtx = vtx[fk];
            foreach (var d in cands)
                if (vtx[d] > bestVtx) { best = d; bestVtx = vtx[d]; }
            if (best != fk) map[h] = best;
        }

        // Pass 2: leaf bones (toes) with no twin -> the mesh-bearing descendant
        // of the already-remapped parent (e.g. toe deform bone under foot-D).
        foreach (var (leaf, parent) in new[]
        {
            (VrmHumanBone.LeftToes, VrmHumanBone.LeftFoot),
            (VrmHumanBone.RightToes, VrmHumanBone.RightFoot),
        })
        {
            if (!map.TryGetValue(leaf, out var cur) || vtx[cur] > 0) continue;
            if (!map.TryGetValue(parent, out var p)) continue;
            int best = -1, bestVtx = 0;
            for (int i = 0; i < model.Bones.Count; i++)
                if (vtx[i] > bestVtx && !map.ContainsValue(i) && IsAncestorOrSelf(model, p, i) && i != p)
                { best = i; bestVtx = vtx[i]; }
            if (best >= 0) map[leaf] = best;
        }
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

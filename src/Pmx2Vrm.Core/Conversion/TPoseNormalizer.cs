using System.Numerics;
using Pmx2Vrm.Core.Vrm;

namespace Pmx2Vrm.Core.Conversion;

/// <summary>
/// Re-poses an A-pose MMD skeleton into the T-pose that VRM expects, baking
/// the change into the rest pose. MMD arms hang down (~A-pose); VRM/Unity
/// humanoid retargeting assumes arms are horizontal, so idle animations push the
/// arms into the body otherwise.
///
/// The arm bones (upper/lower) are rotated about their joints so each segment
/// points straight out along ±X. The accumulated per-bone world rotation and new
/// world position are exposed so the mesh vertices, node transforms and inverse
/// bind matrices can all be re-derived consistently.
/// </summary>
public sealed class TPoseNormalizer
{
    private readonly ConvertedSkeleton _skel;
    public Quaternion[] AccRot { get; }
    public Vector3[] NewWorldPos { get; }
    public Vector3[] OrigWorldPos { get; }

    private readonly VrmVersion _version;

    public TPoseNormalizer(ConvertedSkeleton skeleton, bool enabled = true, VrmVersion version = VrmVersion.Vrm0)
    {
        _skel = skeleton;
        _version = version;
        int n = skeleton.Nodes.Count;
        AccRot = new Quaternion[n];
        NewWorldPos = new Vector3[n];
        OrigWorldPos = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            AccRot[i] = Quaternion.Identity;
            OrigWorldPos[i] = skeleton.Nodes[i].WorldPosition;
            NewWorldPos[i] = OrigWorldPos[i];
        }

        if (enabled) Build();
    }

    private void Build()
    {
        // Bone -> the child whose segment we straighten, and the target axis.
        var straighten = new Dictionary<int, (int child, Vector3 target)>();
        void Pair(VrmHumanBone a, VrmHumanBone b, Vector3 target)
        {
            if (_skel.Humanoid.TryGetValue(a, out var ai) && _skel.Humanoid.TryGetValue(b, out var bi))
                straighten[ai] = (bi, target);
        }
        // Arms straight out along ±X, in glTF space (Z-reflected from MMD). MMD's
        // 左 (left) bones keep their +X side after Z-reflection, so the left arm
        // extends to +X here; UniVRM's import flips X, landing it on the model's
        // left (-X) in Unity — matching the reference.
        Pair(VrmHumanBone.LeftUpperArm, VrmHumanBone.LeftLowerArm, new Vector3(1, 0, 0));
        Pair(VrmHumanBone.LeftLowerArm, VrmHumanBone.LeftHand, new Vector3(1, 0, 0));
        Pair(VrmHumanBone.RightUpperArm, VrmHumanBone.RightLowerArm, new Vector3(-1, 0, 0));
        Pair(VrmHumanBone.RightLowerArm, VrmHumanBone.RightHand, new Vector3(-1, 0, 0));
        // Legs are kept nearly vertical (-Y) with only a SLIGHT forward knee
        // bend. Unity's foot-IK solver needs the knee clearly forward to know
        // which way to flex, but a large bend (was 0.5 ≈ 27°) looks like a squat
        // AND pushes the feet ahead of the body. So bend the thigh forward by a
        // gentle ~8° and lean the shin back by the same amount, which keeps the
        // ankle under the hip while still giving IK its hint.
        // Forward sign is version-specific: VRM 0.x is imported with ReverseZ,
        // so the knee must lean toward -Z in glTF to end up forward (+Z) in Unity;
        // VRM 1.0 (ReverseX) keeps Z, so it leans toward +Z.
        const float kneeBias = 0.14f; // tan(angle); ~8° from vertical
        float fwd = _version == VrmVersion.Vrm0 ? -1f : 1f;
        var thigh = Vector3.Normalize(new Vector3(0, -1, fwd * kneeBias));
        var shin = Vector3.Normalize(new Vector3(0, -1, -fwd * kneeBias));
        Pair(VrmHumanBone.LeftUpperLeg, VrmHumanBone.LeftLowerLeg, thigh);
        Pair(VrmHumanBone.LeftLowerLeg, VrmHumanBone.LeftFoot, shin);
        Pair(VrmHumanBone.RightUpperLeg, VrmHumanBone.RightLowerLeg, thigh);
        Pair(VrmHumanBone.RightLowerLeg, VrmHumanBone.RightFoot, shin);

        if (straighten.Count == 0) return; // nothing to do (no arms mapped)

        foreach (int b in BonesByDepth())
        {
            int parent = _skel.Nodes[b].ParentIndex;
            var inherited = parent >= 0 ? AccRot[parent] : Quaternion.Identity;

            NewWorldPos[b] = parent >= 0
                ? NewWorldPos[parent] + Vector3.Transform(OrigWorldPos[b] - OrigWorldPos[parent], inherited)
                : OrigWorldPos[b];

            if (straighten.TryGetValue(b, out var s))
            {
                var dir = Vector3.Transform(OrigWorldPos[s.child] - OrigWorldPos[b], inherited);
                var delta = FromToRotation(dir, s.target);
                AccRot[b] = delta * inherited;
            }
            else
            {
                AccRot[b] = inherited;
            }
        }
    }

    /// <summary>Bone indices ordered so every parent precedes its children.</summary>
    private IEnumerable<int> BonesByDepth()
    {
        int n = _skel.Nodes.Count;
        var depth = new int[n];
        for (int i = 0; i < n; i++)
        {
            int d = 0;
            for (int p = _skel.Nodes[i].ParentIndex; p >= 0; p = _skel.Nodes[p].ParentIndex) d++;
            depth[i] = d;
        }
        return Enumerable.Range(0, n).OrderBy(i => depth[i]);
    }

    /// <summary>Skinned re-pose of a vertex position from old rest to new rest.</summary>
    public Vector3 BakePosition(Vector3 pos, ReadOnlySpan<ushort> joints, Vector4 weights)
    {
        var acc = Vector3.Zero;
        Span<float> w = stackalloc float[4] { weights.X, weights.Y, weights.Z, weights.W };
        for (int k = 0; k < 4; k++)
        {
            if (w[k] <= 0f) continue;
            int b = joints[k];
            acc += w[k] * (NewWorldPos[b] + Vector3.Transform(pos - OrigWorldPos[b], AccRot[b]));
        }
        return acc;
    }

    public Vector3 BakeNormal(Vector3 normal, ReadOnlySpan<ushort> joints, Vector4 weights)
    {
        var acc = Vector3.Zero;
        Span<float> w = stackalloc float[4] { weights.X, weights.Y, weights.Z, weights.W };
        for (int k = 0; k < 4; k++)
        {
            if (w[k] <= 0f) continue;
            acc += w[k] * Vector3.Transform(normal, AccRot[joints[k]]);
        }
        return acc.LengthSquared() < 1e-12f ? normal : Vector3.Normalize(acc);
    }

    /// <summary>New parent-relative local translation and rotation for a bone node.</summary>
    public (Vector3 translation, Quaternion rotation) LocalTransform(int bone)
    {
        // Bones keep IDENTITY rest rotation: the T-pose is carried by the bone
        // positions (NewWorldPos) and the baked mesh. Baking the T-pose rotation
        // into the bones leaves non-canonical rest rotations whose descendants
        // make Unity's AvatarBuilder emit ValidTRS failures. With identity
        // rotation the local translation is just the T-pose offset to the parent.
        int parent = _skel.Nodes[bone].ParentIndex;
        var localTrans = parent < 0 ? NewWorldPos[bone] : NewWorldPos[bone] - NewWorldPos[parent];
        return (localTrans, Quaternion.Identity);
    }

    public Matrix4x4 InverseBind(int bone)
    {
        // Rest transform is translation-only (identity rotation), so the inverse
        // bind matrix is the inverse of the bone's T-pose translation. This is
        // consistent with LocalTransform (identity rotation): IBM * restTransform
        // = I, so the baked T-pose mesh renders as-is at rest.
        return Matrix4x4.Invert(Matrix4x4.CreateTranslation(NewWorldPos[bone]), out var inv)
            ? inv : Matrix4x4.Identity;
    }

    private static Quaternion FromToRotation(Vector3 from, Vector3 to)
    {
        if (from.LengthSquared() < 1e-12f || to.LengthSquared() < 1e-12f) return Quaternion.Identity;
        from = Vector3.Normalize(from);
        to = Vector3.Normalize(to);
        float d = Vector3.Dot(from, to);
        if (d >= 0.99999f) return Quaternion.Identity;
        if (d <= -0.99999f)
        {
            var axis = Vector3.Cross(Vector3.UnitY, from);
            if (axis.LengthSquared() < 1e-6f) axis = Vector3.Cross(Vector3.UnitX, from);
            return Quaternion.CreateFromAxisAngle(Vector3.Normalize(axis), MathF.PI);
        }
        var a = Vector3.Normalize(Vector3.Cross(from, to));
        return Quaternion.CreateFromAxisAngle(a, MathF.Acos(Math.Clamp(d, -1f, 1f)));
    }
}

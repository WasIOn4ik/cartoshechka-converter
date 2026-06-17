using System.Numerics;
using Pmx2Vrm.Core.Vrm;

namespace Pmx2Vrm.Core.Conversion;

/// <summary>
/// Builds a small, curated set of spring-bone colliders along the humanoid
/// skeleton — capsules down the torso and limbs plus a head sphere — the way
/// stable VRoid exports do.
///
/// MMD models ship dozens of small rigid bodies on nearly every bone; dumping
/// all of them as colliders made cloth fight a thicket of capsules and jitter.
/// A handful of body-sized capsules instead gives hair/skirt something clean to
/// drape over without the chaos.
///
/// Colliders are emitted in each bone node's local space. The bone rest pose is
/// rotation-free (see <see cref="TPoseNormalizer.LocalTransform"/>), so a node's
/// local axes equal world axes and an offset is simply the target world position
/// minus the node's world position.
/// </summary>
public static class BodyColliders
{
    /// <param name="humanoid">VRM humanoid bone → glTF node index.</param>
    /// <param name="worldPos">Final (T-posed, grounded) world position per node.</param>
    public static List<SpringColliderDef> Build(
        IReadOnlyDictionary<VrmHumanBone, int> humanoid, Vector3[] worldPos)
    {
        var list = new List<SpringColliderDef>();
        if (humanoid.Count == 0) return list;

        // Radii are tuned for a ~1.5 m model and scaled by the actual height so
        // taller/shorter rigs get proportionate body capsules.
        float minY = float.MaxValue, maxY = float.MinValue;
        foreach (int n in humanoid.Values)
        {
            if (n < 0 || n >= worldPos.Length) continue;
            minY = MathF.Min(minY, worldPos[n].Y);
            maxY = MathF.Max(maxY, worldPos[n].Y);
        }
        float height = maxY > minY ? maxY - minY : 1.5f;
        float s = MathF.Max(height, 0.1f) / 1.5f;

        bool Has(VrmHumanBone b) => humanoid.TryGetValue(b, out int n) && n >= 0 && n < worldPos.Length;

        void Capsule(VrmHumanBone from, VrmHumanBone to, float radius)
        {
            if (!Has(from) || !Has(to)) return;
            int a = humanoid[from], b = humanoid[to];
            list.Add(new SpringColliderDef
            {
                NodeIndex = a,
                Shape = SpringColliderShape.Capsule,
                Offset = Vector3.Zero,
                Radius = radius * s,
                TailOffset = worldPos[b] - worldPos[a],
            });
        }

        void Sphere(VrmHumanBone bone, Vector3 offset, float radius)
        {
            if (!Has(bone)) return;
            list.Add(new SpringColliderDef
            {
                NodeIndex = humanoid[bone],
                Shape = SpringColliderShape.Sphere,
                Offset = offset * s,
                Radius = radius * s,
            });
        }

        // Torso: hips → spine → (upper torso) and, when a distinct chest exists,
        // chest → neck, so the trunk is covered top to bottom. Radii are kept
        // snug to the body — a fat hip/torso capsule shoves the skirt outward and
        // rides it up, so the trunk colliders sit just inside the silhouette.
        Capsule(VrmHumanBone.Hips, VrmHumanBone.Spine, 0.065f);
        VrmHumanBone upper =
            Has(VrmHumanBone.Chest) ? VrmHumanBone.Chest :
            Has(VrmHumanBone.UpperChest) ? VrmHumanBone.UpperChest :
            Has(VrmHumanBone.Neck) ? VrmHumanBone.Neck : VrmHumanBone.Head;
        Capsule(VrmHumanBone.Spine, upper, 0.06f);
        if (Has(VrmHumanBone.Chest) && Has(VrmHumanBone.Neck))
            Capsule(VrmHumanBone.Chest, VrmHumanBone.Neck, 0.05f);

        // Head: a sphere nudged up from the head bone.
        Sphere(VrmHumanBone.Head, new Vector3(0, 0.07f, 0), 0.08f);

        // Arms (thin — sleeves cling close).
        Capsule(VrmHumanBone.LeftUpperArm, VrmHumanBone.LeftLowerArm, 0.035f);
        Capsule(VrmHumanBone.LeftLowerArm, VrmHumanBone.LeftHand, 0.03f);
        Capsule(VrmHumanBone.RightUpperArm, VrmHumanBone.RightLowerArm, 0.035f);
        Capsule(VrmHumanBone.RightLowerArm, VrmHumanBone.RightHand, 0.03f);

        // Legs (kept thin so a skirt drapes past them instead of being pushed up).
        Capsule(VrmHumanBone.LeftUpperLeg, VrmHumanBone.LeftLowerLeg, 0.05f);
        Capsule(VrmHumanBone.LeftLowerLeg, VrmHumanBone.LeftFoot, 0.04f);
        Capsule(VrmHumanBone.RightUpperLeg, VrmHumanBone.RightLowerLeg, 0.05f);
        Capsule(VrmHumanBone.RightLowerLeg, VrmHumanBone.RightFoot, 0.04f);

        return list;
    }
}

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
    /// <param name="forwardZ">
    /// Sign of the model's facing direction in glTF +Z: +1 for VRM 0.x (faces +Z),
    /// -1 for VRM 1.0 (faces -Z). The hips capsule is nudged this way so it sits
    /// on the (forward) pelvis rather than engulfing the back skirt that hangs
    /// behind it. 0 disables the nudge.
    /// </param>
    public static List<SpringColliderDef> Build(
        IReadOnlyDictionary<VrmHumanBone, int> humanoid, Vector3[] worldPos, float forwardZ = 0f)
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

        void Capsule(VrmHumanBone from, VrmHumanBone to, float radius, Vector3 shift = default)
        {
            if (!Has(from) || !Has(to)) return;
            int a = humanoid[from], b = humanoid[to];
            list.Add(new SpringColliderDef
            {
                NodeIndex = a,
                Shape = SpringColliderShape.Capsule,
                Offset = shift,
                Radius = radius * s,
                TailOffset = (worldPos[b] - worldPos[a]) + shift,
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
        // The hip bone sits forward of the pelvis, so the back skirt hangs behind
        // it. A hips capsule centred on the bone engulfs the upper back-skirt
        // segments (even allowing for their hit radius) and ejects them up-and-back
        // ("standing on end"). Keep the radius moderate AND nudge the capsule
        // toward the model's front so its back surface clears the back skirt while
        // the front/sides still drape over it.
        Capsule(VrmHumanBone.Hips, VrmHumanBone.Spine, 0.095f, new Vector3(0, 0, forwardZ * 0.035f * s));
        VrmHumanBone upper =
            Has(VrmHumanBone.Chest) ? VrmHumanBone.Chest :
            Has(VrmHumanBone.UpperChest) ? VrmHumanBone.UpperChest :
            Has(VrmHumanBone.Neck) ? VrmHumanBone.Neck : VrmHumanBone.Head;
        // The spine capsule's lower end is right where the skirt attaches at the
        // waist; nudge it forward too (less than the hips) so its back surface
        // clears the back-skirt root.
        Capsule(VrmHumanBone.Spine, upper, 0.075f, new Vector3(0, 0, forwardZ * 0.025f * s));
        if (Has(VrmHumanBone.Chest) && Has(VrmHumanBone.Neck))
            Capsule(VrmHumanBone.Chest, VrmHumanBone.Neck, 0.065f);

        // Head: a sphere nudged up from the head bone.
        Sphere(VrmHumanBone.Head, new Vector3(0, 0.07f, 0), 0.09f);

        // Arms.
        Capsule(VrmHumanBone.LeftUpperArm, VrmHumanBone.LeftLowerArm, 0.04f);
        Capsule(VrmHumanBone.LeftLowerArm, VrmHumanBone.LeftHand, 0.035f);
        Capsule(VrmHumanBone.RightUpperArm, VrmHumanBone.RightLowerArm, 0.04f);
        Capsule(VrmHumanBone.RightLowerArm, VrmHumanBone.RightHand, 0.035f);

        // Legs (kept a touch slimmer than the body so a skirt still drapes past
        // them rather than being shoved up).
        Capsule(VrmHumanBone.LeftUpperLeg, VrmHumanBone.LeftLowerLeg, 0.083f);
        Capsule(VrmHumanBone.LeftLowerLeg, VrmHumanBone.LeftFoot, 0.05f);
        Capsule(VrmHumanBone.RightUpperLeg, VrmHumanBone.RightLowerLeg, 0.083f);
        Capsule(VrmHumanBone.RightLowerLeg, VrmHumanBone.RightFoot, 0.05f);

        return list;
    }

    public static void Report(IReadOnlyList<SpringColliderDef> colliders, Func<int, string> nodeName, Action<string> log)
    {
        if (colliders.Count == 0) { log("spring-bone colliders: none"); return; }
        var groups = colliders.GroupBy(c => c.NodeIndex).ToList();
        log($"spring-bone colliders [{colliders.Count} total, {groups.Count} bones]");
        log("  bone                    r(m)   n  type");
        log("  ----------------------  -----  -  ----");
        foreach (var g in groups)
        {
            string name = nodeName(g.Key);
            float r = g.First().Radius;
            string type = g.First().Shape == SpringColliderShape.Capsule ? "caps" : "sph";
            log($"  {name,-22}  {r:F3}  {g.Count()}  {type}");
        }
    }
}

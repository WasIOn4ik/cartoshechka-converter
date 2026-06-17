using System.Numerics;
using Pmx2Vrm.Core.Gltf;
using Pmx2Vrm.Core.Vrm;

namespace Pmx2Vrm.Core.Conversion;

/// <summary>
/// Builds the VRM JSON extension blocks (both 0.x and 1.0) from the converted
/// intermediate data and injects them into a <see cref="GltfRoot"/>.
/// </summary>
public sealed class VrmExtensionBuilder
{
    public sealed class Inputs
    {
        public required VrmMeta Meta { get; init; }
        public required IReadOnlyDictionary<VrmHumanBone, int> Humanoid { get; init; }
        public required IReadOnlyList<ExpressionDef> Expressions { get; init; }
        public required ConvertedPhysics Physics { get; init; }
        public required IReadOnlyList<MToonMaterialDef> Materials { get; init; }
        public required int MeshNodeIndex { get; init; }
        public required int MeshIndex { get; init; }
    }

    public void Apply(GltfRoot root, VrmVersion version, Inputs input)
    {
        if (version == VrmVersion.Vrm1) ApplyVrm1(root, input);
        else ApplyVrm0(root, input);
    }

    // ===================== VRM 1.0 =========================================

    private void ApplyVrm1(GltfRoot root, Inputs i)
    {
        root.ExtensionsUsed.Add("VRMC_vrm");

        var vrm = new Dictionary<string, object>
        {
            ["specVersion"] = "1.0",
            ["meta"] = Vrm1Meta(i.Meta),
            ["humanoid"] = new Dictionary<string, object> { ["humanBones"] = Vrm1HumanBones(i.Humanoid) },
            ["expressions"] = Vrm1Expressions(i.Expressions, i.MeshNodeIndex),
        };
        root.Extensions["VRMC_vrm"] = vrm;

        ApplyMToonVrm1(root, i.Materials);
        ApplySpringVrm1(root, i.Physics);
    }

    private static Dictionary<string, object> Vrm1Meta(VrmMeta m)
    {
        var meta = new Dictionary<string, object>
        {
            ["name"] = m.Title,
            ["version"] = m.Version,
            ["authors"] = new[] { m.Author },
            ["avatarPermission"] = m.AvatarPermission,
            ["allowExcessivelyViolentUsage"] = m.AllowExcessivelyViolentUsage,
            ["allowExcessivelySexualUsage"] = m.AllowExcessivelySexualUsage,
            ["commercialUsage"] = m.CommercialUsage,
            ["creditNotation"] = m.CreditNotation,
            ["allowRedistribution"] = m.AllowRedistribution,
            ["modification"] = m.Modification,
            ["licenseUrl"] = "https://vrm.dev/licenses/1.0/",
        };
        if (!string.IsNullOrEmpty(m.ContactInformation)) meta["contactInformation"] = m.ContactInformation!;
        if (!string.IsNullOrEmpty(m.Reference)) meta["references"] = new[] { m.Reference! };
        return meta;
    }

    private static Dictionary<string, object> Vrm1HumanBones(IReadOnlyDictionary<VrmHumanBone, int> humanoid)
    {
        var bones = new Dictionary<string, object>();
        foreach (var (bone, node) in humanoid)
            bones[bone.ToVrm10Key()] = new Dictionary<string, object> { ["node"] = node };
        return bones;
    }

    private static Dictionary<string, object> Vrm1Expressions(IReadOnlyList<ExpressionDef> exprs, int meshNode)
    {
        var preset = new Dictionary<string, object>();
        var custom = new Dictionary<string, object>();

        foreach (var e in exprs)
        {
            var binds = e.Binds.Select(b => (object)new Dictionary<string, object>
            {
                ["node"] = meshNode,
                ["index"] = b.MorphTargetIndex,
                ["weight"] = b.Weight,
            }).ToArray();

            var entry = new Dictionary<string, object>
            {
                ["isBinary"] = false,
                ["morphTargetBinds"] = binds,
            };

            if (e.Preset is { } p) preset[p.ToVrm10Key()] = entry;
            else custom[e.Name] = entry;
        }

        return new Dictionary<string, object> { ["preset"] = preset, ["custom"] = custom };
    }

    private static void ApplyMToonVrm1(GltfRoot root, IReadOnlyList<MToonMaterialDef> mats)
    {
        if (mats.Count == 0) return;
        root.ExtensionsUsed.Add("VRMC_materials_mtoon");

        for (int idx = 0; idx < mats.Count && idx < root.Materials.Count; idx++)
        {
            var d = mats[idx];
            var mtoon = new Dictionary<string, object>
            {
                ["specVersion"] = "1.0",
                ["transparentWithZWrite"] = false,
                ["renderQueueOffsetNumber"] = 0,
                ["shadeColorFactor"] = Rgb(d.ShadeColor),
                ["shadingShiftFactor"] = -0.05f,
                ["shadingToonyFactor"] = 0.95f,
                ["giEqualizationFactor"] = 0.9f,
                ["outlineWidthMode"] = d.OutlineMode switch
                {
                    MToonOutlineMode.World => "worldCoordinates",
                    MToonOutlineMode.Screen => "screenCoordinates",
                    _ => "none",
                },
                ["outlineWidthFactor"] = d.OutlineWidth,
                ["outlineColorFactor"] = Rgb(d.OutlineColor),
            };
            if (d.ShadeMultiplyTexture >= 0)
                mtoon["shadeMultiplyTexture"] = new Dictionary<string, object> { ["index"] = d.ShadeMultiplyTexture };
            if (d.SphereTexture >= 0 && d.SphereMode == MToonSphereMode.AdditiveMatcap)
            {
                mtoon["matcapFactor"] = new[] { 1f, 1f, 1f };
                mtoon["matcapTexture"] = new Dictionary<string, object> { ["index"] = d.SphereTexture };
            }

            (root.Materials[idx].Extensions ??= new())["VRMC_materials_mtoon"] = mtoon;
        }
    }

    private static void ApplySpringVrm1(GltfRoot root, ConvertedPhysics physics)
    {
        if (physics.Chains.Count == 0 && physics.Colliders.Count == 0) return;
        root.ExtensionsUsed.Add("VRMC_springBone");

        var colliders = physics.Colliders.Select(c =>
        {
            var shape = c.Shape == SpringColliderShape.Capsule
                ? new Dictionary<string, object>
                {
                    ["capsule"] = new Dictionary<string, object>
                    {
                        ["offset"] = Xyz(c.Offset),
                        ["radius"] = c.Radius,
                        ["tail"] = Xyz(c.TailOffset),
                    },
                }
                : new Dictionary<string, object>
                {
                    ["sphere"] = new Dictionary<string, object> { ["offset"] = Xyz(c.Offset), ["radius"] = c.Radius },
                };
            return (object)new Dictionary<string, object> { ["node"] = c.NodeIndex, ["shape"] = shape };
        }).ToArray();

        var groups = new List<object>();
        var groupRefs = new List<int>();
        if (colliders.Length > 0)
        {
            groups.Add(new Dictionary<string, object>
            {
                ["name"] = "colliders",
                ["colliders"] = Enumerable.Range(0, colliders.Length).ToArray(),
            });
            groupRefs.Add(0);
        }

        var springs = physics.Chains.Select(ch =>
        {
            var spring = new Dictionary<string, object>
            {
                ["name"] = ch.Name,
                ["joints"] = ch.Joints.Select(j => (object)new Dictionary<string, object>
                {
                    ["node"] = j.NodeIndex,
                    ["hitRadius"] = j.HitRadius,
                    ["stiffness"] = ch.Stiffness,
                    ["gravityPower"] = ch.GravityPower,
                    ["gravityDir"] = Xyz(ch.GravityDir),
                    ["dragForce"] = ch.DragForce,
                }).ToArray(),
                ["colliderGroups"] = groupRefs.ToArray(),
            };
            if (ch.Center >= 0) spring["center"] = ch.Center;
            return (object)spring;
        }).ToArray();

        root.Extensions["VRMC_springBone"] = new Dictionary<string, object>
        {
            ["specVersion"] = "1.0",
            ["colliders"] = colliders,
            ["colliderGroups"] = groups.ToArray(),
            ["springs"] = springs,
        };
    }

    // ===================== VRM 0.x =========================================

    private void ApplyVrm0(GltfRoot root, Inputs i)
    {
        root.ExtensionsUsed.Add("VRM");

        var vrm = new Dictionary<string, object>
        {
            ["exporterVersion"] = "Pmx2Vrm",
            ["specVersion"] = "0.0",
            ["meta"] = Vrm0Meta(i.Meta),
            ["humanoid"] = new Dictionary<string, object> { ["humanBones"] = Vrm0HumanBones(i.Humanoid) },
            ["firstPerson"] = new Dictionary<string, object> { ["firstPersonBone"] = HeadNode(i.Humanoid) },
            ["blendShapeMaster"] = new Dictionary<string, object> { ["blendShapeGroups"] = Vrm0BlendShapes(i.Expressions, i.MeshIndex) },
            ["secondaryAnimation"] = Vrm0Secondary(i.Physics),
            ["materialProperties"] = Vrm0Materials(i.Materials),
        };
        root.Extensions["VRM"] = vrm;
    }

    private static Dictionary<string, object> Vrm0Meta(VrmMeta m) => new()
    {
        ["title"] = m.Title,
        ["author"] = m.Author,
        ["version"] = m.Version,
        ["contactInformation"] = m.ContactInformation ?? "",
        ["reference"] = m.Reference ?? "",
        ["allowedUserName"] = "OnlyAuthor",
        ["violentUssageName"] = m.AllowExcessivelyViolentUsage ? "Allow" : "Disallow",
        ["sexualUssageName"] = m.AllowExcessivelySexualUsage ? "Allow" : "Disallow",
        ["commercialUssageName"] = m.CommercialUsage == "personalNonProfit" ? "Disallow" : "Allow",
        ["licenseName"] = m.AllowRedistribution ? "Redistribution_Prohibited" : "Redistribution_Prohibited",
    };

    private static object[] Vrm0HumanBones(IReadOnlyDictionary<VrmHumanBone, int> humanoid) =>
        humanoid.Select(kv => (object)new Dictionary<string, object>
        {
            ["bone"] = kv.Key.ToVrm0Key(),
            ["node"] = kv.Value,
        }).ToArray();

    private static int HeadNode(IReadOnlyDictionary<VrmHumanBone, int> humanoid) =>
        humanoid.TryGetValue(VrmHumanBone.Head, out var n) ? n : 0;

    private static object[] Vrm0BlendShapes(IReadOnlyList<ExpressionDef> exprs, int meshIndex) =>
        exprs.Select(e => (object)new Dictionary<string, object>
        {
            ["name"] = e.Preset?.ToString() ?? e.Name,
            ["presetName"] = e.Preset?.ToVrm0Key() ?? "unknown",
            ["isBinary"] = false,
            ["binds"] = e.Binds.Select(b => (object)new Dictionary<string, object>
            {
                ["mesh"] = meshIndex,
                ["index"] = b.MorphTargetIndex,
                ["weight"] = b.Weight * 100f, // VRM 0.x weights are 0..100
            }).ToArray(),
            ["materialValues"] = Array.Empty<object>(),
        }).ToArray();

    private static Dictionary<string, object> Vrm0Secondary(ConvertedPhysics physics)
    {
        // VRM 0.x secondaryAnimation colliders are spheres only, so a capsule is
        // approximated by a short string of spheres laid along its axis.
        var colliderGroups = physics.Colliders
            .GroupBy(c => c.NodeIndex)
            .Select(g => (object)new Dictionary<string, object>
            {
                ["node"] = g.Key,
                ["colliders"] = g.SelectMany(Vrm0Spheres).ToArray(),
            }).ToArray();

        // VRM 0.x grows a verlet chain from each root in `bones`, following bone
        // children. List only top-level swaying roots; the engine grows each into
        // its full child tree (branches included), so listing a branch too would
        // make that bone swing twice and jitter.
        var boneGroups = physics.Roots
            .Select(ch => (object)new Dictionary<string, object>
            {
                ["comment"] = ch.Name,
                ["stiffiness"] = ch.Stiffness, // (sic) VRM 0.x spelling
                ["gravityPower"] = ch.GravityPower,
                ["gravityDir"] = XyzObj(ch.GravityDir),
                ["dragForce"] = ch.DragForce,
                ["center"] = ch.Center,
                ["hitRadius"] = ch.Joints.Count > 0 ? ch.Joints[^1].HitRadius : 0.02f,
                ["bones"] = new[] { ch.FirstDynamicNode },
                ["colliderGroups"] = colliderGroups.Length > 0 ? Enumerable.Range(0, colliderGroups.Length).ToArray() : Array.Empty<int>(),
            }).ToArray();

        return new Dictionary<string, object>
        {
            ["boneGroups"] = boneGroups,
            ["colliderGroups"] = colliderGroups,
        };
    }

    /// <summary>One sphere for a sphere collider; several along the axis for a capsule.</summary>
    private static IEnumerable<object> Vrm0Spheres(SpringColliderDef c)
    {
        if (c.Shape != SpringColliderShape.Capsule)
        {
            yield return new Dictionary<string, object> { ["offset"] = XyzObj(c.Offset), ["radius"] = c.Radius };
            yield break;
        }

        // Walk from Offset to TailOffset, spacing spheres about one radius apart
        // so they overlap into a continuous capsule (capped at a small count).
        var axis = c.TailOffset - c.Offset;
        float len = axis.Length();
        int steps = Math.Clamp((int)MathF.Ceiling(len / MathF.Max(c.Radius, 1e-3f)), 1, 6);
        for (int k = 0; k <= steps; k++)
        {
            var p = c.Offset + axis * (k / (float)steps);
            yield return new Dictionary<string, object> { ["offset"] = XyzObj(p), ["radius"] = c.Radius };
        }
    }

    private static object[] Vrm0Materials(IReadOnlyList<MToonMaterialDef> mats) =>
        mats.Select(d =>
        {
            var floatProps = new Dictionary<string, object>
            {
                ["_BlendMode"] = d.AlphaMode == MToonAlphaMode.Blend ? 2f : d.AlphaMode == MToonAlphaMode.Mask ? 1f : 0f,
                ["_Cutoff"] = d.AlphaCutoff,
                ["_OutlineWidthMode"] = d.OutlineMode == MToonOutlineMode.None ? 0f : 1f,
                ["_OutlineWidth"] = d.OutlineWidth * 100f,
                ["_ShadeShift"] = -0.05f,
                ["_ShadeToony"] = 0.95f,
            };
            var vectorProps = new Dictionary<string, object>
            {
                ["_Color"] = Rgba(d.BaseColor),
                ["_ShadeColor"] = new[] { d.ShadeColor.X, d.ShadeColor.Y, d.ShadeColor.Z, 1f },
                ["_OutlineColor"] = new[] { d.OutlineColor.X, d.OutlineColor.Y, d.OutlineColor.Z, 1f },
                ["_EmissionColor"] = new[] { d.EmissiveColor.X, d.EmissiveColor.Y, d.EmissiveColor.Z, 1f },
            };
            var textureProps = new Dictionary<string, object>();
            if (d.BaseColorTexture >= 0) textureProps["_MainTex"] = d.BaseColorTexture;
            if (d.ShadeMultiplyTexture >= 0) textureProps["_ShadeTexture"] = d.ShadeMultiplyTexture;
            if (d.SphereTexture >= 0) textureProps["_SphereAdd"] = d.SphereTexture;

            return (object)new Dictionary<string, object>
            {
                ["name"] = d.Name,
                ["shader"] = "VRM/MToon",
                ["renderQueue"] = 2000,
                ["floatProperties"] = floatProps,
                ["vectorProperties"] = vectorProps,
                ["textureProperties"] = textureProps,
                ["keywordMap"] = new Dictionary<string, object>(),
                ["tagMap"] = new Dictionary<string, object> { ["RenderType"] = d.AlphaMode == MToonAlphaMode.Blend ? "Transparent" : "Opaque" },
            };
        }).ToArray();

    // ---- helpers ----------------------------------------------------------

    private static float[] Rgb(Vector3 v) => new[] { v.X, v.Y, v.Z };
    private static float[] Rgba(Vector4 v) => new[] { v.X, v.Y, v.Z, v.W };
    private static float[] Xyz(Vector3 v) => new[] { v.X, v.Y, v.Z };
    private static Dictionary<string, object> XyzObj(Vector3 v) => new() { ["x"] = v.X, ["y"] = v.Y, ["z"] = v.Z };
}

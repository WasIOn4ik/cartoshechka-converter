using System.Numerics;
using Pmx2Vrm.Core.Gltf;
using Pmx2Vrm.Core.Mapping;
using Pmx2Vrm.Core.Pmx;
using Pmx2Vrm.Core.Vrm;

namespace Pmx2Vrm.Core.Conversion;

public sealed class ExpressionBind
{
    public required int MorphTargetIndex { get; init; }
    public required float Weight { get; init; }
}

public sealed class ExpressionDef
{
    public required string Name { get; init; }
    public VrmExpressionPreset? Preset { get; init; }
    public List<ExpressionBind> Binds { get; } = new();
}

public sealed class ConvertedMorphs
{
    public required IReadOnlyList<string> TargetNames { get; init; }
    public required IReadOnlyList<ExpressionDef> Expressions { get; init; }
}

/// <summary>
/// Converts PMX morphs into glTF morph targets and VRM expression definitions.
///
/// Vertex morphs become POSITION-delta morph targets shared by every primitive.
/// Group/flip morphs become expressions that bind several vertex-morph targets.
/// Bone / UV / material morphs are not representable as glTF morph targets and
/// are skipped (with a warning) — a documented limitation.
/// </summary>
public sealed class MorphConverter
{
    private readonly CoordinateConverter _coords;
    private readonly Action<string>? _warn;

    public MorphConverter(CoordinateConverter coords, Action<string>? warn = null)
    {
        _coords = coords;
        _warn = warn;
    }

    public ConvertedMorphs Convert(PmxModel model, ConvertedMesh mesh, BufferBuilder buffer)
    {
        var targetNames = new List<string>();
        // Blend-shape names must be unique: UniGLTF/Unity's AddBlendShapeFrame
        // throws ("frame weight must be greater than previous frame weight") when
        // two morph targets share a name. Track used names to disambiguate.
        var usedNames = new HashSet<string>(StringComparer.Ordinal);
        // PMX morph index -> glTF morph target index (vertex morphs only).
        var targetByMorph = new Dictionary<int, int>();

        for (int mi = 0; mi < model.Morphs.Count; mi++)
        {
            var morph = model.Morphs[mi];
            if (morph.Type != PmxMorphType.Vertex || morph.Vertex.Count == 0) continue;

            var entries = CollectDeltas(model.Vertices.Count, morph);
            if (entries.Count == 0) continue;

            int accessor = buffer.AddSparseVec3(model.Vertices.Count, entries);
            int targetIndex = targetNames.Count;
            targetByMorph[mi] = targetIndex;
            targetNames.Add(UniqueName(MorphName(morph), usedNames));

            foreach (var prim in mesh.Mesh.Primitives)
            {
                prim.Targets ??= new List<Dictionary<string, int>>();
                prim.Targets.Add(new Dictionary<string, int> { ["POSITION"] = accessor });
            }
        }

        mesh.Mesh.Weights = new float[targetNames.Count];
        if (targetNames.Count > 0)
            mesh.Mesh.Extras = new Dictionary<string, object> { ["targetNames"] = targetNames.ToArray() };

        var expressions = BuildExpressions(model, targetByMorph);
        return new ConvertedMorphs { TargetNames = targetNames, Expressions = expressions };
    }

    /// <summary>Return <paramref name="name"/>, suffixed if needed so it is unique.</summary>
    private static string UniqueName(string name, HashSet<string> used)
    {
        if (string.IsNullOrEmpty(name)) name = "morph";
        if (used.Add(name)) return name;
        for (int i = 2; ; i++)
        {
            var candidate = $"{name}_{i}";
            if (used.Add(candidate)) return candidate;
        }
    }

    /// <summary>
    /// Non-zero position deltas of a vertex morph, in glTF space. A delta
    /// transforms like a position (the conversion is linear), and the small,
    /// localised set is written as a sparse accessor.
    /// </summary>
    private List<(int, Vector3)> CollectDeltas(int vertexCount, PmxMorph morph)
    {
        var entries = new List<(int, Vector3)>(morph.Vertex.Count);
        foreach (var off in morph.Vertex)
        {
            if (off.VertexIndex < 0 || off.VertexIndex >= vertexCount) continue;
            entries.Add((off.VertexIndex, _coords.Position(off.Offset)));
        }
        return entries;
    }

    private List<ExpressionDef> BuildExpressions(PmxModel model, IReadOnlyDictionary<int, int> targetByMorph)
    {
        var result = new List<ExpressionDef>();

        for (int mi = 0; mi < model.Morphs.Count; mi++)
        {
            var morph = model.Morphs[mi];
            ExpressionDef? def = morph.Type switch
            {
                PmxMorphType.Vertex when targetByMorph.TryGetValue(mi, out var t) => Single(morph, t),
                PmxMorphType.Group or PmxMorphType.Flip => Grouped(morph, targetByMorph),
                PmxMorphType.Bone or PmxMorphType.Uv or PmxMorphType.UvExt1 or PmxMorphType.UvExt2
                    or PmxMorphType.UvExt3 or PmxMorphType.UvExt4 or PmxMorphType.Material
                    or PmxMorphType.Impulse => Warn(morph),
                _ => null,
            };
            if (def is { Binds.Count: > 0 }) result.Add(def);
        }

        return result;
    }

    private ExpressionDef Single(PmxMorph morph, int targetIndex)
    {
        var def = NewDef(morph);
        def.Binds.Add(new ExpressionBind { MorphTargetIndex = targetIndex, Weight = 1f });
        return def;
    }

    private ExpressionDef Grouped(PmxMorph morph, IReadOnlyDictionary<int, int> targetByMorph)
    {
        var def = NewDef(morph);
        foreach (var g in morph.Group)
        {
            if (targetByMorph.TryGetValue(g.MorphIndex, out var t))
                def.Binds.Add(new ExpressionBind { MorphTargetIndex = t, Weight = g.Influence });
        }
        return def;
    }

    private ExpressionDef? Warn(PmxMorph morph)
    {
        _warn?.Invoke($"Morph '{MorphName(morph)}' of type {morph.Type} is not convertible to a glTF morph target; skipped.");
        return null;
    }

    private static ExpressionDef NewDef(PmxMorph morph)
    {
        VrmExpressionPreset? preset = null;
        if (MorphNameDictionary.TryGet(morph.NameLocal, out var p) ||
            MorphNameDictionary.TryGet(morph.NameUniversal, out p))
            preset = p;

        return new ExpressionDef { Name = MorphName(morph), Preset = preset };
    }

    private static string MorphName(PmxMorph m) =>
        !string.IsNullOrWhiteSpace(m.NameUniversal) ? m.NameUniversal :
        !string.IsNullOrWhiteSpace(m.NameLocal) ? m.NameLocal : "morph";
}

using System.Numerics;
using Pmx2Vrm.Core.Gltf;
using Pmx2Vrm.Core.Pmx;

namespace Pmx2Vrm.Core.Conversion;

/// <summary>T-posed world position + skinning weights for one vertex, kept for collider fitting.</summary>
public readonly struct SkinVertex(Vector3 position, ushort j0, ushort j1, ushort j2, ushort j3,
    float w0, float w1, float w2, float w3)
{
    public readonly Vector3 Position = position;
    public readonly ushort J0 = j0, J1 = j1, J2 = j2, J3 = j3;
    public readonly float W0 = w0, W1 = w1, W2 = w2, W3 = w3;

    public float WeightOf(int nodeIdx) =>
        (J0 == nodeIdx ? W0 : 0f) + (J1 == nodeIdx ? W1 : 0f) +
        (J2 == nodeIdx ? W2 : 0f) + (J3 == nodeIdx ? W3 : 0f);
}

public sealed class ConvertedMesh
{
    public required GltfMesh Mesh { get; init; }
    public required int VertexCount { get; init; }
    /// <summary>glTF accessor index of POSITION, reused as the base for morph bounds.</summary>
    public required int PositionAccessor { get; init; }
    /// <summary>T-posed positions + bone weights, used by BodyColliders to fit radii to the mesh.</summary>
    public required SkinVertex[] SkinVertices { get; init; }
}

/// <summary>
/// Converts PMX geometry into a single glTF mesh whose primitives are split per
/// material. All primitives share one set of vertex-attribute accessors; each
/// material contributes its own (winding-reversed) index accessor.
/// </summary>
public sealed class MeshConverter
{
    private readonly CoordinateConverter _coords;

    public MeshConverter(CoordinateConverter coords) => _coords = coords;

    public ConvertedMesh Convert(PmxModel model, BufferBuilder buffer, TPoseNormalizer? tpose = null)
    {
        int n = model.Vertices.Count;
        var positions = new Vector3[n];
        var normals = new Vector3[n];
        var uvs = new Vector2[n];
        var joints = new (ushort, ushort, ushort, ushort)[n];
        var weights = new Vector4[n];
        var skinVerts = new SkinVertex[n];

        Span<ushort> j = stackalloc ushort[4];
        for (int i = 0; i < n; i++)
        {
            var v = model.Vertices[i];
            positions[i] = _coords.Position(v.Position);
            normals[i] = Vector3.Normalize(SafeDir(_coords.Direction(v.Normal)));
            uvs[i] = v.Uv; // PMX and glTF both use a top-left UV origin
            (joints[i], weights[i]) = ResolveSkin(v);

            if (tpose is not null)
            {
                j[0] = joints[i].Item1; j[1] = joints[i].Item2; j[2] = joints[i].Item3; j[3] = joints[i].Item4;
                positions[i] = tpose.BakePosition(positions[i], j, weights[i]);
                normals[i] = tpose.BakeNormal(normals[i], j, weights[i]);
            }

            skinVerts[i] = new SkinVertex(
                positions[i],
                joints[i].Item1, joints[i].Item2, joints[i].Item3, joints[i].Item4,
                weights[i].X, weights[i].Y, weights[i].Z, weights[i].W);
        }

        int posAcc = buffer.AddVec3(positions, computeBounds: true);
        int nrmAcc = buffer.AddVec3(normals, computeBounds: false);
        int uvAcc = buffer.AddVec2(uvs);
        int jointAcc = buffer.AddJoints(joints);
        int weightAcc = buffer.AddVec4(weights);

        var attributes = new Dictionary<string, int>
        {
            ["POSITION"] = posAcc,
            ["NORMAL"] = nrmAcc,
            ["TEXCOORD_0"] = uvAcc,
            ["JOINTS_0"] = jointAcc,
            ["WEIGHTS_0"] = weightAcc,
        };

        var mesh = new GltfMesh { Name = string.IsNullOrEmpty(model.NameUniversal) ? model.NameLocal : model.NameUniversal };

        int cursor = 0;
        for (int matIndex = 0; matIndex < model.Materials.Count; matIndex++)
        {
            int surfaceCount = model.Materials[matIndex].SurfaceCount;
            var slice = model.Indices.GetRange(cursor, surfaceCount);
            cursor += surfaceCount;

            var reversed = CoordinateConverter.ReverseWinding(slice).ToList();
            int idxAcc = buffer.AddIndices(reversed);

            mesh.Primitives.Add(new GltfPrimitive
            {
                Attributes = new Dictionary<string, int>(attributes),
                Indices = idxAcc,
                Material = matIndex,
            });
        }

        // Materials with no surfaces (rare) or models with no materials at all:
        // emit a single primitive over the whole index buffer.
        if (mesh.Primitives.Count == 0 && model.Indices.Count > 0)
        {
            var reversed = CoordinateConverter.ReverseWinding(model.Indices).ToList();
            mesh.Primitives.Add(new GltfPrimitive
            {
                Attributes = new Dictionary<string, int>(attributes),
                Indices = buffer.AddIndices(reversed),
            });
        }

        return new ConvertedMesh { Mesh = mesh, VertexCount = n, PositionAccessor = posAcc, SkinVertices = skinVerts };
    }

    private static Vector3 SafeDir(Vector3 d) => d.LengthSquared() < 1e-12f ? Vector3.UnitZ : d;

    /// <summary>
    /// Map PMX bone weights to glTF JOINTS_0/WEIGHTS_0. The skin's joint list is
    /// the bone list in order, so the glTF joint index equals the PMX bone index.
    /// Weights are clamped, negative/none entries dropped, and renormalised.
    /// </summary>
    private static ((ushort, ushort, ushort, ushort), Vector4) ResolveSkin(PmxVertex v)
    {
        Span<int> b = stackalloc int[4] { 0, 0, 0, 0 };
        Span<float> w = stackalloc float[4] { 0, 0, 0, 0 };

        int k = 0;
        foreach (var bw in v.Weights)
        {
            if (k >= 4) break;
            if (bw.BoneIndex < 0 || bw.Weight <= 0f) continue;
            b[k] = bw.BoneIndex;
            w[k] = bw.Weight;
            k++;
        }

        float sum = w[0] + w[1] + w[2] + w[3];
        if (sum <= 0f) { b[0] = 0; w[0] = 1f; sum = 1f; }
        var weights = new Vector4(w[0], w[1], w[2], w[3]) / sum;

        var joints = ((ushort)b[0], (ushort)b[1], (ushort)b[2], (ushort)b[3]);
        return (joints, weights);
    }
}

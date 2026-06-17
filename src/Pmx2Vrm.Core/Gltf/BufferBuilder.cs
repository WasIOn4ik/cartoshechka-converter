using System.Buffers.Binary;
using System.Numerics;

namespace Pmx2Vrm.Core.Gltf;

/// <summary>
/// Accumulates binary geometry into a single glTF buffer, creating the matching
/// bufferViews and accessors. All bufferViews are 4-byte aligned as required by
/// the spec. Returns accessor indices for callers to reference.
/// </summary>
public sealed class BufferBuilder
{
    // glTF component types
    private const int UnsignedShort = 5123;
    private const int UnsignedInt = 5125;
    private const int Float = 5126;
    // bufferView targets
    private const int ArrayBuffer = 34962;
    private const int ElementArrayBuffer = 34963;

    private readonly GltfRoot _root;
    private readonly MemoryStream _data = new();

    public BufferBuilder(GltfRoot root) => _root = root;

    public int AddIndices(IReadOnlyList<int> indices)
    {
        int offset = Align();
        Span<byte> tmp = stackalloc byte[4];
        foreach (var i in indices)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(tmp, (uint)i);
            _data.Write(tmp);
        }
        int view = AddBufferView(offset, indices.Count * 4, null, ElementArrayBuffer);
        return AddAccessor(view, UnsignedInt, indices.Count, "SCALAR");
    }

    public int AddVec3(IReadOnlyList<Vector3> values, bool computeBounds)
    {
        int offset = Align();
        foreach (var v in values) WriteVec3(v);
        int view = AddBufferView(offset, values.Count * 12, null, ArrayBuffer);
        var accessor = NewAccessor(view, Float, values.Count, "VEC3");
        if (computeBounds) (accessor.Min, accessor.Max) = Bounds(values);
        return _root.Accessors.Count - 1;
    }

    public int AddVec2(IReadOnlyList<Vector2> values)
    {
        int offset = Align();
        Span<byte> tmp = stackalloc byte[4];
        foreach (var v in values)
        {
            WriteFloat(tmp, v.X); WriteFloat(tmp, v.Y);
        }
        int view = AddBufferView(offset, values.Count * 8, null, ArrayBuffer);
        return AddAccessor(view, Float, values.Count, "VEC2");
    }

    public int AddVec4(IReadOnlyList<Vector4> values)
    {
        int offset = Align();
        foreach (var v in values) WriteVec4(v);
        int view = AddBufferView(offset, values.Count * 16, null, ArrayBuffer);
        return AddAccessor(view, Float, values.Count, "VEC4");
    }

    /// <summary>JOINTS_n: four unsigned shorts per vertex.</summary>
    public int AddJoints(IReadOnlyList<(ushort, ushort, ushort, ushort)> joints)
    {
        int offset = Align();
        Span<byte> tmp = stackalloc byte[2];
        foreach (var (a, b, c, d) in joints)
        {
            WriteUShort(tmp, a); WriteUShort(tmp, b); WriteUShort(tmp, c); WriteUShort(tmp, d);
        }
        int view = AddBufferView(offset, joints.Count * 8, null, ArrayBuffer);
        return AddAccessor(view, UnsignedShort, joints.Count, "VEC4");
    }

    /// <summary>
    /// A sparse VEC3 float accessor: the base is implicitly all-zero (no
    /// bufferView), and only <paramref name="entries"/> override values. Ideal
    /// for morph targets, which touch a small subset of vertices. Entries are
    /// sorted ascending by index (deduplicated, last wins) as the spec requires.
    /// </summary>
    public int AddSparseVec3(int count, IReadOnlyList<(int index, Vector3 value)> entries)
    {
        var dedup = new SortedDictionary<int, Vector3>();
        foreach (var (i, v) in entries)
        {
            if (i < 0 || i >= count) continue;
            dedup[i] = v;
        }
        if (dedup.Count == 0)
            throw new ArgumentException("Sparse accessor requires at least one entry.", nameof(entries));

        // Indices block (unsigned int), then values block (vec3 float).
        int indexOffset = Align();
        Span<byte> tmp = stackalloc byte[4];
        foreach (var i in dedup.Keys)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(tmp, (uint)i);
            _data.Write(tmp);
        }
        int indexView = AddBufferView(indexOffset, dedup.Count * 4, null, null);

        int valueOffset = Align();
        foreach (var v in dedup.Values) WriteVec3(v);
        int valueView = AddBufferView(valueOffset, dedup.Count * 12, null, null);

        var (min, max) = SparseBounds(dedup.Values);
        _root.Accessors.Add(new GltfAccessor
        {
            ComponentType = Float,
            Count = count,
            Type = "VEC3",
            Min = min,
            Max = max,
            Sparse = new GltfAccessorSparse
            {
                Count = dedup.Count,
                Indices = new GltfSparseIndices { BufferView = indexView, ComponentType = UnsignedInt },
                Values = new GltfSparseValues { BufferView = valueView },
            },
        });
        return _root.Accessors.Count - 1;
    }

    /// <summary>Inverse bind matrices: column-major MAT4 floats.</summary>
    public int AddMatrices(IReadOnlyList<Matrix4x4> matrices)
    {
        int offset = Align();
        Span<byte> tmp = stackalloc byte[4];
        foreach (var m in matrices)
        {
            // System.Numerics row-major memory (translation in row 4) matches the
            // glTF column-major layout for the same logical transform.
            WriteFloat(tmp, m.M11); WriteFloat(tmp, m.M12); WriteFloat(tmp, m.M13); WriteFloat(tmp, m.M14);
            WriteFloat(tmp, m.M21); WriteFloat(tmp, m.M22); WriteFloat(tmp, m.M23); WriteFloat(tmp, m.M24);
            WriteFloat(tmp, m.M31); WriteFloat(tmp, m.M32); WriteFloat(tmp, m.M33); WriteFloat(tmp, m.M34);
            WriteFloat(tmp, m.M41); WriteFloat(tmp, m.M42); WriteFloat(tmp, m.M43); WriteFloat(tmp, m.M44);
        }
        int view = AddBufferView(offset, matrices.Count * 64, null, null);
        return AddAccessor(view, Float, matrices.Count, "MAT4");
    }

    /// <summary>Append raw bytes (e.g. an embedded texture) and return its bufferView index.</summary>
    public int AddRawBufferView(ReadOnlySpan<byte> bytes)
    {
        int offset = Align();
        _data.Write(bytes);
        return AddBufferView(offset, bytes.Length, null, null);
    }

    /// <summary>Finalise: register the single buffer and return its byte payload.</summary>
    public byte[] Build()
    {
        var bytes = _data.ToArray();
        _root.Buffers.Add(new GltfBuffer { ByteLength = bytes.Length });
        return bytes;
    }

    // ---- internals --------------------------------------------------------

    private int Align()
    {
        while (_data.Length % 4 != 0) _data.WriteByte(0);
        return (int)_data.Length;
    }

    private void WriteVec3(Vector3 v)
    {
        Span<byte> tmp = stackalloc byte[4];
        WriteFloat(tmp, v.X); WriteFloat(tmp, v.Y); WriteFloat(tmp, v.Z);
    }

    private void WriteVec4(Vector4 v)
    {
        Span<byte> tmp = stackalloc byte[4];
        WriteFloat(tmp, v.X); WriteFloat(tmp, v.Y); WriteFloat(tmp, v.Z); WriteFloat(tmp, v.W);
    }

    private void WriteFloat(Span<byte> tmp, float f)
    {
        BinaryPrimitives.WriteSingleLittleEndian(tmp, f);
        _data.Write(tmp);
    }

    private void WriteUShort(Span<byte> tmp, ushort u)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(tmp, u);
        _data.Write(tmp);
    }

    private int AddBufferView(int offset, int length, int? stride, int? target)
    {
        _root.BufferViews.Add(new GltfBufferView
        {
            Buffer = 0,
            ByteOffset = offset,
            ByteLength = length,
            ByteStride = stride,
            Target = target,
        });
        return _root.BufferViews.Count - 1;
    }

    private GltfAccessor NewAccessor(int bufferView, int componentType, int count, string type)
    {
        var a = new GltfAccessor
        {
            BufferView = bufferView,
            ComponentType = componentType,
            Count = count,
            Type = type,
        };
        _root.Accessors.Add(a);
        return a;
    }

    private int AddAccessor(int bufferView, int componentType, int count, string type)
    {
        NewAccessor(bufferView, componentType, count, type);
        return _root.Accessors.Count - 1;
    }

    /// <summary>Bounds over sparse values, folding in the implicit zero baseline.</summary>
    private static (float[] min, float[] max) SparseBounds(IEnumerable<Vector3> values)
    {
        var min = Vector3.Zero;
        var max = Vector3.Zero;
        foreach (var v in values)
        {
            min = Vector3.Min(min, v);
            max = Vector3.Max(max, v);
        }
        return (new[] { min.X, min.Y, min.Z }, new[] { max.X, max.Y, max.Z });
    }

    private static (float[] min, float[] max) Bounds(IReadOnlyList<Vector3> values)
    {
        var min = new Vector3(float.PositiveInfinity);
        var max = new Vector3(float.NegativeInfinity);
        foreach (var v in values)
        {
            min = Vector3.Min(min, v);
            max = Vector3.Max(max, v);
        }
        if (values.Count == 0) { min = Vector3.Zero; max = Vector3.Zero; }
        return (new[] { min.X, min.Y, min.Z }, new[] { max.X, max.Y, max.Z });
    }
}

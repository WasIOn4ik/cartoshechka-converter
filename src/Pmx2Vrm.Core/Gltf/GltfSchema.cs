using System.Text.Json.Serialization;

namespace Pmx2Vrm.Core.Gltf;

// Minimal glTF 2.0 object model — only the parts this converter emits. Property
// order and naming follow the spec; extensions are kept as open dictionaries so
// VRM (0.x and 1.0) JSON can be attached without bespoke schema classes.

public sealed class GltfRoot
{
    [JsonPropertyName("asset")] public GltfAsset Asset { get; set; } = new();
    [JsonPropertyName("scene")] public int? Scene { get; set; }
    [JsonPropertyName("scenes")] public List<GltfScene> Scenes { get; set; } = new();
    [JsonPropertyName("nodes")] public List<GltfNode> Nodes { get; set; } = new();
    [JsonPropertyName("meshes")] public List<GltfMesh> Meshes { get; set; } = new();
    [JsonPropertyName("skins")] public List<GltfSkin> Skins { get; set; } = new();
    [JsonPropertyName("materials")] public List<GltfMaterial> Materials { get; set; } = new();
    [JsonPropertyName("textures")] public List<GltfTexture> Textures { get; set; } = new();
    [JsonPropertyName("images")] public List<GltfImage> Images { get; set; } = new();
    [JsonPropertyName("samplers")] public List<GltfSampler> Samplers { get; set; } = new();
    [JsonPropertyName("accessors")] public List<GltfAccessor> Accessors { get; set; } = new();
    [JsonPropertyName("bufferViews")] public List<GltfBufferView> BufferViews { get; set; } = new();
    [JsonPropertyName("buffers")] public List<GltfBuffer> Buffers { get; set; } = new();

    [JsonPropertyName("extensionsUsed")] public List<string> ExtensionsUsed { get; set; } = new();
    [JsonPropertyName("extensions")] public Dictionary<string, object> Extensions { get; set; } = new();

    [JsonIgnore] public bool ShouldEmptyCollectionsBeOmitted => true;
}

public sealed class GltfAsset
{
    [JsonPropertyName("version")] public string Version { get; set; } = "2.0";
    [JsonPropertyName("generator")] public string Generator { get; set; } = "Pmx2Vrm";
}

public sealed class GltfScene
{
    [JsonPropertyName("nodes")] public List<int> Nodes { get; set; } = new();
}

public sealed class GltfNode
{
    [JsonPropertyName("name")][JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Name { get; set; }
    [JsonPropertyName("children")][JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public List<int>? Children { get; set; }
    [JsonPropertyName("translation")][JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public float[]? Translation { get; set; }
    [JsonPropertyName("rotation")][JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public float[]? Rotation { get; set; }
    [JsonPropertyName("scale")][JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public float[]? Scale { get; set; }
    [JsonPropertyName("mesh")][JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? Mesh { get; set; }
    [JsonPropertyName("skin")][JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? Skin { get; set; }
}

public sealed class GltfMesh
{
    [JsonPropertyName("name")][JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Name { get; set; }
    [JsonPropertyName("primitives")] public List<GltfPrimitive> Primitives { get; set; } = new();
    [JsonPropertyName("weights")][JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public float[]? Weights { get; set; }
    [JsonPropertyName("extras")][JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public object? Extras { get; set; }
}

public sealed class GltfPrimitive
{
    [JsonPropertyName("attributes")] public Dictionary<string, int> Attributes { get; set; } = new();
    [JsonPropertyName("indices")][JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? Indices { get; set; }
    [JsonPropertyName("material")][JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? Material { get; set; }
    [JsonPropertyName("mode")][JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? Mode { get; set; }
    [JsonPropertyName("targets")][JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public List<Dictionary<string, int>>? Targets { get; set; }
}

public sealed class GltfSkin
{
    [JsonPropertyName("name")][JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Name { get; set; }
    [JsonPropertyName("inverseBindMatrices")][JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? InverseBindMatrices { get; set; }
    [JsonPropertyName("skeleton")][JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? Skeleton { get; set; }
    [JsonPropertyName("joints")] public List<int> Joints { get; set; } = new();
}

public sealed class GltfAccessor
{
    [JsonPropertyName("bufferView")][JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? BufferView { get; set; }
    [JsonPropertyName("byteOffset")][JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int ByteOffset { get; set; }
    [JsonPropertyName("componentType")] public int ComponentType { get; set; }
    [JsonPropertyName("normalized")][JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public bool Normalized { get; set; }
    [JsonPropertyName("count")] public int Count { get; set; }
    [JsonPropertyName("type")] public string Type { get; set; } = "SCALAR";
    [JsonPropertyName("min")][JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public float[]? Min { get; set; }
    [JsonPropertyName("max")][JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public float[]? Max { get; set; }
    [JsonPropertyName("sparse")][JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public GltfAccessorSparse? Sparse { get; set; }
}

public sealed class GltfAccessorSparse
{
    [JsonPropertyName("count")] public int Count { get; set; }
    [JsonPropertyName("indices")] public GltfSparseIndices Indices { get; set; } = new();
    [JsonPropertyName("values")] public GltfSparseValues Values { get; set; } = new();
}

public sealed class GltfSparseIndices
{
    [JsonPropertyName("bufferView")] public int BufferView { get; set; }
    [JsonPropertyName("byteOffset")][JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int ByteOffset { get; set; }
    [JsonPropertyName("componentType")] public int ComponentType { get; set; }
}

public sealed class GltfSparseValues
{
    [JsonPropertyName("bufferView")] public int BufferView { get; set; }
    [JsonPropertyName("byteOffset")][JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int ByteOffset { get; set; }
}

public sealed class GltfBufferView
{
    [JsonPropertyName("buffer")] public int Buffer { get; set; }
    [JsonPropertyName("byteOffset")][JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int ByteOffset { get; set; }
    [JsonPropertyName("byteLength")] public int ByteLength { get; set; }
    [JsonPropertyName("byteStride")][JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? ByteStride { get; set; }
    [JsonPropertyName("target")][JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? Target { get; set; }
}

public sealed class GltfBuffer
{
    [JsonPropertyName("byteLength")] public int ByteLength { get; set; }
    [JsonPropertyName("uri")][JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Uri { get; set; }
}

public sealed class GltfTexture
{
    [JsonPropertyName("sampler")][JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? Sampler { get; set; }
    [JsonPropertyName("source")][JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? Source { get; set; }
    [JsonPropertyName("name")][JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Name { get; set; }
}

public sealed class GltfImage
{
    [JsonPropertyName("name")][JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Name { get; set; }
    [JsonPropertyName("mimeType")][JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? MimeType { get; set; }
    [JsonPropertyName("bufferView")][JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? BufferView { get; set; }
    [JsonPropertyName("uri")][JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Uri { get; set; }
}

public sealed class GltfSampler
{
    [JsonPropertyName("magFilter")][JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? MagFilter { get; set; }
    [JsonPropertyName("minFilter")][JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? MinFilter { get; set; }
    [JsonPropertyName("wrapS")][JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? WrapS { get; set; }
    [JsonPropertyName("wrapT")][JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? WrapT { get; set; }
}

public sealed class GltfMaterial
{
    [JsonPropertyName("name")][JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Name { get; set; }
    [JsonPropertyName("pbrMetallicRoughness")][JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public GltfPbr? PbrMetallicRoughness { get; set; }
    [JsonPropertyName("alphaMode")][JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? AlphaMode { get; set; }
    [JsonPropertyName("alphaCutoff")][JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public float? AlphaCutoff { get; set; }
    [JsonPropertyName("doubleSided")][JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public bool DoubleSided { get; set; }
    [JsonPropertyName("emissiveFactor")][JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public float[]? EmissiveFactor { get; set; }
    [JsonPropertyName("extensions")][JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public Dictionary<string, object>? Extensions { get; set; }
}

public sealed class GltfPbr
{
    [JsonPropertyName("baseColorFactor")][JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public float[]? BaseColorFactor { get; set; }
    [JsonPropertyName("baseColorTexture")][JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public GltfTextureRef? BaseColorTexture { get; set; }
    [JsonPropertyName("metallicFactor")] public float MetallicFactor { get; set; }
    [JsonPropertyName("roughnessFactor")] public float RoughnessFactor { get; set; } = 1f;
}

public sealed class GltfTextureRef
{
    [JsonPropertyName("index")] public int Index { get; set; }
    [JsonPropertyName("texCoord")][JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int TexCoord { get; set; }
}

using System.Numerics;

namespace Pmx2Vrm.Core.Pmx;

/// <summary>
/// In-memory representation of a parsed PMX model. All data is kept in the
/// original MMD coordinate space (left-handed, Y-up); coordinate conversion is
/// performed later in the pipeline by <c>CoordinateConverter</c>.
/// </summary>
public sealed class PmxModel
{
    public PmxHeader Header { get; set; } = new();
    public string NameLocal { get; set; } = "";
    public string NameUniversal { get; set; } = "";
    public string CommentLocal { get; set; } = "";
    public string CommentUniversal { get; set; } = "";

    public List<PmxVertex> Vertices { get; } = new();
    /// <summary>Flat list of vertex indices; every 3 form one triangle.</summary>
    public List<int> Indices { get; } = new();
    public List<string> TexturePaths { get; } = new();
    public List<PmxMaterial> Materials { get; } = new();
    public List<PmxBone> Bones { get; } = new();
    public List<PmxMorph> Morphs { get; } = new();
    public List<PmxDisplayFrame> DisplayFrames { get; } = new();
    public List<PmxRigidBody> RigidBodies { get; } = new();
    public List<PmxJoint> Joints { get; } = new();
}

public sealed class PmxHeader
{
    public float Version { get; set; } = 2.0f;
    public PmxEncoding Encoding { get; set; } = PmxEncoding.Utf16Le;
    public int AdditionalVec4Count { get; set; }
    public int VertexIndexSize { get; set; } = 4;
    public int TextureIndexSize { get; set; } = 1;
    public int MaterialIndexSize { get; set; } = 1;
    public int BoneIndexSize { get; set; } = 2;
    public int MorphIndexSize { get; set; } = 2;
    public int RigidBodyIndexSize { get; set; } = 2;
}

public struct PmxBoneWeight
{
    public int BoneIndex;
    public float Weight;
    public PmxBoneWeight(int boneIndex, float weight) { BoneIndex = boneIndex; Weight = weight; }
}

public sealed class PmxVertex
{
    public Vector3 Position;
    public Vector3 Normal;
    public Vector2 Uv;
    public Vector4[] AdditionalUv = System.Array.Empty<Vector4>();
    public PmxWeightType WeightType;
    /// <summary>Up to 4 bone/weight pairs (BDEF1/2/4, SDEF, QDEF normalised).</summary>
    public PmxBoneWeight[] Weights = System.Array.Empty<PmxBoneWeight>();
    public float EdgeScale = 1f;

    // SDEF-only auxiliary vectors (preserved but not used by VRM conversion).
    public Vector3 SdefC;
    public Vector3 SdefR0;
    public Vector3 SdefR1;
}

public sealed class PmxMaterial
{
    public string NameLocal { get; set; } = "";
    public string NameUniversal { get; set; } = "";
    public Vector4 Diffuse;
    public Vector3 Specular;
    public float SpecularStrength;
    public Vector3 Ambient;
    public PmxMaterialFlags Flags;
    public Vector4 EdgeColor;
    public float EdgeScale;
    public int TextureIndex = -1;
    public int SphereTextureIndex = -1;
    public PmxSphereMode SphereMode;
    public PmxToonReference ToonReference;
    /// <summary>Texture index when <see cref="ToonReference"/> is Texture, else shared toon id (0..9).</summary>
    public int ToonValue = -1;
    public string Memo { get; set; } = "";
    /// <summary>Number of indices (multiple of 3) belonging to this material.</summary>
    public int SurfaceCount;
}

public sealed class PmxIkLink
{
    public int BoneIndex;
    public bool HasLimit;
    public Vector3 LimitMin;
    public Vector3 LimitMax;
}

public sealed class PmxBone
{
    public string NameLocal { get; set; } = "";
    public string NameUniversal { get; set; } = "";
    public Vector3 Position;
    public int ParentIndex = -1;
    public int Layer;
    public PmxBoneFlags Flags;

    public int TailBoneIndex = -1;
    public Vector3 TailOffset;

    public int InheritParentIndex = -1;
    public float InheritWeight;

    public Vector3 FixedAxis;
    public Vector3 LocalAxisX;
    public Vector3 LocalAxisZ;
    public int ExternalParentKey;

    // IK
    public int IkTargetIndex = -1;
    public int IkLoopCount;
    public float IkLimitRadians;
    public List<PmxIkLink> IkLinks { get; } = new();

    public bool HasFlag(PmxBoneFlags f) => (Flags & f) == f;
}

public sealed class PmxMorph
{
    public string NameLocal { get; set; } = "";
    public string NameUniversal { get; set; } = "";
    public byte Panel { get; set; }
    public PmxMorphType Type { get; set; }
    public List<PmxGroupMorphOffset> Group { get; } = new();
    public List<PmxVertexMorphOffset> Vertex { get; } = new();
    public List<PmxBoneMorphOffset> Bone { get; } = new();
    public List<PmxUvMorphOffset> Uv { get; } = new();
    public List<PmxMaterialMorphOffset> Material { get; } = new();
    public List<PmxImpulseMorphOffset> Impulse { get; } = new();
}

public struct PmxGroupMorphOffset { public int MorphIndex; public float Influence; }
public struct PmxVertexMorphOffset { public int VertexIndex; public Vector3 Offset; }
public struct PmxBoneMorphOffset { public int BoneIndex; public Vector3 Translation; public Quaternion Rotation; }
public struct PmxUvMorphOffset { public int VertexIndex; public Vector4 Offset; }
public struct PmxImpulseMorphOffset { public int RigidBodyIndex; public bool IsLocal; public Vector3 Velocity; public Vector3 Torque; }

public struct PmxMaterialMorphOffset
{
    public int MaterialIndex;        // -1 = applies to all materials
    public PmxMaterialMorphMode Mode;
    public Vector4 Diffuse;
    public Vector3 Specular;
    public float SpecularStrength;
    public Vector3 Ambient;
    public Vector4 EdgeColor;
    public float EdgeScale;
    public Vector4 TextureTint;
    public Vector4 SphereTint;
    public Vector4 ToonTint;
}

public sealed class PmxDisplayFrame
{
    public string NameLocal { get; set; } = "";
    public string NameUniversal { get; set; } = "";
    public bool IsSpecial { get; set; }
    public List<PmxDisplayItem> Items { get; } = new();
}

public struct PmxDisplayItem { public PmxDisplayItemType Type; public int Index; }

public sealed class PmxRigidBody
{
    public string NameLocal { get; set; } = "";
    public string NameUniversal { get; set; } = "";
    public int BoneIndex = -1;
    public byte Group;
    public ushort CollisionMask;
    public PmxRigidBodyShape Shape;
    public Vector3 Size;
    public Vector3 Position;
    public Vector3 Rotation;     // radians, euler
    public float Mass;
    public float LinearDamping;
    public float AngularDamping;
    public float Restitution;
    public float Friction;
    public PmxPhysicsMode PhysicsMode;
}

public sealed class PmxJoint
{
    public string NameLocal { get; set; } = "";
    public string NameUniversal { get; set; } = "";
    public PmxJointType Type;
    public int RigidBodyAIndex = -1;
    public int RigidBodyBIndex = -1;
    public Vector3 Position;
    public Vector3 Rotation;
    public Vector3 MoveLimitMin;
    public Vector3 MoveLimitMax;
    public Vector3 RotationLimitMin;
    public Vector3 RotationLimitMax;
    public Vector3 SpringMove;
    public Vector3 SpringRotation;
}

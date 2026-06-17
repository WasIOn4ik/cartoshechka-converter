namespace Pmx2Vrm.Core.Pmx;

/// <summary>Text encoding declared in the PMX globals block.</summary>
public enum PmxEncoding : byte
{
    Utf16Le = 0,
    Utf8 = 1,
}

/// <summary>Vertex skinning deform method.</summary>
public enum PmxWeightType : byte
{
    Bdef1 = 0,
    Bdef2 = 1,
    Bdef4 = 2,
    Sdef = 3,
    Qdef = 4,
}

/// <summary>Sphere (environment) texture blend mode on a material.</summary>
public enum PmxSphereMode : byte
{
    None = 0,
    Multiply = 1,
    Add = 2,
    SubTexture = 3,
}

/// <summary>How a material resolves its toon ramp texture.</summary>
public enum PmxToonReference : byte
{
    Texture = 0,
    Shared = 1,
}

[System.Flags]
public enum PmxMaterialFlags : byte
{
    None = 0,
    NoCull = 1 << 0,
    GroundShadow = 1 << 1,
    DrawShadow = 1 << 2,
    ReceiveShadow = 1 << 3,
    HasEdge = 1 << 4,
    VertexColor = 1 << 5,
    PointDrawing = 1 << 6,
    LineDrawing = 1 << 7,
}

[System.Flags]
public enum PmxBoneFlags : ushort
{
    None = 0,
    IndexedTailPosition = 1 << 0,
    Rotatable = 1 << 1,
    Translatable = 1 << 2,
    Visible = 1 << 3,
    Enabled = 1 << 4,
    Ik = 1 << 5,
    InheritLocal = 1 << 7,
    InheritRotation = 1 << 8,
    InheritTranslation = 1 << 9,
    FixedAxis = 1 << 10,
    LocalCoordinate = 1 << 11,
    PhysicsAfterDeform = 1 << 12,
    ExternalParentDeform = 1 << 13,
}

public enum PmxMorphType : byte
{
    Group = 0,
    Vertex = 1,
    Bone = 2,
    Uv = 3,
    UvExt1 = 4,
    UvExt2 = 5,
    UvExt3 = 6,
    UvExt4 = 7,
    Material = 8,
    Flip = 9,
    Impulse = 10,
}

public enum PmxMaterialMorphMode : byte
{
    Multiply = 0,
    Additive = 1,
}

public enum PmxDisplayItemType : byte
{
    Bone = 0,
    Morph = 1,
}

public enum PmxRigidBodyShape : byte
{
    Sphere = 0,
    Box = 1,
    Capsule = 2,
}

/// <summary>How a rigid body interacts with its bone.</summary>
public enum PmxPhysicsMode : byte
{
    FollowBone = 0,
    Physics = 1,
    PhysicsAndBone = 2,
}

public enum PmxJointType : byte
{
    Spring6Dof = 0,
    SixDof = 1,
    P2P = 2,
    ConeTwist = 3,
    Slider = 4,
    Hinge = 5,
}

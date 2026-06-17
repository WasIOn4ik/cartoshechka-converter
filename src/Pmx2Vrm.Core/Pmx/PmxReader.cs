using System.Numerics;
using System.Text;

namespace Pmx2Vrm.Core.Pmx;

/// <summary>
/// Parser for PMX 2.0 / 2.1 binary files. Soft-body data (2.1) is skipped
/// gracefully if encountered. All values are read in little-endian order, as
/// mandated by the format.
/// </summary>
public sealed class PmxReader
{
    private static readonly byte[] Magic = { 0x50, 0x4D, 0x58, 0x20 }; // "PMX "

    private readonly BinaryReader _r;
    private PmxHeader _h = new();

    private PmxReader(BinaryReader reader) => _r = reader;

    public static PmxModel ReadFromFile(string path)
    {
        using var fs = File.OpenRead(path);
        return Read(fs);
    }

    public static PmxModel Read(Stream stream)
    {
        using var br = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
        var reader = new PmxReader(br);
        return reader.ReadModel();
    }

    private PmxModel ReadModel()
    {
        var model = new PmxModel();
        ReadHeader(model);
        ReadModelInfo(model);
        ReadVertices(model);
        ReadSurfaces(model);
        ReadTextures(model);
        ReadMaterials(model);
        ReadBones(model);
        ReadMorphs(model);
        ReadDisplayFrames(model);
        ReadRigidBodies(model);
        ReadJoints(model);
        // PMX 2.1 soft bodies intentionally left unread.
        return model;
    }

    private void ReadHeader(PmxModel model)
    {
        var magic = _r.ReadBytes(4);
        if (!magic.AsSpan().SequenceEqual(Magic))
            throw new InvalidDataException("Not a PMX file: bad magic signature.");

        var h = new PmxHeader { Version = _r.ReadSingle() };
        byte globalCount = _r.ReadByte();
        var globals = _r.ReadBytes(globalCount);
        if (globalCount < 8)
            throw new InvalidDataException($"Unexpected PMX globals count: {globalCount}.");

        h.Encoding = (PmxEncoding)globals[0];
        h.AdditionalVec4Count = globals[1];
        h.VertexIndexSize = globals[2];
        h.TextureIndexSize = globals[3];
        h.MaterialIndexSize = globals[4];
        h.BoneIndexSize = globals[5];
        h.MorphIndexSize = globals[6];
        h.RigidBodyIndexSize = globals[7];

        _h = h;
        model.Header = h;
    }

    private void ReadModelInfo(PmxModel model)
    {
        model.NameLocal = ReadText();
        model.NameUniversal = ReadText();
        model.CommentLocal = ReadText();
        model.CommentUniversal = ReadText();
    }

    private void ReadVertices(PmxModel model)
    {
        int count = _r.ReadInt32();
        model.Vertices.Capacity = count;
        for (int i = 0; i < count; i++)
        {
            var v = new PmxVertex
            {
                Position = ReadVec3(),
                Normal = ReadVec3(),
                Uv = ReadVec2(),
            };

            if (_h.AdditionalVec4Count > 0)
            {
                v.AdditionalUv = new Vector4[_h.AdditionalVec4Count];
                for (int a = 0; a < _h.AdditionalVec4Count; a++)
                    v.AdditionalUv[a] = ReadVec4();
            }

            v.WeightType = (PmxWeightType)_r.ReadByte();
            v.Weights = ReadWeights(v);
            v.EdgeScale = _r.ReadSingle();
            model.Vertices.Add(v);
        }
    }

    private PmxBoneWeight[] ReadWeights(PmxVertex v)
    {
        switch (v.WeightType)
        {
            case PmxWeightType.Bdef1:
                return new[] { new PmxBoneWeight(ReadBoneIndex(), 1f) };

            case PmxWeightType.Bdef2:
            {
                int b1 = ReadBoneIndex();
                int b2 = ReadBoneIndex();
                float w1 = _r.ReadSingle();
                return new[] { new PmxBoneWeight(b1, w1), new PmxBoneWeight(b2, 1f - w1) };
            }

            case PmxWeightType.Bdef4:
            case PmxWeightType.Qdef:
            {
                int b1 = ReadBoneIndex(), b2 = ReadBoneIndex(), b3 = ReadBoneIndex(), b4 = ReadBoneIndex();
                float w1 = _r.ReadSingle(), w2 = _r.ReadSingle(), w3 = _r.ReadSingle(), w4 = _r.ReadSingle();
                return new[]
                {
                    new PmxBoneWeight(b1, w1), new PmxBoneWeight(b2, w2),
                    new PmxBoneWeight(b3, w3), new PmxBoneWeight(b4, w4),
                };
            }

            case PmxWeightType.Sdef:
            {
                int b1 = ReadBoneIndex();
                int b2 = ReadBoneIndex();
                float w1 = _r.ReadSingle();
                v.SdefC = ReadVec3();
                v.SdefR0 = ReadVec3();
                v.SdefR1 = ReadVec3();
                return new[] { new PmxBoneWeight(b1, w1), new PmxBoneWeight(b2, 1f - w1) };
            }

            default:
                throw new InvalidDataException($"Unknown weight type: {v.WeightType}.");
        }
    }

    private void ReadSurfaces(PmxModel model)
    {
        int count = _r.ReadInt32();
        model.Indices.Capacity = count;
        for (int i = 0; i < count; i++)
            model.Indices.Add(ReadVertexIndex());
    }

    private void ReadTextures(PmxModel model)
    {
        int count = _r.ReadInt32();
        for (int i = 0; i < count; i++)
            model.TexturePaths.Add(ReadText());
    }

    private void ReadMaterials(PmxModel model)
    {
        int count = _r.ReadInt32();
        for (int i = 0; i < count; i++)
        {
            var m = new PmxMaterial
            {
                NameLocal = ReadText(),
                NameUniversal = ReadText(),
                Diffuse = ReadVec4(),
                Specular = ReadVec3(),
                SpecularStrength = _r.ReadSingle(),
                Ambient = ReadVec3(),
                Flags = (PmxMaterialFlags)_r.ReadByte(),
                EdgeColor = ReadVec4(),
                EdgeScale = _r.ReadSingle(),
                TextureIndex = ReadTextureIndex(),
                SphereTextureIndex = ReadTextureIndex(),
                SphereMode = (PmxSphereMode)_r.ReadByte(),
                ToonReference = (PmxToonReference)_r.ReadByte(),
            };

            m.ToonValue = m.ToonReference == PmxToonReference.Shared
                ? _r.ReadByte()
                : ReadTextureIndex();

            m.Memo = ReadText();
            m.SurfaceCount = _r.ReadInt32();
            model.Materials.Add(m);
        }
    }

    private void ReadBones(PmxModel model)
    {
        int count = _r.ReadInt32();
        for (int i = 0; i < count; i++)
        {
            var b = new PmxBone
            {
                NameLocal = ReadText(),
                NameUniversal = ReadText(),
                Position = ReadVec3(),
                ParentIndex = ReadBoneIndex(),
                Layer = _r.ReadInt32(),
                Flags = (PmxBoneFlags)_r.ReadUInt16(),
            };

            if (b.HasFlag(PmxBoneFlags.IndexedTailPosition))
                b.TailBoneIndex = ReadBoneIndex();
            else
                b.TailOffset = ReadVec3();

            if (b.HasFlag(PmxBoneFlags.InheritRotation) || b.HasFlag(PmxBoneFlags.InheritTranslation))
            {
                b.InheritParentIndex = ReadBoneIndex();
                b.InheritWeight = _r.ReadSingle();
            }

            if (b.HasFlag(PmxBoneFlags.FixedAxis))
                b.FixedAxis = ReadVec3();

            if (b.HasFlag(PmxBoneFlags.LocalCoordinate))
            {
                b.LocalAxisX = ReadVec3();
                b.LocalAxisZ = ReadVec3();
            }

            if (b.HasFlag(PmxBoneFlags.ExternalParentDeform))
                b.ExternalParentKey = _r.ReadInt32();

            if (b.HasFlag(PmxBoneFlags.Ik))
            {
                b.IkTargetIndex = ReadBoneIndex();
                b.IkLoopCount = _r.ReadInt32();
                b.IkLimitRadians = _r.ReadSingle();
                int linkCount = _r.ReadInt32();
                for (int l = 0; l < linkCount; l++)
                {
                    var link = new PmxIkLink { BoneIndex = ReadBoneIndex(), HasLimit = _r.ReadByte() != 0 };
                    if (link.HasLimit)
                    {
                        link.LimitMin = ReadVec3();
                        link.LimitMax = ReadVec3();
                    }
                    b.IkLinks.Add(link);
                }
            }

            model.Bones.Add(b);
        }
    }

    private void ReadMorphs(PmxModel model)
    {
        int count = _r.ReadInt32();
        for (int i = 0; i < count; i++)
        {
            var morph = new PmxMorph
            {
                NameLocal = ReadText(),
                NameUniversal = ReadText(),
                Panel = _r.ReadByte(),
                Type = (PmxMorphType)_r.ReadByte(),
            };

            int offsetCount = _r.ReadInt32();
            for (int o = 0; o < offsetCount; o++)
                ReadMorphOffset(morph, offsetCount, o);

            model.Morphs.Add(morph);
        }
    }

    private void ReadMorphOffset(PmxMorph morph, int _, int __)
    {
        switch (morph.Type)
        {
            case PmxMorphType.Group:
            case PmxMorphType.Flip:
                morph.Group.Add(new PmxGroupMorphOffset
                {
                    MorphIndex = ReadMorphIndex(),
                    Influence = _r.ReadSingle(),
                });
                break;

            case PmxMorphType.Vertex:
                morph.Vertex.Add(new PmxVertexMorphOffset
                {
                    VertexIndex = ReadVertexIndex(),
                    Offset = ReadVec3(),
                });
                break;

            case PmxMorphType.Bone:
                morph.Bone.Add(new PmxBoneMorphOffset
                {
                    BoneIndex = ReadBoneIndex(),
                    Translation = ReadVec3(),
                    Rotation = ReadQuat(),
                });
                break;

            case PmxMorphType.Uv:
            case PmxMorphType.UvExt1:
            case PmxMorphType.UvExt2:
            case PmxMorphType.UvExt3:
            case PmxMorphType.UvExt4:
                morph.Uv.Add(new PmxUvMorphOffset
                {
                    VertexIndex = ReadVertexIndex(),
                    Offset = ReadVec4(),
                });
                break;

            case PmxMorphType.Material:
                morph.Material.Add(new PmxMaterialMorphOffset
                {
                    MaterialIndex = ReadMaterialIndex(),
                    Mode = (PmxMaterialMorphMode)_r.ReadByte(),
                    Diffuse = ReadVec4(),
                    Specular = ReadVec3(),
                    SpecularStrength = _r.ReadSingle(),
                    Ambient = ReadVec3(),
                    EdgeColor = ReadVec4(),
                    EdgeScale = _r.ReadSingle(),
                    TextureTint = ReadVec4(),
                    SphereTint = ReadVec4(),
                    ToonTint = ReadVec4(),
                });
                break;

            case PmxMorphType.Impulse:
                morph.Impulse.Add(new PmxImpulseMorphOffset
                {
                    RigidBodyIndex = ReadRigidBodyIndex(),
                    IsLocal = _r.ReadByte() != 0,
                    Velocity = ReadVec3(),
                    Torque = ReadVec3(),
                });
                break;

            default:
                throw new InvalidDataException($"Unknown morph type: {morph.Type}.");
        }
    }

    private void ReadDisplayFrames(PmxModel model)
    {
        int count = _r.ReadInt32();
        for (int i = 0; i < count; i++)
        {
            var frame = new PmxDisplayFrame
            {
                NameLocal = ReadText(),
                NameUniversal = ReadText(),
                IsSpecial = _r.ReadByte() != 0,
            };

            int items = _r.ReadInt32();
            for (int j = 0; j < items; j++)
            {
                var type = (PmxDisplayItemType)_r.ReadByte();
                int index = type == PmxDisplayItemType.Bone ? ReadBoneIndex() : ReadMorphIndex();
                frame.Items.Add(new PmxDisplayItem { Type = type, Index = index });
            }

            model.DisplayFrames.Add(frame);
        }
    }

    private void ReadRigidBodies(PmxModel model)
    {
        int count = _r.ReadInt32();
        for (int i = 0; i < count; i++)
        {
            model.RigidBodies.Add(new PmxRigidBody
            {
                NameLocal = ReadText(),
                NameUniversal = ReadText(),
                BoneIndex = ReadBoneIndex(),
                Group = _r.ReadByte(),
                CollisionMask = _r.ReadUInt16(),
                Shape = (PmxRigidBodyShape)_r.ReadByte(),
                Size = ReadVec3(),
                Position = ReadVec3(),
                Rotation = ReadVec3(),
                Mass = _r.ReadSingle(),
                LinearDamping = _r.ReadSingle(),
                AngularDamping = _r.ReadSingle(),
                Restitution = _r.ReadSingle(),
                Friction = _r.ReadSingle(),
                PhysicsMode = (PmxPhysicsMode)_r.ReadByte(),
            });
        }
    }

    private void ReadJoints(PmxModel model)
    {
        int count = _r.ReadInt32();
        for (int i = 0; i < count; i++)
        {
            model.Joints.Add(new PmxJoint
            {
                NameLocal = ReadText(),
                NameUniversal = ReadText(),
                Type = (PmxJointType)_r.ReadByte(),
                RigidBodyAIndex = ReadRigidBodyIndex(),
                RigidBodyBIndex = ReadRigidBodyIndex(),
                Position = ReadVec3(),
                Rotation = ReadVec3(),
                MoveLimitMin = ReadVec3(),
                MoveLimitMax = ReadVec3(),
                RotationLimitMin = ReadVec3(),
                RotationLimitMax = ReadVec3(),
                SpringMove = ReadVec3(),
                SpringRotation = ReadVec3(),
            });
        }
    }

    // ---- primitive readers ------------------------------------------------

    private Vector2 ReadVec2() => new(_r.ReadSingle(), _r.ReadSingle());
    private Vector3 ReadVec3() => new(_r.ReadSingle(), _r.ReadSingle(), _r.ReadSingle());
    private Vector4 ReadVec4() => new(_r.ReadSingle(), _r.ReadSingle(), _r.ReadSingle(), _r.ReadSingle());
    private Quaternion ReadQuat() => new(_r.ReadSingle(), _r.ReadSingle(), _r.ReadSingle(), _r.ReadSingle());

    private string ReadText()
    {
        int len = _r.ReadInt32();
        if (len == 0) return "";
        var bytes = _r.ReadBytes(len);
        return _h.Encoding == PmxEncoding.Utf16Le
            ? Encoding.Unicode.GetString(bytes)
            : Encoding.UTF8.GetString(bytes);
    }

    /// <summary>Vertex indices are unsigned and cannot be -1.</summary>
    private int ReadVertexIndex() => _h.VertexIndexSize switch
    {
        1 => _r.ReadByte(),
        2 => _r.ReadUInt16(),
        4 => _r.ReadInt32(),
        _ => throw new InvalidDataException($"Bad vertex index size: {_h.VertexIndexSize}."),
    };

    /// <summary>Reference indices are signed; -1 (all bits set) means "none".</summary>
    private int ReadSignedIndex(int size) => size switch
    {
        1 => _r.ReadSByte(),
        2 => _r.ReadInt16(),
        4 => _r.ReadInt32(),
        _ => throw new InvalidDataException($"Bad reference index size: {size}."),
    };

    private int ReadTextureIndex() => ReadSignedIndex(_h.TextureIndexSize);
    private int ReadMaterialIndex() => ReadSignedIndex(_h.MaterialIndexSize);
    private int ReadBoneIndex() => ReadSignedIndex(_h.BoneIndexSize);
    private int ReadMorphIndex() => ReadSignedIndex(_h.MorphIndexSize);
    private int ReadRigidBodyIndex() => ReadSignedIndex(_h.RigidBodyIndexSize);
}

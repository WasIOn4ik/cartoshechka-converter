using System.Numerics;
using System.Text;
using Pmx2Vrm.Core.Pmx;

namespace Pmx2Vrm.Tests.TestSupport;

/// <summary>
/// Builds a tiny but fully valid PMX 2.0 byte stream in memory, mirroring the
/// exact layout <see cref="PmxReader"/> expects. Used by unit tests so they do
/// not depend on any licensed external model. The shape exercises every reader
/// branch we care about: BDEF1/BDEF2 weights, a textured toon material with an
/// edge, an IK bone, a vertex morph, a display frame, a rigid body and a joint.
/// </summary>
public static class SyntheticPmx
{
    // Encoding/index sizes chosen to be small and to differ from defaults so the
    // reader's size handling is genuinely tested.
    public const PmxEncoding Encoding = PmxEncoding.Utf16Le;
    private const int VertexIndexSize = 2;
    private const int RefIndexSize = 2; // texture/material/bone/morph/rigidbody

    public static byte[] Build()
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true);

        // Header
        w.Write(new byte[] { 0x50, 0x4D, 0x58, 0x20 }); // "PMX "
        w.Write(2.0f);
        w.Write((byte)8);
        w.Write(new byte[]
        {
            (byte)Encoding, 0, VertexIndexSize, RefIndexSize,
            RefIndexSize, RefIndexSize, RefIndexSize, RefIndexSize,
        });

        // Model info
        WriteText(w, "テストモデル");   // name local
        WriteText(w, "TestModel");      // name universal
        WriteText(w, "コメント");
        WriteText(w, "comment");

        // Vertices (3 -> one triangle). v0 BDEF1, v1 BDEF2, v2 BDEF1.
        w.Write(3);
        WriteVertexBdef1(w, new Vector3(0, 0, 0), Vector3.UnitZ, new Vector2(0, 0), boneIndex: 0);
        WriteVertexBdef2(w, new Vector3(1, 0, 0), Vector3.UnitZ, new Vector2(1, 0), b1: 0, b2: 1, w1: 0.75f);
        WriteVertexBdef1(w, new Vector3(0, 1, 0), Vector3.UnitZ, new Vector2(0, 1), boneIndex: 1);

        // Surfaces (one triangle)
        w.Write(3);
        WriteVertexIndex(w, 0);
        WriteVertexIndex(w, 1);
        WriteVertexIndex(w, 2);

        // Textures
        w.Write(1);
        WriteText(w, "tex/body.png");

        // Materials (one toon material with edge + sphere)
        w.Write(1);
        WriteText(w, "顔");
        WriteText(w, "Face");
        WriteVec4(w, new Vector4(0.8f, 0.7f, 0.6f, 1f)); // diffuse
        WriteVec3(w, new Vector3(0.1f, 0.1f, 0.1f));     // specular
        w.Write(5f);                                      // specular strength
        WriteVec3(w, new Vector3(0.2f, 0.2f, 0.2f));     // ambient
        w.Write((byte)(PmxMaterialFlags.HasEdge | PmxMaterialFlags.GroundShadow));
        WriteVec4(w, new Vector4(0, 0, 0, 1));           // edge color
        w.Write(0.5f);                                    // edge scale
        WriteRefIndex(w, 0);                              // texture index
        WriteRefIndex(w, -1);                             // sphere texture index (none)
        w.Write((byte)PmxSphereMode.None);
        w.Write((byte)PmxToonReference.Shared);
        w.Write((byte)1);                                 // shared toon id
        WriteText(w, "memo");
        w.Write(3);                                       // surface count (3 indices)

        // Bones: 0 = center (root), 1 = arm with IK targeting 0,
        // 2 = hair (non-humanoid, carries the physics body)
        w.Write(3);
        WriteBoneSimple(w, "センター", "center", new Vector3(0, 0, 0), parent: -1);
        WriteBoneIk(w, "左腕", "leftArm", new Vector3(0, 1, 0), parent: 0, ikTarget: 0);
        WriteBoneSimple(w, "髪", "hair", new Vector3(0, 2, 0), parent: 1);

        // Morphs: one vertex morph named like a blink
        w.Write(1);
        WriteText(w, "まばたき");
        WriteText(w, "blink");
        w.Write((byte)4);                                 // panel: eye
        w.Write((byte)PmxMorphType.Vertex);
        w.Write(1);                                       // one offset
        WriteVertexIndex(w, 2);
        WriteVec3(w, new Vector3(0, -0.1f, 0));

        // Display frames
        w.Write(1);
        WriteText(w, "表情");
        WriteText(w, "Exp");
        w.Write((byte)1);                                 // special
        w.Write(1);                                       // one item
        w.Write((byte)PmxDisplayItemType.Morph);
        WriteRefIndex(w, 0);

        // Rigid bodies (one dynamic body bound to bone 1)
        w.Write(1);
        WriteText(w, "髪物理");
        WriteText(w, "hairPhys");
        WriteRefIndex(w, 2);                              // bone (hair, non-humanoid)
        w.Write((byte)0);                                 // group
        w.Write((ushort)0);                               // collision mask
        w.Write((byte)PmxRigidBodyShape.Capsule);
        WriteVec3(w, new Vector3(0.2f, 0.5f, 0.2f));     // size
        WriteVec3(w, new Vector3(0, 1, 0));              // position
        WriteVec3(w, Vector3.Zero);                       // rotation
        w.Write(1.0f);                                    // mass
        w.Write(0.9f);                                    // linear damping
        w.Write(0.99f);                                   // angular damping
        w.Write(0.0f);                                    // restitution
        w.Write(0.5f);                                    // friction
        w.Write((byte)PmxPhysicsMode.Physics);

        // Joints (one 6dof spring joint)
        w.Write(1);
        WriteText(w, "髪ジョイント");
        WriteText(w, "hairJoint");
        w.Write((byte)PmxJointType.Spring6Dof);
        WriteRefIndex(w, 0);
        WriteRefIndex(w, 0);
        WriteVec3(w, new Vector3(0, 1, 0));
        WriteVec3(w, Vector3.Zero);
        WriteVec3(w, Vector3.Zero);                       // move limit min
        WriteVec3(w, Vector3.Zero);                       // move limit max
        WriteVec3(w, new Vector3(-1, -1, -1));            // rot limit min
        WriteVec3(w, new Vector3(1, 1, 1));               // rot limit max
        WriteVec3(w, Vector3.Zero);                       // spring move
        WriteVec3(w, new Vector3(10, 10, 10));            // spring rotation

        w.Flush();
        return ms.ToArray();
    }

    // ---- helpers ----------------------------------------------------------

    private static void WriteText(BinaryWriter w, string s)
    {
        var bytes = System.Text.Encoding.Unicode.GetBytes(s);
        w.Write(bytes.Length);
        w.Write(bytes);
    }

    private static void WriteVec2(BinaryWriter w, Vector2 v) { w.Write(v.X); w.Write(v.Y); }
    private static void WriteVec3(BinaryWriter w, Vector3 v) { w.Write(v.X); w.Write(v.Y); w.Write(v.Z); }
    private static void WriteVec4(BinaryWriter w, Vector4 v) { w.Write(v.X); w.Write(v.Y); w.Write(v.Z); w.Write(v.W); }

    private static void WriteVertexIndex(BinaryWriter w, int i) => w.Write((ushort)i);

    private static void WriteRefIndex(BinaryWriter w, int i) => w.Write((short)i);

    private static void WriteVertexBdef1(BinaryWriter w, Vector3 pos, Vector3 nrm, Vector2 uv, int boneIndex)
    {
        WriteVec3(w, pos); WriteVec3(w, nrm); WriteVec2(w, uv);
        w.Write((byte)PmxWeightType.Bdef1);
        WriteRefIndex(w, boneIndex);
        w.Write(1f); // edge scale
    }

    private static void WriteVertexBdef2(BinaryWriter w, Vector3 pos, Vector3 nrm, Vector2 uv, int b1, int b2, float w1)
    {
        WriteVec3(w, pos); WriteVec3(w, nrm); WriteVec2(w, uv);
        w.Write((byte)PmxWeightType.Bdef2);
        WriteRefIndex(w, b1); WriteRefIndex(w, b2); w.Write(w1);
        w.Write(1f); // edge scale
    }

    private static void WriteBoneSimple(BinaryWriter w, string local, string uni, Vector3 pos, int parent)
    {
        WriteText(w, local); WriteText(w, uni);
        WriteVec3(w, pos);
        WriteRefIndex(w, parent);
        w.Write(0);                              // layer
        var flags = PmxBoneFlags.Rotatable | PmxBoneFlags.Translatable | PmxBoneFlags.Visible | PmxBoneFlags.Enabled;
        w.Write((ushort)flags);
        WriteVec3(w, Vector3.Zero);              // tail offset (not indexed)
    }

    private static void WriteBoneIk(BinaryWriter w, string local, string uni, Vector3 pos, int parent, int ikTarget)
    {
        WriteText(w, local); WriteText(w, uni);
        WriteVec3(w, pos);
        WriteRefIndex(w, parent);
        w.Write(0);
        var flags = PmxBoneFlags.Rotatable | PmxBoneFlags.Visible | PmxBoneFlags.Enabled | PmxBoneFlags.Ik;
        w.Write((ushort)flags);
        WriteVec3(w, Vector3.Zero);              // tail offset
        // IK block
        WriteRefIndex(w, ikTarget);
        w.Write(20);                             // loop count
        w.Write(1.0f);                           // limit radians
        w.Write(1);                              // one link
        WriteRefIndex(w, 0);                     // link bone
        w.Write((byte)0);                        // no limit
    }
}

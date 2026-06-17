using System.Numerics;

namespace Pmx2Vrm.Core.Conversion;

/// <summary>
/// Converts geometry from MMD/PMX space into glTF/VRM space.
///
/// MMD  : left-handed, Y up, model faces +Z,
///        units where roughly 12.5 units == 1 metre (a ~20 unit tall model is
///        about 1.6 m).
/// glTF : right-handed, Y up, VRM model faces +Z, units are metres.
///
/// The handedness change is a reflection across the X axis (S = diag(-1,1,1)).
/// Positions/directions negate X; rotation quaternions are conjugated by S,
/// which negates their Y and Z components; triangle winding must be reversed to
/// keep the original front faces.
/// Negating X (instead of Z) changes handedness while preserving the model's
/// +Z front direction — a Z-reflection would flip the model 180°.
///
/// Both VRM 0.x and 1.0 use X-reflection in glTF space. The difference is only
/// at import: VRM 0.x uses ReverseZ, VRM 1.0 uses ReverseX. Either way, the
/// glTF data itself is always X-reflected from MMD.
/// </summary>
public sealed class CoordinateConverter
{
    public const float DefaultScale = 0.08f;

    public float Scale { get; }

    public CoordinateConverter(float scale = DefaultScale)
    {
        if (scale <= 0f) throw new ArgumentOutOfRangeException(nameof(scale));
        Scale = scale;
    }

    public Vector3 Position(Vector3 p) => new(-p.X * Scale, p.Y * Scale, p.Z * Scale);

    public Vector3 Direction(Vector3 d) => new(-d.X, d.Y, d.Z);

    public Quaternion Rotation(Quaternion q) => new(q.X, -q.Y, -q.Z, q.W);

    public Quaternion EulerToQuaternion(Vector3 radians)
    {
        var q = Quaternion.CreateFromYawPitchRoll(radians.Y, radians.X, radians.Z);
        return Rotation(q);
    }

    public static IEnumerable<int> ReverseWinding(IReadOnlyList<int> indices)
    {
        if (indices.Count % 3 != 0)
            throw new ArgumentException("Index count must be a multiple of 3.", nameof(indices));

        for (int i = 0; i < indices.Count; i += 3)
        {
            yield return indices[i];
            yield return indices[i + 2];
            yield return indices[i + 1];
        }
    }
}

using System.Numerics;

namespace Pmx2Vrm.Core.Conversion;

/// <summary>
/// Converts geometry from MMD/PMX space into glTF/VRM space.
///
/// MMD  : left-handed, Y up, units where roughly 12.5 units == 1 metre
///        (a ~20 unit tall model is about 1.6 m).
/// glTF : right-handed, Y up, units are metres.
///
/// The handedness change is a reflection across the Z axis (S = diag(1,1,-1)).
/// Positions/directions negate Z; rotation quaternions are conjugated by S,
/// which negates their X and Y components; triangle winding must be reversed to
/// keep the original front faces.
///
/// Verified empirically in Unity against a known-good reference VRM: with
/// Z-reflection the imported avatar faces +Z with the left side on -X, matching
/// the reference. (X-reflection instead left the model facing -Z — a 180° turn —
/// which broke foot-IK knee direction and mouse-tracking.)
/// </summary>
public sealed class CoordinateConverter
{
    public const float DefaultScale = 0.08f;

    public float Scale { get; }

    /// <summary>Axis negated to convert handedness: true = Z (VRM 1.0), false = X (VRM 0.x).</summary>
    private readonly bool _reflectZ;

    public CoordinateConverter(float scale = DefaultScale, Vrm.VrmVersion version = Vrm.VrmVersion.Vrm1)
    {
        if (scale <= 0f) throw new ArgumentOutOfRangeException(nameof(scale));
        Scale = scale;
        // The reflection axis must match UniVRM's import flip so the avatar ends
        // up facing +Z with its left on -X: VRM 1.0 imports with ReverseX, so the
        // glTF must be Z-reflected; VRM 0.x imports with ReverseZ, so it must be
        // X-reflected. Using the wrong axis turns the model 180° at import.
        _reflectZ = version == Vrm.VrmVersion.Vrm1;
    }

    public Vector3 Position(Vector3 p) => _reflectZ
        ? new(p.X * Scale, p.Y * Scale, -p.Z * Scale)
        : new(-p.X * Scale, p.Y * Scale, p.Z * Scale);

    public Vector3 Direction(Vector3 d) => _reflectZ
        ? new(d.X, d.Y, -d.Z)
        : new(-d.X, d.Y, d.Z);

    public Quaternion Rotation(Quaternion q) => _reflectZ
        ? new(-q.X, -q.Y, q.Z, q.W)
        : new(q.X, -q.Y, -q.Z, q.W);

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

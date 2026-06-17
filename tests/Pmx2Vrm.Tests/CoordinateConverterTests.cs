using System.Numerics;
using Pmx2Vrm.Core.Conversion;
using Xunit;

namespace Pmx2Vrm.Tests;

public class CoordinateConverterTests
{
    [Fact]
    public void Position_negates_x_and_scales()
    {
        var c = new CoordinateConverter(scale: 0.1f);
        var p = c.Position(new Vector3(2, 4, 6));
        Assert.Equal(new Vector3(-0.2f, 0.4f, 0.6f), p);
    }

    [Fact]
    public void Direction_negates_x_without_scaling()
    {
        var c = new CoordinateConverter(scale: 0.1f);
        var d = c.Direction(new Vector3(1, 0, 0));
        Assert.Equal(new Vector3(-1, 0, 0), d);
    }

    [Fact]
    public void Rotation_negates_y_and_z()
    {
        var c = new CoordinateConverter();
        var q = c.Rotation(new Quaternion(0.1f, 0.2f, 0.3f, 0.9f));
        Assert.Equal(new Quaternion(0.1f, -0.2f, -0.3f, 0.9f), q);
    }

    [Fact]
    public void Conversion_preserves_handedness_consistency()
    {
        var c = new CoordinateConverter(scale: 1f);
        var mmdRot = Quaternion.CreateFromAxisAngle(Vector3.UnitY, 0.7f);
        var mmdPoint = new Vector3(1, 2, 3);

        var rotatedThenConverted = c.Position(Vector3.Transform(mmdPoint, mmdRot));
        var convertedThenRotated = Vector3.Transform(c.Position(mmdPoint), c.Rotation(mmdRot));

        Assert.True(Vector3.Distance(rotatedThenConverted, convertedThenRotated) < 1e-5f,
            $"{rotatedThenConverted} != {convertedThenRotated}");
    }

    [Fact]
    public void ReverseWinding_swaps_last_two_of_each_triangle()
    {
        var result = CoordinateConverter.ReverseWinding(new[] { 0, 1, 2, 3, 4, 5 }).ToArray();
        Assert.Equal(new[] { 0, 2, 1, 3, 5, 4 }, result);
    }

    [Fact]
    public void ReverseWinding_rejects_non_multiple_of_three()
    {
        Assert.Throws<ArgumentException>(() => CoordinateConverter.ReverseWinding(new[] { 0, 1 }).ToArray());
    }

    [Fact]
    public void Default_scale_yields_humanlike_height()
    {
        var c = new CoordinateConverter();
        Assert.Equal(1.6f, c.Position(new Vector3(0, 20, 0)).Y, 2);
    }
}
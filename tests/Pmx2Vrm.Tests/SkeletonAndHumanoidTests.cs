using System.Numerics;
using Pmx2Vrm.Core.Conversion;
using Pmx2Vrm.Core.Mapping;
using Pmx2Vrm.Core.Pmx;
using Pmx2Vrm.Core.Vrm;
using Pmx2Vrm.Tests.TestSupport;
using Xunit;

namespace Pmx2Vrm.Tests;

public class BoneNameDictionaryTests
{
    [Theory]
    [InlineData("頭", VrmHumanBone.Head)]
    [InlineData("首", VrmHumanBone.Neck)]
    [InlineData("左腕", VrmHumanBone.LeftUpperArm)]
    [InlineData("右ひざ", VrmHumanBone.RightLowerLeg)]
    [InlineData("左足首", VrmHumanBone.LeftFoot)]
    [InlineData("センター", VrmHumanBone.Hips)]
    [InlineData("上半身", VrmHumanBone.Spine)]
    public void Maps_standard_japanese_names(string name, VrmHumanBone expected)
    {
        Assert.True(BoneNameDictionary.TryGet(name, out var bone));
        Assert.Equal(expected, bone);
    }

    [Theory]
    [InlineData("左親指０", VrmHumanBone.LeftThumbMetacarpal)]
    [InlineData("左人指１", VrmHumanBone.LeftIndexProximal)]
    [InlineData("左人指３", VrmHumanBone.LeftIndexDistal)]
    [InlineData("右小指２", VrmHumanBone.RightLittleIntermediate)]
    [InlineData("右中指１", VrmHumanBone.RightMiddleProximal)]
    public void Maps_finger_names(string name, VrmHumanBone expected)
    {
        Assert.True(BoneNameDictionary.TryGet(name, out var bone));
        Assert.Equal(expected, bone);
    }

    [Fact]
    public void Unknown_name_is_not_mapped()
    {
        Assert.False(BoneNameDictionary.TryGet("髪001", out _));
    }
}

public class HumanoidBoneKeyTests
{
    [Fact]
    public void Vrm10_key_is_camel_case()
    {
        Assert.Equal("leftUpperArm", VrmHumanBone.LeftUpperArm.ToVrm10Key());
        Assert.Equal("leftThumbMetacarpal", VrmHumanBone.LeftThumbMetacarpal.ToVrm10Key());
    }

    [Fact]
    public void Vrm0_thumb_keys_shift_by_one_segment()
    {
        Assert.Equal("leftThumbProximal", VrmHumanBone.LeftThumbMetacarpal.ToVrm0Key());
        Assert.Equal("leftThumbIntermediate", VrmHumanBone.LeftThumbProximal.ToVrm0Key());
        Assert.Equal("leftThumbDistal", VrmHumanBone.LeftThumbDistal.ToVrm0Key());
    }
}

public class SkeletonConverterTests
{
    private static PmxModel Parse() => PmxReader.Read(new MemoryStream(SyntheticPmx.Build()));

    [Fact]
    public void Local_translation_is_parent_relative()
    {
        var model = Parse();
        var conv = new SkeletonConverter(new CoordinateConverter(scale: 1f));
        var skel = conv.Convert(model);

        // bone 0 center at (0,0,0) is a root; bone 1 arm at (0,1,0) child of bone 0.
        Assert.Equal(3, skel.Nodes.Count);
        Assert.Equal(-1, skel.Nodes[0].ParentIndex);
        Assert.Equal(new Vector3(0, 1, 0), skel.Nodes[1].LocalTranslation);
        Assert.Equal(new Vector3(0, 1, 0), skel.Nodes[1].WorldPosition);
    }

    [Fact]
    public void Humanoid_mapping_resolves_known_bones()
    {
        var model = Parse();
        var conv = new SkeletonConverter(new CoordinateConverter());
        var skel = conv.Convert(model);

        Assert.Equal(0, skel.Humanoid[VrmHumanBone.Hips]);        // センター
        Assert.Equal(1, skel.Humanoid[VrmHumanBone.LeftUpperArm]); // 左腕
    }

    [Fact]
    public void Earliest_bone_wins_for_duplicate_humanoid_slot()
    {
        var model = new PmxModel();
        model.Bones.Add(new PmxBone { NameLocal = "センター" });
        model.Bones.Add(new PmxBone { NameLocal = "下半身" }); // also maps to Hips

        var mapping = HumanoidMapper.Map(model);
        Assert.Equal(0, mapping[VrmHumanBone.Hips]);
    }

    [Fact]
    public void Hips_is_reanchored_to_pelvis_lca()
    {
        // センター(0) -> 腰(1) -> { 上半身(2)=spine, 下半身(3) -> 左足(4), 右足(5) }
        // Name match puts hips on センター, but the legs+spine converge at 腰(1).
        var model = new PmxModel();
        model.Bones.Add(new PmxBone { NameLocal = "センター", ParentIndex = -1 }); // 0
        model.Bones.Add(new PmxBone { NameLocal = "腰", ParentIndex = 0 });        // 1 (non-humanoid)
        model.Bones.Add(new PmxBone { NameLocal = "上半身", ParentIndex = 1 });    // 2 spine
        model.Bones.Add(new PmxBone { NameLocal = "下半身", ParentIndex = 1 });    // 3
        model.Bones.Add(new PmxBone { NameLocal = "左足", ParentIndex = 3 });      // 4 leftUpperLeg
        model.Bones.Add(new PmxBone { NameLocal = "右足", ParentIndex = 3 });      // 5 rightUpperLeg

        var map = HumanoidMapper.Map(model);
        Assert.Equal(1, map[VrmHumanBone.Hips]);          // 腰, not センター
        Assert.Equal(2, map[VrmHumanBone.Spine]);
        Assert.Equal(4, map[VrmHumanBone.LeftUpperLeg]);
    }
}

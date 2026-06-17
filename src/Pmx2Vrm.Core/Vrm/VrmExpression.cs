namespace Pmx2Vrm.Core.Vrm;

/// <summary>VRM expression presets (VRM 1.0 naming).</summary>
public enum VrmExpressionPreset
{
    Neutral,
    Aa, Ih, Ou, Ee, Oh,
    Blink, BlinkLeft, BlinkRight,
    Happy, Angry, Sad, Relaxed, Surprised,
    LookUp, LookDown, LookLeft, LookRight,
}

public static class VrmExpressionPresetExtensions
{
    /// <summary>preset key used by VRM 1.0 expressions.preset.</summary>
    public static string ToVrm10Key(this VrmExpressionPreset p) => p switch
    {
        VrmExpressionPreset.BlinkLeft => "blinkLeft",
        VrmExpressionPreset.BlinkRight => "blinkRight",
        VrmExpressionPreset.LookUp => "lookUp",
        VrmExpressionPreset.LookDown => "lookDown",
        VrmExpressionPreset.LookLeft => "lookLeft",
        VrmExpressionPreset.LookRight => "lookRight",
        _ => char.ToLowerInvariant(p.ToString()[0]) + p.ToString()[1..],
    };

    /// <summary>presetName used by VRM 0.x blendShapeGroups.</summary>
    public static string ToVrm0Key(this VrmExpressionPreset p) => p switch
    {
        VrmExpressionPreset.Aa => "a",
        VrmExpressionPreset.Ih => "i",
        VrmExpressionPreset.Ou => "u",
        VrmExpressionPreset.Ee => "e",
        VrmExpressionPreset.Oh => "o",
        VrmExpressionPreset.Blink => "blink",
        VrmExpressionPreset.BlinkLeft => "blink_l",
        VrmExpressionPreset.BlinkRight => "blink_r",
        VrmExpressionPreset.Happy => "joy",
        VrmExpressionPreset.Angry => "angry",
        VrmExpressionPreset.Sad => "sorrow",
        VrmExpressionPreset.Relaxed => "fun",
        VrmExpressionPreset.Surprised => "surprised",
        VrmExpressionPreset.LookUp => "lookup",
        VrmExpressionPreset.LookDown => "lookdown",
        VrmExpressionPreset.LookLeft => "lookleft",
        VrmExpressionPreset.LookRight => "lookright",
        _ => "neutral",
    };
}

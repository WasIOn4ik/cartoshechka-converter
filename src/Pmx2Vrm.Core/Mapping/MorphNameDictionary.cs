using Pmx2Vrm.Core.Vrm;

namespace Pmx2Vrm.Core.Mapping;

/// <summary>
/// Maps standard MMD morph names to VRM expression presets. Both Japanese and
/// common English names are recognised.
/// </summary>
public static class MorphNameDictionary
{
    private static readonly Dictionary<string, VrmExpressionPreset> Map = Build();

    public static bool TryGet(string name, out VrmExpressionPreset preset) =>
        Map.TryGetValue(name.Trim(), out preset);

    private static Dictionary<string, VrmExpressionPreset> Build()
    {
        var m = new Dictionary<string, VrmExpressionPreset>(StringComparer.Ordinal);

        void Add(VrmExpressionPreset p, params string[] names)
        {
            foreach (var n in names) m[n] = p;
        }

        // Vowels (lip sync)
        Add(VrmExpressionPreset.Aa, "あ", "a");
        Add(VrmExpressionPreset.Ih, "い", "i");
        Add(VrmExpressionPreset.Ou, "う", "u");
        Add(VrmExpressionPreset.Ee, "え", "e");
        Add(VrmExpressionPreset.Oh, "お", "o");

        // Blinking
        Add(VrmExpressionPreset.Blink, "まばたき", "瞬き", "blink");
        Add(VrmExpressionPreset.BlinkLeft, "ウィンク", "ウインク", "wink", "blink_l");
        Add(VrmExpressionPreset.BlinkRight, "ウィンク右", "ウインク右", "wink_r", "blink_r");

        // Emotions
        Add(VrmExpressionPreset.Happy, "笑い", "にっこり", "喜び", "happy", "joy");
        Add(VrmExpressionPreset.Angry, "怒り", "むっ", "angry");
        Add(VrmExpressionPreset.Sad, "悲しい", "困る", "sad", "sorrow");
        Add(VrmExpressionPreset.Relaxed, "なごみ", "relaxed", "fun");
        Add(VrmExpressionPreset.Surprised, "びっくり", "驚き", "surprised");

        return m;
    }
}

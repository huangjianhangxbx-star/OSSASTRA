// 功能：执行实际Settings的外缘默认、克隆隔离和非法输入归一合同；不加载或修改场景。
using System;
using System.Linq;
using System.Reflection;
using Ossastra.CharacterLighting;
public static class Program
{
    static int checks;
    static void Check(bool value, string name)
    { checks++; if (!value) throw new Exception(name); }
    static FieldInfo Field(string name) => typeof(CharacterLightingSettings).GetField(name);
    static float Read(CharacterLightingSettings state, string name) => (float)Field(name).GetValue(state);
    public static int Main()
    {
        try
        {
            string[] fields = { "outerGlow", "outerDiagnostic", "outerWidth", "outerStrength", "maxOuterGain", "outerSoftness" };
            Check(fields.All(name => Field(name) != null), "External glow Settings contract missing");
            var state = new CharacterLightingSettings();
            state.Sanitize();
            Check((bool)Field("outerGlow").GetValue(state) && !(bool)Field("outerDiagnostic").GetValue(state), "Normal default enables glow rather than diagnostics");
            Check(Read(state, "outerWidth") >= 1 && Read(state, "outerWidth") <= 4 && Read(state, "outerStrength") > 0 && Read(state, "maxOuterGain") > 0, "Normal preset provides bounded nonzero external energy");
            Field("outerWidth").SetValue(state, 3.5f); Field("outerStrength").SetValue(state, .73f);
            Field("maxOuterGain").SetValue(state, .42f); Field("outerSoftness").SetValue(state, 2.7f);
            Field("outerDiagnostic").SetValue(state, true);
            var clone = state.Clone();
            Check(!ReferenceEquals(state, clone) && fields.All(name => Equals(Field(name).GetValue(state), Field(name).GetValue(clone))), "Clone preserves every external control");
            Field("outerStrength").SetValue(clone, .1f);
            Check(Math.Abs(Read(state, "outerStrength") - .73f) < .00001f, "Runtime clone cannot modify author settings");
            Field("outerWidth").SetValue(state, float.NaN); Field("outerStrength").SetValue(state, float.PositiveInfinity);
            Field("maxOuterGain").SetValue(state, -1f); Field("outerSoftness").SetValue(state, 100f);
            state.Sanitize();
            Check(Read(state, "outerWidth") == 2 && Math.Abs(Read(state, "outerStrength") - .22f) < .00001f && Read(state, "maxOuterGain") == 0 && Read(state, "outerSoftness") == 4, "Illegal outer controls normalize to finite bounded values");
            Field("outerStrength").SetValue(state, 0f); state.Sanitize();
            Check(Read(state, "outerStrength") == 0, "Zero energy remains an exact disable path");
            Console.WriteLine("PASS " + checks + " Settings behavior checks"); return 0;
        }
        catch (Exception error) { Console.Error.WriteLine("FAIL " + error.Message); return 1; }
    }
}

// 功能：验证用户明确批准的max缺省与融合控制独立Clone/Sanitize。
using System;
using Ossastra.CharacterLighting;
public static class Program
{
 static void Check(bool x,string label){if(!x)throw new Exception(label);}
 public static int Main(){try{
 var s=new CharacterLightingSettings();
 Check(s.rimStrength==2 && s.softness==2 && s.maxRimGain==2 && s.knee==16 && s.ceiling==32,"Approved max defaults missing");
 Check(s.outerWidth==4 && s.outerStrength==2 && s.maxOuterGain==1 && s.outerSoftness==4 && !s.outerDiagnostic,"Approved outer defaults/normal rendering missing");
 var mode=typeof(CharacterLightingSettings).GetField("enhancedEnvironmentTint");var blend=typeof(CharacterLightingSettings).GetField("environmentBlend");
 Check(mode!=null && blend!=null,"Fusion controls missing");
 mode.SetValue(s,false);blend.SetValue(s,.37f);var c=s.Clone();Check(!(bool)mode.GetValue(c) && (float)blend.GetValue(c)==.37f,"Clone lost fusion parameters");
 blend.SetValue(c,.8f);Check((float)blend.GetValue(s)==.37f,"Clone modified author settings");
 blend.SetValue(c,float.NaN);c.outerStrength=float.NaN;c.Sanitize();Check((float)blend.GetValue(c)==2 && c.outerStrength==2,"Finite defaults inconsistent");
 blend.SetValue(c,0f);c.Sanitize();Check((float)blend.GetValue(c)==0,"Zero must preserve A path");
 Console.WriteLine("PASS approved defaults / clone / sanitize / zero");return 0;
 }catch(Exception e){Console.WriteLine("FAIL "+e.Message);return 1;}}
}

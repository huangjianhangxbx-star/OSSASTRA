/* 功能：显式另存唯一候选，或将运行副本保存至该角色已绑定的独立默认Profile并读回。
 * 边界：不覆盖共享SO、不替换角色引用、不保存运行场景/全部资产；Player仅返回不可用说明。
 * 生命周期：保存失败仅清理本调用新建但未进入AssetDatabase的SO，不删除用户文件。
 */
using System;
using System.Linq;
using UnityEngine;
using Ossastra.CharacterLighting;
namespace Ossastra.Development.CharacterLighting
{
 internal static class CharacterLightingProfileSave
 {
  // 主场由作者预先绑定每角色独立资产；Play中只写该资产，不把运行场景或另一人的Clone存回去。
  internal static bool TrySaveDefault(CharacterLightingReceiver receiver,out string result)
  {
#if UNITY_EDITOR
   result="未登记角色独立Profile，先另存候选后在编辑态绑定。";
   if(receiver==null||receiver.profile==null)return false;
   var profile=receiver.profile;string path=UnityEditor.AssetDatabase.GetAssetPath(profile);
   if(!path.StartsWith("Assets/OSSASTRA/CharacterLighting/Profiles/",StringComparison.Ordinal)){result="默认保存只允许角色光感配置目录。";return false;}
   foreach(var other in Resources.FindObjectsOfTypeAll<CharacterLightingReceiver>())
    if(other!=null&&other!=receiver&&other.gameObject.scene.IsValid()&&other.profile==profile){result="此Profile仍被其他角色共享；请先另存独立候选。";return false;}
   var copy=receiver.Settings.Clone();copy.Sanitize();string expected=JsonUtility.ToJson(copy);
   UnityEditor.Undo.RecordObject(profile,"Save character lighting default");profile.settings=copy;
   UnityEditor.EditorUtility.SetDirty(profile);UnityEditor.AssetDatabase.SaveAssetIfDirty(profile);
   var saved=UnityEditor.AssetDatabase.LoadAssetAtPath<CharacterLightingProfileSO>(path);
   if(saved==null||JsonUtility.ToJson(saved.settings)!=expected)throw new InvalidOperationException("Profile读回不一致。");
   result=path;return true;
#else
   result="保存默认仅在Editor可用；当前运行副本未写入资源。";return false;
#endif
  }
  internal static bool TrySave(CharacterLightingSettings settings,string actorId,out string path)
  {
#if UNITY_EDITOR
   if(settings==null)throw new ArgumentNullException(nameof(settings));
   const string folder="Assets/OSSASTRA/CharacterLighting/Profiles";EnsureFolder(folder);
   var invalid=System.IO.Path.GetInvalidFileNameChars();var safe=new string((string.IsNullOrWhiteSpace(actorId)?"Character":actorId).Select(c=>Array.IndexOf(invalid,c)>=0?'_':c).ToArray());
   path=UnityEditor.AssetDatabase.GenerateUniqueAssetPath(folder+"/"+safe+"_Candidate.asset");
   var candidate=ScriptableObject.CreateInstance<CharacterLightingProfileSO>();candidate.settings=settings.Clone();candidate.settings.Sanitize();
   try{UnityEditor.AssetDatabase.CreateAsset(candidate,path);UnityEditor.AssetDatabase.SaveAssetIfDirty(candidate);return true;}
   catch{if(candidate!=null&&!UnityEditor.AssetDatabase.Contains(candidate))UnityEngine.Object.DestroyImmediate(candidate);throw;}
#else
   path="候选Profile仅可在Editor显式保存；当前运行副本未写入共享资源。";return false;
#endif
  }
#if UNITY_EDITOR
  static void EnsureFolder(string path)
  {int split=path.LastIndexOf('/');if(split<0)return;string parent=path.Substring(0,split);if(!UnityEditor.AssetDatabase.IsValidFolder(parent))EnsureFolder(parent);if(!UnityEditor.AssetDatabase.IsValidFolder(path))UnityEditor.AssetDatabase.CreateFolder(parent,path.Substring(split+1));}
#endif
 }
}

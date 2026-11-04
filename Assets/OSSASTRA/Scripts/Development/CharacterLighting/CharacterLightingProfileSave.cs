/* 功能：只在显式作者操作时，把角色运行时副本保存为新的唯一候选Profile资产。
 * 边界：不覆盖原共享SO、不替换角色引用、不保存场景/全部资产；Player仅返回不可用说明。
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


/* 脚本：GroundAssetLedger
 * 功能：记录本次操作创建的精确资产/文件，失败只撤回本次输出，成功保留供Undo/Redo引用。
 * 边界：不是全局垃圾清理；不删除已有文件/其他任务内容，不依赖Undo恢复磁盘写入。
 */
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace Ossastra.GroundResearch.Editor
{
    internal sealed class GroundAssetLedger : IDisposable
    {
        private readonly List<string> created = new List<string>();
        private readonly List<string> folders = new List<string>();
        private bool committed;
        public void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int cut=path.LastIndexOf('/');
            if(cut<0) throw new InvalidOperationException("非法Assets目录："+path);
            Folder(path.Substring(0,cut));
            AssetDatabase.CreateFolder(path.Substring(0,cut),path.Substring(cut+1)); folders.Add(path);
        }
        public void Reserve(string path)
        {
            if ((!GroundWorkshopOwnership.IsUnder(path,GroundWorkshopOwnership.Root) && !GroundWorkshopOwnership.IsScenePath(path)) || File.Exists(path) ||
                File.Exists(path+".meta") || AssetDatabase.LoadMainAssetAtPath(path)!=null)
                throw new InvalidOperationException("拒绝覆盖已有/非受控资产："+path);
            Folder(Path.GetDirectoryName(path).Replace('\\','/')); created.Add(path);
        }
        public T Create<T>(T value,string path) where T:UnityEngine.Object
        {
            Reserve(path); AssetDatabase.CreateAsset(value,path); EditorUtility.SetDirty(value);
            AssetDatabase.SaveAssetIfDirty(value); return value;
        }
        public void Commit() { committed=true; }
        public void Dispose()
        {
            if (committed) return;
            // 清单由单一Editor事务生成，绝不枚举目录递归删除；提交后的资产供原生Undo重做引用。
            foreach(string path in created.AsEnumerable().Reverse())
                if (File.Exists(path) || File.Exists(path+".meta") || AssetDatabase.LoadMainAssetAtPath(path)!=null)
                    AssetDatabase.DeleteAsset(path);
            foreach(string folder in folders.AsEnumerable().Reverse())
                if (AssetDatabase.IsValidFolder(folder) && !Directory.EnumerateFileSystemEntries(folder).Any()) AssetDatabase.DeleteAsset(folder);
        }
    }
}
#endif

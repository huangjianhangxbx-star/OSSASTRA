/* 功能：将明确Renderer完整槽接入已声明材质角色；沿用原Comparison唯一Look写入者。
 * 边界：仅Edit作者接线；预检全部目标后只改绑定策略与角色引用，不改任何材质/Look/默认或运行缓存。
 * 保护：原A/B/C源数组与未知槽不动，私有材质、跨场景和不完整角色声明拒绝；重复接线幂等。
 */
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ossastra.WorldRendering.VoxelSurface
{
    public static class VoxelSurfaceMaterialAuthoring
    {
        public static int ApplyToComparison(VoxelSurfaceComparison comparison,VoxelSurfaceMaterialRoleSetSO roleSet,Renderer[] renderers)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("须保留运行参数并退出Play后登记作者材质角色");
            if(comparison==null||roleSet==null)throw new ArgumentNullException(comparison==null?nameof(comparison):nameof(roleSet));
            var scene=comparison.gameObject.scene;
            if(!scene.IsValid()||!scene.isLoaded||EditorUtility.IsPersistent(comparison))throw new InvalidOperationException("Comparison必须属于明确已加载作者场景");
            ValidatePersistentRoles(roleSet);
            if(comparison.materialRoles!=null&&comparison.materialRoles!=roleSet)throw new InvalidOperationException("Comparison已声明其他材质角色集，拒绝静默替换");
            if(renderers==null||renderers.Length==0)throw new InvalidOperationException("须显式给出本次换色Renderer，不能扫描全场");
            var byRenderer=new Dictionary<Renderer,VoxelSurfaceComparison.Binding>();
            foreach(var binding in comparison.bindings??Array.Empty<VoxelSurfaceComparison.Binding>())
            {
                if(binding==null||binding.renderer==null||byRenderer.ContainsKey(binding.renderer))throw new InvalidOperationException("原绑定缺失或重复，先修正作者接线");
                byRenderer.Add(binding.renderer,binding);
            }
            var selected=new List<VoxelSurfaceComparison.Binding>();var unique=new HashSet<Renderer>();
            foreach(var renderer in renderers)
            {
                if(renderer==null||!unique.Add(renderer)||renderer.gameObject.scene!=scene||EditorUtility.IsPersistent(renderer))throw new InvalidOperationException("明确目标缺失、重复或跨场景");
                if(!byRenderer.TryGetValue(renderer,out var binding))throw new InvalidOperationException("目标尚未登记到原统一受光Comparison："+renderer.name);
                var current=renderer.sharedMaterials;
                if(!Same(binding.original,current)||!Same(binding.calibrated,current)||!Same(binding.shaped,current))throw new InvalidOperationException("需要完整且未被运行预览接管的同源A/B/C作者槽："+renderer.name);
                if(!roleSet.ContainsAll(current,out var reason)||current.Any(m=>m==null||!EditorUtility.IsPersistent(m)))throw new InvalidOperationException("完整材质槽未获持久角色声明："+renderer.name+" / "+reason);
                selected.Add(binding);
            }
            int changed=selected.Count(b=>b.lightingOnly);
            if(changed==0&&comparison.materialRoles==roleSet)return 0;
            Undo.RecordObject(comparison,"登记显式体素材质角色");
            comparison.materialRoles=roleSet;
            foreach(var binding in selected)binding.lightingOnly=false;
            if(PrefabUtility.IsPartOfPrefabInstance(comparison))PrefabUtility.RecordPrefabInstancePropertyModifications(comparison);
            EditorUtility.SetDirty(comparison);EditorSceneManager.MarkSceneDirty(scene);
            return changed;
        }

        public static void ValidatePersistentRoles(VoxelSurfaceMaterialRoleSetSO roleSet)
        {
            if(roleSet==null||!EditorUtility.IsPersistent(roleSet))throw new InvalidOperationException("材质角色声明必须是持久作者资产");
            if(!roleSet.Validate(out var reason))throw new InvalidOperationException(reason);
            foreach(var entry in roleSet.entries)if(!EditorUtility.IsPersistent(entry.source))throw new InvalidOperationException("角色声明不能引用运行私有材质");
        }
        static bool Same(Material[] a,Material[] b) { return a!=null&&b!=null&&a.SequenceEqual(b); }
    }
}
#endif

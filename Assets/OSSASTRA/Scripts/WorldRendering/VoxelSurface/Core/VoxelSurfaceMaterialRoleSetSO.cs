/* 功能：以持久作者材质身份声明可参与Look换色的语义角色与变体。
 * 边界：不按名字/RGB推断、不扫描Renderer、不创建材质；仅供原Look作者接线验证消费。
 */
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ossastra.WorldRendering.VoxelSurface
{
    [CreateAssetMenu(menuName="OSSASTRA/Rendering/Voxel Surface Material Roles",fileName="VoxelSurfaceMaterialRoles")]
    public sealed class VoxelSurfaceMaterialRoleSetSO : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public Material source;
            [Tooltip("明确的作者语义，例如wood.panel、roof.base、stone.cap；不从材质名推断。")]
            public string roleId;
            [Tooltip("同一角色的稳定变体，例如base、dark、light。")]
            public string variantId="base";
        }
        public Entry[] entries=Array.Empty<Entry>();

        public bool Validate(out string reason)
        {
            if(entries==null||entries.Length==0) { reason="材质角色声明为空";return false; }
            var sources=new HashSet<Material>();
            foreach(var entry in entries)
            {
                if(entry==null||entry.source==null||!sources.Add(entry.source)) { reason="材质源缺失或重复";return false; }
                if(string.IsNullOrWhiteSpace(entry.roleId)||string.IsNullOrWhiteSpace(entry.variantId)) { reason="材质角色或变体身份为空";return false; }
                if(entry.source.shader==null||entry.source.shader.name!="Ossastra/WorldRendering/VoxelLit") { reason="角色声明只接受明确VoxelLit作者材质";return false; }
            }
            reason="";return true;
        }

        public bool TryGetRole(Material source,out Entry entry)
        {
            if(source!=null&&entries!=null)foreach(var item in entries)if(item!=null&&item.source==source){entry=item;return true;}
            entry=null;return false;
        }
        public bool Contains(Material source) { return TryGetRole(source,out _); }
        public bool ContainsAll(Material[] sources,out string reason)
        {
            if(!Validate(out reason))return false;
            if(sources==null||sources.Length==0) { reason="Renderer完整材质槽为空";return false; }
            foreach(var source in sources)if(!Contains(source)){reason="存在未显式声明的材质槽";return false;}
            reason="";return true;
        }
    }
}

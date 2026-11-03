/* 功能：提供主场环境 Look 的独立效果开关，供作者在 Inspector 比较原图与单项贡献。
 * 边界：开关只影响环境显示，不推进角色、视野资格、灯光、业务或存档。
 * 协作与生命周期：WorldLookController 在原房间帧发布后读取；本类型不订阅事件、不拥有渲染资源。
 */
using System;

namespace Ossastra.WorldRendering
{
    [Serializable]
    public sealed class WorldLookEffectOptions
    {
        public bool enabled = true;
        public bool airEnabled = true;
        public bool focusEnabled = true;
        public bool bloomEnabled = true;
        public bool gradeEnabled = true;
        public bool vignetteEnabled = true;
    }
}

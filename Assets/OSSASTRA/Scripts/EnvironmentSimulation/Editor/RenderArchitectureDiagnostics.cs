/* 功能：只读导出当前环境/Look/角色链与实际材质Pass准备状态，供青蓝故障保留现场。
 * 边界：只读当前场的明确根和它们登记的Renderer，不扫描历史Assets、不编译或修改参数。
 */
#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Ossastra.CharacterLighting;
using Ossastra.WorldRendering;
using Ossastra.EnvironmentSimulation.Rendering;
namespace Ossastra.EnvironmentSimulation.Editor
{
    public static class RenderArchitectureDiagnostics
    {
        [MenuItem("XingHai/渲染/复制当前渲染诊断")]
        static void CopySnapshot() { EditorGUIUtility.systemCopyBuffer = Capture(); Debug.Log("当前渲染诊断已复制；未修改运行现场。"); }
        public static string Capture()
        {
            var scene = SceneManager.GetActiveScene();
            var components = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MonoBehaviour>(true)).Where(c => c != null).ToArray();
            var system = components.OfType<CharacterLightingSystem>().FirstOrDefault();
            var environment = components.OfType<EnvironmentRoot>().FirstOrDefault();
            var sky = components.OfType<EnvironmentSkyBinding>().FirstOrDefault();
            var look = components.OfType<WorldLookController>().FirstOrDefault();
            var passes = new List<Pass>();
            var seen = new HashSet<int>();
            if (system != null && system.receivers != null)
                foreach (var receiver in system.receivers)
                    if (receiver != null)
                        foreach (var renderer in receiver.Members)
                            if (renderer != null)
                                foreach (var material in renderer.sharedMaterials)
                                    if (material != null && seen.Add(material.GetInstanceID()))
                                        for (int pass = 0; pass < material.passCount; ++pass)
                                            passes.Add(new Pass { material = material.name, shader = material.shader == null ? "missing" : material.shader.name, pass = pass, compiled = ShaderUtil.IsPassCompiled(material, pass), supported = material.shader != null && material.shader.isSupported });
            return JsonUtility.ToJson(new Snapshot { scene = scene.path, frame = Time.frameCount, playing = Application.isPlaying,
                asyncCompilation = ShaderUtil.allowAsyncCompilation, anythingCompiling = ShaderUtil.anythingCompiling,
                environment = environment == null ? "missing" : environment.Status, surface = sky == null ? "missing" : sky.SurfaceStatus,
                sky = sky == null ? "missing" : sky.Status, look = look == null ? "missing" : look.Status,
                coverage = system == null ? "missing" : system.Status, glow = system == null ? "missing" : system.OuterGlowStatus, passes = passes.ToArray() }, true);
        }
        [System.Serializable] sealed class Snapshot { public string scene, environment, surface, sky, look, coverage, glow; public int frame; public bool playing, asyncCompilation, anythingCompiling; public Pass[] passes; }
        [System.Serializable] sealed class Pass { public string material, shader; public int pass; public bool compiled, supported; }
    }
}
#endif

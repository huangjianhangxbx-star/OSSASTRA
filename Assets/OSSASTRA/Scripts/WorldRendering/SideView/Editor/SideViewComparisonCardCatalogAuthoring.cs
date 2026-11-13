/*
 * 编辑期生成入口。配套清单保存两次场景审查与原作实例比对得到的元数据。
 * 将此文件作为 .cs 放入 Editor 目录。无自动运行入口，由正式接入操作显式调用。
 */
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ossastra.WorldRendering.SideView
{
    public static class SideViewComparisonCardCatalogAuthoring
    {
        // JsonUtility 从清单填充这些字段。
#pragma warning disable 0649
        [Serializable]
        private sealed class Manifest
        {
            public int schema;
            public string scenePath;
            public Card[] cards;
        }

        [Serializable]
        private sealed class Card
        {
            public string root;
            public string rendererName;
            public string materialPath;
            public string materialShader;
            public string sourceShader;
            public string surfaceKind;
            public Vector3 localPosition;
            public int sortingOrder;
            public int sortingLayer;
            public bool belowActors;
            public bool farBackground;
            public bool replacementCoverage;
            public bool projectLightMap;
            public float sourceBlend;
            public float destinationBlend;
            public float premultiply;
            public float blurPixels;
            public float environmentRole;
            public float metadataRole;
            public float originalAmbient;
            public Vector4 originalColorScale;
        }
#pragma warning restore 0649

        // roots 使用 SideViewCharacterRendering 当前配置中五个明确的画片根。
        // 调用者在所有配方资产、RendererData 与场景改动准备完毕后统一保存。
        public static int Apply(SideViewComparisonCardCatalog catalog, TextAsset manifest, params Transform[] roots)
        {
            if (catalog == null || manifest == null || roots == null)
                throw new ArgumentException("生成横版卡片登记需要正式场景组件、清单及五个画片根。");

            Manifest data = JsonUtility.FromJson<Manifest>(manifest.text);
            var scene = catalog.gameObject.scene;
            if (data == null || data.schema != 1 || data.cards == null || scene.path != data.scenePath)
                throw new InvalidOperationException("当前场景与横版卡片登记清单不匹配。");

            var renderers = new Dictionary<(string Root, string RendererName), MeshRenderer>();
            var rootNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (Transform root in roots)
            {
                if (root == null || root.gameObject.scene != scene || !rootNames.Add(root.name))
                    throw new InvalidOperationException("横版卡片根为空、重复或属于其他场景。");

                foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (!renderers.TryAdd((root.name, renderer.name), renderer))
                        throw new InvalidOperationException($"画片名重复：{root.name}/{renderer.name}");
                }
            }

            if (rootNames.Count != 5 || renderers.Count != data.cards.Length)
                throw new InvalidOperationException($"横版卡片清单数量不匹配：现场 {renderers.Count}，清单 {data.cards.Length}。");

            var entries = new SideViewComparisonCardCatalog.Entry[data.cards.Length];
            for (int index = 0; index < data.cards.Length; index++)
            {
                Card card = data.cards[index];
                if (card == null || !renderers.TryGetValue((card.root, card.rendererName), out MeshRenderer renderer))
                    throw new InvalidOperationException($"横版画片不存在或在清单中重复：{card?.root}/{card?.rendererName}");

                Material[] materials = renderer.sharedMaterials;
                Material material = materials.Length == 1 ? materials[0] : null;
                if (!renderer.enabled || !renderer.gameObject.activeSelf || renderer.gameObject.layer != 0 ||
                    renderer.sortingOrder != card.sortingOrder || renderer.sortingLayerID != card.sortingLayer ||
                    (renderer.transform.localPosition - card.localPosition).sqrMagnitude > 0.000001f ||
                    card.belowActors != (renderer.sortingOrder == 0) ||
                    card.farBackground != (card.belowActors && renderer.transform.position.z >= 19f) ||
                    material == null || material.shader == null || !material.HasProperty("_MainTex") ||
                    material.GetTexture("_MainTex") == null || material.shader.name != card.materialShader ||
                    AssetDatabase.GetAssetPath(material) != card.materialPath || material.shaderKeywords.Length != 0 ||
                    !BlendMatches(material, card))
                    throw new InvalidOperationException($"横版画片内容与清单不匹配：{card.root}/{card.rendererName}");

                entries[index] = new SideViewComparisonCardCatalog.Entry(renderer, material, card.belowActors,
                    card.farBackground, card.replacementCoverage, card.projectLightMap, card.environmentRole,
                    card.metadataRole, card.originalAmbient, card.originalColorScale, card.sourceShader, card.surfaceKind);
                renderers.Remove((card.root, card.rendererName));
            }

            Undo.RecordObject(catalog, "Bind Side View Comparison Cards");
            catalog.SetAuthoringEntries(entries);
            EditorSceneManager.MarkSceneDirty(scene);
            return entries.Length;
        }

        private static bool BlendMatches(Material material, Card card)
        {
            if (material.renderQueue != (int)RenderQueue.Transparent)
                return false;
            if (card.materialShader == "Sprites/Default")
                return material.GetFloat("PixelSnap") == 0 && material.GetFloat("_EnableExternalAlpha") == 0 &&
                    card.sourceBlend == (int)BlendMode.One && card.destinationBlend == (int)BlendMode.OneMinusSrcAlpha &&
                    card.premultiply == 1 && card.blurPixels == 0;
            return card.materialShader == "SceneLab/SourceSprite" &&
                material.GetFloat("_SrcBlend") == card.sourceBlend && material.GetFloat("_DstBlend") == card.destinationBlend &&
                material.GetFloat("_Premultiply") == card.premultiply && material.GetFloat("_BlurPixels") == card.blurPixels;
        }
    }
}
#endif

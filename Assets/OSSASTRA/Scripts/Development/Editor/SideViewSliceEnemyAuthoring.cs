/*
 * 脚本：SideViewSliceEnemyAuthoring
 * 功能：给横版场景中的敌人绑定身体、动作、感知和本机参考动画。
 * 负责：生成及升级既有实例的素材接线，保持稳定身份和世界存档语义。
 * 边界：不运行敌人的行为，原素材留在本机，使用 AssetDatabase 做实际导入。
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ossastra.CharacterCore;
using Ossastra.Combat;
using Ossastra.Enemies;
using UnityEditor;
using UnityEngine;
using SliceEnemyRoot = Ossastra.Enemies.EnemyRoot;

namespace Ossastra.SideView.Slice.Editor
{
    public static class SideViewSliceEnemyAuthoring
    {
        private const string PrivateAssets = "Assets/OSSASTRA/SideView/Slice/PrivateArt";
        private const string SpriteMaterial = "Assets/OSSASTRA/SideView/Slice/Materials/SideViewEnemySprite.mat";

        [Serializable] private sealed class Manifest { public Import import; public Clip[] clips; }
        [Serializable] private sealed class Import { public float pixelsPerUnit; public Vector2 pivot; public float feetOffsetYFromColliderUnits; }
        [Serializable] private sealed class Clip { public string name; public float fps; public int wrapMode, loopStart; public Frame[] frames; }
        [Serializable] private sealed class Frame { public string sprite; }
        [Serializable] private sealed class CorpseManifest { public CorpseClip corpseRoar; }
        [Serializable] private sealed class CorpseClip { public float pixelsPerUnit, fps; public Vector2 pivot; public Frame[] frames; }

        private sealed class Spec
        {
            public SideViewEnemyKind kind;
            public float bodyHeight, visualHeight, release, active, recovery, reach, radius, wake, range, vertical, speed, dash;
            public int hp, damage;
            public string[] poses;
        }

        private static readonly string[] Keys = { "idle", "alert", "windup", "active", "recover", "death", "windup2", "active2", "recover2", "bump", "wake", "turn", "sing", "ascend", "walk", "hurt" };

        public static SliceEnemyRoot Place(Transform anchor, string stableId, string candidateId, CombatVitals[] targets, string artRoot)
        {
            if (anchor == null || string.IsNullOrWhiteSpace(stableId) || targets == null || targets.Length == 0 ||
                targets.Any(target => target == null))
                throw new ArgumentException("Enemy placement needs an anchor, stable identity and live character references.");
            var spec = Specification(candidateId);
            var existing = anchor.Find(stableId);
            if (existing != null) throw new InvalidOperationException("Enemy is already authored at " + stableId);
            var clips = LoadClips(candidateId, artRoot, spec, out var manifest);
            var material = LoadSpriteMaterial();
            var enemy = new GameObject(stableId);
            Undo.RegisterCreatedObjectUndo(enemy, "Place side-view enemy");
            enemy.transform.SetParent(anchor, false);
            enemy.SetActive(false);
            var body = enemy.AddComponent<CharacterController>();
            body.height = spec.bodyHeight;
            body.radius = Mathf.Clamp(spec.bodyHeight * .23f, .16f, .56f);
            body.center = Vector3.up * spec.bodyHeight * .5f;
            body.skinWidth = .03f;
            body.stepOffset = .06f;
            body.minMoveDistance = 0;
            var brain = enemy.AddComponent<SideViewEnemyBrain>();
            var vitals = enemy.AddComponent<CombatVitals>();
            var hit = enemy.AddComponent<CombatMeleeHit>();
            var root = enemy.AddComponent<SliceEnemyRoot>();
            Set(vitals, "maximumHp", spec.hp);
            var combat = new SerializedObject(vitals);
            combat.FindProperty("faction").enumValueIndex = (int)CombatFaction.Enemy;
            combat.ApplyModifiedPropertiesWithoutUndo();
            Set(root, "actorId", stableId);
            Set(root, "damage", spec.damage);
            Set(root, "releaseAt", spec.release);
            Set(root, "moveReadyAt", spec.release + spec.active);
            Set(root, "attackReadyAt", spec.release + spec.active + spec.recovery);
            Set(root, "finishAt", spec.release + spec.active + spec.recovery);
            Set(hit, "reach", spec.reach);
            Set(hit, "radius", spec.radius);
            Set(hit, "height", spec.bodyHeight * .55f);
            var brainSettings = new SerializedObject(brain);
            brainSettings.FindProperty("kind").enumValueIndex = (int)spec.kind;
            brainSettings.FindProperty("wakeRange").floatValue = spec.wake;
            brainSettings.FindProperty("attackRange").floatValue = spec.range;
            brainSettings.FindProperty("verticalRange").floatValue = spec.vertical;
            brainSettings.FindProperty("walkSpeed").floatValue = spec.speed;
            brainSettings.FindProperty("attackSpeed").floatValue = spec.dash;
            var candidates = brainSettings.FindProperty("targets");
            candidates.arraySize = targets.Length;
            for (int index = 0; index < targets.Length; index++)
                candidates.GetArrayElementAtIndex(index).objectReferenceValue = targets[index];
            brainSettings.ApplyModifiedPropertiesWithoutUndo();
            var visual = new GameObject("SourceSprites");
            visual.transform.SetParent(enemy.transform, false);
            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sharedMaterial = material;
            renderer.sprite = clips[0].frames[0];
            renderer.sortingOrder = 5;
            var drawnHeight = Mathf.Max(.01f, renderer.sprite.bounds.size.y);
            var scale = candidateId.StartsWith("EM-") ? 1.12f : spec.visualHeight / drawnHeight;
            visual.transform.localScale = Vector3.one * scale;
            visual.transform.localPosition = new Vector3(0, manifest.import.feetOffsetYFromColliderUnits * scale, -.025f);
            enemy.AddComponent<SideViewEnemySprites>().Configure(renderer, clips, candidateId != "SS-N03");
            EditorUtility.SetDirty(enemy.GetComponent<SideViewEnemySprites>());
            enemy.SetActive(true);
            return root;
        }

        /// <summary>在既有真实场景上补齐状态动画和受击材质，维持实例标记、碰撞和档案身份。</summary>
        public static void UpgradeExisting(SliceEnemyRoot enemy, string artRoot)
        {
            if (enemy == null || !enemy.TryGetComponent<SideViewEnemySprites>(out var sprites))
                throw new ArgumentException("Enemy upgrade requires an existing side-view root and sprite view.");
            var marker = enemy.GetComponentInParent<SVSliceEnemySpawn>();
            if (marker == null || string.IsNullOrWhiteSpace(marker.candidateId))
                throw new InvalidOperationException("Missing candidate identity on " + enemy.name);
            var renderer = new SerializedObject(sprites).FindProperty("visual").objectReferenceValue as SpriteRenderer;
            if (renderer == null) throw new InvalidOperationException("Missing sprite view on " + enemy.name);
            var material = LoadSpriteMaterial();
            var clips = LoadClips(marker.candidateId, artRoot, Specification(marker.candidateId), out _);
            Undo.RecordObjects(new UnityEngine.Object[] { sprites, renderer }, "Update side-view enemy appearance");
            renderer.sharedMaterial = material;
            sprites.Configure(renderer, clips, marker.candidateId != "SS-N03");
            EditorUtility.SetDirty(sprites);
            EditorUtility.SetDirty(renderer);
        }

        private static SideViewEnemySprites.Sequence[] LoadClips(string candidateId, string artRoot, Spec spec, out Manifest manifest)
        {
            var directory = candidateId.StartsWith("EM-", StringComparison.Ordinal)
                ? Path.Combine(artRoot, "InPlace", candidateId)
                : Path.Combine(artRoot, candidateId);
            manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(Path.Combine(directory, "manifest.json")));
            if (manifest?.import == null || manifest.clips == null || manifest.import.pixelsPerUnit <= 0)
                throw new InvalidDataException("Enemy sprite manifest is incomplete for " + candidateId);
            PrepareAssets(candidateId, directory, manifest, spec.poses);
            var clips = ImportClips(candidateId, manifest, spec.poses);
            return candidateId == "SS-B01" ? clips.Append(ImportMotherCorpse(artRoot)).ToArray() : clips;
        }

        private static Material LoadSpriteMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(SpriteMaterial);
            var shader = Shader.Find("Ossastra/Combat/SideViewHitSprite");
            if (shader == null || !shader.isSupported)
                throw new InvalidOperationException("横版敌人受击 Shader 未导入或不受支持。");
            if (material != null)
            {
                if (material.shader != shader)
                    throw new InvalidOperationException("共用敌人材质引用了其他 Shader：" + SpriteMaterial);
                return material;
            }
            const string parent = "Assets/OSSASTRA/SideView/Slice";
            if (!AssetDatabase.IsValidFolder(parent + "/Materials"))
                AssetDatabase.CreateFolder(parent, "Materials");
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, SpriteMaterial);
            return material;
        }

        private static void PrepareAssets(string id, string directory, Manifest manifest, string[] names)
        {
            var actorPath = PrivateAssets + "/" + id;
            Directory.CreateDirectory(actorPath);
            File.WriteAllText(actorPath + "/SpriteImport.json", JsonUtility.ToJson(manifest.import));
            var stride = id.StartsWith("EM-") ? 2 : 1;
            var stored = new HashSet<string>();
            foreach (var name in names.Where(name => !string.IsNullOrEmpty(name)))
            {
                var clip = Array.Find(manifest.clips, item => item.name == name);
                if (clip == null || clip.frames == null || clip.frames.Length == 0)
                    throw new InvalidDataException(id + " missing authored animation " + name);
                for (int index = 0; index < clip.frames.Length; index += stride)
                {
                    var sprite = Path.Combine(directory, clip.frames[index].sprite);
                    var target = FramePath(id, clip.frames[index].sprite);
                    if (!stored.Add(target) || File.Exists(target)) continue;
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    File.Copy(sprite, target);
                }
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        private static string FramePath(string id, string source)
        {
            var relative = source.Replace('\\', '/');
            if (!relative.StartsWith("Frames/") || relative.Split('/').Any(part => part == ".."))
                throw new InvalidDataException("Invalid relative sprite path: " + source);
            return PrivateAssets + "/" + id + "/" + relative;
        }

        private static SideViewEnemySprites.Sequence[] ImportClips(string id, Manifest manifest, string[] names)
        {
            var result = new List<SideViewEnemySprites.Sequence>();
            var stride = id.StartsWith("EM-") ? 2 : 1;
            for (int index = 0; index < names.Length; index++)
            {
                if (string.IsNullOrEmpty(names[index])) continue;
                var clip = Array.Find(manifest.clips, item => item.name == names[index]);
                if (clip == null || clip.frames.Length == 0 || clip.fps <= 0)
                    throw new InvalidDataException(id + " missing authored animation " + names[index]);
                var sprites = clip.frames.Where((_, frame) => frame % stride == 0)
                    .Select(frame => AssetDatabase.LoadAssetAtPath<Sprite>(FramePath(id, frame.sprite))).ToArray();
                if (sprites.Any(sprite => sprite == null)) throw new InvalidDataException(id + " missing imported sprites for " + clip.name);
                result.Add(new SideViewEnemySprites.Sequence
                {
                    key = Keys[index], sourceName = clip.name, framesPerSecond = clip.fps / stride, frames = sprites,
                    loop = id.StartsWith("SS-") ? clip.wrapMode < 2 : index < 2 || Keys[index] == "walk" ||
                        (id != "EM-e0100" && (index == 3 || index == 7)),
                    loopStart = Mathf.Clamp(clip.loopStart / stride, 0, sprites.Length - 1)
                });
            }
            return result.ToArray();
        }

        private static SideViewEnemySprites.Sequence ImportMotherCorpse(string artRoot)
        {
            const string id = "SS-B01-Corpse";
            var directory = Path.Combine(artRoot, "SS-B01-Death");
            var corpse = JsonUtility.FromJson<CorpseManifest>(File.ReadAllText(Path.Combine(directory, "manifest.json"))).corpseRoar;
            var clip = new Clip
            {
                name = "Roar",
                fps = corpse.fps,
                frames = corpse.frames.Select(frame => new Frame { sprite = "Frames/" + frame.sprite }).ToArray()
            };
            var actorPath = PrivateAssets + "/" + id;
            Directory.CreateDirectory(actorPath + "/Frames");
            foreach (var frame in corpse.frames)
                File.Copy(Path.Combine(directory, frame.sprite), actorPath + "/Frames/" + frame.sprite, true);
            var manifest = new Manifest
            {
                import = new Import { pixelsPerUnit = corpse.pixelsPerUnit, pivot = corpse.pivot },
                clips = new[] { clip }
            };
            var poses = new[] { null, null, null, null, null, "Roar" };
            PrepareAssets(id, directory, manifest, poses);
            var sequence = ImportClips(id, manifest, poses).Single();
            sequence.loop = true;
            sequence.limitSeconds = 3.7f;
            return sequence;
        }

        internal static void ApplyTextureSettings(string path, TextureImporter importer)
        {
            if (!path.StartsWith(PrivateAssets + "/", StringComparison.Ordinal)) return;
            var frames = path.IndexOf("/Frames/", PrivateAssets.Length, StringComparison.Ordinal);
            if (frames < 0) return;
            var settingsPath = path.Substring(0, frames) + "/SpriteImport.json";
            if (!File.Exists(settingsPath)) return;
            var import = JsonUtility.FromJson<Import>(File.ReadAllText(settingsPath));
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = import.pixelsPerUnit;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.maxTextureSize = 2048;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = import.pivot;
            importer.SetTextureSettings(settings);
        }

        private static void Set(UnityEngine.Object target, string name, object value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(name);
            if (property == null) throw new InvalidOperationException("Missing authored field " + name);
            if (value is float number) property.floatValue = number;
            else if (value is int integer) property.intValue = integer;
            else property.stringValue = (string)value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Spec Specification(string id)
        {
            switch (id)
            {
                case "SS-N03": return new Spec { kind = SideViewEnemyKind.Thumper, bodyHeight = 1.05f, visualHeight = 2.4f,
                    release = .72f, active = 1.05f, recovery = .44f, reach = .75f, radius = .72f,
                    wake = 5, range = 2.8f, vertical = 1.25f, speed = 1.3f, dash = 5.8f,
                    hp = 45, damage = 10, poses = new[] { "Sleep", "Idle", "Roll Antic", "Roll Attack", "Roll End", "Death Land",
                        null, null, null, "Roll Hit", "Roar", "Roll Turn" } };
                case "SS-N01": return new Spec { kind = SideViewEnemyKind.Fly, bodyHeight = .56f, visualHeight = 1.35f,
                    release = .6f, active = .6f, recovery = .7f, reach = .55f, radius = .6f,
                    wake = 5, range = 2, vertical = 4.6f, speed = 2.3f, dash = 8,
                    hp = 20, damage = 8, poses = new[] { "Fly", "Chase", "Antic", "Drill", "Drill End", "Death Land",
                        null, null, null, "Drill Bounce", "Startle" } };
                case "SS-N02": return new Spec { kind = SideViewEnemyKind.Crawler, bodyHeight = .55f, visualHeight = .95f,
                    release = 0, active = .24f, recovery = .75f, reach = .15f, radius = .62f,
                    wake = 2, range = .85f, vertical = .82f, speed = 2, dash = 0,
                    hp = 18, damage = 5, poses = new[] { "Walk", "Walk", "Walk", "Walk", "Walk", "Death Land",
                        null, null, null, null, "Startle", "Turn", "Sing" } };
                case "EM-e0161": return new Spec { kind = SideViewEnemyKind.Gang, bodyHeight = 1.65f, visualHeight = 3.1f,
                    release = 2, active = 1.667f, recovery = 2.5f, reach = 1.05f, radius = 1.8f,
                    wake = 7, range = 2.8f, vertical = 1.7f, speed = 1.65f, dash = 0,
                    hp = 62, damage = 14, poses = new[] { "idle", "walk", "attack_a_start", "attack_a_loop", "attack_a_end", "down_loop",
                        "attack_b_start", "attack_b_loop", "attack_b_end", null, "find_enemy", null, null, null, "walk", "damage_a_end" } };
                case "EM-e0100": return new Spec { kind = SideViewEnemyKind.Mimic, bodyHeight = 1.05f, visualHeight = 2.1f,
                    release = .43f, active = 1.237f, recovery = 1.333f, reach = .7f, radius = .7f,
                    wake = 4, range = 2, vertical = 1.3f, speed = 0, dash = 4.7f,
                    hp = 35, damage = 10, poses = new[] { "idle_peace", "idle", "attack_a", "attack_a", "backstep", "down_loop",
                        "attack_b", "attack_b", "backstep", null, "wake", null, null, null, "walk", "damage_a_end" } };
                case "SS-B01": return new Spec { kind = SideViewEnemyKind.Mother, bodyHeight = 1.85f, visualHeight = 3.2f,
                    release = .66f, active = .8f, recovery = .8f, reach = .8f, radius = 1.25f,
                    wake = 11, range = 4.5f, vertical = 4.8f, speed = 3.6f, dash = 7,
                    hp = 120, damage = 16, poses = new[] { "Fly", "Fly", "Antic", "Charge", "Charge Recover", null,
                        "RoofAntic", "Smash", "Recover", null, "Roar", null, null, "FlyUp" } };
                default: throw new ArgumentException("Unknown enemy candidate " + id);
            }
        }
    }

    internal sealed class SideViewSliceSpriteImporter : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (assetImporter is TextureImporter importer)
                SideViewSliceEnemyAuthoring.ApplyTextureSettings(assetPath, importer);
        }
    }
}

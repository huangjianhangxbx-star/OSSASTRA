// 组装正式横版路线并落盘敌方实例、Boss 和本场交战事实接线。
using System;
using System.Linq;
using Ossastra.Characters;
using Ossastra.Combat;
using Ossastra.SideViewWorld;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ossastra.SideView.Slice.Editor
{
    public static class SideViewSliceBatchAuthoring
    {
        public static void Assemble()
        {
            SVSliceSceneAuthoring.Assemble();
            AttachEnemies();
            SideViewSliceTitleAuthoring.Assemble();
        }

        public static void AttachEnemies()
        {
            var art = "E:/WORLDCREATOR/XingHaiHuiLang/work/SV-SLICE-01/PrivateArt";
            foreach (var spec in SliceSceneCatalog.GameplayScenes)
            {
                var scene = EditorSceneManager.OpenScene(spec.Path);
                var roots = scene.GetRootGameObjects();
                var spawns = roots.SelectMany(root => root.GetComponentsInChildren<SVSliceEnemySpawn>(true)).ToArray();
                if (spawns.Length == 0) continue;
                var party = roots.SelectMany(root => root.GetComponentsInChildren<CharacterControlCoordinator>(true)).Single();
                var progress = roots.SelectMany(root => root.GetComponentsInChildren<SideViewSliceProgress>(true)).Single();
                var targets = party.Characters.Select(character => character.Vitals).ToArray();
                foreach (var marker in spawns)
                {
                    var enemy = SideViewSliceEnemyAuthoring.Place(marker.transform, marker.stableId, marker.candidateId, targets, art);
                    if (marker.isBoss) AddBoss(marker, enemy, progress, party, roots);
                }
                FormalSideViewSceneAuthoring.InstallCombatAudio(scene);
                if (!EditorSceneManager.SaveScene(scene))
                    throw new InvalidOperationException("Enemy scene save failed: " + spec.Path);
            }
        }

        private static void AddBoss(SVSliceEnemySpawn marker, Ossastra.Enemies.EnemyRoot enemy,
            SideViewSliceProgress progress, CharacterControlCoordinator party, GameObject[] roots)
        {
            var exit = roots.SelectMany(root => root.GetComponentsInChildren<SideViewTransitionGate>(true)).Single();
            var barrier = GameObject.CreatePrimitive(PrimitiveType.Cube);
            barrier.name = "Boss entrance barrier";
            barrier.transform.SetParent(marker.transform, false);
            barrier.transform.position = exit.transform.position + Vector3.right * .9f + Vector3.up * 1.2f;
            barrier.transform.localScale = new Vector3(.4f, 5.3f, 2);
            barrier.SetActive(false);
            var encounter = marker.gameObject.AddComponent<SVSliceBossEncounter>();
            encounter.bossId = marker.stableId;
            encounter.enemy = enemy;
            encounter.progress = progress;
            encounter.party = party;
            encounter.exit = exit;
            encounter.barrier = barrier;
        }
    }
}

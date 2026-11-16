/*
 * 横版 Scene、稳定地图身份及成对入口表。
 * 只定义路线身份。运行状态与存档由换区会话拥有。
 */
using System;
using UnityEngine.SceneManagement;

namespace Ossastra.SideViewWorld
{
    public static class SideViewTransitionCatalog
    {
        public const string Tut01 = "Assets/OSSASTRA/SideView/SC_SideViewAdventure.unity";
        public const string Tut02 = "Assets/OSSASTRA/SideView/PrivateValidation/SV-WORLD-01/SC_Tut02Entry.unity";
        private const string TowerFolder = "Assets/OSSASTRA/SideView/Slice/";
        public const string TowerRest = TowerFolder + "SC_Slice_TowerRest.unity";
        public const string TowerLift = TowerFolder + "SC_Slice_TowerLift.unity";
        public const string TowerCombat = TowerFolder + "SC_Slice_TowerCombat.unity";
        public const string TowerBoss = TowerFolder + "SC_Slice_TowerBoss.unity";

        public const string Tut01MapId = "tut-01";
        public const string Tut02MapId = "tut-02";
        public const string TowerRestMapId = "tower-rest";
        public const string TowerLiftMapId = "tower-lift";
        public const string TowerCombatMapId = "tower-combat";
        public const string TowerBossMapId = "tower-boss";

        public const string Tut01Left3 = "Tut_01:left3";
        public const string Tut02Right2 = "Tut_02:right2";
        public const string Tut02Tower = "Tut_02:tower";
        public const string TowerRestEntry = "TowerRest:entry";
        public const string TowerRestExit = "TowerRest:lift";
        public const string TowerLiftUpper = "TowerLift:upper";
        public const string TowerLiftLower = "TowerLift:lower";
        public const string TowerCombatEntry = "TowerCombat:lift";
        public const string TowerCombatExit = "TowerCombat:boss";
        public const string TowerBossEntry = "TowerBoss:combat";

        private static readonly (string path, string id)[] Maps =
        {
            (Tut01, Tut01MapId), (Tut02, Tut02MapId), (TowerRest, TowerRestMapId),
            (TowerLift, TowerLiftMapId), (TowerCombat, TowerCombatMapId), (TowerBoss, TowerBossMapId)
        };

        private static readonly (string source, string sourceGate, string destination, string destinationGate)[] Links =
        {
            (Tut01, Tut01Left3, Tut02, Tut02Right2),
            (Tut02, Tut02Tower, TowerRest, TowerRestEntry),
            (TowerRest, TowerRestExit, TowerLift, TowerLiftUpper),
            (TowerLift, TowerLiftLower, TowerCombat, TowerCombatEntry),
            (TowerCombat, TowerCombatExit, TowerBoss, TowerBossEntry)
        };

        public static string MapIdForScene(string path)
        {
            foreach (var map in Maps)
                if (Same(path, map.path)) return map.id;
            return null;
        }

        public static bool IsRoute(string source, string sourceGate, string destination, string destinationGate)
        {
            foreach (var link in Links)
                if (Same(source, link.source) && Same(sourceGate, link.sourceGate) &&
                    Same(destination, link.destination) && Same(destinationGate, link.destinationGate) ||
                    Same(source, link.destination) && Same(sourceGate, link.destinationGate) &&
                    Same(destination, link.source) && Same(destinationGate, link.sourceGate))
                    return true;
            return false;
        }

        public static bool SceneAvailable(string path)
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.SceneAsset>(path) != null;
#else
            return SceneUtility.GetBuildIndexByScenePath(path) >= 0;
#endif
        }

        private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.Ordinal);
    }
}
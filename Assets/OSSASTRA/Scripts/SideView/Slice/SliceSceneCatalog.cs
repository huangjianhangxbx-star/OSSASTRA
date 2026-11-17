/* SV-SLICE-01: map authoring contract consumed read-only by title assembly. */
using System;
using System.Collections.Generic;

namespace Ossastra.SideView.Slice
{
    public sealed class SliceSceneSpec
    {
        public readonly string Path, MapId, InitialAnchorId;
        public readonly string[] RequiredAnchorIds;

        public SliceSceneSpec(string path, string mapId, string initialAnchorId, params string[] requiredAnchorIds)
        {
            Path = path;
            MapId = mapId;
            InitialAnchorId = initialAnchorId;
            RequiredAnchorIds = requiredAnchorIds ?? Array.Empty<string>();
        }
    }

    public static class SliceSceneCatalog
    {
        public const string TitleScenePath =
            "Assets/OSSASTRA/SideView/SC_SideViewTitle.unity";
        public const string Tut01ScenePath = "Assets/OSSASTRA/SideView/SC_SideViewAdventure.unity";
        public const string Tut02ScenePath =
            "Assets/OSSASTRA/SideView/PrivateValidation/SV-WORLD-01/SC_Tut02Entry.unity";
        public const string TowerRestScenePath = "Assets/OSSASTRA/SideView/Slice/SC_Slice_TowerRest.unity";
        public const string TowerLiftScenePath = "Assets/OSSASTRA/SideView/Slice/SC_Slice_TowerLift.unity";
        public const string TowerCombatScenePath = "Assets/OSSASTRA/SideView/Slice/SC_Slice_TowerCombat.unity";
        public const string TowerBossScenePath = "Assets/OSSASTRA/SideView/Slice/SC_Slice_TowerBoss.unity";
        public const string Tut01MapId = "tut-01";
        public const string Tut02MapId = "tut-02";
        public const string TowerRestMapId = "tower-rest";
        public const string TowerLiftMapId = "tower-lift";
        public const string TowerCombatMapId = "tower-combat";
        public const string TowerBossMapId = "tower-boss";
        public const string OpeningAnchorId = "opening-safe";
        public const string RestDoorAnchorId = "door-safe";
        public const string RestCheckpointAnchorId = "tower-high";
        public const string LiftAnchorId = "upper-safe";
        public const string CombatAnchorId = "west-safe";
        public const string BossAnchorId = "arena-entrance-safe";

        public static IReadOnlyList<SliceSceneSpec> GameplayScenes { get; } = new[]
        {
            new SliceSceneSpec(Tut01ScenePath, Tut01MapId, OpeningAnchorId, OpeningAnchorId),
            new SliceSceneSpec(Tut02ScenePath, Tut02MapId, OpeningAnchorId, OpeningAnchorId),
            new SliceSceneSpec(TowerRestScenePath, TowerRestMapId, RestDoorAnchorId,
                RestDoorAnchorId, RestCheckpointAnchorId),
            new SliceSceneSpec(TowerLiftScenePath, TowerLiftMapId, LiftAnchorId, LiftAnchorId),
            new SliceSceneSpec(TowerCombatScenePath, TowerCombatMapId, CombatAnchorId, CombatAnchorId),
            new SliceSceneSpec(TowerBossScenePath, TowerBossMapId, BossAnchorId, BossAnchorId)
        };
    }
}

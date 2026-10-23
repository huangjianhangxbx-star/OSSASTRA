#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class DRPGExpeditionInstaller
{
    const string Root="Assets/GameResources/DRPG/Exploration";
    [MenuItem("Tools/ANKALUTE/Expedition/Install HTML Exploration")]
    public static void Install()
    {
        if(Application.isPlaying)throw new InvalidOperationException("Exit Play Mode before installation.");
        var scene=EditorSceneManager.GetActiveScene();
        if(scene.path!="Assets/Scenes/测试场3_电车45.unity")throw new InvalidOperationException("Open verified test scene first: "+scene.path);
        var coordinator=UnityEngine.Object.FindFirstObjectByType<DRPGNodeBattleCoordinator>();
        var cards=UnityEngine.Object.FindFirstObjectByType<BattleCardHandView>(FindObjectsInactive.Include);
        var old=UnityEngine.Object.FindFirstObjectByType<WebCryptBattleBackdrop>(FindObjectsInactive.Include);
        if(coordinator==null||coordinator.Context?.PartyRoot==null||cards==null||old==null)throw new InvalidOperationException("Existing node/coordinator/card hand/backdrop missing; installation stopped.");
        var hud=cards.transform.parent as RectTransform;
        if(hud==null||hud.name!="BattleV2Hud")throw new InvalidOperationException("Expected existing BattleV2Hud parent.");
        AssetDatabase.Refresh();
        var path=Root+"/Art/corridor.png";var importer=AssetImporter.GetAtPath(path) as TextureImporter;
        if(importer==null)throw new InvalidOperationException("Missing corridor art: "+path);
        importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.spritePixelsPerUnit=100;importer.maxTextureSize=2048;importer.SaveAndReimport();
        var runtime=coordinator.GetComponent<DRPGDungeonRuntime>();if(runtime==null)runtime=Undo.AddComponent<DRPGDungeonRuntime>(coordinator.gameObject);Undo.RecordObject(runtime,"Configure expedition");
        runtime.coordinator=coordinator;runtime.encounter=AssetDatabase.LoadAssetAtPath<DRPGEncounterDefinitionSO>("Assets/GameResources/DRPG/WebPrototypeImport/Encounters/WebCrypt.asset");
        runtime.partySetup=AssetDatabase.LoadAssetAtPath<DRPGPartySetupSO>("Assets/GameResources/DRPG/WebPrototypeImport/Encounters/WebParty.asset");
        runtime.backdrop=old.GetComponent<SpriteRenderer>();runtime.roomArt=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/GameResources/DRPG/WebPrototypeImport/Art/crypt.png");runtime.corridorArt=AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if(runtime.roomArt==null||runtime.corridorArt==null||runtime.encounter==null||runtime.partySetup==null)throw new InvalidOperationException("Imported prototype content missing.");
        // The same scene actors are borrowed by battle. Persist matching exploration previews.
        foreach(var profile in new[]{runtime.partySetup.Protagonist}.Concat(runtime.partySetup.PartyMembers)) {
            if(profile==null)continue;
            var member=coordinator.Context.PartyRoot.GetComponentsInChildren<DRPGScenePartyMember>(true).FirstOrDefault(m=>m.StableId==profile.CombatantId);
            if(member==null)throw new InvalidOperationException("Missing actor identity: "+profile.CombatantId);
            var renderer=member.View.VisualRoot.GetComponent<SpriteRenderer>();
            var driver=member.View.VisualRoot.GetComponent<BattleWorldActorSpriteDriver>();
            if(renderer==null||driver==null)throw new InvalidOperationException("Missing scene sprite driver: "+profile.CombatantId);
            Undo.RecordObjects(new UnityEngine.Object[]{renderer,driver,renderer.transform},"Match exploration and battle actor art");
            renderer.sprite=profile.FullPortrait;renderer.sortingLayerID=runtime.backdrop.sortingLayerID;renderer.sortingOrder=200;
            driver.ConfigureNodePreview(renderer,4.2f);EditorUtility.SetDirty(renderer);EditorUtility.SetDirty(driver);
        }
        runtime.shovel=Item("shovel","铲子",4);runtime.key=Item("key","万能钥匙",6);runtime.gold=Item("gold","金币",1750);runtime.gem=Item("gem","翡翠",5);runtime.food=Item("food","食物",12);runtime.torch=Item("torch","火把",8);
        var view=coordinator.GetComponent<DRPGExpeditionHud>();if(view==null)view=Undo.AddComponent<DRPGExpeditionHud>(coordinator.gameObject);Undo.RecordObject(view,"Configure expedition HUD");view.runtime=runtime;view.hudRoot=hud;view.cardArea=cards.transform as RectTransform;view.font=hud.GetComponentsInChildren<TMPro.TMP_Text>(true).First(t=>t.font!=null).font;
        EditorUtility.SetDirty(runtime);EditorUtility.SetDirty(view);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        Debug.Log("Expedition installed on existing node and battle HUD; existing cards retained.");
    }
    static DRPGLootItemDefinitionSO Item(string id,string title,int max)
    {
        string path=Root+"/"+id+".asset";var item=AssetDatabase.LoadAssetAtPath<DRPGLootItemDefinitionSO>(path);
        if(item==null){item=ScriptableObject.CreateInstance<DRPGLootItemDefinitionSO>();AssetDatabase.CreateAsset(item,path);}
        var so=new SerializedObject(item);so.FindProperty("itemId").stringValue="expedition_"+id;so.FindProperty("displayName").stringValue=title;so.FindProperty("description").stringValue="远征补给 / 战利品";so.FindProperty("maxStack").intValue=max;so.FindProperty("weight").floatValue=.01f;so.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(item);AssetDatabase.SaveAssetIfDirty(item);return item;
    }
}
#endif

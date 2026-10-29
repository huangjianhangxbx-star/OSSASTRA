/*
 * 脚本：GroundWorkshopPlaygroundSession
 * 功能：为当前地图建立一次性原控制器试玩，跨Play/domain恢复作者Scene与地图选择。
 * 负责：唯一会话身份、准确SceneSetup/PlayStartScene快照、原生Prefab/Scene副本和精确清理。
 * 不负责：人物输入、运行时保存、作者格修改、全局素材清理或删除未知目录。
 * 协作：可选Builder装配双角色原链；Core Maps只保存作者图；SessionState保存Editor生命周期状态。
 * 关键约束：只关闭clean场；只删GUID/marker匹配的三个自有文件和空会话目录；失败保留可恢复现场。
 */
#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ossastra.GroundResearch.Editor
{
    [InitializeOnLoad]
    public static class GroundWorkshopPlaygroundSession
    {
        private const string stateKey="Ossastra.GroundWorkshop.PlaygroundSession";
        private const string sessionRoot=GroundWorkshopOwnership.Root+"/Maps/PreviewSessions";
        private static double restoreDeadline;

        [Serializable] private sealed class SavedScene
        {public string path,guid;public bool loaded,active;}
        [Serializable] private sealed class State
        {
            public int schemaVersion=1;
            public string sessionId,phase,folder,scenePath,prefabPath,markerPath;
            public string sceneGuid,prefabGuid,markerGuid,markerSha256;
            public string authorScenePath,authorSceneGuid,authorMapId,previousStartScene;
            public SavedScene[] scenes;
            public bool cleanupVerified;
            public string[] cleanupHashes,deletedPaths;
        }
        [Serializable] private sealed class Marker
        {public string sessionId,folder,scenePath,prefabPath,sceneGuid,prefabGuid,authorScenePath,authorMapId;}

        static GroundWorkshopPlaygroundSession()
        {
            EditorApplication.playModeStateChanged-=OnPlayMode;
            EditorApplication.playModeStateChanged+=OnPlayMode;
            EditorApplication.delayCall+=RecoverAfterReload;
        }

        public static bool HasSession=>!string.IsNullOrEmpty(SessionState.GetString(stateKey,""));
        public static string PreviewScenePath=>Read()?.scenePath;
        public static string AuthorScenePath=>Read()?.authorScenePath;

        internal static void Begin(GroundWorkshopMap map)
        {
            if(HasSession)throw new InvalidOperationException("已有角色试玩恢复记录；先用“返回作者地图”恢复，不覆盖旧会话。");
            GroundWorkshopOwnership.Require(map);
            GroundWorkshopPlaygroundBuilder.RequireCleanContext(null);
            if(map.transform.parent!=null)throw new InvalidOperationException("角色试玩v1需要顶层地图根；未移动或重组作者对象。");
            string templatePath=FrontierGroundSceneBuilder.ScenePath;
            if(AssetDatabase.LoadAssetAtPath<SceneAsset>(templatePath)==null)throw new InvalidOperationException("缺少双角色Frontier模板；不创建替代控制器。");
            var setup=EditorSceneManager.GetSceneManagerSetup();
            string id=Guid.NewGuid().ToString("N"),folder=sessionRoot+"/"+id;
            var state=new State
            {
                sessionId=id,phase="Preparing",folder=folder,scenePath=folder+"/Playground.unity",prefabPath=folder+"/World.prefab",markerPath=folder+"/Session.json",
                authorScenePath=map.gameObject.scene.path,authorSceneGuid=AssetDatabase.AssetPathToGUID(map.gameObject.scene.path),authorMapId=map.registration.mapId,
                previousStartScene=EditorSceneManager.playModeStartScene==null?"":AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene),
                scenes=setup.Select(s=>new SavedScene{path=s.path,guid=AssetDatabase.AssetPathToGUID(s.path),loaded=s.isLoaded,active=s.isActive}).ToArray()
            };
            if(state.scenes.Any(s=>string.IsNullOrEmpty(s.path)||string.IsNullOrEmpty(s.guid)))throw new InvalidOperationException("当前SceneSetup存在无法恢复的未保存/无GUID场景。");
            if(Directory.Exists(folder)||File.Exists(folder+".meta"))throw new InvalidOperationException("新的试玩会话目录意外存在，未覆盖。");
            Vector3 position=map.transform.position,scale=map.transform.localScale;Quaternion rotation=map.transform.rotation;
            Write(state);
            using(var ledger=new GroundAssetLedger())
            {
                try
                {
                    ledger.Folder(folder);VerifyFolder(state);
                    CapturePrefab(map,state.prefabPath,ledger);
                    var template=SceneManager.GetSceneByPath(templatePath);
                    if(!template.IsValid()||!template.isLoaded)template=EditorSceneManager.OpenScene(templatePath,OpenSceneMode.Additive);
                    var oldSurface=GroundWorkshopPlaygroundBuilder.InScene<GroundResearchSurface>(template).SingleOrDefault();
                    GroundWorkshopPlaygroundBuilder.VerifyGameplay(template,oldSurface);
                    if(template.isDirty)throw new InvalidOperationException("只读模板在复制前成为dirty，保留现场。");
                    ledger.Reserve(state.scenePath);
                    if(!EditorSceneManager.SaveScene(template,state.scenePath,true))throw new IOException("临时双角色场另存失败。");
                    var preview=EditorSceneManager.OpenScene(state.scenePath,OpenSceneMode.Additive);SceneManager.SetActiveScene(preview);
                    var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(state.prefabPath);
                    if(prefab==null)throw new InvalidOperationException("缺少刚保存的作者Prefab快照。");
                    var root=PrefabUtility.InstantiatePrefab(prefab,preview) as GameObject;
                    if(root==null)throw new InvalidOperationException("作者Prefab未能实例化到临时场。");
                    if(PrefabUtility.IsPartOfPrefabInstance(root))PrefabUtility.UnpackPrefabInstance(root,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
                    root.transform.SetPositionAndRotation(position,rotation);root.transform.localScale=scale;
                    GroundWorkshopPlaygroundBuilder.AssembleTemporary(preview,root,id);
                    if(!EditorSceneManager.SaveScene(preview,state.scenePath))throw new IOException("临时试玩地表装配保存失败。");
                    AssetDatabase.ImportAsset(state.scenePath,ImportAssetOptions.ForceSynchronousImport);
                    state.sceneGuid=AssetDatabase.AssetPathToGUID(state.scenePath);state.prefabGuid=AssetDatabase.AssetPathToGUID(state.prefabPath);
                    if(string.IsNullOrEmpty(state.sceneGuid)||string.IsNullOrEmpty(state.prefabGuid))throw new InvalidOperationException("临时Scene/Prefab缺GUID，保留检查点。");
                    WriteMarker(state,ledger);state.phase="Prepared";Write(state);ledger.Commit();
                    // Play入口只临时指向此副本；用户之后若改成别的起始场，退出时不覆盖其新值。
                    var startScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(state.scenePath);
                    if(startScene==null)throw new InvalidOperationException("已保存临时场无法解析为SceneAsset，保留检查点且不改起始场设置。");
                    EditorSceneManager.playModeStartScene=startScene;
                    for(int i=SceneManager.sceneCount-1;i>=0;i--)
                    {
                        var other=SceneManager.GetSceneAt(i);if(other==preview)continue;
                        if(other.isDirty)throw new InvalidOperationException("其他Scene在准备中变dirty；未关闭或进入Play："+other.path);
                        if(!EditorSceneManager.CloseScene(other,true))throw new InvalidOperationException("未能关闭clean源场："+other.path);
                    }
                    Selection.activeGameObject=root;state.phase="EnteringPlay";Write(state);
                    EditorApplication.isPlaying=true;
                }
                catch
                {
                    // 一旦场景副本出现，未知失败不删潜在恢复引用；原作者文件已保存且未在此处覆写。
                    if(File.Exists(state.scenePath))
                    {
                        ledger.Commit();state.phase="RecoveryRequired";
                        state.sceneGuid=AssetDatabase.AssetPathToGUID(state.scenePath);state.prefabGuid=AssetDatabase.AssetPathToGUID(state.prefabPath);
                        Write(state);RestoreStartSceneIfOwned(state);
                        Debug.LogError("角色试玩准备未完成。作者图已保存；临时副本保留供恢复检查："+state.folder);
                    }
                    else{SessionState.EraseString(stateKey);RestoreStartSceneIfOwned(state);}
                    throw;
                }
            }
        }

        /// <summary>在自有短生命周期Scene克隆地图，剔除只属于玩法模板的观察面板；不污染作者根或外部Scene引用。</summary>
        private static void CapturePrefab(GroundWorkshopMap map,string path,GroundAssetLedger ledger)
        {
            var author=map.gameObject.scene;Scene scratch=default;
            try
            {
                scratch=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.SetActiveScene(scratch);
                var clone=UnityEngine.Object.Instantiate(map.gameObject);clone.name=map.gameObject.name;
                if(clone.scene!=scratch)SceneManager.MoveGameObjectToScene(clone,scratch);
                if(PrefabUtility.IsPartOfPrefabInstance(clone))PrefabUtility.UnpackPrefabInstance(clone,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
                foreach(var view in clone.GetComponentsInChildren<GroundResearchView>(true))UnityEngine.Object.DestroyImmediate(view);
                ledger.Reserve(path);
                var prefab=PrefabUtility.SaveAsPrefabAsset(clone,path,out bool success);
                if(!success||prefab==null)throw new InvalidOperationException("作者地表Prefab快照保存失败。");
            }
            finally
            {
                if(scratch.IsValid()&&scratch.isLoaded)EditorSceneManager.CloseScene(scratch,true);
                if(author.IsValid()&&author.isLoaded)SceneManager.SetActiveScene(author);
            }
        }

        [MenuItem("Tools/OSSASTRA/地表工坊/返回作者地图")]
        public static void ReturnToAuthorMap()
        {
            if(!HasSession)throw new InvalidOperationException("没有待恢复的地表试玩会话。");
            if(EditorApplication.isPlayingOrWillChangePlaymode){EditorApplication.isPlaying=false;return;}
            RestoreAuthor();
        }

        private static void OnPlayMode(PlayModeStateChange change)
        {
            var state=Read();if(state==null)return;
            if(state.phase=="Preparing"||state.phase=="RecoveryRequired")return;
            if(change==PlayModeStateChange.EnteredPlayMode&&SceneManager.GetActiveScene().path!=state.scenePath)return;
            if(change==PlayModeStateChange.EnteredPlayMode){state.phase="Playing";Write(state);}
            else if(change==PlayModeStateChange.ExitingPlayMode){state.phase="ExitingPlay";Write(state);}
            else if(change==PlayModeStateChange.EnteredEditMode)ScheduleRestore();
        }
        private static void RecoverAfterReload()
        {
            var state=Read();if(state==null||EditorApplication.isPlayingOrWillChangePlaymode)return;
            if(state.phase=="Playing"||state.phase=="ExitingPlay"||state.phase=="Returning"||state.phase=="CleanupPending")ScheduleRestore();
        }
        private static void ScheduleRestore()
        {
            EditorApplication.update-=WaitForRestore;restoreDeadline=EditorApplication.timeSinceStartup+20;
            EditorApplication.update+=WaitForRestore;
        }
        private static void WaitForRestore()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating)
            {
                if(EditorApplication.timeSinceStartup>restoreDeadline){EditorApplication.update-=WaitForRestore;Debug.LogWarning("Editor尚未恢复ready，角色试玩检查点保留；可稍后使用“返回作者地图”。");}
                return;
            }
            EditorApplication.update-=WaitForRestore;
            try{RestoreAuthor();}catch(Exception e){Debug.LogWarning("地表试玩恢复未完成，未覆盖未知现场："+e.Message);}
        }

        private static void RestoreAuthor()
        {
            if(!GroundWorkshopOwnership.ContextReady(out string reason))throw new InvalidOperationException(reason);
            var state=Read();if(state==null)throw new InvalidOperationException("会话检查点已释放。");
            VerifyFolder(state);
            if(state.scenes==null||state.scenes.Length==0||state.scenes.Any(s=>string.IsNullOrEmpty(s.path)||AssetDatabase.AssetPathToGUID(s.path)!=s.guid)||
                AssetDatabase.AssetPathToGUID(state.authorScenePath)!=state.authorSceneGuid)
                throw new InvalidOperationException("作者Scene身份已改变，保留会话，不猜测恢复路径。");
            var active=SceneManager.GetActiveScene();
            bool alreadyReturned=active.path==state.authorScenePath;
            if(!alreadyReturned)
            {
                if(active.path!=state.scenePath||AssetDatabase.AssetPathToGUID(state.scenePath)!=state.sceneGuid)
                    throw new InvalidOperationException("当前已切到非本会话场景；保留用户现场，不自动切回。");
                GroundWorkshopPlaygroundBuilder.RequireCleanContext(null);
                state.phase="Returning";Write(state);RestoreStartSceneIfOwned(state);
                EditorSceneManager.RestoreSceneManagerSetup(state.scenes.Select(s=>new SceneSetup{path=s.path,isLoaded=s.loaded,isActive=s.active}).ToArray());
                var author=SceneManager.GetSceneByPath(state.authorScenePath);
                var maps=GroundWorkshopPlaygroundBuilder.InScene<GroundWorkshopMap>(author).Where(m=>m.registration!=null&&m.registration.mapId==state.authorMapId).ToArray();
                if(maps.Length!=1)throw new InvalidOperationException("作者图已打开但mapId不唯一；保留恢复记录。");
                Selection.activeGameObject=maps[0].gameObject;
            }
            else RestoreStartSceneIfOwned(state);
            state.phase="CleanupPending";Write(state);
            Cleanup(state);SessionState.EraseString(stateKey);
            Debug.Log("已返回已保存作者地图，精确清理本次临时Scene/Prefab/marker；角色试玩没有回写作者格子。");
        }

        private static void WriteMarker(State state,GroundAssetLedger ledger)
        {
            var marker=new Marker{sessionId=state.sessionId,folder=state.folder,scenePath=state.scenePath,prefabPath=state.prefabPath,sceneGuid=state.sceneGuid,prefabGuid=state.prefabGuid,authorScenePath=state.authorScenePath,authorMapId=state.authorMapId};
            ledger.Reserve(state.markerPath);File.WriteAllText(state.markerPath,JsonUtility.ToJson(marker,true));
            AssetDatabase.ImportAsset(state.markerPath,ImportAssetOptions.ForceSynchronousImport);
            state.markerGuid=AssetDatabase.AssetPathToGUID(state.markerPath);state.markerSha256=Hash(state.markerPath);
        }
        private static void Cleanup(State state)
        {
            VerifyFolder(state);
            var loaded=SceneManager.GetSceneByPath(state.scenePath);
            if(loaded.IsValid()&&loaded.isLoaded)throw new InvalidOperationException("临时Scene仍加载，未删除。");
            var exact=new[]{state.scenePath,state.prefabPath,state.markerPath};
            var allowed=exact.Concat(exact.Select(p=>p+".meta")).Select(Path.GetFullPath).ToArray();
            if(Directory.Exists(state.folder)&&Directory.EnumerateFileSystemEntries(state.folder).Any(path=>!allowed.Contains(Path.GetFullPath(path),StringComparer.OrdinalIgnoreCase)))
                throw new InvalidOperationException("临时目录有未知新增内容，未批量删除。");
            var guids=new[]{state.sceneGuid,state.prefabGuid,state.markerGuid};
            string[] deleted=state.deletedPaths??Array.Empty<string>();
            if(deleted.Length>exact.Length||!deleted.SequenceEqual(exact.Take(deleted.Length))||(!state.cleanupVerified&&deleted.Length!=0))
                throw new InvalidOperationException("临时删除进度不是本会话的有序前缀，保留检查点。");
            if(!state.cleanupVerified)
            {
                // 首次清理必须同时验证三文件原GUID与marker，然后把整批授权/哈希写入SessionState。
                // 后续每次原生DeleteAsset确认成功立即推进前缀，允许跨Domain在中途失败后安全续清。
                if(!File.Exists(state.markerPath)||Hash(state.markerPath)!=state.markerSha256||AssetDatabase.AssetPathToGUID(state.markerPath)!=state.markerGuid)
                    throw new InvalidOperationException("临时marker缺失或变化；保留目录供检查。");
                var marker=JsonUtility.FromJson<Marker>(File.ReadAllText(state.markerPath));
                if(marker==null||marker.sessionId!=state.sessionId||marker.folder!=state.folder||marker.scenePath!=state.scenePath||marker.prefabPath!=state.prefabPath||marker.sceneGuid!=state.sceneGuid||marker.prefabGuid!=state.prefabGuid)
                    throw new InvalidOperationException("临时marker身份不匹配。");
                for(int i=0;i<exact.Length;i++)
                    if(!File.Exists(exact[i])||string.IsNullOrEmpty(guids[i])||AssetDatabase.AssetPathToGUID(exact[i])!=guids[i])
                        throw new InvalidOperationException("临时文件GUID变化："+exact[i]);
                state.cleanupHashes=exact.Select(Hash).ToArray();state.deletedPaths=Array.Empty<string>();state.cleanupVerified=true;Write(state);
            }
            if(state.cleanupHashes==null||state.cleanupHashes.Length!=exact.Length)
                throw new InvalidOperationException("临时文件删除授权缺哈希快照，保留检查点。");
            // 重试先检查全体：已删项必须持续不存在，尚未删项必须保持最初授权的GUID与内容。
            // 遇陌生重现或用户更改，不能借用之前授权删除它。
            for(int i=0;i<exact.Length;i++)
            {
                if(i<deleted.Length)
                {
                    // 已删路径的GUID缓存可能尚未释放；按实际文件/meta及可解析资产判断是否重现。
                    if(File.Exists(exact[i])||File.Exists(exact[i]+".meta")||AssetDatabase.LoadMainAssetAtPath(exact[i])!=null)
                        throw new InvalidOperationException("已删临时文件陌生重现，未再次删除："+exact[i]);
                }
                else if(!File.Exists(exact[i])||string.IsNullOrEmpty(guids[i])||AssetDatabase.AssetPathToGUID(exact[i])!=guids[i]||Hash(exact[i])!=state.cleanupHashes[i])
                    throw new InvalidOperationException("剩余临时文件身份/内容变化，未继续删除："+exact[i]);
            }
            for(int i=deleted.Length;i<exact.Length;i++)
            {
                if(!AssetDatabase.DeleteAsset(exact[i]))throw new IOException("精确删除临时资产失败，已保存前序进度："+exact[i]);
                state.deletedPaths=exact.Take(i+1).ToArray();Write(state);
            }
            VerifyFolder(state);
            if(Directory.Exists(state.folder))
            {
                if(Directory.EnumerateFileSystemEntries(state.folder).Any())throw new InvalidOperationException("删除三文件后目录出现未知内容，保留空目录清理进度。");
                if(!AssetDatabase.DeleteAsset(state.folder))throw new IOException("临时三文件已删，空会话目录尚未删除，可续清："+state.folder);
            }
            else if(File.Exists(state.folder+".meta")||AssetDatabase.IsValidFolder(state.folder))
                throw new InvalidOperationException("临时空目录身份未完全释放，保留检查点。");
        }
        private static void RestoreStartSceneIfOwned(State state)
        {
            var current=EditorSceneManager.playModeStartScene;
            if(current!=null&&AssetDatabase.GetAssetPath(current)==state.scenePath)
                EditorSceneManager.playModeStartScene=string.IsNullOrEmpty(state.previousStartScene)?null:AssetDatabase.LoadAssetAtPath<SceneAsset>(state.previousStartScene);
        }
        private static void VerifyFolder(State state)
        {
            if(state==null||state.schemaVersion!=1||!Guid.TryParseExact(state.sessionId,"N",out _)||state.folder!=sessionRoot+"/"+state.sessionId||
                !GroundWorkshopOwnership.IsUnder(state.folder,sessionRoot)||state.scenePath!=state.folder+"/Playground.unity"||state.prefabPath!=state.folder+"/World.prefab"||state.markerPath!=state.folder+"/Session.json")
                throw new InvalidOperationException("试玩会话路径/身份不在本工具精确白名单。");
            string cursor=Path.GetFullPath(state.folder),stop=Path.GetFullPath(GroundWorkshopOwnership.Root);
            while(!string.Equals(cursor,stop,StringComparison.OrdinalIgnoreCase))
            {
                if(Directory.Exists(cursor)&&(File.GetAttributes(cursor)&FileAttributes.ReparsePoint)!=0)throw new InvalidOperationException("会话路径含重解析点，未写入或删除："+cursor);
                string parent=Path.GetDirectoryName(cursor);if(string.IsNullOrEmpty(parent)||parent==cursor)throw new InvalidOperationException("会话目录根校验失败。");cursor=parent;
            }
        }
        private static State Read()
        {
            string value=SessionState.GetString(stateKey,"");
            return string.IsNullOrEmpty(value)?null:JsonUtility.FromJson<State>(value);
        }
        private static void Write(State value)=>SessionState.SetString(stateKey,JsonUtility.ToJson(value));
        private static string Hash(string path)
        {using(var stream=File.OpenRead(path))using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","");}
    }
}
#endif

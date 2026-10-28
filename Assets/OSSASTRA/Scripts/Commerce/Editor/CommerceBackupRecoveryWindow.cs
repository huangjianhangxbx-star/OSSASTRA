/* 脚本：CommerceBackupRecoveryWindow
 * 功能：检查所选自有CommerceService的主档/备份来源与版本，明确确认后恢复该份冻结备份。
 * 边界：仅EditMode操作文件，不进入Play、不重载场景、不播种、不启用领域状态；完整配置兼容性仍由下次启动门校验。
 * 协作：文件适配在同一写锁中重验预览、保留原主档并撤销旧写代际；窗口不创建第二保存协调器。
 */
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Ossastra.Commerce.Persistence;
using Ossastra.Commerce.Runtime;

namespace Ossastra.Commerce.Editor
{
    public sealed class CommerceBackupRecoveryWindow : EditorWindow
    {
        private CommerceService target, inspectedTarget;
        private CommerceFileSaveStore inspectedStore;
        private CommerceSaveSnapshot inspectedBackup;
        private string inspectedProfile, inspectedDirectory, inspectedScene, backupFormatError;
        private Vector2 scroll;
        public CommerceSaveLoadResult MainInspection {get;private set;}
        public CommerceSaveLoadResult BackupInspection {get;private set;}
        public string LastReport {get;private set;}="选择自有场景中的CommerceService，再检查备份。";
        public string PreservedMainPath {get;private set;}
        public bool CanRestore=>!EditorApplication.isPlayingOrWillChangePlaymode&&inspectedBackup!=null&&backupFormatError==null&&TargetUnchanged();

        // 文件来源和版本必须可直接辨认；避免默认小窗把完整路径挤成逐字换行。
        private void OnEnable(){minSize=new Vector2(620,650);}

        [MenuItem("Tools/XingHai/Commerce/备份检查与显式恢复")]
        public static void Open()
        {
            var window=GetWindow<CommerceBackupRecoveryWindow>("贸易备份检查");
            var selected=Selection.activeGameObject;
            if(selected!=null)window.target=selected.GetComponent<CommerceService>();
            window.Show();
        }

        public void InspectSelected()
        {
            var selected=Selection.activeGameObject;
            if(selected==null)throw new InvalidOperationException("请先选中自有场景中的CommerceService对象");
            Inspect(selected.GetComponent<CommerceService>());
        }

        /// <summary>只读文件预览；公开口供隔离测试调用，不调用运行初始化或角色生命周期。</summary>
        public void Inspect(CommerceService service)
        {
            RequireEditMode();RequireOwnedService(service);
            target=inspectedTarget=service;inspectedProfile=service.profileId;inspectedDirectory=DirectoryFor(service);inspectedScene=service.gameObject.scene.path;
            inspectedStore=new CommerceFileSaveStore(inspectedDirectory);
            MainInspection=inspectedStore.Load(inspectedProfile);
            BackupInspection=inspectedStore.LoadBackup(inspectedProfile);
            inspectedBackup=BackupInspection.Status==CommerceSaveLoadStatus.Loaded?BackupInspection.Snapshot:null;
            backupFormatError=inspectedBackup==null?BackupInspection.Error??"备份不存在":ValidateFormat(inspectedBackup);
            PreservedMainPath=null;
            LastReport=backupFormatError==null?"备份基础格式通过；尚未验证当前定义、角色和箱子兼容性。":"备份不可恢复："+backupFormatError;
            Repaint();
        }

        /// <summary>实际按钮需确认框；外部验收须显式传true。不会自动Play、重载或授予物品。</summary>
        public CommerceSaveLoadResult RestoreInspectedBackup(bool confirmed)
        {
            RequireEditMode();
            if(!confirmed)throw new InvalidOperationException("尚未明确确认恢复该备份");
            if(!CanRestore)throw new InvalidOperationException("预览缺失、不可用或目标配置已改变，请重新检查");
            RequireOwnedService(target);
            var path=inspectedStore.PathForProfile(inspectedProfile);var directory=Path.GetDirectoryName(path);
            var pattern=Path.GetFileName(path)+".corrupt-*";
            var before=Directory.GetFiles(directory,pattern);
            var result=inspectedStore.RestoreBackup(inspectedProfile,inspectedBackup,ValidateFormat);
            PreservedMainPath=string.Join("\n",Directory.GetFiles(directory,pattern).Except(before,StringComparer.Ordinal).OrderBy(p=>p,StringComparer.Ordinal));
            if(result.Status==CommerceSaveLoadStatus.Loaded)
            {
                MainInspection=result;
                LastReport="已从明确确认的备份恢复文件；没有启用内存状态或自动进入Play。\n完整配置兼容性将在下次启动门校验；失败仍保留文件且不播种。";
            }
            else LastReport="未完成恢复："+(result.Error??result.Status.ToString())+"\n原档案与备份未被窗口删除；请重新检查。";
            // 确认只使用一次；失败也必须重新检查，不能无限重放旧预览。
            inspectedBackup=null;Repaint();return result;
        }

        private static string ValidateFormat(CommerceSaveSnapshot snapshot)
        {
            try
            {
                if(snapshot.PayloadSchema!=2)return "不支持的载荷schema："+snapshot.PayloadSchema;
                var value=CommerceSnapshotCodec.Decode(snapshot.Payload);
                if(value.revision!=snapshot.StateRevision)return "文件封套与载荷的StateRevision不一致";
                if(value.economy==null||value.inventoryRuntime==null)return "经济/库存宿主载荷缺失";
                if(value.economy.definitionFingerprint!=snapshot.DefinitionFingerprint)return "文件封套与载荷的fingerprint不一致";
                return null;
            }
            catch(Exception error){return error.GetType().Name+": "+error.Message;}
        }
        private static string DirectoryFor(CommerceService service)
            =>Path.GetFullPath(string.IsNullOrWhiteSpace(service.saveDirectory)?Path.Combine(Application.persistentDataPath,"Commerce"):service.saveDirectory);
        private static void RequireEditMode()
        {if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("请退出Play并等待保存结束后再检查或恢复备份");}
        private static void RequireOwnedService(CommerceService service)
        {
            if(service==null||EditorUtility.IsPersistent(service)||!service.gameObject.scene.IsValid()||!service.gameObject.scene.isLoaded||
                !service.gameObject.scene.path.StartsWith("Assets/OSSASTRA/",StringComparison.Ordinal))
                throw new InvalidOperationException("仅允许选中Assets/OSSASTRA自有场景中的CommerceService，不恢复Prefab或其他场景档案");
        }
        private bool TargetUnchanged()
        {
            try{return target!=null&&target==inspectedTarget&&target.profileId==inspectedProfile&&DirectoryFor(target)==inspectedDirectory&&target.gameObject.scene.path==inspectedScene;}
            catch{return false;}
        }
        private void ClearPreview()
        {inspectedTarget=null;inspectedStore=null;inspectedBackup=null;MainInspection=null;BackupInspection=null;PreservedMainPath=null;}
        private void OnGUI()
        {
            EditorGUILayout.HelpBox("只检查和显式恢复本模块文件。不会启动游戏、重载场景或重新授予初始物品。",MessageType.Info);
            var chosen=(CommerceService)EditorGUILayout.ObjectField("自有CommerceService",target,typeof(CommerceService),true);
            if(chosen!=target){target=chosen;ClearPreview();}
            using(new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if(GUILayout.Button("读取当前选中对象"))Run(InspectSelected);
                if(GUILayout.Button("检查当前对象主档与备份"))Run(()=>Inspect(target));
            }
            scroll=EditorGUILayout.BeginScrollView(scroll);
            if(inspectedTarget!=null)
            {
                EditorGUILayout.LabelField("场景",inspectedScene,EditorStyles.wordWrappedLabel);
                EditorGUILayout.LabelField("档案profile",inspectedProfile,EditorStyles.wordWrappedLabel);
                EditorGUILayout.LabelField("实际目录",inspectedDirectory,EditorStyles.wordWrappedLabel);
                DrawFile("当前主档",MainInspection);
                DrawFile("拟恢复的备份来源",BackupInspection);
                EditorGUILayout.HelpBox("校验范围：文件完整性、载荷schema/Codec、文件内外版本及fingerprint一致。\n当前定义、角色、箱子和引用尚未验证，须由下次启动门完整验证，不能据此声称配置兼容。",MessageType.Warning);
            }
            using(new EditorGUI.DisabledScope(!CanRestore))
            {
                if(GUILayout.Button("明确恢复这份已检查备份"))Run(()=>
                {
                    var text="档案："+inspectedProfile+"\n备份来源："+BackupInspection.Path+"\nStateRevision："+inspectedBackup.StateRevision+"  SaveSequence："+inspectedBackup.SaveSequence+
                        "\n主档将保留为.corrupt-*证据，再以这份备份替换。不会自动进入Play；完整兼容性仍须下次启动校验。";
                    if(EditorUtility.DisplayDialog("恢复已检查的贸易备份？",text,"恢复这份备份","取消"))RestoreInspectedBackup(true);
                });
            }
            EditorGUILayout.HelpBox(LastReport??"",MessageType.Info);
            if(!string.IsNullOrEmpty(PreservedMainPath))EditorGUILayout.LabelField("原主档保留路径",PreservedMainPath,EditorStyles.wordWrappedLabel);
            EditorGUILayout.EndScrollView();
        }
        private static void DrawFile(string title,CommerceSaveLoadResult result)
        {
            if(result==null)return;
            EditorGUILayout.BeginVertical("box");EditorGUILayout.LabelField(title,EditorStyles.boldLabel);
            EditorGUILayout.LabelField("来源路径",result.Path,EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("读取结果",result.Status.ToString());
            if(result.Snapshot!=null)
            {
                var value=result.Snapshot;
                EditorGUILayout.LabelField("Payload schema",value.PayloadSchema.ToString());
                EditorGUILayout.LabelField("StateRevision / SaveSequence",value.StateRevision+" / "+value.SaveSequence);
                EditorGUILayout.LabelField("Fingerprint（文件记录）",value.DefinitionFingerprint,EditorStyles.wordWrappedLabel);
                EditorGUILayout.LabelField("文件内外基础校验",ValidateFormat(value)??"通过（未验证当前配置兼容性）",EditorStyles.wordWrappedLabel);
            }
            if(!string.IsNullOrEmpty(result.Error))EditorGUILayout.LabelField("拒绝原因",result.Error,EditorStyles.wordWrappedLabel);
            EditorGUILayout.EndVertical();
        }
        private void Run(Action action)
        {try{action();}catch(Exception error){LastReport=error.GetType().Name+": "+error.Message;Repaint();}}
    }
}

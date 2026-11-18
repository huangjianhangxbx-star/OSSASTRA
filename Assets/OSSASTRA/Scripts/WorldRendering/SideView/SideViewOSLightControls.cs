using Ossastra.Characters;
using Ossastra.Characters.Rendering;
using UnityEngine;
using UnityEngine.UI;

namespace Ossastra.WorldRendering.SideView
{
    // 接入现有比较面板；保存动作明确区分角色配置与共同成片配置。
    public sealed class SideViewOSLightControls : MonoBehaviour
    {
        private const string ResourceFolder = "OSLightCandidate/";
        private const string AssetFolder = "Assets/OSSASTRA/SideView/Resources/OSLightCandidate";
        private static SideViewOSLightSettings shared;
        private static SideViewOSLightSettings blueSession, freyciaSession;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession()
        {
            // 禁用 Domain Reload 时仍从上次明确保存的候选重新开始。
            ClearSession();
            Application.quitting -= ClearSession;
            Application.quitting += ClearSession;
        }

        private static void ClearSession()
        {
            if (shared) Destroy(shared);
            if (blueSession) Destroy(blueSession);
            if (freyciaSession) Destroy(freyciaSession);
            shared = blueSession = freyciaSession = null;
        }
        public static bool BloomEnabled => !shared || shared.bloom;
        public static bool GradeEnabled => !shared || shared.grade;
        public static bool HeroLightEnabled => shared && shared.heroLight;
        private readonly SideViewOSLightSettings[] profiles = new SideViewOSLightSettings[2];
        private readonly string[] keys = new string[2];
        private SideViewRenderComparison comparison;
        private SideViewCharacterRendering characters;
        private SideViewCameraFollow follow;
        private Button select, debug;
        private Font font;
        private bool expanded, held;
        private float followProjection;
        private bool orthographic;
        private int slot;
        private string notice;
        private Vector2 scroll;
        public static void Attach(SideViewRenderComparison comparison, Text statusLabel, Component blue, Component freycia)
        {
            var controls = comparison.GetComponent<SideViewOSLightControls>();
            if (controls) return;
            controls = comparison.gameObject.AddComponent<SideViewOSLightControls>();
            controls.comparison = comparison;
            controls.characters = comparison.GetComponent<SideViewCharacterRendering>();
            controls.follow = comparison.GetComponent<SideViewCameraFollow>();
            if (!shared) shared = Load("World");
            for (int i = 0; i < 2 && i < controls.characters.actors.Length; ++i)
            {
                var actor = controls.characters.actors[i];
                if (!actor) continue;
                bool isBlue = blue && blue.transform.IsChildOf(actor.transform);
                bool isFreycia = freycia && freycia.transform.IsChildOf(actor.transform);
                if (isBlue == isFreycia) continue;
                controls.keys[i] = isBlue ? "Blue" : "Freycia";
                if (isBlue && !blueSession) blueSession = Load("Blue");
                if (isFreycia && !freyciaSession) freyciaSession = Load("Freycia");
                controls.profiles[i] = isBlue ? blueSession : freyciaSession;
                controls.Apply(i);
            }
            if (!statusLabel) return;
            controls.font = statusLabel.font;
            var row = statusLabel.transform.parent;
            controls.select = controls.MakeButton(row, "SelectD", "D  OSLight", 316, -20, comparison.SelectOSLight);
            controls.debug = controls.MakeButton(row, "OSLightDebug", "渲染调试", 316, -55, controls.TogglePanel);
            controls.debug.gameObject.SetActive(false);
        }

        private Button MakeButton(Transform parent, string name, string title, float x, float y, UnityEngine.Events.UnityAction click)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(148, 32);
            var bg = go.GetComponent<Image>();
            bg.color = new Color(.10f,.16f,.18f,.96f);
            var button = go.GetComponent<Button>();
            button.targetGraphic = bg;
            button.onClick.AddListener(click);
            var label = new GameObject("Label", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            label.transform.SetParent(rect,false);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            label.font = font;
            label.fontSize = 15;
            label.alignment = TextAnchor.MiddleCenter;
            label.text = title;
            label.raycastTarget = false;
            return button;
        }

        private static SideViewOSLightSettings Load(string key)
        {
            var asset = Resources.Load<SideViewOSLightSettings>(ResourceFolder + key);
            var instance = asset ? Instantiate(asset) : ScriptableObject.CreateInstance<SideViewOSLightSettings>();
            instance.hideFlags = HideFlags.DontSave;
#if !UNITY_EDITOR
            if (PlayerPrefs.HasKey(ResourceFolder + key))
                JsonUtility.FromJsonOverwrite(PlayerPrefs.GetString(ResourceFolder + key), instance);
#endif
            return instance;
        }

        public bool SaveSelectedActorCandidate(int actorSlot)
        {
            if (actorSlot < 0 || actorSlot >= profiles.Length || !profiles[actorSlot]) return false;
            return Save(keys[actorSlot], profiles[actorSlot]);
        }

        public bool SaveSharedCandidate() => shared && Save("World", shared);

        private bool Save(string key, SideViewOSLightSettings source)
        {
#if UNITY_EDITOR
            string parent = "Assets/OSSASTRA/SideView";
            foreach (string folder in new[] { "Resources", "OSLightCandidate" })
            {
                if (!UnityEditor.AssetDatabase.IsValidFolder(parent + "/" + folder))
                    UnityEditor.AssetDatabase.CreateFolder(parent, folder);
                parent += "/" + folder;
            }
            string path = AssetFolder + "/" + key + ".asset";
            var dest = UnityEditor.AssetDatabase.LoadAssetAtPath<SideViewOSLightSettings>(path);
            if (!dest && UnityEditor.AssetDatabase.LoadMainAssetAtPath(path)) { notice = "保存失败，路径已有其他资产"; return false; }
            if (!dest)
            {
                dest = Instantiate(source);
                dest.hideFlags = HideFlags.None;
                UnityEditor.AssetDatabase.CreateAsset(dest, path);
            }
            else JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(source), dest);
            UnityEditor.EditorUtility.SetDirty(dest);
            UnityEditor.AssetDatabase.SaveAssetIfDirty(dest);
#else
            PlayerPrefs.SetString(ResourceFolder + key, JsonUtility.ToJson(source));
            PlayerPrefs.Save();
#endif
            notice = "已保存 " + key;
            return true;
        }

        private void Apply(int i)
        {
            var p = profiles[i];
            characters.SetCandidateActor(i, new Vector3(p.midtone,p.environmentTint,p.highlightProtection),
                new Vector4(p.innerWidth,p.innerEnabled ? p.innerStrength : 0,p.outerWidth,p.outerEnabled ? p.outerStrength : 0));
        }

        private void Update()
        {
            bool available = comparison && comparison.isActiveAndEnabled;
            if (select && select.gameObject.activeSelf != available) select.gameObject.SetActive(available);
            bool showDebug = available && comparison.IsOSLightCandidate;
            if (debug && debug.gameObject.activeSelf != showDebug) debug.gameObject.SetActive(showDebug);
            if (!available) ReleaseCamera();
        }

        private void TogglePanel()
        {
            expanded = !expanded;
            if (!expanded) ReleaseCamera();
        }

        private void ReleaseCamera()
        {
            if (held && follow)
            {
                var view = follow.GetComponent<Camera>();
                if (orthographic) view.orthographicSize = followProjection;
                else view.fieldOfView = followProjection;
                follow.enabled = true;
            }
            held = false;
        }

        private void HoldCamera()
        {
            var view = follow.GetComponent<Camera>();
            orthographic = view.orthographic;
            float captured = orthographic ? view.orthographicSize : view.fieldOfView;
            follow.enabled = false;
            // 跟随组件撤销交接推近时会还原投影。比较期保留这一帧的倍率，归还时恢复。
            followProjection = orthographic ? view.orthographicSize : view.fieldOfView;
            if (orthographic) view.orthographicSize = captured;
            else view.fieldOfView = captured;
            held = true;
        }

        private void OnDisable()
        {
            ReleaseCamera();
            if (select) select.gameObject.SetActive(false);
            if (debug) debug.gameObject.SetActive(false);
        }
        private void OnDestroy()
        {
            ReleaseCamera();
            if (select) Destroy(select.gameObject);
            if (debug) Destroy(debug.gameObject);
        }

        private static float Field(string label, float value, float low, float high)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(88));
            value = GUILayout.HorizontalSlider(value,low,high);
            GUILayout.Label(value.ToString("0.00"), GUILayout.Width(42));
            GUILayout.EndHorizontal();
            return value;
        }

        private void OnGUI()
        {
            if (!comparison || !comparison.isActiveAndEnabled || !expanded || (!comparison.IsOSLightCandidate && !held)) return;
            var oldFont = GUI.skin.font;
            GUI.skin.font = font;
            float w = Mathf.Min(Screen.width - 12, 310);
            GUILayout.BeginArea(new Rect(Screen.width-w-6, 80, w, Mathf.Min(520,Screen.height-84)),GUI.skin.box);
            scroll = GUILayout.BeginScrollView(scroll);
            if (follow && GUILayout.Button(held ? "恢复镜头跟随" : "固定当前比较机位"))
            {
                if (held) ReleaseCamera();
                else if (follow.enabled) HoldCamera();
            }
            if (comparison.IsOSLightCandidate)
            {
                string Name(int i) => keys[i] == "Blue" ? "小蓝" : keys[i] == "Freycia" ? "芙蕾" : "未绑定";
                slot = GUILayout.Toolbar(slot, new[] { Name(0), Name(1) });
                var p = profiles[slot];
                if (p)
                {
                    p.midtone = Field("主体中间调",p.midtone,.5f,1.5f);
                    p.environmentTint = Field("环境染色",p.environmentTint,0,1);
                    p.highlightProtection = Field("高光保护",p.highlightProtection,0,1);
                    p.innerEnabled = GUILayout.Toggle(p.innerEnabled,"方向内边光");
                    p.innerStrength = Field("内光强度",p.innerStrength,0,3);
                    p.innerWidth = Field("内光宽度",p.innerWidth,1,8);
                    p.outerEnabled = GUILayout.Toggle(p.outerEnabled,"外柔光");
                    p.outerStrength = Field("外光强度",p.outerStrength,0,2);
                    p.outerWidth = Field("外光宽度",p.outerWidth,1,4);
                    if (GUILayout.Button("恢复该角色推荐值"))
                    {
                        var defaults = ScriptableObject.CreateInstance<SideViewOSLightSettings>();
                        JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(defaults), p);
                        Destroy(defaults);
                    }
                    Apply(slot);
                    if (GUILayout.Button("保存 " + Name(slot) + " 候选配置")) SaveSelectedActorCandidate(slot);
                }
                GUILayout.Label("共同成片");
                shared.bloom = GUILayout.Toggle(shared.bloom,"Bloom");
                shared.grade = GUILayout.Toggle(shared.grade,"颜色曲线");
                shared.heroLight = GUILayout.Toggle(shared.heroLight,"角色跟随背景光斑");
                if (GUILayout.Button("恢复成片推荐值")) { shared.bloom = shared.grade = true; shared.heroLight = false; }
                if (GUILayout.Button("保存共同成片配置")) SaveSharedCandidate();
                if (!string.IsNullOrEmpty(notice)) GUILayout.Label(notice);
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            GUI.skin.font = oldFont;
        }
    }
}

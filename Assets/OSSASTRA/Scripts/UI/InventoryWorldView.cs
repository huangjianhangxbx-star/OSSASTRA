/* 脚本：InventoryWorldView
 * 功能：用同一物品Sprite绘制场景掉落，投影可点击/拖动的轻量标签和容器入口。
 * 边界：当前地图地面掉落仅显示最近的有界数量、固定箱保留；显隐不改变账本或拾取资格，销毁只回收自有表现。
 */
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Ossastra.Inventory;
namespace Ossastra.UserInterface
{
    public sealed class InventoryWorldView:MonoBehaviour
    {
        [SerializeField] private InventorySession session;
        [SerializeField] private InventoryView view;
        [Tooltip("只限制地面掉落的显示，固定箱保留；原账本、访问和F拾取不受显示预算影响。")]
        [Range(1,256)] public int maximumGroundMarkers=64;
        public int OwnedMarkerCount=>marks.Count;
        public int SelectedGroundMarkerCount {get;private set;}
        private Canvas canvas;private RectTransform root;private Font font;
        private sealed class Mark{public RectTransform rect;public Text text;public InventoryPointerTarget target;public SpriteRenderer sprite;}
        private readonly Dictionary<string,Mark> marks=new Dictionary<string,Mark>();
        private void Start()
        {
            var go=new GameObject("InventoryWorldLabels",typeof(RectTransform),typeof(Canvas),typeof(GraphicRaycaster));go.transform.SetParent(transform,false);root=(RectTransform)go.transform;canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=120;font=Font.CreateDynamicFontFromOSFont("Microsoft YaHei",12);
        }
        private void LateUpdate()
        {
            if(session==null||!session.Ready||view==null||Camera.main==null||root==null)return;
            var worldIds=session.WorldContainerIds.ToArray(); // 会话已按当前地图过滤，不能把其他区域掉落显示进来。
            var groundIds=new HashSet<string>(session.GroundContainers,StringComparer.Ordinal);
            var actor=session.Coordinator?.Current;
            var origin=actor==null?Camera.main.transform.position:actor.transform.position;
            var selectedGround=worldIds.Where(groundIds.Contains).Where(id=>session.ContainerPosition(id).HasValue)
                .OrderBy(id=>Vector3.SqrMagnitude(session.ContainerPosition(id).Value-origin))
                .ThenBy(id=>id,StringComparer.Ordinal).Take(Mathf.Clamp(maximumGroundMarkers,1,256)).ToArray();
            SelectedGroundMarkerCount=selectedGround.Length;
            var ids=worldIds.Where(id=>!groundIds.Contains(id)).Concat(selectedGround).ToArray();
            var selectedIds=new HashSet<string>(ids,StringComparer.Ordinal);
            // 仅回收不再显示的表现，绝不回收逻辑物品/容器；走近后可从同一账本重建。
            foreach(var id in marks.Keys.Where(id=>!selectedIds.Contains(id)).ToArray()){Destroy(marks[id].rect.gameObject);if(marks[id].sprite!=null)Destroy(marks[id].sprite.gameObject);marks.Remove(id);}
            // 一次索引当前条目，避免每份已选掉落都从头扫描整个Ledger。
            var firstItems=session.Ledger.Items.Where(item=>selectedIds.Contains(item.Location.ContainerId))
                .GroupBy(item=>item.Location.ContainerId).ToDictionary(group=>group.Key,group=>group.First(),StringComparer.Ordinal);
            foreach(var id in ids)
            {
                var position=session.ContainerPosition(id);if(!position.HasValue)continue;
                var item=session.IsLoosePickup(id)&&firstItems.TryGetValue(id,out var first)?first:null;var definition=session.Definition(item?.DefinitionId);
                if(!marks.TryGetValue(id,out var mark))
                {
                    mark=new Mark();var go=new GameObject("Mark_"+id,typeof(RectTransform),typeof(Image));go.transform.SetParent(root,false);mark.rect=(RectTransform)go.transform;mark.rect.anchorMin=mark.rect.anchorMax=new Vector2(0,0);mark.rect.pivot=new Vector2(.5f,0);mark.rect.sizeDelta=item==null?new Vector2(110,28):new Vector2(70,52);go.GetComponent<Image>().color=item==null?new Color32(11,20,29,218):Color.clear;mark.target=go.AddComponent<InventoryPointerTarget>();mark.target.View=view;mark.target.ContainerId=id;
                    var label=new GameObject("Label",typeof(RectTransform),typeof(Text));label.transform.SetParent(go.transform,false);var rt=(RectTransform)label.transform;rt.anchorMin=Vector2.zero;rt.anchorMax=Vector2.one;rt.offsetMin=rt.offsetMax=Vector2.zero;mark.text=label.GetComponent<Text>();mark.text.font=font;mark.text.fontSize=12;mark.text.color=new Color32(238,233,233,255);mark.text.alignment=TextAnchor.LowerCenter;mark.text.raycastTarget=false;
                    if(item==null)
                    {
                        var button=go.AddComponent<Button>();button.targetGraphic=go.GetComponent<Image>();
                        // 世界标签只接受点击或 F 交互；范围拒绝后不能继续占据 UI 导航焦点并拦截角色移动。
                        button.navigation=new Navigation{mode=Navigation.Mode.None};
                        var key=id;button.onClick.AddListener(()=>view.OpenExternal(key));
                    }
                    marks.Add(id,mark);
                }
                mark.target.ItemId=item?.Id;mark.target.ContainerId=id;
                if(item!=null&&definition?.icon!=null&&mark.sprite==null){var go=new GameObject("GroundItemSprite_"+item.Id);go.transform.SetParent(transform,false);mark.sprite=go.AddComponent<SpriteRenderer>();mark.sprite.sortingOrder=40;go.transform.localScale=Vector3.one*.5f;}
                if(mark.sprite!=null){mark.sprite.sprite=definition?.icon;mark.sprite.transform.position=position.Value+Vector3.up*.15f;mark.sprite.transform.rotation=Camera.main.transform.rotation;}
                var screen=Camera.main.WorldToScreenPoint(position.Value);mark.rect.gameObject.SetActive(screen.z>0);mark.rect.anchoredPosition=new Vector2(screen.x,screen.y-12);
                mark.text.text=item!=null?definition?.displayName+" ×"+item.Quantity:"□ "+session.ContainerDisplayName(id);mark.text.color=session.CanAccessContainer(id)?Color.white:new Color32(155,145,150,255);
                if(item==null){mark.rect.sizeDelta=new Vector2(Mathf.Min(140,mark.text.preferredWidth+12),28);mark.text.alignment=TextAnchor.MiddleCenter;}
            }
        }
        private void OnDestroy(){foreach(var m in marks.Values)if(m.sprite!=null)Destroy(m.sprite.gameObject);if(font!=null)Destroy(font);}
    }
}

/* 脚本：EnemyHealthBarsView
 * 功能：按冻结原型的66×8屏幕像素、1像素米色边与红色填充呈现敌方头顶HP。
 * 边界：只读EnemyRoot的唯一Vitals；不写生命、不阻挡指针、不为死亡/屏幕外单位显示血条。
 * 协作：HUD组合时注入敌人列表和相机；每帧只投影身体锚点，不根据Spine透明画布推算位置。
 * 可选房间许可只筛世界血条表现；没有该相机登记时保持原投影行为，不改变敌人业务资格。
 */
using UnityEngine;
using UnityEngine.UI;
using Ossastra.SceneVisibility.Rooms.Rendering;

namespace Ossastra.UserInterface
{
    public sealed class EnemyHealthBarsView : MonoBehaviour
    {
        private sealed class Bar
        {
            public Ossastra.Enemies.EnemyRoot enemy;
            public RectTransform rect;
            public Image fill;
            public Material appliedMaterial;
        }
        private Bar[] bars;
        private Camera worldCamera;
        private Canvas canvas;
        private Material roomLabelMaterial, portalLabelMaterial;
        private const float BodyAnchorHeight=1.8f;

        public void Initialize(Ossastra.Enemies.EnemyRoot[] enemies,Camera camera)
        {
            if(bars!=null)throw new System.InvalidOperationException("Enemy HP view already initialized");
            if(enemies==null||camera==null)throw new System.ArgumentException("Enemy HP targets and camera required");
            worldCamera=camera;
            var host=new GameObject("EnemyHealthCanvas",typeof(RectTransform),typeof(Canvas));
            host.transform.SetParent(transform,false);canvas=host.GetComponent<Canvas>();
            canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=22;canvas.scaleFactor=1;canvas.pixelPerfect=true;
            bars=new Bar[enemies.Length];
            for(int i=0;i<enemies.Length;i++)
            {
                var rect=Create(host.transform,"EnemyHealth"+i,new Color32(194,188,167,255));
                rect.anchorMin=rect.anchorMax=Vector2.zero;rect.pivot=new Vector2(.5f,0);rect.sizeDelta=new Vector2(66,8);
                var background=Create(rect,"Background",new Color32(7,10,14,255));Inset(background);
                var fill=Create(background,"Fill",new Color32(185,86,99,255));
                fill.anchorMin=fill.anchorMax=Vector2.zero;fill.pivot=Vector2.zero;fill.anchoredPosition=Vector2.zero;
                bars[i]=new Bar{enemy=enemies[i],rect=rect,fill=fill.GetComponent<Image>()};rect.gameObject.SetActive(false);
            }
        }
        private static RectTransform Create(Transform parent,string name,Color color)
        {
            var node=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));
            node.transform.SetParent(parent,false);var image=node.GetComponent<Image>();image.color=color;image.raycastTarget=false;
            return (RectTransform)node.transform;
        }
        private static void Inset(RectTransform rect)
        {rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=Vector2.one;rect.offsetMax=-Vector2.one;}
        private void OnEnable(){if(canvas!=null)canvas.enabled=true;RoomMaskCameraRegistry.FramePublished+=OnRoomFrame;}
        private void OnDisable(){RoomMaskCameraRegistry.FramePublished-=OnRoomFrame;if(canvas!=null)canvas.enabled=false;}
        private void OnDestroy()
        {
            RoomMaskCameraRegistry.FramePublished-=OnRoomFrame;
            if(roomLabelMaterial!=null)Destroy(roomLabelMaterial);if(portalLabelMaterial!=null)Destroy(portalLabelMaterial);
        }
        private void OnRoomFrame(Camera camera){if(camera==worldCamera)UpdateBars();}
        private void LateUpdate()
        {UpdateBars();}
        private void UpdateBars()
        {
            if(bars==null)return;
            if(roomLabelMaterial==null)roomLabelMaterial=RoomWorldPresentationGate.CreateLabelMaterial(worldCamera);
            if(portalLabelMaterial==null)portalLabelMaterial=RoomWorldPresentationGate.CreateLabelMaterial(worldCamera,true);
            foreach(var bar in bars)
            {
                var enemy=bar.enemy;var vitals=enemy==null?null:enemy.Vitals;
                bool available=worldCamera!=null&&worldCamera.isActiveAndEnabled&&enemy!=null&&enemy.isActiveAndEnabled&&vitals!=null&&vitals.IsAvailable;
                var point=available?worldCamera.WorldToScreenPoint(enemy.transform.position+Vector3.up*BodyAnchorHeight):Vector3.zero;
                bool visible=available&&point.z>0&&point.x>=-50&&point.x<=Screen.width+50&&point.y>=0&&point.y<=Screen.height&&
                    RoomWorldPresentationGate.AllowsLabel(worldCamera,enemy.transform.position,enemy.transform);
                bar.rect.gameObject.SetActive(visible);if(!visible)continue;
                var material=RoomWorldPresentationGate.RequiresPortal(worldCamera,enemy.transform.position)?portalLabelMaterial:roomLabelMaterial;
                if(material!=null&&bar.appliedMaterial!=material)
                {foreach(var graphic in bar.rect.GetComponentsInChildren<Graphic>(true))graphic.material=material;bar.appliedMaterial=material;}
                bar.rect.anchoredPosition=new Vector2(point.x,point.y);
                bar.fill.rectTransform.sizeDelta=new Vector2(64*Mathf.Clamp01((float)vitals.Health.Current/vitals.Health.Maximum),6);
            }
        }
    }
}

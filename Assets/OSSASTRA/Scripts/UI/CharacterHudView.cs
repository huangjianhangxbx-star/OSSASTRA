/* 脚本：CharacterHudView
 * 功能：以c21d4fc现行六区锚点和原素材构造uGUI；每帧只消费快照。
 * 边界：按钮提交窄命令；不结算HP、恢复资源或创建装备实例。未迁移系统明确不可用。
 * 协作：使用min(宽/1920,高/1080)缩放、物理边缘锚定；不是居中整幅画布的同比留边。
 */
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
namespace Ossastra.UserInterface
{
    [DefaultExecutionOrder(200)]
    public sealed class CharacterHudView:MonoBehaviour
    {
        private sealed class Card
        {
            public Text name,control,values,stamina,right,left,condition,bottle;
            public Image hp,gray,posture,staminaFill;
            public Button leftButton,rightButton;
            public CharacterHudSnapshot snapshot;
        }
        [SerializeField] private CharacterHudAdapter adapter;
        [SerializeField] private HudSkinSO skin;
        public InputAction closeAction=new InputAction("CloseEquipment",InputActionType.Button,binding:"<Keyboard>/escape");
        private void OnEnable(){closeAction.Enable();}
        private ICharacterHudPort port;
        private Canvas canvas;
        private RectTransform surface,popup,modal;
        private Font font,mono;
        private readonly List<(Text text,int size,int minimum)> typography=new List<(Text,int,int)>();
        private Card hero,companion;
        private Text clock,notice,skillOwner,popupNote,popupIdentity;
        private string popupActorId;
        private bool popupRight;
        private readonly List<(string id,Image image)> mainActionChoices=new List<(string,Image)>();
        private readonly List<(string id,Image image,Text status)> popupChoices=new List<(string,Image,Text)>();
        private readonly List<(string id,Text text)> sourcePopupChoices=new List<(string,Text)>();
        private readonly Text[] skillLabels=new Text[3],skillStates=new Text[3];
        private readonly Image[] skillImages=new Image[3];
        private static readonly Color ink=new Color32(9,11,13,240),edge=new Color32(140,169,163,255),gold=new Color32(201,177,107,255);
        private void Start()
        {
            port=adapter;if(port==null||skin==null||skin.barBase==null||skin.skillFrame==null){Debug.LogError("Prototype HUD skin/port missing",this);enabled=false;return;}
            font=Font.CreateDynamicFontFromOSFont("SimSun",16);mono=Font.CreateDynamicFontFromOSFont(new[]{"Consolas","Microsoft YaHei"},14);
            var go=new GameObject("PrototypeHudCanvas",typeof(RectTransform),typeof(Canvas),typeof(GraphicRaycaster));
            go.transform.SetParent(transform,false);surface=(RectTransform)go.transform;canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=100;canvas.pixelPerfect=true;
            canvas.scaleFactor=Mathf.Min(Screen.width/1920f,Screen.height/1080f);
            hero=BuildCard(Cluster("HeroCluster",34,41,529,163,new Vector2(0,1)),false);
            companion=BuildCard(Cluster("CompanionCluster",3,0,377,220,Vector2.zero),true);
            BuildWorld();BuildSkills();BuildQuickSlots();BuildVitality();
            // 场景级HUD组合一次性收集敌人；头顶HP视图独立于玩家卡片，读取敌方原生命所有者。
            var enemyHealth=gameObject.AddComponent<EnemyHealthBarsView>();
            enemyHealth.Initialize(FindObjectsOfType<Ossastra.Enemies.EnemyRoot>(),Camera.main);
            notice=Label(surface,"Notice",new Rect(0,0,550,36),"",13,gold);var nr=notice.rectTransform;nr.anchorMin=nr.anchorMax=new Vector2(.5f,0);nr.pivot=new Vector2(.5f,0);nr.anchoredPosition=new Vector2(0,128);
        }
        private RectTransform Cluster(string name,float x,float y,float w,float h,Vector2 anchor)
        {
            var r=Node(surface,name,new Rect(0,0,w,h));r.anchorMin=r.anchorMax=anchor;r.pivot=anchor;
            r.anchoredPosition=new Vector2(anchor.x==1?-x:x,anchor.y==1?-y:y);return r;
        }
        private static RectTransform Node(Transform parent,string name,Rect rect)
        {
            var r=(RectTransform)new GameObject(name,typeof(RectTransform)).transform;r.SetParent(parent,false);
            r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(rect.x,-rect.y);r.sizeDelta=rect.size;return r;
        }
        private Image Picture(Transform parent,string name,Rect rect,Sprite sprite,Color? color=null,bool raycast=false)
        {var r=Node(parent,name,rect);var image=r.gameObject.AddComponent<Image>();image.sprite=sprite;image.color=color??Color.white;image.raycastTarget=raycast;return image;}
        private Text Label(Transform parent,string name,Rect rect,string value,int size,Color color,TextAnchor align=TextAnchor.MiddleCenter,bool sans=false)
        {
            var r=Node(parent,name,rect);var text=r.gameObject.AddComponent<Text>();text.font=sans?mono:font;text.fontSize=size;text.color=color;text.text=value;text.alignment=align;text.raycastTarget=false;
            typography.Add((text,size,name=="Glyph"?8:name=="Unavailable"?7:size<=9?6:size<=12?7:9));
            text.horizontalOverflow=HorizontalWrapMode.Wrap;text.verticalOverflow=VerticalWrapMode.Truncate;return text;
        }
        private Button Button(Transform parent,string name,Rect rect,Sprite background,Action command)
        {
            var image=Picture(parent,name,rect,background,null,true);var button=image.gameObject.AddComponent<Button>();button.targetGraphic=image;
            var colors=button.colors;colors.highlightedColor=new Color(1.08f,1.08f,1.08f);colors.pressedColor=new Color(.8f,.8f,.8f);button.colors=colors;
            button.onClick.AddListener(()=>command?.Invoke());return button;
        }
        private Image Bar(Transform parent,string name,Rect rect,Color fill)
        {
            var root=Node(parent,name,rect);Picture(root,"Base",new Rect(0,0,rect.width,rect.height),skin.barBase);
            var image=Picture(root,"Value",new Rect(0,0,rect.width,rect.height),skin.barFill,fill);image.type=Image.Type.Filled;image.fillMethod=Image.FillMethod.Horizontal;image.fillOrigin=0;
            Picture(root,"Frame",new Rect(0,0,rect.width,rect.height),skin.barFrame);return image;
        }
        private Card BuildCard(RectTransform group,bool isCompanion)
        {
            var c=new Card();var width=group.rect.width;
            c.hp=Bar(group,"Health",new Rect(0,0,width,23),new Color32(175,96,103,255));
            c.gray=Picture(c.hp.transform.parent,"RecoverableHealth",new Rect(0,0,width,23),skin.barFill,new Color32(146,147,138,255));
            c.gray.type=Image.Type.Filled;c.gray.fillMethod=Image.FillMethod.Horizontal;c.gray.fillOrigin=0;
            c.gray.transform.SetSiblingIndex(c.hp.transform.GetSiblingIndex());c.gray.enabled=false;
            c.name=Label(group,"Name",new Rect(12,3,width*.46f,16),"",14,new Color32(239,236,224,255),TextAnchor.MiddleLeft);
            c.control=Label(group,"Control",new Rect(50,3,96,16),"",10,edge,TextAnchor.MiddleLeft);
            c.values=Label(group,"Values",new Rect(width*.43f,isCompanion?25:4,width*.55f,15),"",isCompanion?7:9,Color.white,TextAnchor.MiddleRight,true);
            Picture(group,"StaminaBase",new Rect(0,24,width*.5f-2,11),null,new Color32(21,33,28,255));
            c.staminaFill=Picture(group,"StaminaFill",new Rect(0,24,width*.5f-2,11),null,new Color32(100,148,125,255));
            c.stamina=Label(group,"Stamina",new Rect(0,24,width*.5f-2,11),"",8,edge,TextAnchor.MiddleCenter,true);
            c.posture=Bar(group,"Posture",new Rect(width*.5f+1,24,width*.5f-1,11),gold);
            var kit=Node(group,"Kit",new Rect(0,isCompanion?42:40,width,isCompanion?178:123));
            var x=isCompanion?177:0;var y=isCompanion?56:0;
            c.rightButton=Button(kit,"RightHand",new Rect(x,y,52,isCompanion?60:52),isCompanion?skin.square:skin.metalSquare,()=>OpenEquipment(c,true));
            c.right=Label(c.rightButton.transform,"Glyph",new Rect(0,0,52,isCompanion?60:52),"I",11,edge,TextAnchor.MiddleCenter,true);
            c.leftButton=Button(kit,"LeftHand",new Rect(x,y+63,52,isCompanion?60:52),isCompanion?skin.square:skin.metalSquare,()=>OpenEquipment(c,false));
            c.left=Label(c.leftButton.transform,"Glyph",new Rect(0,0,52,isCompanion?60:52),"",11,edge,TextAnchor.MiddleCenter,true);
            for(int i=0;i<3;i++)
            {var px=(isCompanion?177:153)+i*(isCompanion?66:50);var box=Picture(kit,"Passive"+(i+1),new Rect(px,0,isCompanion?61:45,isCompanion?45:50),skin.square,Color.gray,true);Label(box.transform,"Unavailable",new Rect(2,0,isCompanion?57:41,isCompanion?45:50),"L"+(i+1)+"\n未接入",9,edge);}
            if(!isCompanion)
            {
                var bottle=Button(kit,"SharedHealingBottle",new Rect(59,0,90,90),skin.metalSquare,()=>port.UseBottle());
                c.bottle=Label(bottle.transform,"Balance",new Rect(3,0,84,90),"Y",25,edge);
            }
            else
            {
                var face=Node(group,"Portrait",new Rect(0,48,172,172));face.gameObject.AddComponent<CircularHudMask>().raycastTarget=true;face.gameObject.AddComponent<Mask>().showMaskGraphic=false;
                var portrait=Picture(face,"UserPortrait",new Rect(0,0,172,172),skin.portrait);
                var aspect=portrait.gameObject.AddComponent<AspectRatioFitter>();aspect.aspectMode=AspectRatioFitter.AspectMode.EnvelopeParent;aspect.aspectRatio=skin.portrait.rect.width/skin.portrait.rect.height;
                // 头像仅属于固定伙伴身份；切控不交换两张状态卡，也不临时换头像。
                Picture(group,"PortraitFrame",new Rect(0,48,172,172),skin.portraitFrame);
                var free=Picture(kit,"FreeItem",new Rect(233,56,144,122),skin.square,Color.gray,true);Label(free.transform,"Unavailable",new Rect(4,0,136,122),"优先物品\n未接入",12,edge);
            }
            c.condition=Label(group,"Condition",new Rect(0,isCompanion?-24:166,width,23),"",11,gold,TextAnchor.MiddleLeft);
            return c;
        }
        private void BuildWorld()
        {
            var g=Cluster("WorldCluster",13,17,259,117,new Vector2(1,1));
            Picture(g,"ClockPanel",new Rect(104,0,155,73),null,ink,true);clock=Label(g,"SimulationTime",new Rect(110,6,143,33),"",17,Color.white,TextAnchor.MiddleRight,true);
            Label(g,"Calendar",new Rect(110,40,143,20),"日历 / 天气未接入",10,edge,TextAnchor.MiddleRight);
            Picture(g,"WorldStatus",new Rect(0,76,259,41),null,ink,true);Label(g,"Location",new Rect(6,76,247,41),"角色纵向闭环 · 测试场\n世界系统未接入",9,edge,TextAnchor.MiddleRight);
        }
        private void BuildSkills()
        {
            var g=Cluster("SkillCluster",1,47,254,273,new Vector2(1,0));
            skillOwner=Label(g,"ControlledCharacter",new Rect(-51,-44,305,24),"",14,Color.white,TextAnchor.MiddleRight);
            var positions=new[]{new Vector2(128,0),new Vector2(8,80),new Vector2(128,147)};
            for(int i=0;i<3;i++)
            {
                var index=i;var pos=positions[i];var button=Button(g,"Skill"+i,new Rect(pos.x,pos.y,126,126),skin.skillFrame,()=>port.UseSkill(index));
                skillImages[i]=Picture(button.transform,"Emblem",new Rect(38,29,50,50),i==0?skin.slash:i==1?skin.rocket:skin.empty);
                Label(button.transform,"Key",new Rect(0,10,126,20),new[]{"Q","E","R"}[i],14,Color.white);
                skillLabels[i]=Label(button.transform,"Definition",new Rect(18,80,90,18),"",11,Color.white,TextAnchor.MiddleCenter,true);
                skillStates[i]=Label(button.transform,"State",new Rect(22,98,82,15),"",8,edge,TextAnchor.MiddleCenter,true);
            }
            Label(g,"InputHints",new Rect(-51,-120,305,72),"LMB 普攻  ·  RMB 副手\nShift 闪避  ·  Z 切换角色\n点击左手装备切换盾 / 炸弹",10,edge,TextAnchor.MiddleRight);
        }
        private void BuildQuickSlots()
        {
            var g=Cluster("QuickCluster",23,5,866,110,new Vector2(.5f,0));
            var bag=Picture(g,"Backpack",new Rect(0,0,73,110),skin.square,Color.gray,true);Label(bag.transform,"Unavailable",new Rect(0,0,73,110),"Tab\n背包 · 未接入",12,edge);
            Picture(g,"SlotsBase",new Rect(82,0,784,110),null,ink,true);
            for(int i=0;i<10;i++)
            {var r=Node(g,"QuickSlot"+(i+1),new Rect(88+i*77.2f,6,77.2f,98));if(i<9)Picture(r,"Divider",new Rect(76,0,1,98),null,new Color32(140,169,163,51));Label(r,"Key",new Rect(7,4,60,20),((i+1)%10).ToString(),13,edge,TextAnchor.MiddleLeft,true);Label(r,"Unavailable",new Rect(2,35,72,30),"未接入",9,edge);}
        }
        private void BuildVitality()
        {
            var g=Cluster("VitalityCluster",2,0,350,30,new Vector2(1,0));Picture(g,"Upgrade",new Rect(0,0,100,30),skin.square,Color.gray,true);Label(g,"UpgradeText",new Rect(0,0,100,30),"升级 · 未接入",11,edge);
            Picture(g,"CarriedVitality",new Rect(104,0,246,30),null,ink,true);Label(g,"Balance",new Rect(104,0,246,30),"携带活力 · 未接入",11,gold);
        }
        private void Refresh(Card card,bool isCompanion)
        {
            var s=port.Read(isCompanion);card.snapshot=s;
            card.name.text=s.name??"无角色";card.control.text=s.controlled?"◆ 主控":"◇ 同行";
            card.control.rectTransform.anchoredPosition=new Vector2(22+card.name.preferredWidth,-3);card.control.color=s.controlled?new Color32(255,235,187,255):edge;
            card.values.text=$"生命 {Mathf.CeilToInt(s.hp)}/{s.maxHp:0} · 虚血 {Mathf.CeilToInt(s.grayHp)} · 架势 {s.posture:0}/{s.maxPosture:0}";
            // 原型fill_full含2.15%两端留白，按原CSS端点映射，避免低血量刻度偏短。
            card.hp.fillAmount=.0215f+.957f*(s.maxHp<=0?0:Mathf.Clamp01(s.hp/s.maxHp));card.posture.fillAmount=.0215f+.957f*(s.maxPosture<=0?0:Mathf.Clamp01(s.posture/s.maxPosture));
            card.gray.enabled=s.grayHp>0;card.gray.fillAmount=.0215f+.957f*(s.maxHp<=0?0:Mathf.Clamp01((s.hp+s.grayHp)/s.maxHp));
            card.staminaFill.rectTransform.sizeDelta=new Vector2(((RectTransform)card.staminaFill.transform.parent).rect.width*.5f-2,11)*new Vector2(s.maxStamina<=0?0:Mathf.Clamp01(s.stamina/s.maxStamina),1);
            card.stamina.text=$"体力 {Mathf.CeilToInt(s.stamina)}/{s.maxStamina:0}";
            card.left.text=s.leftHand=="空"?"—":s.leftHand.Contains("盾")?"盾":"弹";card.condition.text=string.IsNullOrEmpty(s.condition)?s.abilityResources:s.condition;
            card.right.text=s.mainHandForm??"I";
            card.rightButton.interactable=s.alive;card.leftButton.interactable=s.alive;
            if(card.bottle!=null)card.bottle.text="Y\n<size=14>"+port.BottleCount+"/1</size>";
        }
        private void LateUpdate()
        {
            if(canvas==null)return;
            if(popup!=null&&closeAction.WasPressedThisFrame())CloseEquipment();
            canvas.scaleFactor=Mathf.Min(Screen.width/1920f,Screen.height/1080f);
            foreach(var metric in typography)if(metric.text!=null)metric.text.fontSize=Mathf.CeilToInt(Mathf.Max(metric.size,metric.minimum/canvas.scaleFactor));
            Refresh(hero,false);Refresh(companion,true);var s=port.ReadCurrent();
            skillOwner.text=(s.name??"无角色")+"  当前主控";
            for(int i=0;i<3;i++){skillLabels[i].text=s.skillNames!=null&&i<s.skillNames.Length?s.skillNames[i]:"未接入";var cd=s.skillCooldowns!=null&&i<s.skillCooldowns.Length?s.skillCooldowns[i]:0;skillStates[i].text=s.skillStates==null||i>=s.skillStates.Length?"未接入":s.skillStates[i];skillImages[i].color=cd>0?Color.gray:Color.white;}
            if(popup!=null)RefreshEquipmentWindow();
            clock.text="时间未接入";clock.color=edge;notice.text=port.Notice;
        }
        private void OpenEquipment(Card card,bool right)
        {
            if(popup!=null)CloseEquipment();
            port.SetModalCapture(this,true);popupActorId=card.snapshot.actorId;popupRight=right;popupChoices.Clear();
            modal=Node(surface,"EquipmentModal",new Rect(0,0,surface.rect.width,surface.rect.height));modal.anchorMin=Vector2.zero;modal.anchorMax=Vector2.one;modal.offsetMin=modal.offsetMax=Vector2.zero;
            Picture(modal,"ModalInputCapture",new Rect(0,0,surface.rect.width,surface.rect.height),null,new Color(0,0,0,.35f),true);
            popup=Node(modal,"EquipmentPanel",new Rect(0,0,720,600));popup.anchorMin=popup.anchorMax=popup.pivot=new Vector2(.5f,.5f);popup.anchoredPosition=Vector2.zero;
            Picture(popup,"Background",new Rect(0,0,720,600),null,new Color32(9,13,16,255),true);
            Label(popup,"Title",new Rect(24,18,620,35),(card.snapshot.name??"")+" · "+(right?"右手装备":"左手装备"),20,Color.white,TextAnchor.MiddleLeft);
            var close=Button(popup,"Close",new Rect(660,20,36,32),skin.square,()=>CloseEquipment());Label(close.transform,"Glyph",new Rect(0,0,36,32),"×",20,edge);
            popupNote=Label(popup,"OwnedNote",new Rect(24,58,672,25),"仅展示本体持有装备 · 当前左手："+card.snapshot.leftHand+" · 耐久 "+card.snapshot.durability,12,edge,TextAnchor.MiddleLeft);
            if(right)
            {
                var skillChoices=port.SkillChoices(card.snapshot.actorId);var id=card.snapshot.actorId;
                Label(popup,"MainDefinition",new Rect(24,90,672,30),card.snapshot.rightHand+" · 主手动作组 · 脱战空闲时配置",14,edge);
                var profiles=port.MainActionChoices(id);var width=672f/(profiles.Length+1);
                for(int j=0;j<=profiles.Length;j++)
                {
                    var choice=j==0?(id:"",name:"角色默认 · 四段"):profiles[j-1];
                    var button=Button(popup,"MainProfile"+j,new Rect(24+j*width,126,width-8,42),skin.square,()=>port.ConfigureMainAction(id,choice.id));
                    Label(button.transform,"Choice",new Rect(4,2,width-16,38),choice.name,12,edge);mainActionChoices.Add((choice.id,(Image)button.targetGraphic));
                }
                var skillsViewport=Node(popup,"SkillConfigurationViewport",new Rect(24,178,672,354));skillsViewport.gameObject.AddComponent<RectMask2D>();
                var skillsContent=Node(skillsViewport,"Content",new Rect(0,0,672,Mathf.Max(354,40+(skillChoices.Length+1)*52)));
                var skillScroll=skillsViewport.gameObject.AddComponent<ScrollRect>();skillScroll.viewport=skillsViewport;skillScroll.content=skillsContent;skillScroll.horizontal=false;skillScroll.vertical=true;skillScroll.scrollSensitivity=35;skillScroll.movementType=ScrollRect.MovementType.Clamped;
                for(int column=0;column<2;column++)
                {
                    var slot=column==0?0:2;Label(skillsContent,"SlotTitle"+slot,new Rect(column*344,0,328,28),slot==0?"Q 技能":"R 技能",16,edge);
                    for(int j=0;j<=skillChoices.Length;j++)
                    {
                        var choice=j<skillChoices.Length?skillChoices[j]:(id:"",name:"清空槽位");
                        var button=Button(skillsContent,"Configure"+slot+"_"+j,new Rect(column*344,36+j*52,328,44),skin.square,()=>port.ConfigureSkill(id,slot,choice.id));
                        Label(button.transform,"Choice",new Rect(8,2,312,40),choice.name,14,edge);
                    }
                }
                var experimental=Button(popup,"ExperimentalSources",new Rect(24,550,672,32),skin.square,()=>OpenSources(card));
                Label(experimental.transform,"Label",new Rect(8,0,656,32),"实验来源配置 · 测试授予/撤回",13,edge);
                return;
            }
            popupIdentity=Label(popup,"EquipmentInstance",new Rect(24,550,672,40),"实例："+card.snapshot.leftInstanceId,11,edge,TextAnchor.MiddleLeft,true);
            var choices=port.EquipmentChoices(card.snapshot.actorId);var actorId=card.snapshot.actorId;
            var viewport=Node(popup,"EquipmentViewport",new Rect(24,102,672,430));viewport.gameObject.AddComponent<RectMask2D>();
            var content=Node(viewport,"Content",new Rect(0,0,672,Mathf.Max(430,choices.Length*100)));
            var scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.viewport=viewport;scroll.content=content;scroll.horizontal=false;scroll.vertical=true;scroll.scrollSensitivity=35;scroll.movementType=ScrollRect.MovementType.Clamped;
            for(int i=0;i<choices.Length;i++)
            {var choice=choices[i];var button=Button(content,"OwnedEquipment"+i,new Rect(0,i*100,672,88),skin.square,()=>port.Equip(actorId,choice.id));if(choice.sprite!=null)Picture(button.transform,"Icon",new Rect(12,10,68,68),choice.sprite);Label(button.transform,"Definition",new Rect(94,12,560,26),choice.name,18,edge,TextAnchor.MiddleLeft);var status=Label(button.transform,"Instance",new Rect(94,42,560,32),"本体持有 · 空闲时装备 · 保留原实例",12,edge,TextAnchor.MiddleLeft);popupChoices.Add((choice.id,(Image)button.targetGraphic,status));}
        }
        private void RefreshEquipmentWindow()
        {
            var actor=hero.snapshot.actorId==popupActorId?hero.snapshot:companion.snapshot;
            if(actor.actorId!=popupActorId||!actor.alive){CloseEquipment();return;}
            if(sourcePopupChoices.Count>0)
            {
                var sources=port.SourceChoices(popupActorId);
                foreach(var item in sourcePopupChoices)
                {foreach(var source in sources)if(source.id==item.id){item.text.text=source.name+" · "+source.state+"  ["+(source.held?"撤回":"试验授予")+"]";item.text.color=source.state=="生效"?gold:edge;break;}}
                return;
            }
            if(popupNote!=null)popupNote.text=(popupRight?"当前右手："+actor.rightHand:"当前左手："+actor.leftHand+" · 耐久 "+actor.durability)+" · "+actor.abilityResources;
            if(popupRight){var selected=port.SelectedMainAction(popupActorId);foreach(var choice in mainActionChoices)choice.image.color=choice.id==selected?new Color(.65f,.9f,1):Color.white;}
            if(popupIdentity!=null)popupIdentity.text="实例："+actor.leftInstanceId;
            foreach(var choice in popupChoices){var equipped=choice.id==actor.leftDefinitionId;choice.image.color=equipped?new Color(.65f,.9f,1):Color.white;choice.status.text=equipped?"已装备 · 耐久 "+actor.durability:"本体持有 · 空闲时装备 · 保留原实例";}
        }
        private void OpenSources(Card card)
        {
            CloseEquipment();port.SetModalCapture(this,true);popupActorId=card.snapshot.actorId;
            modal=Node(surface,"SourceModal",new Rect(0,0,surface.rect.width,surface.rect.height));modal.anchorMin=Vector2.zero;modal.anchorMax=Vector2.one;modal.offsetMin=modal.offsetMax=Vector2.zero;
            Picture(modal,"InputCapture",new Rect(0,0,surface.rect.width,surface.rect.height),null,new Color(0,0,0,.35f),true);
            popup=Node(modal,"SourcePanel",new Rect(0,0,720,600));popup.anchorMin=popup.anchorMax=popup.pivot=new Vector2(.5f,.5f);popup.anchoredPosition=Vector2.zero;
            Picture(popup,"Background",new Rect(0,0,720,600),null,new Color32(9,13,16,255),true);
            Label(popup,"Title",new Rect(24,18,620,35),card.snapshot.name+" · 实验来源",20,Color.white,TextAnchor.MiddleLeft);
            var close=Button(popup,"Close",new Rect(660,20,36,32),skin.square,CloseEquipment);Label(close.transform,"Glyph",new Rect(0,0,36,32),"×",20,edge);
            Label(popup,"Boundary",new Rect(24,58,672,40),"仅测试授予/撤回，不代表已迁移正式成长和掉落。缺少技能根的来源休眠；未实现项不执行。",12,edge);
            var choices=port.SourceChoices(popupActorId);var viewport=Node(popup,"Viewport",new Rect(24,110,672,460));viewport.gameObject.AddComponent<RectMask2D>();
            var content=Node(viewport,"Content",new Rect(0,0,672,Mathf.Max(460,choices.Length*54)));
            var scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.viewport=viewport;scroll.content=content;scroll.horizontal=false;scroll.vertical=true;scroll.scrollSensitivity=35;scroll.movementType=ScrollRect.MovementType.Clamped;
            for(int i=0;i<choices.Length;i++)
            {var choice=choices[i];var button=Button(content,"Source"+i,new Rect(0,i*54,672,48),skin.square,()=>port.ToggleExperimentalSource(popupActorId,choice.id));var text=Label(button.transform,"State",new Rect(12,2,648,44),choice.name+" · "+choice.state,14,edge,TextAnchor.MiddleLeft);sourcePopupChoices.Add((choice.id,text));}
        }
        private void CloseEquipment(){sourcePopupChoices.Clear();popupChoices.Clear();mainActionChoices.Clear();popupRight=false;popupNote=null;popupIdentity=null;if(modal!=null)Destroy(modal.gameObject);modal=null;popup=null;port?.SetModalCapture(this,false);}
        private void OnDisable(){closeAction.Disable();CloseEquipment();}
        private void OnDestroy(){closeAction.Dispose();CloseEquipment();if(canvas!=null)Destroy(canvas.gameObject);if(font!=null)Destroy(font);if(mono!=null)Destroy(mono);}
    }
}

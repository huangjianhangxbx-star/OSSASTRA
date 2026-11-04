/* 功能：每个受控相机Pass维护两个相互隔离的完整角色alpha缓存，并在逐角色合法绘制时绑定。
 * 边界：不绘制世界颜色、不使用场景遮挡作为形状输入，不修改源材质/Renderer/相机；无CPU纹理读回。
 * 生命周期：每帧重画当前网格，未登记/失活立即失效；固定1024×512单RT与单材质，停用/销毁精确释放。
 */
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
namespace Ossastra.CharacterLighting.Rendering
{
 public sealed class CharacterCoverageActor
 {
  public readonly object Owner;
  public readonly IReadOnlyList<Renderer> Renderers;
  public CharacterCoverageActor(object owner,IReadOnlyList<Renderer> renderers){Owner=owner??throw new ArgumentNullException(nameof(owner));Renderers=renderers??throw new ArgumentNullException(nameof(renderers));}
 }
 public struct CharacterCoverageBinding
 {
  public object Owner;
  public int Slot;
  // 最终target的左上角UV矩形；atlas也按采样UV存储，不使用场景裁剪结果。
  public Vector4 ScreenRect,AtlasRect,ScreenSize;
 }
 public sealed class CharacterCoverageCache:IDisposable
 {
  public const int MaxActors=2,TileSize=512,MaxRenderersPerActor=8,MaxSlotsPerRenderer=16;
  readonly Shader shader;
  readonly List<CharacterCoverageActor> actors=new List<CharacterCoverageActor>(2);
  readonly Dictionary<Renderer,CharacterCoverageBinding> bindings=new Dictionary<Renderer,CharacterCoverageBinding>();
  readonly List<Material> sourceSlots=new List<Material>(8);
  readonly List<Renderer> currentRenderers=new List<Renderer>(8);
  readonly HashSet<Renderer> acceptedRenderers=new HashSet<Renderer>();
  readonly MaterialPropertyBlock rendererBlock=new MaterialPropertyBlock(),slotBlock=new MaterialPropertyBlock();
  Material material;RenderTexture atlas;
  static readonly int mainTexId=Shader.PropertyToID("_MainTex"),mainStId=Shader.PropertyToID("_MainTex_ST"),colorId=Shader.PropertyToID("_Color"),cutoffId=Shader.PropertyToID("_Cutoff");
  public RenderTexture Atlas=>atlas;
  public int ActiveActorCount{get;private set;}
  public int DrawCount{get;private set;}
  public long TextureBytes=>atlas==null?0:(long)atlas.width*atlas.height*4;
  public int OwnedMaterialCount=>material==null?0:1;
  public string Status{get;private set;}="尚未生成完整角色轮廓";
  public CharacterCoverageCache(Shader shader){this.shader=shader;}
  public void Append(CommandBuffer cmd,Camera camera,Matrix4x4 gpuViewProjection,int width,int height)
  {CharacterLightingReceiver.Collect(camera,actors);Prepare(cmd,gpuViewProjection,width,height,actors);}
  public void Prepare(CommandBuffer cmd,Matrix4x4 gpuViewProjection,int width,int height,IReadOnlyList<CharacterCoverageActor> sources)
  {
   if(cmd==null)throw new ArgumentNullException(nameof(cmd));
   bindings.Clear();acceptedRenderers.Clear();ActiveActorCount=DrawCount=0;
   cmd.SetGlobalFloat("_CharacterCoverageReady",0);
   if(sources==null||sources.Count==0){ReleaseResources();Status="无有效角色；已释放缓存";return;}
   if(sources.Count>MaxActors){ReleaseResources();Status="角色轮廓超过2个显式预算；本帧关闭轮廓，不错用其它槽";return;}
   if(shader==null||!shader.isSupported||width<=0||height<=0||!Finite(gpuViewProjection))
   {ReleaseResources();Status="覆盖Shader/目标尺寸/投影无效；仅保留原主体绘制";return;}
   foreach(var actor in sources)if(actor!=null&&actor.Renderers.Count>MaxRenderersPerActor){ReleaseResources();Status="单角色超过8个明确轮廓成员；已关闭本帧轮廓";return;}
   EnsureResources();if(atlas==null||!atlas.IsCreated()){ReleaseResources();Status="完整轮廓RT创建失败；保留原主体";return;}cmd.SetRenderTarget(atlas);cmd.SetViewport(new Rect(0,0,atlas.width,atlas.height));cmd.ClearRenderTarget(false,true,Color.clear);
   for(int slot=0;slot<sources.Count;slot++)
   {
    var actor=sources[slot];if(actor==null)continue;
    currentRenderers.Clear();bool anyBounds=false;Vector2 min=Vector2.one*float.PositiveInfinity,max=Vector2.one*float.NegativeInfinity;
    foreach(var renderer in actor.Renderers)
    {
     if(!CanDraw(renderer))continue;
     if(actor.Owner is CharacterLightingReceiver receiver&&(receiver.Owner==null||!renderer.transform.IsChildOf(receiver.Owner.transform)))continue;
     if(!acceptedRenderers.Add(renderer)){bindings.Clear();ActiveActorCount=0;Status="同一Renderer被多个角色登记；本帧关闭轮廓";return;}
     if(!TryProjectBounds(renderer.bounds,gpuViewProjection,ref min,ref max)){currentRenderers.Clear();anyBounds=false;break;}
     anyBounds=true;currentRenderers.Add(renderer);
    }
    if(!anyBounds||currentRenderers.Count==0)continue;
    if(max.x<0||min.x>1||max.y<0||min.y>1)continue;
    // 保留屏幕外的自身形状；不能把视口边界变成新的人物边缘。四个最终像素留透明边。
    var pad=new Vector2(4f/width,4f/height);min-=pad;max+=pad;
    var size=max-min;if(!Finite(min)||!Finite(size)||size.x<=.000001f||size.y<=.000001f)continue;
    var screenRect=new Vector4(min.x,min.y,size.x,size.y);
    var crop=Matrix4x4.identity;crop.m00=1f/size.x;crop.m03=(1f-min.x-max.x)/size.x;crop.m11=1f/size.y;crop.m13=(min.y+max.y-1f)/size.y;
    cmd.SetGlobalMatrix("_CharacterCoverageVP",crop*gpuViewProjection);
    cmd.SetViewport(new Rect(slot*TileSize,0,TileSize,TileSize));
    var binding=new CharacterCoverageBinding{Owner=actor.Owner,Slot=slot,ScreenRect=screenRect,AtlasRect=new Vector4(slot/(float)MaxActors,0,1f/MaxActors,1),ScreenSize=new Vector4(width,height,1f/width,1f/height)};
    foreach(var renderer in currentRenderers)
    {
     renderer.GetSharedMaterials(sourceSlots);renderer.GetPropertyBlock(rendererBlock);
     if(sourceSlots.Count>MaxSlotsPerRenderer){bindings.Clear();ActiveActorCount=0;Status="单成员超过16材质槽；本帧关闭轮廓";return;}
     int slotCount=renderer is SpriteRenderer?Math.Min(1,sourceSlots.Count):sourceSlots.Count;
     for(int part=0;part<slotCount;part++)
     {
      Material source=sourceSlots[part];if(source==null)continue;
      renderer.GetPropertyBlock(slotBlock,part);var block=slotBlock.isEmpty?rendererBlock:slotBlock;
      Texture texture=source.HasProperty(mainTexId)?source.GetTexture(mainTexId):null;
      Color tint=source.HasProperty(colorId)?source.GetColor(colorId):Color.white;
      Vector2 scale=source.HasProperty(mainTexId)?source.GetTextureScale("_MainTex"):Vector2.one;
      Vector2 offset=source.HasProperty(mainTexId)?source.GetTextureOffset("_MainTex"):Vector2.zero;
      var st=new Vector4(scale.x,scale.y,offset.x,offset.y);
      if(block.HasProperty(mainTexId))texture=block.GetTexture(mainTexId);
      if(block.HasProperty(colorId))tint=block.GetColor(colorId);
      if(block.HasProperty(mainStId))st=block.GetVector(mainStId);
      float cutoff=source.shader!=null&&source.shader.name.IndexOf("DepthPlane",StringComparison.Ordinal)>=0&&source.HasProperty(cutoffId)?source.GetFloat(cutoffId):0;
      if(block.HasProperty(cutoffId))cutoff=block.GetFloat(cutoffId);
      var flip=Vector4.one;
      if(renderer is SpriteRenderer sprite){texture=sprite.sprite==null?null:sprite.sprite.texture;tint.a*=sprite.color.a;flip.x=sprite.flipX?-1:1;flip.y=sprite.flipY?-1:1;st=new Vector4(1,1,0,0);}
      if(texture==null)continue;
      cmd.SetGlobalTexture("_CharacterCoverageSourceTexture",texture);cmd.SetGlobalVector("_CharacterCoverageSourceST",st);
      cmd.SetGlobalFloat("_CharacterCoverageSourceAlpha",Mathf.Clamp01(tint.a));cmd.SetGlobalFloat("_CharacterCoverageSourceCutoff",Mathf.Clamp01(cutoff));cmd.SetGlobalVector("_CharacterCoverageSourceFlip",flip);
      cmd.DrawRenderer(renderer,material,part,0);DrawCount++;
     }
     bindings[renderer]=binding;
    }
    ActiveActorCount++;
   }
   // 调用方恢复实际世界target与viewport；自有VP不会改写任何Unity相机矩阵。
   Status="完整轮廓 "+ActiveActorCount+"/2；覆盖draw "+DrawCount+"；1024×512 RGBA8";
  }
  public bool TryGet(Renderer renderer,out CharacterCoverageBinding binding)
  {binding=default;return CanDraw(renderer)&&bindings.TryGetValue(renderer,out binding);}
  public bool Bind(CommandBuffer cmd,Renderer renderer)
  {
   if(cmd==null)throw new ArgumentNullException(nameof(cmd));
   if(atlas==null||!TryGet(renderer,out var binding)){cmd.SetGlobalFloat("_CharacterCoverageReady",0);return false;}
   cmd.SetGlobalTexture("_CharacterCoverageTexture",atlas);cmd.SetGlobalVector("_CharacterCoverageRect",binding.ScreenRect);cmd.SetGlobalVector("_CharacterCoverageAtlasRect",binding.AtlasRect);cmd.SetGlobalVector("_CharacterCoverageScreenSize",binding.ScreenSize);cmd.SetGlobalVector("_CharacterCoverageTexelSize",new Vector4(1f/atlas.width,1f/atlas.height,atlas.width,atlas.height));cmd.SetGlobalFloat("_CharacterCoverageReady",1);return true;
  }
  static bool CanDraw(Renderer renderer)
  {
   if(renderer==null||!renderer.enabled||renderer.forceRenderingOff||!renderer.gameObject.activeInHierarchy)return false;
   if(renderer is SpriteRenderer sprite)return sprite.sprite!=null&&sprite.drawMode==SpriteDrawMode.Simple&&sprite.maskInteraction==SpriteMaskInteraction.None;
   if(!(renderer is MeshRenderer))return false;
   var filter=renderer.GetComponent<MeshFilter>();return filter!=null&&filter.sharedMesh!=null&&filter.sharedMesh.vertexCount>0;
  }
  static bool TryProjectBounds(Bounds bounds,Matrix4x4 vp,ref Vector2 min,ref Vector2 max)
  {
   var center=bounds.center;var extents=bounds.extents;
   for(int corner=0;corner<8;corner++)
   {
    var world=center+Vector3.Scale(extents,new Vector3((corner&1)==0?-1:1,(corner&2)==0?-1:1,(corner&4)==0?-1:1));
    Vector4 clip=vp*new Vector4(world.x,world.y,world.z,1);if(!Finite(clip)||clip.w<=.00001f)return false;
    var uv=new Vector2(clip.x/clip.w*.5f+.5f,.5f-clip.y/clip.w*.5f);min=Vector2.Min(min,uv);max=Vector2.Max(max,uv);
   }
   return true;
  }
  static bool Finite(Vector2 value)=>Finite(value.x)&&Finite(value.y);
  static bool Finite(Vector4 value)=>Finite(value.x)&&Finite(value.y)&&Finite(value.z)&&Finite(value.w);
  static bool Finite(Matrix4x4 value){for(int i=0;i<16;i++)if(!Finite(value[i]))return false;return true;}
  static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
  void EnsureResources()
  {
   if(material==null)material=new Material(shader){name="Character full silhouette alpha",hideFlags=HideFlags.HideAndDontSave};
   if(atlas!=null)return;
   atlas=new RenderTexture(MaxActors*TileSize,TileSize,0){name="Character full silhouettes [isolated x2]",hideFlags=HideFlags.HideAndDontSave,graphicsFormat=GraphicsFormat.R8G8B8A8_UNorm,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp,useMipMap=false,autoGenerateMips=false,antiAliasing=1};atlas.Create();
  }
  void ReleaseResources()
  {if(atlas!=null){atlas.Release();DestroyOwned(atlas);atlas=null;}DestroyOwned(material);material=null;}
  public void Dispose(){bindings.Clear();actors.Clear();acceptedRenderers.Clear();ActiveActorCount=DrawCount=0;ReleaseResources();Shader.SetGlobalFloat("_CharacterCoverageReady",0);}
  static void DestroyOwned(UnityEngine.Object value){if(value==null)return;if(Application.isPlaying)UnityEngine.Object.Destroy(value);else UnityEngine.Object.DestroyImmediate(value);}
 }
}

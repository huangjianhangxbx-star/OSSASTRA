/* 功能：在唯一合法世界Pass中生成当帧角色受光源，合并最近实体/半透明角色深度，并逐角色追加受限外缘柔光。
 * 边界：不写人物材质、MPB、物理、业务、相机或历史；仅支持已校验Spine/DepthPlane家族，缺数据保守不画。
 * 生命周期：三张全目标私有RT（RGBA32F独立源、R32F最近深度、RGBA16F已裁剪外缘能量）及三材质复用；无历史或CPU读回。
 */
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Experimental.Rendering;
using Ossastra.SceneVisibility.Rooms.Rendering;
namespace Ossastra.CharacterLighting.Rendering
{
 public sealed class CharacterOuterGlowCache:IDisposable
 {
  readonly Shader seedShader,compositeShader,depthShader;
  readonly List<Material> slots=new List<Material>(8);
  readonly List<RoomPositiveSurface> members=new List<RoomPositiveSurface>(16);
  readonly MaterialPropertyBlock rendererBlock=new MaterialPropertyBlock(),slotBlock=new MaterialPropertyBlock();
  Material seedMaterial,compositeMaterial,depthMaterial;
  RTHandle seed,legalDepth,energy;
  public RTHandle Energy=>energy;
  public int DrawCount{get;private set;}
  public int ActiveActorCount{get;private set;}
  public int OwnedTextureCount=>(seed!=null?1:0)+(legalDepth!=null?1:0)+(energy!=null?1:0);
  public int OwnedMaterialCount=>(seedMaterial!=null?1:0)+(compositeMaterial!=null?1:0)+(depthMaterial!=null?1:0);
  public long TextureBytes=>seed==null||seed.rt==null?0:(long)seed.rt.width*seed.rt.height*28;
  public string Status{get;private set;}="外缘等待合法世界";
  public CharacterOuterGlowCache(Shader seedShader,Shader compositeShader,Shader depthShader){this.seedShader=seedShader;this.compositeShader=compositeShader;this.depthShader=depthShader;}
  public void Append(CommandBuffer cmd,Camera camera,RenderTextureDescriptor descriptor,RTHandle color,RTHandle depth,RTHandle permission,CharacterLightingSystem system,CharacterCoverageCache coverage,RoomMaskFrame frame,Action<CommandBuffer,RoomPositiveSurface> uploadOwner)
  {
   try {
   DrawCount=ActiveActorCount=0;
   if(cmd==null)throw new ArgumentNullException(nameof(cmd));
   if(camera==null||system==null||!system.WantsOuterGlow||coverage==null||coverage.ActiveActorCount==0||frame==null||frame.TruthMode||!frame.Enabled||!frame.PositiveRenderingEnabled)
   {Dispose();Status="外缘已关闭/失权；资源已释放";return;}
   if(color==null||depth==null||depth.rt==null||permission==null||permission.rt==null||uploadOwner==null||descriptor.width<1||descriptor.height<1||descriptor.width>4096||descriptor.height>2160||descriptor.dimension!=TextureDimension.Tex2D)
   {Dispose();Status="外缘拒绝缺失附件、非2D或超出4096×2160目标；原主体保留";return;}
   int samples=Mathf.Max(1,depth.rt.antiAliasing);
   if((samples!=1&&samples!=2&&samples!=4&&samples!=8)||descriptor.width!=depth.rt.width||descriptor.height!=depth.rt.height||descriptor.width!=permission.rt.width||descriptor.height!=permission.rt.height||!Supported(seedShader)||!Supported(compositeShader)||!Supported(depthShader))
   {Dispose();Status="外缘缺失显式Shader/附件尺寸或MSAA不支持；原主体保留";return;}
   // 收集当前明确登记的实际身体/装备；任何可见但不支持的前景成员都关闭该帧，避免漏线。
   members.Clear();
   foreach(var surface in frame.PositiveSurfaces)
   {
    var r=surface.Renderer;if(!Drawable(r)||!coverage.TryGet(r,out var binding))continue;
    if(!(binding.Owner is CharacterLightingReceiver receiver)||receiver.Owner==null||!r.transform.IsChildOf(receiver.Owner.transform))continue;
    if(HasRenderer(r))continue;
    if(!(r is MeshRenderer)){Dispose();Status="外缘保守拒绝未认证Sprite/其他前景成员："+r.name;return;}
    r.GetSharedMaterials(slots);
    if(slots.Count>CharacterCoverageCache.MaxSlotsPerRenderer){Dispose();Status="外缘材质槽超过预算";return;}
    r.GetPropertyBlock(rendererBlock);
    for(int part=0;part<slots.Count;++part){var source=slots[part];if(source==null)continue;r.GetPropertyBlock(slotBlock,part);var block=slotBlock.isEmpty?rendererBlock:slotBlock;
     if(!SourceSupported(source)||(block.HasProperty("_StencilComp")&&Mathf.RoundToInt(block.GetFloat("_StencilComp"))!=8)){Dispose();Status="外缘保守拒绝源Shader/Stencil："+source.name;return;}}
    members.Add(surface);
   }
   if(members.Count==0){Dispose();Status="没有获准绘制的外缘成员";return;}
   Ensure(descriptor);
   if(seed==null||legalDepth==null||energy==null||seed.rt==null||legalDepth.rt==null||energy.rt==null){Dispose();Status="外缘RT创建失败";return;}
   var size=new Vector4(descriptor.width,descriptor.height,1f/descriptor.width,1f/descriptor.height);
   cmd.BeginSample("Character outer glow");cmd.SetGlobalVector("_CharacterCoverageScreenSize",size);
   CoreUtils.SetKeyword(depthMaterial,"_CHARACTER_OUTER_MSAA_2",samples==2);CoreUtils.SetKeyword(depthMaterial,"_CHARACTER_OUTER_MSAA_4",samples==4);CoreUtils.SetKeyword(depthMaterial,"_CHARACTER_OUTER_MSAA_8",samples==8);
   cmd.SetGlobalTexture("_CharacterOuterDepthSource",depth.nameID);CoreUtils.SetRenderTarget(cmd,legalDepth);cmd.SetViewport(new Rect(0,0,descriptor.width,descriptor.height));cmd.DrawProcedural(Matrix4x4.identity,depthMaterial,0,MeshTopology.Triangles,3);DrawCount++;
   // 使用Nearest混合补齐ZWriteOff人物的半透明前景。此Pass不采样正在写的RT，无读写反馈。
   foreach(var surface in members){uploadOwner(cmd,surface);DrawMember(cmd,surface.Renderer,1);}
   cmd.SetGlobalTexture("_CharacterOuterLegalDepth",legalDepth.nameID);cmd.SetGlobalTexture("_CharacterOuterPermission",permission.nameID);
   bool diagnostic=system.OuterDiagnostic;
   if(diagnostic){CoreUtils.SetRenderTarget(cmd,color);cmd.ClearRenderTarget(false,true,Color.black);}
   // 每个角色先独立卷积，再合入此层；身份不会在滤波前union。
   CoreUtils.SetRenderTarget(cmd,energy,ClearFlag.Color,Color.clear);
   foreach(var receiver in system.receivers)
   {
    if(receiver==null||!receiver.isActiveAndEnabled||receiver.Owner==null||!receiver.Owner.isActiveAndEnabled||!receiver.Owner.IsAlive||receiver.gameplayCamera!=camera||!receiver.Settings.outerGlow||receiver.Settings.outerStrength<=0||receiver.Settings.maxOuterGain<=0||(diagnostic&&!receiver.Settings.outerDiagnostic))continue;
    Renderer bindRenderer=null;CharacterCoverageBinding actorBinding=default;
    foreach(var surface in members)if(coverage.TryGet(surface.Renderer,out var b)&&ReferenceEquals(b.Owner,receiver)){bindRenderer=surface.Renderer;actorBinding=b;break;}
    if(bindRenderer==null)continue;
    CoreUtils.SetRenderTarget(cmd,seed,ClearFlag.Color,Color.clear);cmd.SetViewport(new Rect(0,0,descriptor.width,descriptor.height));
    foreach(var surface in members)if(coverage.TryGet(surface.Renderer,out var b)&&ReferenceEquals(b.Owner,receiver))
    {uploadOwner(cmd,surface);if(system.BindOuter(cmd,surface.Renderer,coverage,frame))DrawMember(cmd,surface.Renderer,0);}
    if(!system.BindOuter(cmd,bindRenderer,coverage,frame))continue;
    // owner授权统一取该角色实际身体表面，不能继承上一名角色或环境owner。
    foreach(var surface in members)if(surface.Renderer==bindRenderer){uploadOwner(cmd,surface);break;}
    cmd.SetGlobalTexture("_CharacterOuterSeed",seed.nameID);CoreUtils.SetRenderTarget(cmd,energy);
    cmd.SetViewport(new Rect(0,0,descriptor.width,descriptor.height));
    float pad=receiver.Settings.outerWidth*descriptor.height/1080f+2;
    var rect=actorBinding.ScreenRect;float x=Mathf.Max(0,rect.x*descriptor.width-pad),y=Mathf.Max(0,rect.y*descriptor.height-pad);
    float right=Mathf.Min(descriptor.width,(rect.x+rect.z)*descriptor.width+pad),bottom=Mathf.Min(descriptor.height,(rect.y+rect.w)*descriptor.height+pad);
    if(right>x&&bottom>y){cmd.EnableScissorRect(new Rect(x,descriptor.height-bottom,right-x,bottom-y));cmd.DrawProcedural(Matrix4x4.identity,compositeMaterial,0,MeshTopology.Triangles,3);cmd.DisableScissorRect();DrawCount++;ActiveActorCount++;}
   }
   // 世界先追加一次；WorldLook在空间滤波前扣除Energy，滤波后按中心像素归还。
   // 用户Focus/Bloom继续工作，已通过许可与深度路径的柔光不会被它们再次扩散。
   // 环境Look接管时，首次在空间滤波之后加能量，避免低精度CameraColor加后再减产生扩散残差。
   // 无后端时沿本Pass直接追加；Look后端异常则保守少画柔光，不扩大许可。
   bool deferred=!diagnostic&&Ossastra.WorldRendering.WorldLookCameraRegistry.TryGet(camera,out var look)&&look.Enabled;
   if(!deferred)
   {
    cmd.SetGlobalTexture("_CharacterOuterAccumulated",energy.nameID);CoreUtils.SetRenderTarget(cmd,color);
    cmd.SetViewport(new Rect(0,0,descriptor.width,descriptor.height));cmd.DrawProcedural(Matrix4x4.identity,compositeMaterial,1,MeshTopology.Triangles,3);DrawCount++;
   }
   CoreUtils.SetRenderTarget(cmd,color);cmd.SetViewport(new Rect(0,0,descriptor.width,descriptor.height));
   cmd.EndSample("Character outer glow");CharacterLightingSystem.Clear(cmd);
   Status="外缘 "+ActiveActorCount+"/2；draw "+DrawCount+"；"+descriptor.width+"×"+descriptor.height+" / RGBA32F+R32F+RGBA16F / 无历史";
   } finally { if(system!=null)system.ReportOuterGlow(DrawCount,TextureBytes,Status); }
  }
  void DrawMember(CommandBuffer cmd,Renderer renderer,int pass)
  {
   renderer.GetSharedMaterials(slots);renderer.GetPropertyBlock(rendererBlock);
   for(int i=0;i<slots.Count;i++)
   {
    var source=slots[i];if(source==null)continue;renderer.GetPropertyBlock(slotBlock,i);var block=slotBlock.isEmpty?rendererBlock:slotBlock;
    Texture texture=source.GetTexture("_MainTex");Color tint=source.HasProperty("_Color")?source.GetColor("_Color"):Color.white;
    Vector2 scale=source.GetTextureScale("_MainTex"),offset=source.GetTextureOffset("_MainTex");var st=new Vector4(scale.x,scale.y,offset.x,offset.y);
    Vector4 foot=source.HasProperty("_VisualDepthFoot")?source.GetVector("_VisualDepthFoot"):Vector4.zero,normal=source.HasProperty("_VisualDepthNormal")?source.GetVector("_VisualDepthNormal"):new Vector4(0,0,1,0);
    float upright=source.HasProperty("_VisualDepthEnabled")?source.GetFloat("_VisualDepthEnabled"):0;
    float cutoff=source.shader.name.IndexOf("DepthPlane",StringComparison.Ordinal)>=0&&source.HasProperty("_Cutoff")?source.GetFloat("_Cutoff"):0;
    if(block.HasProperty("_MainTex"))texture=block.GetTexture("_MainTex");if(block.HasProperty("_Color"))tint=block.GetColor("_Color");if(block.HasProperty("_MainTex_ST"))st=block.GetVector("_MainTex_ST");
    if(block.HasProperty("_VisualDepthFoot"))foot=block.GetVector("_VisualDepthFoot");if(block.HasProperty("_VisualDepthNormal"))normal=block.GetVector("_VisualDepthNormal");if(block.HasProperty("_VisualDepthEnabled"))upright=block.GetFloat("_VisualDepthEnabled");if(block.HasProperty("_Cutoff"))cutoff=block.GetFloat("_Cutoff");
    if(texture==null)continue;
    cmd.SetGlobalTexture("_CharacterOuterSourceTexture",texture);cmd.SetGlobalVector("_CharacterOuterSourceST",st);cmd.SetGlobalFloat("_CharacterOuterSourceAlpha",tint.a);cmd.SetGlobalFloat("_CharacterOuterSourceCutoff",cutoff);
    cmd.SetGlobalVector("_CharacterOuterSourceFoot",foot);cmd.SetGlobalVector("_CharacterOuterSourceNormal",normal);cmd.SetGlobalFloat("_CharacterOuterSourceUpright",upright);
    cmd.DrawRenderer(renderer,seedMaterial,i,pass);DrawCount++;
   }
  }
  bool HasRenderer(Renderer r){foreach(var surface in members)if(surface.Renderer==r)return true;return false;}
  static bool Drawable(Renderer r){if(r==null||!r.enabled||r.forceRenderingOff||!r.gameObject.activeInHierarchy)return false;if(r is MeshRenderer){var filter=r.GetComponent<MeshFilter>();return filter!=null&&filter.sharedMesh!=null&&filter.sharedMesh.vertexCount>0;}return r is SpriteRenderer;}
  static bool SourceSupported(Material source){if(source==null||source.shader==null||!source.HasProperty("_MainTex"))return false;string n=source.shader.name;return (n=="Spine/Skeleton"||n=="Ossastra/Characters/UprightSpinePma"||n=="Ossastra/MapResearch/DepthPlane"||n=="Ossastra/Characters/UprightDepthPlane")&&(!source.HasProperty("_StencilComp")||Mathf.RoundToInt(source.GetFloat("_StencilComp"))==8);}
  static bool Supported(Shader shader)=>shader!=null&&shader.isSupported;
  void Ensure(RenderTextureDescriptor descriptor)
  {
   if(seedMaterial==null){seedMaterial=CoreUtils.CreateEngineMaterial(seedShader);seedMaterial.name="Character outer current energy";seedMaterial.SetFloat("_OuterDepthBlend",SystemInfo.usesReversedZBuffer?(float)BlendOp.Max:(float)BlendOp.Min);}
   if(compositeMaterial==null){compositeMaterial=CoreUtils.CreateEngineMaterial(compositeShader);compositeMaterial.name="Character outer constrained composite";}
   if(depthMaterial==null){depthMaterial=CoreUtils.CreateEngineMaterial(depthShader);depthMaterial.name="Character outer nearest legal depth";}
   descriptor.depthBufferBits=0;descriptor.depthStencilFormat=GraphicsFormat.None;descriptor.msaaSamples=1;descriptor.bindMS=false;descriptor.enableRandomWrite=false;descriptor.useMipMap=descriptor.autoGenerateMips=false;descriptor.memoryless=RenderTextureMemoryless.None;
   descriptor.graphicsFormat=GraphicsFormat.R32G32B32A32_SFloat;RenderingUtils.ReAllocateIfNeeded(ref seed,descriptor,FilterMode.Point,TextureWrapMode.Clamp,name:"Character outer current seed [reused per actor]");
   descriptor.graphicsFormat=GraphicsFormat.R32_SFloat;RenderingUtils.ReAllocateIfNeeded(ref legalDepth,descriptor,FilterMode.Point,TextureWrapMode.Clamp,name:"Character outer nearest scene and body depth");
   descriptor.graphicsFormat=GraphicsFormat.R16G16B16A16_SFloat;RenderingUtils.ReAllocateIfNeeded(ref energy,descriptor,FilterMode.Point,TextureWrapMode.Clamp,name:"Character outer validated accumulated energy");
  }
  public void Dispose(){seed?.Release();seed=null;legalDepth?.Release();legalDepth=null;energy?.Release();energy=null;CoreUtils.Destroy(seedMaterial);seedMaterial=null;CoreUtils.Destroy(compositeMaterial);compositeMaterial=null;CoreUtils.Destroy(depthMaterial);depthMaterial=null;members.Clear();slots.Clear();DrawCount=ActiveActorCount=0;}
 }
}



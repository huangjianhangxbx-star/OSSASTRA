/* 功能：明确相机的环境渲染接收方；上传受光、照明房间与局部灯，解析角色/场景自然及手动环境目标，绘制合法天空/降雨。
 * 边界：不创建普通Renderer，不推进环境，不读取旧世界深度；只借用天空/反射设置。
 * 生命周期：唯一相机登记；天空、反射、雨与按需预览分别准备/降级/释放，失败不撤销有效环境帧。
 */
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Experimental.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using Ossastra.SceneVisibility.Rooms;
using Ossastra.SceneVisibility.Rooms.Rendering;
namespace Ossastra.EnvironmentSimulation.Rendering
{
 [ExecuteAlways,DisallowMultipleComponent]
 public sealed class EnvironmentSkyBinding:MonoBehaviour
 {
  public EnvironmentRoot environment;
  [HideInInspector,FormerlySerializedAs("panel")]
  public MonoBehaviour previewConsumer; // 仅保存旧作者引用以供迁移；生产不调用开发面板。
  public RoomRegion[] indoorRegions=Array.Empty<RoomRegion>();
  [Tooltip("可选照明专用内部体积；不改可见性房间范围、碰撞或观察权限。未登记者保留原房间体积。")]
  public EnvironmentIndoorVolumeOverride[] indoorVolumeOverrides=Array.Empty<EnvironmentIndoorVolumeOverride>();
  public Light[] localLights=Array.Empty<Light>();
  public BoxCollider[] rainShelters=Array.Empty<BoxCollider>();
  public Shader skyShader,rainShader,reflectionSkyShader,rainDepthCopyShader;
  [Range(0,2)] public float reflectionIntensity=1;
  [Min(1)] public float reflectionUpdateSeconds=1;
  [Range(.0001f,.05f)] public float reflectionChangeThreshold=.002f;
  [Header("运行态天空覆盖；不修改昼夜源档")]
  public bool overrideSkyColors;
  public Color zenithColor=new Color(.13f,.36f,.65f),horizonColor=new Color(.67f,.78f,.82f);
  [Header("自然/手动环境色与登记角色同步；不改变直射/局部灯")]
  public bool syncSkyColorTo2D=true;
  [Range(0,1)] public float stylizedSkyTintStrength=.5f;
  [Header("场景环境融合候选；仅室外环境漫射，不覆盖人物/直射/局部灯")]
  public SceneEnvironmentProfileSO sceneEnvironmentProfile;
  public bool sceneEnvironmentEnabled;
  [Range(0,1)] public float rainOpacity=.24f;
  [Range(32,1024)] public int rainParticleCount=384;
  public string Status{get;private set;}="等待环境快照";
  public Texture SkyPreview=>preview;
  public bool SkyReady=>skyMaterial!=null;
  public bool ReflectionReady=>cubeMaterial!=null&&reflection!=null;
  public bool RainReady=>rainMaterial!=null&&depthCopyMaterial!=null;
  public bool PreviewReady=>preview!=null&&preview.IsCreated();
  public bool SurfaceReady{get;private set;}
  public string SurfaceStatus{get;private set;}="未准备环境表面数据";
  public string SkyStatus{get;private set;}="未准备";
  public string ReflectionStatus{get;private set;}="未准备";
  public string RainStatus{get;private set;}="未准备";
  public string PreviewStatus{get;private set;}="无预览消费者";
  static readonly Dictionary<int,EnvironmentSkyBinding> bindings=new Dictionary<int,EnvironmentSkyBinding>();
  readonly EnvironmentSurfaceWorkingSet surfaceData=new EnvironmentSurfaceWorkingSet();
  readonly HashSet<object> previewRequests=new HashSet<object>();readonly List<object> stalePreviewRequests=new List<object>();
  Material skyMaterial,rainMaterial,cubeMaterial,depthCopyMaterial,oldSky;
  RTHandle rainDepth;
  Cubemap reflection;
  Texture oldReflection;
  RenderTexture preview;
  DefaultReflectionMode oldMode;float oldReflectionIntensity;
  bool leased;int leaseScene;Camera registeredCamera;double nextReflection;Color lastReflectionTop,lastReflectionEdge;
  readonly Color[] cubePixels=new Color[32*32];
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
  static void ResetStatics(){bindings.Clear();Shader.SetGlobalFloat("_EnvironmentLightingEnabled",0);}
  void OnEnable(){RenderPipelineManager.beginCameraRendering+=BeginCamera;RenderPipelineManager.endCameraRendering+=EndCamera;EnvironmentCameraRegistry.Released+=EnvironmentReleased;Update();}
  void OnDisable(){RenderPipelineManager.beginCameraRendering-=BeginCamera;RenderPipelineManager.endCameraRendering-=EndCamera;EnvironmentCameraRegistry.Released-=EnvironmentReleased;Release();}
  void EnvironmentReleased(int cameraId){if(!ReferenceEquals(registeredCamera,null)&&registeredCamera.GetInstanceID()==cameraId)Release();}
  void OnDestroy(){Release();}
  void BeginCamera(ScriptableRenderContext context,Camera camera)
  {
   // 其它有效接收者服务此相机时不能清掉它刚上传的上下文；订阅先后不影响owner。
   if(camera!=null&&bindings.TryGetValue(camera.GetInstanceID(),out var owner)&&owner!=null&&owner.isActiveAndEnabled&&owner!=this)return;
   var cmd=CommandBufferPool.Get("Environment ordinary surface context");
   try{UploadOrdinaryForCamera(cmd,camera);context.ExecuteCommandBuffer(cmd);}
   finally{CommandBufferPool.Release(cmd);}
  }
  void EndCamera(ScriptableRenderContext context,Camera camera)
  {
   if(camera==null||camera!=registeredCamera||!bindings.TryGetValue(camera.GetInstanceID(),out var owner)||owner!=this)return;
   var cmd=CommandBufferPool.Get("Environment end owned camera context");
   try{ClearSurface(cmd);context.ExecuteCommandBuffer(cmd);}
   finally{CommandBufferPool.Release(cmd);}
  }
  void Update()
  {
   if(environment==null||environment.gameplayCamera==null||!environment.isActiveAndEnabled||SceneManager.GetActiveScene()!=gameObject.scene||!EnvironmentCameraRegistry.TryGet(environment.gameplayCamera,out var frame)||!frame.Enabled){Release();return;}
   var camera=environment.gameplayCamera;
   if(registeredCamera!=camera)Release();
   if(bindings.TryGetValue(camera.GetInstanceID(),out var other)&&other!=this){Status="同相机已有天空接收者";return;}
   registeredCamera=camera;bindings[camera.GetInstanceID()]=this;
   EnsureResources();TryPrepareSurfaceData(camera,out var surfaceReason);
   if(ReflectionReady)
   {
    if(!leased){oldSky=RenderSettings.skybox;oldReflection=RenderSettings.customReflectionTexture;oldMode=RenderSettings.defaultReflectionMode;oldReflectionIntensity=RenderSettings.reflectionIntensity;leaseScene=gameObject.scene.handle;leased=true;nextReflection=0;}
    // 日照对照排除天空反射，回到完整/底光模式时归还用户配置值，不改作者参数。
    RenderSettings.reflectionIntensity=frame.Contribution==EnvironmentContribution.SunOnly?0:Mathf.Clamp(reflectionIntensity,0,2);
    Color reflectionTop=SkyColor(frame,Vector3.up),reflectionEdge=SkyColor(frame,Vector3.forward);
    if(Time.realtimeSinceStartupAsDouble>=nextReflection&&(nextReflection==0||ColorDistance(reflectionTop,lastReflectionTop)>reflectionChangeThreshold||ColorDistance(reflectionEdge,lastReflectionEdge)>reflectionChangeThreshold))
    {UpdateReflection(frame);lastReflectionTop=reflectionTop;lastReflectionEdge=reflectionEdge;nextReflection=Time.realtimeSinceStartupAsDouble+Mathf.Max(1,reflectionUpdateSeconds);}
   }
   if(SkyReady)ConfigureSky(skyMaterial,frame);
   if(PreviewReady&&SkyReady)Graphics.Blit(null,preview,skyMaterial,1);
   Status=surfaceReason+"；天空 "+SkyStatus+"；反射 "+ReflectionStatus+"；雨 "+RainStatus+"；预览 "+PreviewStatus;
  }
  bool EnsureResources()
  {
   EnsureMaterial(ref skyMaterial,skyShader,"Environment legal sky");SkyStatus=SkyReady?"就绪":"缺少/不支持天空Shader，仅天空暂停";
   if(cubeMaterial!=null&&(cubeMaterial.shader!=reflectionSkyShader||!Supported(reflectionSkyShader)))ReleaseReflection();
   EnsureMaterial(ref cubeMaterial,reflectionSkyShader,"Environment reflection sky");
   if(cubeMaterial!=null&&reflection==null){reflection=new Cubemap(32,TextureFormat.RGBAHalf,true){name="Environment sky only reflection",hideFlags=HideFlags.HideAndDontSave};nextReflection=0;}
   ReflectionStatus=ReflectionReady?"就绪":"缺少/不支持反射Shader，仅反射暂停";
   EnsureMaterial(ref rainMaterial,rainShader,"Environment legal rain");EnsureMaterial(ref depthCopyMaterial,rainDepthCopyShader,"Environment current legal rain depth resolve");
   if(!RainReady){rainDepth?.Release();rainDepth=null;}
   RainStatus=RainReady?"就绪":"缺少/不支持雨或雨深度Shader，仅雨暂停";
   stalePreviewRequests.Clear();foreach(var owner in previewRequests)if(owner is UnityEngine.Object value&&value==null)stalePreviewRequests.Add(owner);foreach(var owner in stalePreviewRequests)previewRequests.Remove(owner);
   if(previewRequests.Count>0&&SkyReady)
   {if(preview==null){preview=new RenderTexture(384,192,0,RenderTextureFormat.ARGBHalf){name="Environment sky preview",hideFlags=HideFlags.HideAndDontSave};preview.Create();}PreviewStatus=PreviewReady?"已请求/就绪":"申请失败，仅预览暂停";}
   else {ReleasePreview();PreviewStatus=previewRequests.Count==0?"无消费者/未占用":"天空不可用/预览暂停";}
   // 可选材质全部失效仍保留环境接收登记；基础表面是否可用由独立工作集校验决定。
   return SkyReady||ReflectionReady||RainReady;
  }
  static bool Supported(Shader shader)=>shader!=null&&shader.isSupported;
  static void EnsureMaterial(ref Material material,Shader shader,string name)
  {if(material!=null&&(material.shader!=shader||!Supported(shader))){DestroyOwned(material);material=null;}if(material==null&&Supported(shader))material=new Material(shader){name=name,hideFlags=HideFlags.HideAndDontSave};}
  public void SetPreviewRequested(object owner,bool requested)
  {if(owner==null)return;if(requested)previewRequests.Add(owner);else previewRequests.Remove(owner);if(previewRequests.Count==0)ReleasePreview();}
  void ReleasePreview(){if(preview==null)return;preview.Release();DestroyOwned(preview);preview=null;}
  public bool TryPrepareSurfaceData(Camera camera,out string reason)
  {if(environment==null||camera==null||environment.gameplayCamera!=camera){surfaceData.Clear();SurfaceReady=false;reason=SurfaceStatus="环境表面相机/来源不匹配";return false;}SurfaceReady=surfaceData.TryPrepare(camera,indoorRegions,localLights,rainShelters,indoorVolumeOverrides,out reason);SurfaceStatus=reason;return SurfaceReady;}
  Color SkyColor(EnvironmentFrame frame,Vector3 direction)
  {
   Color top=overrideSkyColors?zenithColor:frame.SkyZenith,edge=overrideSkyColors?horizonColor:frame.SkyHorizon;
   var color=Color.Lerp(edge.linear,top.linear,Mathf.Pow(Mathf.Clamp01(direction.y),.5f))*frame.SkyIntensity;
   return Color.Lerp(color,frame.FogColor.linear*frame.SkyIntensity,frame.CloudCoverage*.18f);
  }
  static float ColorDistance(Color a,Color b)=>Mathf.Max(Mathf.Abs(a.r-b.r),Mathf.Abs(a.g-b.g),Mathf.Abs(a.b-b.b));
  // 与现有帧共享自然/手动色源；日落色取昼夜作者档，避免SunColor切月色时发生跳色。
  // Profile始终只读。资格按实际片元房间计算，主控入室不会把窗外整体切为室内。
  public Vector4 ResolveSceneEnvironmentTint(EnvironmentFrame frame)
  {
   if(!sceneEnvironmentEnabled||sceneEnvironmentProfile==null||sceneEnvironmentProfile.settings==null||frame==null||!frame.Enabled||frame.Contribution==EnvironmentContribution.Original||frame.Contribution==EnvironmentContribution.SunOnly)return new Vector4(1,1,1,0);
   if(!overrideSkyColors&&(environment==null||environment.dayCycle==null))return new Vector4(1,1,1,0);
   return SceneEnvironmentColorResolver.ResolveTint(frame.AmbientSky,frame.AmbientEquator,
    environment!=null&&environment.dayCycle!=null?environment.dayCycle.sunsetSunColor:Color.black,
    frame.Sun.ElevationDegrees,overrideSkyColors,zenithColor,horizonColor,sceneEnvironmentProfile.settings);
  }
  // One camera's explicit Ground/Grass opt-in. No per-frame profile clone is needed.
  public float GroundBaseRecolorStrength
  {
   get{if(!sceneEnvironmentEnabled||sceneEnvironmentProfile==null||sceneEnvironmentProfile.settings==null)return 0;float v=sceneEnvironmentProfile.settings.groundBaseRecolorStrength;return float.IsNaN(v)||float.IsInfinity(v)?0:Mathf.Clamp(v,0,.5f);}
  }
  public Vector4 SceneEnvironmentControls
  {
   get
   {
    if(!sceneEnvironmentEnabled||sceneEnvironmentProfile==null||sceneEnvironmentProfile.settings==null)return Vector4.zero;
    var value=sceneEnvironmentProfile.settings.Sanitize();
    return new Vector4(value.groundStrength,value.decorationStrength,value.voxelStrength,1);
   }
  }
  // 作者颜色仅在此解码一次到Linear；黑色/非法色关闭艺术联动，不冒充物理天空积分。
  public Vector4 ResolveStylizedSkyTint(EnvironmentFrame frame)
  {
   var neutral=new Vector4(1,1,1,0);
   if(!syncSkyColorTo2D||!overrideSkyColors||frame==null||!frame.Enabled||frame.Contribution==EnvironmentContribution.SunOnly||float.IsNaN(stylizedSkyTintStrength)||float.IsInfinity(stylizedSkyTintStrength))return neutral;
   var c=Color.Lerp(zenithColor,horizonColor,.65f);
   if(float.IsNaN(c.r)||float.IsNaN(c.g)||float.IsNaN(c.b)||float.IsInfinity(c.r)||float.IsInfinity(c.g)||float.IsInfinity(c.b))return neutral;
   c=new Color(Mathf.Clamp01(c.r),Mathf.Clamp01(c.g),Mathf.Clamp01(c.b),1).linear;
   float peak=Mathf.Max(c.r,Mathf.Max(c.g,c.b));if(peak<=.00001f)return neutral;
   return new Vector4(Mathf.Max(.25f,c.r/peak),Mathf.Max(.25f,c.g/peak),Mathf.Max(.25f,c.b/peak),Mathf.Clamp01(stylizedSkyTintStrength));
  }
  // B版只发布环境基础贡献的目标色度，不乘Frame强度或原底光，不改变实际天空、太阳与反射。
  // A版仍走上面的原入口；室内外资格由既有逐片元房间判断负责，不能由主控位置统一切色。
  public Vector4 ResolveCharacterEnvironmentTint(EnvironmentFrame frame)
  {
   var neutral=new Vector4(1,1,1,0);
   if(!syncSkyColorTo2D||frame==null||!frame.Enabled||frame.Contribution==EnvironmentContribution.Original||frame.Contribution==EnvironmentContribution.SunOnly||!FiniteTint(stylizedSkyTintStrength)||stylizedSkyTintStrength<=0)return neutral;
   Color first,second,target;
   if(overrideSkyColors)
   {
    if(!TryDecodeTint(zenithColor,out first)||!TryDecodeTint(horizonColor,out second))return neutral;
    target=Color.Lerp(first,second,.65f);
   }
   else
   {
    if(!TryDecodeTint(frame.AmbientSky,out first)||!TryDecodeTint(frame.AmbientEquator,out second))return neutral;
    target=Color.Lerp(first,second,.65f);
    // 现有自然底光只有昼夜插值。低太阳时借用Frame自己的暖日照色作为风格化目标，非真实天空积分。
    // SunColor在地平线下切为月色，因此暖色权重在0度归零，不能把该切换放大成一次跳色。
    float elevation=frame.Sun.ElevationDegrees;
    float warm=Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,6,elevation))*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(12,30,elevation)));
    if(warm>0)
    {
     Color sun;if(!TryDecodeTint(frame.SunColor,out sun))return neutral;
     target=Color.Lerp(target,sun,warm);
    }
   }
   float peak=Mathf.Max(target.r,Mathf.Max(target.g,target.b));if(peak<=.00001f)return neutral;
   // 不沿用A的每通道.25下限；纯色的亮度归一化预算由角色颜色函数另行约束。
   return new Vector4(target.r/peak,target.g/peak,target.b/peak,Mathf.Clamp01(stylizedSkyTintStrength));
  }
  public string CharacterEnvironmentTintSource(EnvironmentFrame frame)
  {
   if(!syncSkyColorTo2D)return "中性回退：环境同步关闭";
   if(frame==null||!frame.Enabled||frame.Contribution==EnvironmentContribution.Original)return "中性回退：环境快照无效或停用";
   if(frame.Contribution==EnvironmentContribution.SunOnly)return "中性回退：仅日照对照";
   if(ResolveCharacterEnvironmentTint(frame).w<=0)return "中性回退：颜色或强度无效/为零";
   return overrideSkyColors?"手动天空覆盖":"自然昼夜/天气（低太阳风格化暖色）";
  }
  static bool FiniteTint(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
  // 作者颜色只在这里解码一次。与Frame作者色范围一致的上界避免非法HDR输入造成溢出。
  static bool TryDecodeTint(Color authored,out Color linear)
  {
   linear=Color.black;
   if(!FiniteTint(authored.r)||!FiniteTint(authored.g)||!FiniteTint(authored.b))return false;
   linear=new Color(Mathf.Clamp(authored.r,0,16),Mathf.Clamp(authored.g,0,16),Mathf.Clamp(authored.b,0,16),1).linear;
   return FiniteTint(linear.r)&&FiniteTint(linear.g)&&FiniteTint(linear.b);
  }
  void UpdateReflection(EnvironmentFrame frame)
  {
   for(int face=0;face<6;face++)
   {
    for(int y=0;y<32;y++)for(int x=0;x<32;x++)
    {
     float u=(x+.5f)/16-1,v=(y+.5f)/16-1;
     Vector3 d=face==0?new Vector3(1,-v,-u):face==1?new Vector3(-1,-v,u):face==2?new Vector3(u,1,v):face==3?new Vector3(u,-1,-v):face==4?new Vector3(u,-v,1):new Vector3(-u,-v,-1);
     cubePixels[y*32+x]=SkyColor(frame,d.normalized);
    }
    reflection.SetPixels(cubePixels,(CubemapFace)face);
   }
   reflection.Apply(true,false);cubeMaterial.SetTexture("_Tex",reflection);cubeMaterial.SetFloat("_Exposure",1);
   RenderSettings.skybox=cubeMaterial;RenderSettings.defaultReflectionMode=DefaultReflectionMode.Custom;RenderSettings.customReflectionTexture=reflection;
  }
  void ConfigureSky(Material material,EnvironmentFrame frame)
  {
   material.SetColor("_EnvironmentSkyZenith",(overrideSkyColors?zenithColor:frame.SkyZenith).linear*frame.SkyIntensity);
   material.SetColor("_EnvironmentSkyHorizon",(overrideSkyColors?horizonColor:frame.SkyHorizon).linear*frame.SkyIntensity);
   material.SetVector("_EnvironmentSkySun",new Vector4(frame.SunDirection.x,frame.SunDirection.y,frame.SunDirection.z,frame.NightFactor));
   material.SetColor("_EnvironmentSkySunColor",frame.SunColor.linear*Mathf.Min(frame.SunIntensity,2));
   material.SetVector("_EnvironmentCloudParameters",new Vector4(frame.CloudCoverage,frame.CloudDensity,frame.CloudShadowStrength,frame.CloudWorldScale));
   material.SetVector("_EnvironmentCloudMotion",new Vector4(frame.WindDirection.x*frame.WindSpeed*frame.CloudSpeed,frame.WindDirection.y*frame.WindSpeed*frame.CloudSpeed,(float)frame.Seconds,1));
  }
  static bool TryGet(Camera camera,out EnvironmentSkyBinding binding,out EnvironmentFrame frame)
  {
   binding=null;frame=null;return camera!=null&&bindings.TryGetValue(camera.GetInstanceID(),out binding)&&binding!=null&&binding.isActiveAndEnabled&&EnvironmentCameraRegistry.TryGet(camera,out frame)&&frame.Enabled;
  }
  // RoomPass只获得环境阶段的能力结果；false不撤销世界颜色、深度或观察许可。
  public static bool PrepareSurfaceForCamera(Camera camera,out string reason)
  {if(!TryGet(camera,out var binding,out var frame)){reason="该相机无有效环境接收登记";return false;}return binding.TryPrepareSurfaceData(camera,out reason);}
  // 仅构造专用环境缓冲，不提交GPU；普通源使用场景物role，许可绘制仍由原MPB/逐draw覆盖。
  // 构造失败清掉该专用缓冲的半成命令后只关闭环境；提交错误由外层保留原故障边界。
  public static void UploadOrdinaryForCamera(CommandBuffer cmd,Camera camera)
  {
   if(cmd==null)throw new ArgumentNullException(nameof(cmd));
   EnvironmentSkyBinding binding=null;
   try
   {
    if(!TryGet(camera,out binding,out var frame)){ClearSurface(cmd);return;}
    cmd.SetGlobalFloat("_EnvironmentRainDepthReady",0);cmd.SetGlobalFloat("_EnvironmentSurfaceRole",1);
    binding.Upload(cmd,frame);
   }
   catch(Exception error)
   {
    cmd.Clear();ClearSurface(cmd);
    if(binding!=null){binding.SurfaceReady=false;binding.SurfaceStatus="普通环境命令构造失败，仅暂停环境："+error.GetType().Name+": "+error.Message;}
   }
  }
  public static void UploadForCamera(CommandBuffer cmd,Camera camera,RoomMaskFrame visibility)
  {
   cmd.SetGlobalFloat("_EnvironmentRainDepthReady",0);
   if(!TryGet(camera,out var binding,out var frame)){ClearSurface(cmd);return;}
   binding.Upload(cmd,frame);
  }
  void Upload(CommandBuffer cmd,EnvironmentFrame frame)
  {
   if(!TryPrepareSurfaceData(environment==null?null:environment.gameplayCamera,out var reason)){ClearSurface(cmd);return;}
   cmd.SetGlobalVector("_EnvironmentStylizedSkyTint",ResolveStylizedSkyTint(frame));
   cmd.SetGlobalVector("_EnvironmentSceneTint",ResolveSceneEnvironmentTint(frame));
   cmd.SetGlobalVector("_EnvironmentSceneChroma",SceneEnvironmentControls);
   cmd.SetGlobalFloat("_EnvironmentGroundBaseStrength",GroundBaseRecolorStrength);
   surfaceData.Upload(cmd);
   cmd.SetGlobalFloat("_EnvironmentLightingEnabled",1);
   cmd.SetGlobalVector("_EnvironmentSkyAmbient",(Vector4)(frame.AmbientSky.linear*frame.AmbientIntensity));cmd.SetGlobalVector("_EnvironmentHorizonAmbient",(Vector4)(frame.AmbientEquator.linear*frame.AmbientIntensity));cmd.SetGlobalVector("_EnvironmentGroundAmbient",(Vector4)(frame.AmbientGround.linear*frame.AmbientIntensity));
   cmd.SetGlobalVector("_EnvironmentLightResponse",new Vector4(frame.GroundLightResponse,frame.DecorationLightResponse,frame.CharacterLightResponse,frame.CharacterMinimum));cmd.SetGlobalFloat("_EnvironmentIndoorAmbientScale",frame.IndoorAmbientScale);
   cmd.SetGlobalVector("_EnvironmentCloudParameters",new Vector4(frame.CloudCoverage,frame.CloudDensity,frame.CloudShadowStrength,frame.CloudWorldScale));cmd.SetGlobalVector("_EnvironmentCloudMotion",new Vector4(frame.WindDirection.x*frame.WindSpeed*frame.CloudSpeed,frame.WindDirection.y*frame.WindSpeed*frame.CloudSpeed,(float)frame.Seconds,1));cmd.SetGlobalFloat("_EnvironmentWetness",frame.Wetness);
  }
  static void ClearSurface(CommandBuffer cmd)
  {cmd.SetGlobalFloat("_EnvironmentLightingEnabled",0);cmd.SetGlobalVector("_EnvironmentSceneChroma",Vector4.zero);cmd.SetGlobalFloat("_EnvironmentGroundBaseStrength",0);cmd.SetGlobalInteger("_EnvironmentIndoorCount",0);cmd.SetGlobalInteger("_EnvironmentLocalLightCount",0);cmd.SetGlobalInteger("_EnvironmentShelterCount",0);cmd.SetGlobalFloat("_EnvironmentRainDepthReady",0);}
  public static void ClearSurfaceForCamera(CommandBuffer cmd)=>ClearSurface(cmd);
  // Side-view draws only for an explicit exterior context. This borrows the existing author material.
  internal static bool TryPrepareSideViewSky(Camera camera,out Material material,out int pass)
  {
   material=null;pass=-1;
   if(!TryGet(camera,out var binding,out var frame))return false;
   EnsureMaterial(ref binding.skyMaterial,binding.skyShader,"Environment legal sky");
   if(binding.skyMaterial==null)return false;
   pass=binding.skyMaterial.FindPass("SideViewSky");
   if(pass<0)return false;
   binding.ConfigureSky(binding.skyMaterial,frame);material=binding.skyMaterial;return true;
  }
  internal static bool TryPrepareSideViewRain(Camera camera,out Material material,out int pass,out int count)
  {
   material=null;pass=-1;count=0;
   if(!TryGet(camera,out var binding,out var frame)||!binding.SurfaceReady||frame.Rain<=.001f)return false;
   EnsureMaterial(ref binding.rainMaterial,binding.rainShader,"Environment legal rain");
   if(binding.rainMaterial==null)return false;
   pass=binding.rainMaterial.FindPass("SideViewRain");
   if(pass<0)return false;
   material=binding.rainMaterial;count=binding.ConfigureRain(frame);return true;
  }
  int ConfigureRain(EnvironmentFrame frame)
  {
   int count=Mathf.Clamp(rainParticleCount,32,1024);
   rainMaterial.SetVector("_EnvironmentRain",new Vector4(frame.Rain,rainOpacity,(float)frame.Seconds,count));
   rainMaterial.SetVector("_EnvironmentRainWind",new Vector4(frame.WindDirection.x*frame.WindSpeed,frame.WindDirection.y*frame.WindSpeed,0,0));
   float rainLight=Mathf.Clamp(frame.SunIntensity*.6f+frame.AmbientSky.grayscale*frame.AmbientIntensity,.13f,1);
   rainMaterial.SetColor("_EnvironmentRainColor",new Color(.67f,.77f,.84f)*rainLight);
   return count;
  }
  public static void AppendSkyForCamera(CommandBuffer cmd,Camera camera,RoomMaskFrame visibility)
  {
   if(!TryGet(camera,out var b,out var frame)||b.skyMaterial==null||visibility.TruthMode)return;
   b.ConfigureSky(b.skyMaterial,frame);cmd.DrawProcedural(Matrix4x4.identity,b.skyMaterial,0,MeshTopology.Triangles,3);
  }
  public static void AppendWeatherForCamera(CommandBuffer cmd,Camera camera,RoomMaskFrame visibility,ref RenderingData data)
  {
   if(!TryGet(camera,out var b,out var frame))return;
   if(!b.RainReady)return;
   if(!b.SurfaceReady){b.RainStatus="环境空间工作集无效；仅雨暂停";return;}
   if(frame.Rain<=.001f||visibility.TruthMode)return;
   var renderer=data.cameraData.renderer;var sourceDepth=renderer.cameraDepthTargetHandle;
   if(sourceDepth==null||sourceDepth.rt==null||!sourceDepth.rt.IsCreated()||b.depthCopyMaterial==null){b.RainStatus="当前相机合法深度未就绪；仅雨暂停";return;}
   int samples=sourceDepth.rt.bindTextureMS?Mathf.Max(1,sourceDepth.rt.antiAliasing):1;if(samples!=1&&samples!=2&&samples!=4&&samples!=8){b.RainStatus="当前深度MSAA不支持；仅雨暂停";return;}
   var descriptor=sourceDepth.rt.descriptor;descriptor.depthBufferBits=0;descriptor.depthStencilFormat=GraphicsFormat.None;descriptor.graphicsFormat=GraphicsFormat.R32_SFloat;
   descriptor.msaaSamples=1;descriptor.bindMS=false;descriptor.useMipMap=descriptor.autoGenerateMips=descriptor.enableRandomWrite=false;descriptor.memoryless=RenderTextureMemoryless.None;
   RenderingUtils.ReAllocateIfNeeded(ref b.rainDepth,descriptor,FilterMode.Point,TextureWrapMode.Clamp,name:"Environment current legal rain depth");
   CoreUtils.SetKeyword(b.depthCopyMaterial,"_WORLD_LOOK_DEPTH_MSAA_2",samples==2);CoreUtils.SetKeyword(b.depthCopyMaterial,"_WORLD_LOOK_DEPTH_MSAA_4",samples==4);CoreUtils.SetKeyword(b.depthCopyMaterial,"_WORLD_LOOK_DEPTH_MSAA_8",samples==8);
   cmd.SetGlobalTexture("_WorldLookDepthSource",sourceDepth.nameID);cmd.SetGlobalVector("_WorldLookDepthSourceSize",new Vector4(sourceDepth.rt.width,sourceDepth.rt.height,1f/sourceDepth.rt.width,1f/sourceDepth.rt.height));
   CoreUtils.SetRenderTarget(cmd,b.rainDepth);
   bool flip=data.cameraData.IsHandleYFlipped(sourceDepth)!=data.cameraData.IsHandleYFlipped(b.rainDepth);
   Blitter.BlitTexture(cmd,sourceDepth,flip?new Vector4(1,-1,0,1):new Vector4(1,1,0,0),b.depthCopyMaterial,0);
   CoreUtils.SetRenderTarget(cmd,renderer.cameraColorTargetHandle,sourceDepth);
   cmd.SetGlobalTexture("_EnvironmentRainLegalDepth",b.rainDepth.nameID);cmd.SetGlobalFloat("_EnvironmentRainDepthReady",1);
   int count=b.ConfigureRain(frame);
   cmd.SetGlobalInteger("_RoomSightOwnerCount",0);cmd.SetGlobalInteger("_RoomSightOwnerExterior",0);cmd.SetGlobalInteger("_RoomSightPassMode",1);
   cmd.DrawProcedural(Matrix4x4.identity,b.rainMaterial,0,MeshTopology.Triangles,6,count);
  }
  void ReleaseReflection()
  {
   if(leased){if(SceneManager.GetActiveScene().handle==leaseScene){if(RenderSettings.skybox==cubeMaterial)RenderSettings.skybox=oldSky;if(RenderSettings.customReflectionTexture==reflection){RenderSettings.customReflectionTexture=oldReflection;RenderSettings.defaultReflectionMode=oldMode;RenderSettings.reflectionIntensity=oldReflectionIntensity;}}leased=false;}
   DestroyOwned(cubeMaterial);cubeMaterial=null;DestroyOwned(reflection);reflection=null;nextReflection=0;
  }
  void Release()
  {
   if(!ReferenceEquals(registeredCamera,null)){int id=registeredCamera.GetInstanceID();if(bindings.TryGetValue(id,out var owner)&&owner==this)bindings.Remove(id);registeredCamera=null;}
   ReleaseReflection();DestroyOwned(skyMaterial);skyMaterial=null;DestroyOwned(rainMaterial);rainMaterial=null;DestroyOwned(depthCopyMaterial);depthCopyMaterial=null;
   rainDepth?.Release();rainDepth=null;
   ReleasePreview();surfaceData.Clear();SurfaceReady=false;SurfaceStatus="接收绑定已释放";Shader.SetGlobalFloat("_EnvironmentLightingEnabled",0);Shader.SetGlobalFloat("_EnvironmentRainDepthReady",0);
  }
  static void DestroyOwned(UnityEngine.Object value){if(value==null)return;if(Application.isPlaying)Destroy(value);else DestroyImmediate(value);}
 }
}

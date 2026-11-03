// 功能：正向绘制的早期片元许可；人物观察点决定逻辑视域，相机只负责投影。
// 边界：有限8开口、每开口10平面、8目标盒，32建筑盒、64不透明OBB；调用侧须拒绝超限。
// 生命周期：每相机绘制显式上传，不读取旧发现状态或旧黑幕纹理。
#ifndef OSSASTRA_ROOM_SIGHT_CLIP_INCLUDED
#define OSSASTRA_ROOM_SIGHT_CLIP_INCLUDED
int _RoomSightSeedCount, _RoomSightBuildingCount, _RoomSightDomainCount, _RoomSightBlockerCount, _RoomSightExteriorView;
float3 _RoomSightObserver;
float4x4 _RoomSightSeedMatrices[8], _RoomSightBuildingMatrices[32], _RoomSightBlockerMatrices[64], _RoomSightTargetMatrices[64];
float4 _RoomSightSeedExtents[8], _RoomSightBuildingExtents[32], _RoomSightBlockerExtents[64], _RoomSightTargetExtents[64];
float4 _RoomSightPlanes[80], _RoomSightMeta[8], _RoomSightExterior[8];
int _RoomSightOwnerCount, _RoomSightOwnerExterior;
float4x4 _RoomSightOwnerMatrices[8];float4 _RoomSightOwnerExtents[8];
sampler2D _RoomSightCoverageTexture;
float _RoomPositiveCoverageMode;
float _RoomPositivePermissionMode;
// 标签许可RGB保持原合同；Alpha仅标记环境颜色可接受后处理。
float _RoomWorldLookEnvironment;
float4 _RoomSightTargetSize;
float4x4 _RoomSightInverseViewProjection;
int _RoomSightWindowCount, _RoomSightCameraBlockerCount, _RoomSightPassMode, _RoomFogHistoryCount;
float3 _RoomSightCameraObserver;
float4x4 _RoomSightCameraBlockerMatrices[64]; float4 _RoomSightCameraBlockerExtents[64];
float4 _RoomSightWindowPlanes[80], _RoomSightWindowMeta[8];
float _RoomSightStaticEnvironment;
float4 _RoomFogTiming, _RoomFogStyle, _RoomFogDither, _RoomFogColor;
float _RoomFogNow;
StructuredBuffer<float4> _RoomFogHistory;
bool RoomSightBuildingContains(float3 worldPosition);
bool RoomSightSeedContains(float3 worldPosition);
float3 RoomSightWorldFromDeviceDepth(float2 rasterPixel, float rawDepth)
{
    float depth = rawDepth;
    #if !UNITY_REVERSED_Z
        depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, rawDepth);
    #endif
    float4 clipPosition = float4(rasterPixel / _RoomSightTargetSize.xy * 2 - 1, depth, 1);
    #if UNITY_UV_STARTS_AT_TOP
        clipPosition.y = -clipPosition.y;
    #endif
    float4 world = mul(_RoomSightInverseViewProjection, clipPosition);
    return world.xyz / world.w;
}
float4 RoomSightTruthColor(float3 world, float4 truthColor, float partial)
{
    if (_RoomPositivePermissionMode > .5)
    {
        bool portal = !RoomSightSeedContains(world) && (_RoomSightExteriorView == 0 || RoomSightBuildingContains(world));
        return float4(1, 1, portal ? 1 : 0, _RoomWorldLookEnvironment);
    }
    if (_RoomPositiveCoverageMode < .5) return truthColor;
    // 真值屋顶候选只消费合法室内可见片元；室外遮挡表面仍正常写黑色与深度。
    return partial > .5 && RoomSightBuildingContains(world) ? 1 : 0;
}

void RoomSightPreviewRoofClip(float2 rasterPixel, float cutRoof)
{
    if (cutRoof > .5 && _RoomPositiveCoverageMode < .5)
    {
        float coverage = tex2D(_RoomSightCoverageTexture, rasterPixel / _RoomSightTargetSize.xy).r;
        // 解析后接近全白才揭该像素，保守保留实际几何的混合边缘；不代签任意亚像素逻辑边界。
        clip(.998 - coverage);
    }
}

bool RoomSightInBox(float3 worldPosition, float4x4 worldToLocal, float3 halfExtents)
{ return all(abs(mul(worldToLocal, float4(worldPosition, 1)).xyz) <= halfExtents + .0002); }

bool RoomSightSeedContains(float3 worldPosition)
{
    if (_RoomSightSeedCount < 0 || _RoomSightSeedCount > 8) return false;
    [loop] for (int i = 0; i < _RoomSightSeedCount; ++i)
        if (RoomSightInBox(worldPosition, _RoomSightSeedMatrices[i], _RoomSightSeedExtents[i].xyz)) return true;
    return false;
}

bool RoomSightBuildingContains(float3 worldPosition)
{
    if (_RoomSightBuildingCount < 0 || _RoomSightBuildingCount > 32) return false;
    [loop] for (int i = 0; i < _RoomSightBuildingCount; ++i)
        if (RoomSightInBox(worldPosition, _RoomSightBuildingMatrices[i], _RoomSightBuildingExtents[i].xyz)) return true;
    return false;
}

// 有限线段相交；端点表面自身不算前景，实体内部与更早的实体仍阻挡。
bool RoomSightBlockedFrom(float3 worldPosition, float3 observer, bool cameraView)
{
    int count = cameraView ? _RoomSightCameraBlockerCount : _RoomSightBlockerCount;
    if (count < 0 || count > 64) return true;
    [loop] for (int i = 0; i < count; ++i)
    {
        float4x4 worldToLocal = cameraView ? _RoomSightCameraBlockerMatrices[i] : _RoomSightBlockerMatrices[i];
        float3 origin = mul(worldToLocal, float4(observer, 1)).xyz;
        float3 target = mul(worldToLocal, float4(worldPosition, 1)).xyz;
        float3 delta = target - origin, extent = cameraView ? _RoomSightCameraBlockerExtents[i].xyz : _RoomSightBlockerExtents[i].xyz;
        float entry = 0, leave = 1;
        bool intersects = true;
        [unroll] for (int axis = 0; axis < 3; ++axis)
        {
            if (abs(delta[axis]) < 1e-7)
            { if (abs(origin[axis]) > extent[axis]) intersects = false; }
            else
            {
                float a = (-extent[axis] - origin[axis]) / delta[axis];
                float b = (extent[axis] - origin[axis]) / delta[axis];
                entry = max(entry, min(a, b)); leave = min(leave, max(a, b));
            }
        }
        float endpointBias = .02 / max(length(worldPosition - observer), .02);
        if (intersects && leave >= entry && leave >= 0 && entry <= 1 - endpointBias) return true;
    }
    return false;
}

bool RoomSightEyeContains(float3 worldPosition)
{
    // SetInt旧API会把float位模式交给HLSL int；即便外部错误上传，也不能出现无界循环或越界放行。
    if (_RoomSightSeedCount < 0 || _RoomSightSeedCount > 8 || _RoomSightBuildingCount < 0 || _RoomSightBuildingCount > 32 ||
        _RoomSightBlockerCount < 0 || _RoomSightBlockerCount > 64 || _RoomSightDomainCount < 0 || _RoomSightDomainCount > 8 ||
        _RoomSightOwnerCount < 0 || _RoomSightOwnerCount > 8 || _RoomSightOwnerExterior < 0 || _RoomSightOwnerExterior > 1 ||
        _RoomSightExteriorView < 0 || _RoomSightExteriorView > 1) return false;
    if (_RoomSightOwnerExterior != 0 && RoomSightBuildingContains(worldPosition)) return false;
    if (_RoomSightOwnerCount > 0)
    {
        bool ownerContains = false;
        [loop] for (int i = 0; i < _RoomSightOwnerCount; ++i)
            if (RoomSightInBox(worldPosition, _RoomSightOwnerMatrices[i], _RoomSightOwnerExtents[i].xyz)) ownerContains = true;
        if (!ownerContains) return false;
    }
    if (_RoomSightExteriorView != 0 && !RoomSightBuildingContains(worldPosition)) return true;
    if (RoomSightSeedContains(worldPosition)) return true;
    bool domainContains = false;
    [loop] for (int domain = 0; domain < _RoomSightDomainCount; ++domain)
    {
        int4 meta = (int4)_RoomSightMeta[domain]; bool inside = true;
        if (meta.x < 0 || meta.y < 4 || meta.y > 10 || meta.x + meta.y > 80 ||
            meta.z < 0 || meta.w < 0 || meta.w > 8 || meta.z + meta.w > 64) continue;
        [loop] for (int p = 0; p < meta.y; ++p)
            if (dot(_RoomSightPlanes[meta.x + p], float4(worldPosition, 1)) < 0) inside = false;
        if (!inside) continue;
        if (_RoomSightExterior[domain].x > .5)
        { if (RoomSightBuildingContains(worldPosition)) continue; }
        else
        {
            bool target = false;
            [loop] for (int v = 0; v < meta.w; ++v)
                if (RoomSightInBox(worldPosition, _RoomSightTargetMatrices[meta.z + v], _RoomSightTargetExtents[meta.z + v].xyz)) target = true;
            if (!target) continue;
        }
        domainContains = true; break;
    }
    return domainContains && !RoomSightBlockedFrom(worldPosition, _RoomSightObserver, false);
}

bool RoomSightOwnerAllows(float3 world)
{
    if (_RoomSightOwnerCount < 0 || _RoomSightOwnerCount > 8) return false;
    if (_RoomSightOwnerExterior != 0 && RoomSightBuildingContains(world)) return false;
    if (_RoomSightOwnerCount == 0) return true;
    [loop] for (int i=0;i<_RoomSightOwnerCount;++i)
        if(RoomSightInBox(world,_RoomSightOwnerMatrices[i],_RoomSightOwnerExtents[i].xyz)) return true;
    return false;
}
bool RoomSightWindowApertureContains(float3 world)
{
    if (_RoomSightPassMode == 0 || _RoomSightWindowCount < 0 || _RoomSightWindowCount > 8 ||
        !RoomSightOwnerAllows(world) || RoomSightBuildingContains(world)) return false;
    bool inside=false;
    [loop] for(int d=0;d<_RoomSightWindowCount;++d)
    {
        int2 meta=(int2)_RoomSightWindowMeta[d];bool accepted=true;
        if(meta.x<0||meta.y<4||meta.y>10||meta.x+meta.y>80)continue;
        [loop]for(int p=0;p<meta.y;++p)if(dot(_RoomSightWindowPlanes[meta.x+p],float4(world,1))<0)accepted=false;
        inside=inside||accepted;
    }
    return inside;
}
bool RoomSightWindowContains(float3 world)
{
    return RoomSightWindowApertureContains(world)&&!RoomSightBlockedFrom(world,_RoomSightCameraObserver,true);
}
float RoomSightBoxMargin(float3 world,float4x4 worldToLocal,float3 extents)
{
    float3 local=mul(worldToLocal,float4(world,1)).xyz;
    float3 scale=float3(length(worldToLocal[0].xyz),length(worldToLocal[1].xyz),length(worldToLocal[2].xyz));
    float3 margin=(extents-abs(local))/max(scale,.000001);
    // 三维界外仍为负；已在盒内的柔边仅沿足迹XZ，避免贴地下边界把整面地板压黑。
    // 逆矩阵行长把本地边距换回世界单位，保持缩放后仍是配置的世界宽度。
    float hard=min(margin.x,min(margin.y,margin.z));return hard<0?hard:min(margin.x,margin.z);
}
float RoomSightExteriorMargin(float3 world)
{
    float result=1000;
    [loop]for(int b=0;b<_RoomSightBuildingCount;++b)
        result=min(result,-RoomSightBoxMargin(world,_RoomSightBuildingMatrices[b],_RoomSightBuildingExtents[b].xyz));
    return result;
}
float RoomSightCurrentMargin(float3 world)
{
    float result=-1000;
    [loop]for(int s=0;s<_RoomSightSeedCount;++s)result=max(result,RoomSightBoxMargin(world,_RoomSightSeedMatrices[s],_RoomSightSeedExtents[s].xyz));
    if(_RoomSightExteriorView!=0)result=max(result,RoomSightExteriorMargin(world));
    [loop]for(int d=0;d<_RoomSightDomainCount;++d)
    {
        int4 m=(int4)_RoomSightMeta[d];float margin=1000;
        [loop]for(int p=0;p<m.y;++p)margin=min(margin,dot(_RoomSightPlanes[m.x+p],float4(world,1)));
        float target=-1000;
        if(_RoomSightExterior[d].x>.5)target=RoomSightExteriorMargin(world);
        else [loop]for(int v=0;v<m.w;++v)target=max(target,RoomSightBoxMargin(world,_RoomSightTargetMatrices[m.z+v],_RoomSightTargetExtents[m.z+v].xyz));
        result=max(result,min(margin,target));
    }
    if(_RoomSightPassMode>0)
    [loop]for(int w=0;w<_RoomSightWindowCount;++w)
    {
        int2 m=(int2)_RoomSightWindowMeta[w];float margin=RoomSightExteriorMargin(world);
        [loop]for(int p=0;p<m.y;++p)margin=min(margin,dot(_RoomSightWindowPlanes[m.x+p],float4(world,1)));
        result=max(result,margin);
    }
    return result;
}
float RoomFogHistoryBox(float3 world,int start)
{
    float4x4 worldToLocal=float4x4(_RoomFogHistory[start],_RoomFogHistory[start+1],_RoomFogHistory[start+2],_RoomFogHistory[start+3]);
    return RoomSightBoxMargin(world,worldToLocal,_RoomFogHistory[start+4].xyz);
}
float RoomFogHistoricalMargin(int index,float3 world,out float3 origin,out bool window)
{
    int start=index*876;float4 header=_RoomFogHistory[start];float result=-1000;
    origin=_RoomFogHistory[start+1].xyz;window=false;
    [loop]for(int s=0;s<(int)header.y;++s)result=max(result,RoomFogHistoryBox(world,start+4+s*5));
    if(header.w>.5)result=max(result,RoomSightExteriorMargin(world));
    // 原Full seed/室外本就覆盖该点，不再扫描后续全部窗口；保留对界外点的完整有限域判断。
    if(result>=0)return result;
    [loop]for(int d=0;d<(int)header.z;++d)
    {
        int ds=start+44+d*52;float4 meta=_RoomFogHistory[ds];float margin=1000;
        [loop]for(int p=0;p<(int)meta.x;++p)margin=min(margin,dot(_RoomFogHistory[ds+2+p],float4(world,1)));
        float target=-1000;
        if(meta.z>.5)target=RoomSightExteriorMargin(world);
        else [loop]for(int v=0;v<(int)meta.y;++v)target=max(target,RoomFogHistoryBox(world,ds+12+v*5));
        margin=min(margin,target);
        // 相机窗景历史永远不能越过当帧真实窗孔；不把旧屏幕位置投到新视图。
        if(meta.w>.5&&margin>=0&&!RoomSightWindowApertureContains(world))margin=-1000;
        if(margin>result){result=margin;origin=_RoomFogHistory[ds+1].xyz;window=meta.w>.5;}
    }
    return result;
}
bool RoomSightStaticHistoryContains(float3 world)
{
    if(_RoomSightPassMode!=1||_RoomSightStaticEnvironment<.5||_RoomFogStyle.x<.5||
       _RoomFogHistoryCount<1||_RoomFogHistoryCount>4||!RoomSightOwnerAllows(world))return false;
    [loop]for(int h=_RoomFogHistoryCount-1;h>=0;--h)
    {
        if(_RoomFogNow-_RoomFogHistory[h*876].x>_RoomFogTiming.z+.17)continue;
        int start=h*876;float4 header=_RoomFogHistory[start];
        [loop]for(int s=0;s<(int)header.y;++s)if(RoomFogHistoryBox(world,start+4+s*5)>=0)return true;
        if(header.w>.5&&RoomSightExteriorMargin(world)>=0)return true;
        float3 origin;bool window;float margin=RoomFogHistoricalMargin(h,world,origin,window);
        if(margin<0)continue;
        return (!window||RoomSightWindowContains(world))&&!RoomSightBlockedFrom(world,origin,window);
    }
    return false;
}
bool RoomSightPartialContains(float3 world)
{
    return RoomSightEyeContains(world)||RoomSightWindowContains(world)||RoomSightStaticHistoryContains(world);
}
float RoomFogValue(float from,bool target,float age,float delay)
{
    float duration=target?_RoomFogTiming.y:_RoomFogTiming.z;
    delay=min(max(0,delay),max(0,duration-.0001));
    float t=saturate((age-delay)/max(.0001,duration-delay));float ease=1-pow(1-t,3);
    return target?lerp(from,1,ease):from*(1-ease);
}
float RoomSightPresentationVisibility(float3 world)
{
    if(_RoomSightPassMode!=1||_RoomFogStyle.x<.5)return 1;
    bool current=RoomSightEyeContains(world)||RoomSightWindowContains(world);
    if(!current&&_RoomSightStaticEnvironment<.5)return 0;
    float currentMargin=RoomSightCurrentMargin(world);
    if(!current)currentMargin=-max(.0001,abs(currentMargin));
    float delay=_RoomFogTiming.w*saturate(distance(world,_RoomSightObserver)/max(.001,_RoomFogStyle.z));
    float from=current?1:0;bool target=current;float started=_RoomFogNow,previous=currentMargin,previousTime=_RoomFogNow;
    if(_RoomFogHistoryCount>0&&_RoomFogHistoryCount<=4)
    {
        float3 origin;bool window;
        previous=RoomFogHistoricalMargin(0,world,origin,window);previousTime=_RoomFogHistory[0].x;
        target=previous>=0;from=target?1:0;started=previousTime;
        [loop]for(int i=1;i<=_RoomFogHistoryCount;++i)
        {
            float margin=currentMargin,time=_RoomFogNow;
            if(i<_RoomFogHistoryCount){margin=RoomFogHistoricalMargin(i,world,origin,window);time=_RoomFogHistory[i*876].x;}
            bool next=margin>=0;
            if(next!=target)
            {
                float crossing=lerp(previousTime,time,abs(previous)/max(.000001,abs(margin-previous)));
                from=RoomFogValue(from,target,crossing-started,delay);
                if(next)from=max(_RoomFogTiming.x,from);
                target=next;started=crossing;
            }
            previous=margin;previousTime=time;
        }
    }
    float value=RoomFogValue(from,target,_RoomFogNow-started,delay);
    float feather=currentMargin;
    if(!current)
    {
        feather=-1000;
        [loop]for(int h=0;h<_RoomFogHistoryCount;++h){float3 origin;bool window;feather=max(feather,RoomFogHistoricalMargin(h,world,origin,window));}
    }
    if(_RoomFogStyle.y>0)value*=smoothstep(0,_RoomFogStyle.y,max(0,feather));
    if(_RoomFogDither.x>.5)
    {
        float3 cell=floor(world/max(.001,_RoomFogDither.y));
        float noise=frac(sin(dot(cell,float3(12.9898,78.233,37.719)))*43758.5453);
        value=saturate(value+(noise-.5)*_RoomFogDither.z*4*value*(1-value));
    }
    return saturate(value);
}
float4 RoomSightFogColor(float4 color,float3 world,float premultiplied)
{
    float visibility=RoomSightPresentationVisibility(world);
    color.rgb=lerp(_RoomFogColor.rgb*lerp(1,color.a,premultiplied),color.rgb,visibility);return color;
}
bool RoomSightIsHistoryOnly(float3 world)
{
    return _RoomSightPassMode==1&&_RoomSightStaticEnvironment>.5&&!RoomSightEyeContains(world)&&!RoomSightWindowContains(world);
}
#endif

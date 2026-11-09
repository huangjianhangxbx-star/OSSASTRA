// 同一世界根与时间驱动所有草Pass；静态变体不执行三角函数。
#ifndef OSSASTRA_GRASS_WIND
#define OSSASTRA_GRASS_WIND
float3 GrassDeform(float3 world,float3 root,float weight,float4 wind)
{
#if defined(_GRASS_WIND)
 float t=saturate(weight);float amplitude=wind.w;
 float phase=wind.z*(6.28318530718/16.0);
 // 相位轴不随瞬间风向重建；Seconds的86400秒绕回是整数个周期。
 float broad=.62+.30*sin(dot(root.xz,float2(.57,.23))-phase);
 float detail=.08*sin(dot(root.xz,float2(1.13,-.71))+phase*2);
 float2 bend=wind.xy*(broad+detail)*amplitude*t*t;
 world.xz+=bend;world.y-=length(bend)*.08*t;
#endif
 return world;
}
#endif

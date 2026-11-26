Shader "Ossastra/OSPhysics/MatterPainted"
{
    Properties
    {
        [HideInInspector] _MatterState ("物质快照", 2D) = "black" {}
        [HideInInspector] _WorldRect ("世界坐标范围", Vector) = (0, 0, 1, 1)
        [HideInInspector] _PaintLayer ("绘制层", Float) = 0
        [HideInInspector] _LayerVisibility ("主体／饰边／植物", Vector) = (1, 1, 1, 1)
        [HideInInspector] _Style ("主题", Float) = 0
        [HideInInspector] _VisualTime ("对照时钟", Float) = 0
        _WorldMood ("环境明暗", Range(0, 2)) = 1
        [HideInInspector] _UsePreviewFireLights ("实验火光", Float) = 1
        [HideInInspector] _UseOSLightWorldMap ("OSLight 世界灯图", Float) = 0
        _WaterColor ("水面颜色", Color) = (.17, .74, .77, 1)
        _WaterDeep ("深水颜色", Color) = (.045, .29, .40, 1)
        _OilColor ("油色", Color) = (.57, .28, .13, 1)
        _StoneColor ("岩石主体", Color) = (.15, .18, .24, 1)
        _EdgeColor ("岩石饰边", Color) = (.33, .43, .56, 1)
        _PlantColor ("草色", Color) = (.25, .42, .31, 1)
        _WoodColor ("木色", Color) = (.41, .24, .14, 1)
        _SmokeColor ("烟色", Color) = (.30, .35, .39, 1)
        _FlameColor ("火色", Color) = (1, .47, .12, 1)
        _WaterAlpha ("水透明度", Range(0, 1)) = .78
        _SurfaceWidth ("液面宽度", Range(.02, .45)) = .14
        _WaveAmount ("细波", Range(0, .11)) = .027
        _FireGain ("火焰强度", Range(0, 2)) = 1
        _SmokeGain ("烟雾强度", Range(0, 2)) = 1
        _SurfaceScales ("岩石／水／油／草木纹理频率", Vector) = (1.35, .8, 1.05, 1.7)
        _EffectScales ("火／烟／沙纹理频率", Vector) = (4.2, .7, 1.6, 0)
        [NoScaleOffset] _StoneTex ("岩石纹理", 2D) = "white" {}
        [NoScaleOffset] _WaterTex ("水纹理", 2D) = "white" {}
        [NoScaleOffset] _WaterEdgeTex ("水面纹理", 2D) = "white" {}
        [HideInInspector] _UseWaterEdgeTex ("使用水面纹理", Float) = 0
        [NoScaleOffset] _OilTex ("油纹理", 2D) = "white" {}
        [NoScaleOffset] _PlantTex ("草叶纹理", 2D) = "white" {}
        [NoScaleOffset] _WoodTex ("木纹", 2D) = "white" {}
        [NoScaleOffset] _FlameTex ("火焰纹理", 2D) = "white" {}
        [NoScaleOffset] _SmokeTex ("烟雾纹理", 2D) = "white" {}
        [NoScaleOffset] _SandTex ("沙纹理", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Off
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"
            #include "MatterPaintedTopology.hlsl"

            Texture2D _StoneTex, _WaterTex, _OilTex, _PlantTex, _WoodTex, _FlameTex, _SmokeTex, _SandTex, _WaterEdgeTex;
            SamplerState sampler_StoneTex, sampler_WaterTex, sampler_OilTex, sampler_PlantTex;
            SamplerState sampler_WoodTex, sampler_FlameTex, sampler_SmokeTex, sampler_SandTex;
            SamplerState sampler_WaterEdgeTex, sampler_MatterLinearClamp;
            Texture2D _SideViewAceWorldAmbient;
            float _SideViewAceLightMapsReady;
            float4 _SideViewAceScreenToMap;
            float4 _WorldRect, _LayerVisibility, _SurfaceScales, _EffectScales;
            float4 _WaterColor, _WaterDeep, _OilColor, _StoneColor, _EdgeColor, _PlantColor, _WoodColor;
            float4 _SmokeColor, _FlameColor;
            float _WaterAlpha, _SurfaceWidth, _WaveAmount, _FireGain, _SmokeGain;
            float _PaintLayer, _Style, _VisualTime, _WorldMood, _UsePreviewFireLights;
            float _UseOSLightWorldMap, _UseWaterEdgeTex, _AssetView;
            float4 _MatterFireLights[4];

            struct VertexInput
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct VertexOutput
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            struct PaintMapping
            {
                float2 world;
                float2 deltaX;
                float2 deltaY;
            };

            VertexOutput Vert(VertexInput input)
            {
                VertexOutput output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                return output;
            }

            float Hash(float2 value)
            {
                float3 p = frac(value.xyx * float3(.1031, .1030, .0973));
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            float Noise(float2 p)
            {
                float2 index = floor(p);
                float2 f = frac(p);
                f *= f * (3 - 2 * f);
                return lerp(lerp(Hash(index), Hash(index + float2(1, 0)), f.x),
                    lerp(Hash(index + float2(0, 1)), Hash(index + 1), f.x), f.y);
            }

            float3 Ink(float3 base, float4 source)
            {
                return base * lerp(1, .37 + .63 * source.rgb, source.a) +
                    .17 * source.a * max(0, source.rgb - max(source.r, max(source.g, source.b)) * .74) *
                    (1 - min(source.r, min(source.g, source.b)));
            }

            float4 BrushSample(Texture2D<float4> brushTexture, SamplerState sampling, PaintMapping mapping,
                float2 scale, float2 offset)
            {
                // 在层分支之前取得导数，和被透明裁掉的邻像素共享同一纹理缩放级。
                return brushTexture.SampleGrad(sampling, mapping.world * scale + offset,
                    mapping.deltaX * scale, mapping.deltaY * scale);
            }

            float4 PaintLight(float2 coordinate, float roomX, float2 raster)
            {
                float4 result = float4(1, 1, 1, 0);
                // 实验相机可绑定项目的世界灯图。加载期间仅保留材质原色。
                [branch] if (_UseOSLightWorldMap > .5)
                {
                    float2 uv = raster * _SideViewAceScreenToMap.xy;
                    uv.y = uv.y * _SideViewAceScreenToMap.z + _SideViewAceScreenToMap.w;
                    if (_SideViewAceLightMapsReady >= .5 && all(uv >= 0) && all(uv <= 1))
                        result = float4(_SideViewAceWorldAmbient.SampleLevel(sampler_MatterLinearClamp, uv, 0).rgb, 1);
                }
                else
                {
                    float leftToRight = smoothstep(.28, .94, roomX);
                    float room = lerp(1.03, .60, leftToRight);
                    float exposure = lerp(.15, room, saturate(_WorldMood)) + max(0, _WorldMood - 1) * .45;
                    float fire = 0;
                    [branch] if (_UsePreviewFireLights > .5 && _MatterFireLights[0].w > 0)
                    {
                        [unroll] for (int i = 0; i < 4; i++)
                        {
                            float4 source = _MatterFireLights[i];
                            float2 delta = coordinate - source.xy;
                            float radius = max(1, source.z);
                            float falloff = saturate(1 - dot(delta, delta) / (radius * radius));
                            fire += falloff * falloff * source.w;
                        }
                    }
                    float3 tint = lerp(float3(.79, .90, 1.04), float3(1.0, 1.04, .98), saturate(_WorldMood));
                    result = float4(exposure * tint + saturate(fire) * float3(.88, .42, .19), 0);
                }
                return result;
            }

            float3 LitColor(float3 color, float4 light)
            {
                if (light.w < .5) return color * light.rgb;
                // OSLight 世界灯图保存 Gamma 值，和正式材质一样在该空间调制。
                color = max(0, color);
                float3 gamma = lerp(color * 12.92, 1.055 * pow(color, 1.0 / 2.4) - .055, step(.0031308, color));
                float3 result = max(0, gamma * light.rgb);
                return lerp(result / 12.92, pow((result + .055) / 1.055, 2.4), step(.04045, result));
            }

            float EdgeDistance(int2 coordinates, float2 direction, float offset)
            {
                if (!IsBlocked(ReadMatter(coordinates + (int2)direction))) return offset;
                if (!IsBlocked(ReadMatter(coordinates + (int2)direction * 2))) return 1 + offset;
                return 2 + offset;
            }

            // 描绘石块质感和外沿。原格仍控制所有实心判定，凹凸只在交界处内缩。
            float4 Terrain(PaintedCell cell, float2 p, PaintMapping mapping, float aa, float4 light)
            {
                if (!IsBlocked(cell)) return 0;
                float2 world = mapping.world;
                float2 local = frac(p);
                if (cell.barrier != 0)
                {
                    if (_LayerVisibility.x < .5) return 0;
                    float plank = abs(frac(p.y * .31) - .5);
                    float scratch = Noise(float2(world.x * 1.7, world.y * 28));
                    float3 panel = Ink(_WoodColor.rgb * (.64 + .34 * scratch),
                        BrushSample(_WoodTex, sampler_WoodTex, mapping, _SurfaceScales.w, 0));
                    float strap = smoothstep(.41, .48, plank);
                    float eye = 1 - smoothstep(.12, .20, length(float2(local.x - .5, plank * 2 - .9)));
                    panel = lerp(panel, _EdgeColor.rgb * .61, strap * .85);
                    return float4(LitColor(panel + eye * float3(.19, .115, .044), light), 1);
                }

                int2 index = (int2)floor(p);
                float top = EdgeDistance(index, float2(0, 1), 1 - local.y);
                float bottom = EdgeDistance(index, float2(0, -1), local.y);
                float left = EdgeDistance(index, float2(-1, 0), local.x);
                float right = EdgeDistance(index, float2(1, 0), 1 - local.x);
                float side = min(left, right);
                float outer = min(min(top, bottom), side);
                float chunkGrain = Noise(world * float2(6.2, 2.8));
                float2 warp = float2(Noise(world * 8 + .7), Noise(world * 6 + 14.8)) - .5;
                float2 stoneP = (world + warp * lerp(.038, .068, _Style)) *
                    lerp(float2(2.8, 5.1), float2(2.2, 4.3), _Style);
                float row = floor(stoneP.y);
                stoneP.x += Hash(float2(row, 38.3)) * 3;
                float2 stoneID = floor(stoneP);
                float2 inside = frac(stoneP);
                float vertexRoughness = Hash(stoneID);
                float border = min(min(inside.x, 1 - inside.x) * .85, min(inside.y, 1 - inside.y));
                float seam = smoothstep(.015, .055, border);
                float brushGrain = Noise(world * float2(13, 27)) * .36 + chunkGrain * .64;
                float deep = smoothstep(.25, 2.8, outer);
                float3 fill = _StoneColor.rgb * (.49 + .36 * brushGrain + .34 * vertexRoughness);
                fill *= lerp(.56, 1, seam) * (1 - deep * .68);
                fill = Ink(fill, BrushSample(_StoneTex, sampler_StoneTex, mapping, _SurfaceScales.x, 0));
                float verticalBevel = smoothstep(.62, .91, inside.y) * seam;
                float rocker = .14 + warp.y * .08 + vertexRoughness * .10;
                float broadTrim = (1 - smoothstep(.35, 2.40, outer)) * seam;
                float cutTrim = (1 - smoothstep(rocker, rocker + .24, top)) * (.55 + brushGrain * .45);
                float downTrim = (1 - smoothstep(.06, .25 + warp.x * .12, bottom)) * .23;
                float wallTrim = (1 - smoothstep(.12, .68, side)) *
                    (left < right ? .49 : .24) * (.35 + brushGrain * .65);
                float shade = min(.92, .21 * broadTrim + .25 * verticalBevel * broadTrim + cutTrim * .54 +
                    downTrim + wallTrim);
                float3 trim = Ink(lerp(_StoneColor.rgb * .67, _EdgeColor.rgb * .91, shade),
                    BrushSample(_StoneTex, sampler_StoneTex, mapping, _SurfaceScales.x, float2(.19, .37)));
                float rounding = max(0, .17 - min(top, bottom)) * max(0, .17 - side) * 6.4;
                float coverage = smoothstep(-aa, aa, outer - rounding + .004);
                if (outer > .20) coverage = 1;
                float trimBlend = saturate(shade * 1.28);
                float filled = saturate(_LayerVisibility.x);
                float trimmed = saturate(_LayerVisibility.y);
                float3 painted = filled > .5 ? lerp(fill, trim, trimBlend * trimmed) : trim;
                return float4(LitColor(painted, light), coverage * max(filled, trimBlend * trimmed));
            }

            float4 DryGrain(PaintedCell cell, float2 p, PaintMapping mapping, float aa, float4 light)
            {
                float2 world = mapping.world;
                int2 index = (int2)floor(p);
                bool covered = ReadMatter(index + int2(0, 1)).kind == cell.kind;
                float height = covered ? 1 : max(.05, cell.amount) + (Noise(world * 47) - .5) * .10;
                float depth = height - frac(p.y);
                float alpha = covered ? 1 : smoothstep(-aa, aa, depth);
                if (alpha <= 0) return 0;
                float grit = .67 * Noise(world * 34) + .33 * Hash(floor(world * 104));
                float strata = .58 + .42 * Noise(float2(world.x * 9, world.y * 49));
                float3 sand = lerp(float3(.18, .13, .09), float3(.53, .39, .24), grit);
                float3 ash = lerp(float3(.13, .15, .17), float3(.39, .43, .48), grit);
                float3 color = Ink((cell.kind == 3 ? sand : ash) * strata,
                    BrushSample(_SandTex, sampler_SandTex, mapping, _EffectScales.z, 0));
                color *= 1 + .10 * (1 - smoothstep(0, .18, max(0, depth)));
                return float4(LitColor(color, light), alpha);
            }

            float4 Liquid(PaintedCell cell, float2 p, PaintMapping mapping, float aa, float4 light)
            {
                bool water = cell.kind == 1;
                float2 world = mapping.world;
                float phase = _VisualTime * (_AssetView > .5 ? 1.1 : (water ? 1.51 : .62));
                float brushCurve = Noise(float2(world.x * 2.7, .151 + phase * .1));
                float wave = (sin(world.x * 9.7 + phase + brushCurve) * .63 +
                    sin(world.x * 19.3 - phase * .53) * .37) * _WaveAmount;
                // 同类格之间借用高度，不直接过滤 ID。水油界面只画色层交接。
                LiquidTrace shape;
                [branch] if (_AssetView > .5) shape = DescribeContinuousLiquid(cell, p, aa, wave);
                else shape = DescribeLiquid(cell, p, aa, wave);
                if (shape.coverage <= 0) return 0;
                int2 index = (int2)floor(p);
                float depth = max(0, shape.surfaceDepth);
                if (_AssetView > .5)
                {
                    water = shape.kind == 1;
                    phase = _VisualTime * (water ? 1.51 : .62);
                }
                else
                {
                    [unroll] for (int sample = 1; sample <= 3; sample++)
                    {
                        PaintedCell higher = ReadMatter(index + int2(0, sample));
                        if (SameLiquid(cell, higher)) depth += higher.amount;
                        else break;
                    }
                }
                float2 brush = float2(world.x * 2.3, world.y * 11.5);
                float rolls = Noise(brush + float2(phase * .26, -.12 * phase * shape.falling));
                float ribbons = smoothstep(.42, .76, Noise(brush * float2(1.7, 2.1) + rolls * 1.7));
                float airContact = step(.0001, shape.airSurface);
                float waterFade = saturate(depth * .31 + rolls * .25);
                float3 shallow = water ? _WaterColor.rgb * .79 : _OilColor.rgb;
                float3 deep = water ? _WaterDeep.rgb * .94 : _OilColor.rgb * float3(.49, .48, .53);
                float3 tint = lerp(shallow, deep, waterFade) * lerp(.75, 1.06, rolls);
                float4 pigment = water
                    ? BrushSample(_WaterTex, sampler_WaterTex, mapping, _SurfaceScales.y, 0)
                    : BrushSample(_OilTex, sampler_OilTex, mapping, _SurfaceScales.z, 0);
                tint = Ink(tint, pigment) + shallow * ribbons * lerp(.105, .065, _Style);
                float edgeWidth = max(_SurfaceWidth, aa);
                float rim = airContact * (1 - smoothstep(edgeWidth * .39, edgeWidth * 1.15, abs(shape.surfaceDepth)));
                float smallBreaks = smoothstep(.42, .78, Noise(float2(world.x * 17, phase * .13 + .6)));
                float edgeRipple = .62 + .38 * Noise(float2(world.x * 9.4, phase * .3));
                float edgeFoam = (abs(shape.slope) > .055 || shape.falling > .5) ? smallBreaks : smallBreaks * .19;
                float3 rimColor = water ? _WaterColor.rgb * (1.07 + edgeFoam * .33) : _OilColor.rgb * 1.28;
                if (water && _UseWaterEdgeTex > .5 && rim > 0)
                {
                    float2 edgeUV = float2(frac(world.x * _SurfaceScales.y * .74),
                        saturate(.54 + shape.surfaceDepth / edgeWidth * .43));
                    float4 paintedRim = _WaterEdgeTex.SampleLevel(sampler_WaterEdgeTex, edgeUV, 0);
                    rim *= paintedRim.a;
                    rimColor = .60 * rimColor + .70 * paintedRim.rgb;
                }
                tint = lerp(tint, rimColor, rim * edgeRipple);
                tint *= 1 - shape.oilBoundary * .27;
                if (shape.falling > .5)
                {
                    float center = _AssetView > .5 ? Noise(float2(world.x * 9 + rolls, world.y * 3)) - .5 : shape.offset.x;
                    float glint = (1 - smoothstep(.018, .16, abs(center + .06))) *
                        smoothstep(.39, .82, Noise(float2(world.x * 10, world.y * 21 + phase * 4)));
                    tint += shallow * glint * (water ? .42 : .22);
                }
                float opacity = water ? _WaterAlpha : min(1, _WaterAlpha + .115);
                return float4(LitColor(tint, light), shape.coverage * saturate(opacity + rim * .16));
            }

            float Leaf(float2 coordinates, float angle, float2 radii)
            {
                float2 local = float2(coordinates.x + coordinates.y * angle, coordinates.y - coordinates.x * angle);
                return saturate(1 - length(local / radii));
            }

            float4 Plants(PaintedCell cell, float2 p, PaintMapping mapping, float aa, float4 light)
            {
                if (IsBlocked(cell) || _LayerVisibility.z < .5) return 0;
                float2 world = mapping.world;
                int2 index = (int2)floor(p);
                float2 local = frac(p);
                PaintedCell below = ReadMatter(index + int2(0, -1));
                bool leafTip = cell.kind == 0 && below.kind == 4 && below.amount > .35 && local.y < .32;
                if (cell.kind != 4 && cell.kind != 5 && !leafTip) return 0;
                if (leafTip) cell = below;
                float heat = saturate((cell.heat - .19) * 1.85 + cell.burning * .21);
                float charAmount = saturate(heat + (1 - cell.amount) * .37);
                if (cell.kind == 5)
                {
                    float bend = Noise(world * float2(4, 5.9)) * .105;
                    float boardY = (world.y + bend * .09) * 5.1;
                    float board = frac(boardY);
                    float woodTexture = Noise(float2(world.x * 5.4, world.y * 89 + bend * 132));
                    float faintScore = sin(world.y * 177 + bend * 94 + board * 6.3);
                    float bevel = smoothstep(.72, 1, board) * .35 + (1 - smoothstep(.033, .063, board)) * .45;
                    float3 wood = _WoodColor.rgb * (.63 + .35 * woodTexture + .06 * faintScore);
                    wood = Ink(wood, BrushSample(_WoodTex, sampler_WoodTex, mapping, _SurfaceScales.w, 0));
                    wood = lerp(wood, wood * float3(.24, .29, .34), charAmount * .94);
                    wood *= 1 + bevel;
                    float alpha = smoothstep(-aa, aa, min(1, cell.amount * 1.65) - max(abs(local.x - .5), abs(local.y - .5)) * .65);
                    return float4(LitColor(wood, light), alpha);
                }

                float2 stemP = world * lerp(float2(12, 13), float2(15, 10), _Style);
                float2 stemID = floor(stemP);
                float seed = Hash(stemID);
                float2 leafLocal = frac(stemP) - .5;
                float sprigA = Leaf(leafLocal - float2(-.17, .07), 1.12, float2(.23, .51));
                float sprigB = Leaf(leafLocal - float2(.11, -.05), -.89, float2(.28, .48));
                float crown = Leaf(leafLocal - float2(.14, .22), .34, float2(.18, .31));
                float form = max(max(sprigA, sprigB), crown);
                float mottled = Noise(world * float2(41, 55));
                float vein = 1 - smoothstep(.009, .046, abs(leafLocal.x * .67 + leafLocal.y * .51));
                float strength = smoothstep(.03, .12, form);
                bool coveredByGrass = ReadMatter(index + int2(0, 1)).kind == 4;
                float body = leafTip ? 0 : coveredByGrass ? 1 : smoothstep(.08, .16, 1 - local.y);
                float shape = max(body, strength);
                if (leafTip) shape *= 1 - smoothstep(.03, .32, local.y);
                float3 color = lerp(_PlantColor.rgb * .34, _PlantColor.rgb * (1.06 + .24 * seed),
                    saturate(form * 2.3 + .19 * mottled)) + _PlantColor.rgb * vein * form * .23;
                color = Ink(color, BrushSample(_PlantTex, sampler_PlantTex, mapping, _SurfaceScales.w, 0));
                color = lerp(color, color * float3(.37, .31, .31), charAmount);
                shape *= saturate(cell.amount * (1.21 + .53 * form));
                return float4(LitColor(color, light), saturate(shape));
            }

            float TraceSmoke(int2 index, float2 p)
            {
                int2 baseIndex = (int2)floor(p - .5);
                float2 fracUV = frac(p - .5);
                float smoke = 0;
                [unroll] for (int y = 0; y <= 1; y++)
                    [unroll] for (int x = 0; x <= 1; x++)
                    {
                        int2 other = baseIndex + int2(x, y);
                        PaintedCell source = ReadMatter(other);
                        if (source.smoke <= 0 || IsBlocked(source)) continue;
                        // 禁止烟边借对角格跨过岩角。
                        if (all(other != index) &&
                            (IsBlocked(ReadMatter(int2(other.x, index.y))) ||
                             IsBlocked(ReadMatter(int2(index.x, other.y))))) continue;
                        float2 weight = lerp(1 - fracUV, fracUV, float2(x, y));
                        smoke += source.smoke * weight.x * weight.y;
                    }
                return smoke;
            }

            float4 FireAndSmoke(PaintedCell cell, float2 p, PaintMapping mapping, float4 light)
            {
                if (IsBlocked(cell)) return 0;
                int2 index = (int2)floor(p);
                float2 local = frac(p);
                float smoke = TraceSmoke(index, p);
                float2 hazeScale = float2(_EffectScales.y * 1.55, _EffectScales.y);
                float2 hazeOffset = -float2(.027, .14) * _VisualTime;
                float2 hazeUV = mapping.world * hazeScale + hazeOffset;
                float mist = 0;
                float3 smokeColor = 0;
                if (smoke > .0001)
                {
                    float4 haze = BrushSample(_SmokeTex, sampler_SmokeTex, mapping, hazeScale, hazeOffset);
                    float turbulence = Noise(hazeUV * 4.3 + Noise(hazeUV * 2.2 + 2.1) * 1.65);
                    mist = min(.82, (smoke * 1.92 + sqrt(smoke) * .24) * _SmokeGain);
                    mist *= (.58 + turbulence * .42) * lerp(.38, 1, haze.a);
                    smokeColor = LitColor(Ink(_SmokeColor.rgb * (1.03 + turbulence * .23), haze), light);
                }
                if (_AssetView > .5) return float4(smokeColor, mist);
                float damp = cell.kind == 1 ? smoothstep(.04, .63, cell.amount) : 0;
                PaintedCell below = ReadMatter(index + int2(0, -1));
                PaintedCell belowTwice = ReadMatter(index + int2(0, -2));
                // 越过一格的火舌只在竖直相通的空位延伸。热度和燃烧位决定发射。
                float attached = cell.burning * (.75 + cell.heat * .30);
                float climb = !IsBlocked(below) ? below.burning * (.70 + below.heat * .36) * (1 - local.y * .54) : 0;
                if (!IsBlocked(below) && (below.kind == 0 || below.burning != 0))
                    climb = max(climb, belowTwice.burning * belowTwice.heat * (1 - local.y) * .48);
                float wick = max(attached, climb) * (1 - damp);
                if (wick <= 0) return float4(smokeColor, mist);
                float rhythm = _VisualTime * 4.9;
                float flicker = Noise(float2(p.x * .43 + sin(p.y * .86 + rhythm) * .29, p.y * .48 - rhythm));
                float edge = abs(local.x - (.50 + (flicker - .5) * .34));
                float feather = (1 - smoothstep(.21, .54, edge)) * (.59 + flicker * .41);
                float flame = smoothstep(.14, .81, wick * feather) * _FireGain;
                float2 flameUV = frac(mapping.world * _EffectScales.x - float2(.03, .86) * _VisualTime);
                float4 brushFire = _FlameTex.SampleGrad(sampler_FlameTex, flameUV,
                    mapping.deltaX * _EffectScales.x, mapping.deltaY * _EffectScales.x);
                flame *= lerp(.38, 1, brushFire.a);
                float core = flame * flame;
                float3 emberColor = Ink(_FlameColor.rgb * float3(.94, .58, .48), brushFire);
                float3 coreColor = float3(1.27, 1.15, .75);
                float3 fireColor = lerp(emberColor, coreColor, saturate(core * .79)) * (.82 + flame * .24);
                float sparkDot = 1 - smoothstep(.039, .079, length(frac(float2(p.x * .72, p.y * .97 - rhythm)) - float2(.38, .68)));
                float ember = sparkDot * .41 * wick * smoothstep(.56, .86, flicker);
                float fireOpacity = saturate(flame * .94 + ember);
                float alpha = fireOpacity + mist * (1 - fireOpacity);
                float3 painted = (fireColor * fireOpacity + smokeColor * mist * (1 - fireOpacity)) / max(alpha, .001);
                return float4(painted, alpha);
            }

            float4 Frag(VertexOutput input) : SV_Target
            {
                float2 p = clamp(input.uv * _MatterGridSize.xy, 0, _MatterGridSize.xy - .0001);
                PaintMapping mapping;
                mapping.world = _WorldRect.xy + input.uv * _WorldRect.zw;
                mapping.deltaX = ddx(mapping.world);
                mapping.deltaY = ddy(mapping.world);
                float aa = clamp(max(fwidth(p.x), fwidth(p.y)) * .63, .012, .24);
                PaintedCell cell = ReadMatter((int2)floor(p));
                if (_PaintLayer < .5 && (!IsBlocked(cell) || (_AssetView > .5 && cell.barrier == 0))) return 0;
                if (_PaintLayer >= .5 && IsBlocked(cell)) return 0;
                if (_PaintLayer >= .5 && _PaintLayer < 1.5 &&
                    ((cell.kind == 0 && _AssetView < .5) || cell.kind == 4 || cell.kind == 5)) return 0;
                if (_PaintLayer >= 1.5 && _PaintLayer < 2.5 &&
                    (cell.kind == 1 || cell.kind == 2 || cell.kind == 3 || cell.kind == 6)) return 0;
                float4 light = PaintLight(p, input.uv.x, input.position.xy);
                [branch] if (_PaintLayer < .5) return Terrain(cell, p, mapping, aa, light);
                [branch] if (_PaintLayer < 1.5)
                {
                    if (IsLiquid(cell) || (_AssetView > .5 && cell.kind == 0)) return Liquid(cell, p, mapping, aa, light);
                    return DryGrain(cell, p, mapping, aa, light);
                }
                [branch] if (_PaintLayer < 2.5) return Plants(cell, p, mapping, aa, light);
                return FireAndSmoke(cell, p, mapping, light);
            }
            ENDHLSL
        }
    }
}

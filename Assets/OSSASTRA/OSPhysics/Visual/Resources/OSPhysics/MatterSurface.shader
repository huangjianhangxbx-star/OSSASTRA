Shader "Ossastra/OSPhysics/MatterSurface"
{
    Properties
    {
        [NoScaleOffset] _MatterState ("Matter state", 2D) = "black" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry-100" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Off
            ZWrite Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"

            Texture2D<float4> _MatterState;
            float4 _MatterGridSize;
            int _MatterDisplay;
            float _MatterSeconds;
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

            VertexOutput Vert(VertexInput input)
            {
                VertexOutput output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                return output;
            }

            struct Cell
            {
                int kind;
                int fixedRock;
                int barrier;
                int burning;
                float amount;
                float heat;
                float smoke;
            };

            Cell Read(float2 grid)
            {
                Cell cell = (Cell)0;
                if (any(grid < 0) || any(grid >= _MatterGridSize.xy))
                {
                    cell.fixedRock = 1;
                    return cell;
                }
                float4 data = _MatterState.Load(int3((int2)grid, 0));
                int flags = (int)round(data.r * 255);
                cell.kind = flags & 7;
                cell.fixedRock = (flags >> 3) & 1;
                cell.barrier = (flags >> 4) & 1;
                cell.burning = (flags >> 5) & 1;
                cell.amount = data.g;
                cell.heat = data.b;
                cell.smoke = data.a;
                return cell;
            }

            int Blocked(Cell cell)
            {
                return cell.fixedRock | cell.barrier;
            }

            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(91.73, 137.57))) * 27391.183);
            }

            float Noise(float2 p)
            {
                float2 base = floor(p);
                float2 f = frac(p);
                f *= f * (3 - 2 * f);
                return lerp(lerp(Hash(base), Hash(base + float2(1, 0)), f.x),
                    lerp(Hash(base + float2(0, 1)), Hash(base + 1), f.x), f.y);
            }

            float3 LabBackdrop(float2 p, float2 uv, float ambient)
            {
                float height = pow(saturate(uv.y), 1.4);
                float strata = abs(sin(p.y * .14 + Noise(p * float2(.021, .044)) * 5.8));
                float wash = Noise(p * float2(.023, .053) + 1.9) * .8 + Noise(p * .086) * .2;
                float3 slate = lerp(float3(.006, .013, .023), float3(.026, .063, .080), height * .55 + wash * .45);
                float3 seam = float3(.010, .026, .027) * smoothstep(.9, .997, strata) * wash;
                float frame = smoothstep(0, 1.1, min(p.x, p.y)) *
                    smoothstep(0, 1.1, min(_MatterGridSize.x - p.x, _MatterGridSize.y - p.y));
                return (slate + seam) * (.65 + ambient * .48) * (.7 + .3 * frame);
            }

            float3 DebugColor(Cell center, float2 p, float2 local)
            {
                float3 base = float3(.015, .024, .033);
                if (_MatterDisplay == 3)
                {
                    if (center.fixedRock) return float3(.26, .32, .39);
                    if (center.barrier) return float3(.30, .57, .16);
                    return base;
                }
                if (_MatterDisplay == 2)
                {
                    float3 cold = lerp(base, float3(.25, .030, .13), saturate(center.heat * 2.3));
                    float3 hot = lerp(float3(.96, .10, .035), float3(1, .84, .45), smoothstep(.47, 1, center.heat));
                    return Blocked(center) ? base * .45 : lerp(cold, hot, smoothstep(.20, .65, center.heat));
                }

                if (center.fixedRock) base = float3(.12, .17, .20);
                else if (center.barrier) base = float3(.32, .56, .22);
                else if (center.kind == 1) base = lerp(base, float3(.016, .49, .89), .15 + .85 * center.amount);
                else if (center.kind == 2) base = lerp(base, float3(.70, .31, .035), .15 + .85 * center.amount);
                else if (center.kind == 3) base = float3(.56, .39, .16);
                else if (center.kind == 4) base = float3(.19, .52, .13);
                else if (center.kind == 5) base = float3(.38, .18, .09);
                else if (center.kind == 6) base = float3(.42, .44, .49);
                if (center.burning) base = float3(1, .19, .035);
                base = lerp(base, float3(.33, .40, .46), center.smoke * .6);
                float2 trace = min(local, 1 - local);
                float grid = smoothstep(0, max(.015, fwidth(p.x)), min(trace.x, trace.y));
                return base * (.6 + grid * .4);
            }

            float3 Stone(Cell center, float2 p, float2 local, Cell up, Cell right, Cell down, Cell left)
            {
                if (center.barrier)
                {
                    float stripe = step(.48, frac((p.x + p.y * .64) * .49));
                    return lerp(float3(.093, .10, .072), float3(.40, .31, .105), stripe);
                }
                float chalk = Noise(p * float2(.36, .58)) * .65 + Hash(floor(p) * .7) * .35;
                float fracture = smoothstep(.77, .99, abs(sin(p.y * 1.1 + Noise(p * .27) * 6.0)));
                float3 color = lerp(float3(.020, .026, .033), float3(.081, .110, .123), chalk);
                color *= .79 + fracture * .21;
                float lip = (1 - Blocked(up)) * smoothstep(.64, 1, local.y);
                float edge = (1 - Blocked(left)) * (1 - smoothstep(0, .42, local.x));
                float floorDark = (1 - Blocked(down)) * (1 - smoothstep(0, .55, local.y));
                float rightDark = (1 - Blocked(right)) * smoothstep(.56, 1, local.x);
                return color * (1 - .18 * floorDark - .12 * rightDark) +
                    float3(.035, .061, .079) * (lip + .44 * edge);
            }

            float3 Liquid(Cell center, float2 p, float2 local, Cell up, Cell right, Cell left, float3 background)
            {
                bool water = center.kind == 1;
                bool upperLiquid = up.kind == center.kind;
                float l = left.kind == center.kind ? left.amount : center.amount;
                float r = right.kind == center.kind ? right.amount : center.amount;
                float tracedHeight = lerp(lerp(l, center.amount, local.x * 2), lerp(center.amount, r, (local.x - .5) * 2), step(.5, local.x));
                float flowSlope = clamp(r - l, -.6, .6);
                float wave = sin(p.x * .9 + _MatterSeconds * (2.5 - 2 * flowSlope)) * (.009 + .014 * abs(flowSlope));
                float height = upperLiquid ? 1.15 : saturate(tracedHeight + wave);
                float distanceToSurface = height - local.y;
                float width = clamp(fwidth(local.y) * .7, .045, .26);
                float opacity = smoothstep(-width, width, distanceToSurface);
                float3 tint = water ? float3(.012, .24, .36) : float3(.36, .151, .027);
                float3 specular = water ? float3(.125, .87, .91) : float3(.97, .63, .155);
                float ripples = .5 + .5 * sin(p.x * .71 - p.y * .8 + _MatterSeconds * (2.1 - flowSlope) + Noise(p * .25) * 7);
                float surface = upperLiquid ? 0 : exp(-max(0, distanceToSurface) * 8);
                float caustic = (.018 + .025 * ripples * ripples) * (1 - surface * .75);
                float3 layered = tint * (.64 + .28 * Noise(p * float2(.21, .34)) + .05 * ripples)
                    + specular * (.62 * surface + caustic);
                return lerp(background, layered, opacity * (water ? .81 : .9));
            }

            float3 DryMatter(Cell center, float2 p, float2 local, Cell up, float3 background)
            {
                float grain = Noise(p * 3.1) * .53 + Hash(floor(p * 9)) * .47;
                float3 color = center.kind == 3 ? float3(.52, .37, .15) : float3(.30, .34, .40);
                float silhouette = 1;
                if (center.kind == 4)
                {
                    color = lerp(float3(.047, .16, .056), float3(.15, .37, .12), grain);
                    float blade = abs(sin(local.x * 20 + p.y * 2 + grain * 5));
                    if (up.kind != 4) silhouette = 1 - smoothstep(.56 + .30 * blade, 1, local.y) * .84;
                }
                else if (center.kind == 5)
                {
                    float rings = abs(sin((p.y + Noise(p * .52) * .7) * 8));
                    color = lerp(float3(.12, .053, .022), float3(.35, .167, .065), rings * .67 + grain * .33);
                }
                float hot = saturate(center.heat * 1.5);
                float charred = center.kind == 4 || center.kind == 5 ? hot : hot * .22;
                color = lerp(color * (.58 + grain * .54), color * .24, charred);
                return lerp(background, color, silhouette);
            }

            float Flame(Cell center, Cell below, float2 p, float2 local)
            {
                float shield = smoothstep(0, .42, (center.kind == 1 ? center.amount : 0));
                float wick = (center.burning + below.burning * .64 * (1 - local.y)) * (1 - shield);
                if (wick <= 0) return 0;
                float turbulence = Noise(float2(p.x * 1.8 + sin(_MatterSeconds * 3 + p.y * .4) * .4,
                    p.y * .85 - _MatterSeconds * 7.1));
                return smoothstep(.19, .79, wick * (.36 + turbulence * .86));
            }

            float4 Frag(VertexOutput input) : SV_Target
            {
                float2 p = min(input.uv * _MatterGridSize.xy, _MatterGridSize.xy - .0001);
                float2 index = floor(p);
                float2 local = frac(p);
                Cell center = Read(index);
                if (_MatterDisplay != 0) return float4(DebugColor(center, p, local), 1);

                Cell up = Read(index + float2(0, 1));
                Cell right = Read(index + float2(1, 0));
                Cell down = Read(index - float2(0, 1));
                Cell left = Read(index - float2(1, 0));
                float dim = smoothstep(.54, .85, input.uv.x);
                float ambient = lerp(.97, .36, dim);
                float3 color = LabBackdrop(p, input.uv, ambient);
                bool blocked = Blocked(center) != 0;
                if (blocked)
                    color = Stone(center, p, local, up, right, down, left) * ambient;
                else if (center.kind == 1 || center.kind == 2)
                {
                    float3 background = color;
                    color = lerp(background, Liquid(center, p, local, up, right, left, background), .36 + .64 * ambient);
                }
                else if (center.kind >= 3)
                {
                    float3 background = color;
                    color = lerp(background, DryMatter(center, p, local, up, background), .28 + .72 * ambient);
                }

                float localLight = 0;
                [unroll] for (int i = 0; i < 4; i++)
                {
                    float4 light = _MatterFireLights[i];
                    float falloff = saturate(1 - length(p - light.xy) / max(1, light.z));
                    localLight += falloff * falloff * light.w;
                }
                color += (color * float3(.62, .39, .22) + float3(.055, .019, .004)) * saturate(localLight * 1.7);
                if (!blocked)
                {
                    float horizontal = lerp(left.smoke, right.smoke, local.x);
                    float vertical = lerp(down.smoke, up.smoke, local.y);
                    float plume = saturate(center.smoke * .62 + .19 * (horizontal + vertical));
                    if (plume > 0)
                    {
                        float curl = Noise(p * float2(.30, .18) + float2(0, -_MatterSeconds * 1.4));
                        float3 mist = lerp(float3(.09, .14, .18), float3(.35, .31, .24), saturate(localLight + center.heat * .72));
                        color = lerp(color, mist, plume * (.52 + .38 * curl) * (.6 + ambient * .4));
                    }
                    float flame = Flame(center, down, p, local);
                    float3 spectrum = lerp(float3(.66, .082, .009), float3(1.20, .82, .17), flame * flame);
                    color = lerp(color, spectrum, flame * .92);
                }
                return float4(color, 1);
            }
            ENDHLSL
        }
    }
}

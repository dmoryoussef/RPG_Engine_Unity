Shader "WorldGrid/TileReliefNoiseUnlit"
{
    Properties
    {
        _MainTex ("Atlas Texture", 2D) = "white" {}

        // Core relief lighting (no Unity lights required)
        _ReliefStrength ("Relief Strength", Range(0, 10)) = 4.0
        _Ambient ("Ambient", Range(0, 1)) = 0.65
        _FakeLightDir ("Fake Light Direction", Vector) = (0.35, 0.9, 0.25, 0)

        // World-space noise controls
        _NoiseScale ("Noise Scale (world)", Range(0.1, 50)) = 6.0
        _NoiseOctaves ("Noise Octaves", Range(1, 6)) = 3
        _NoisePersistence ("Noise Persistence", Range(0.1, 0.9)) = 0.5
        _NoiseLacunarity ("Noise Lacunarity", Range(1.2, 4.0)) = 2.0

        // Height shaping
        _HeightBias ("Height Bias", Range(-1, 1)) = 0.0
        _HeightContrast ("Height Contrast", Range(0.1, 3.0)) = 1.0

        // Terrain-like depth
        _CurvatureAO ("Curvature AO Strength", Range(0, 1)) = 0.45
        _CurvatureAOGain ("Curvature AO Gain", Range(0.1, 10)) = 2.5

        // Tile-structured depth
        _TileEdgeAO ("Tile Edge AO Strength", Range(0, 1)) = 0.30
        _TileEdgeWidth ("Tile Edge Width (world)", Range(0.01, 0.5)) = 0.07
        _TileSize ("Tile Size (world units)", Range(0.1, 5)) = 1.0
    }

    SubShader
    {
        Tags { "Queue"="Geometry" "RenderType"="Opaque" "IgnoreProjector"="True" }
        Cull Off
        ZWrite On
        ZTest LEqual
        Blend One Zero

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;

            float _ReliefStrength;
            float _Ambient;
            float4 _FakeLightDir;

            float _NoiseScale;
            float _NoiseOctaves;
            float _NoisePersistence;
            float _NoiseLacunarity;

            float _HeightBias;
            float _HeightContrast;

            float _CurvatureAO;
            float _CurvatureAOGain;

            float _TileEdgeAO;
            float _TileEdgeWidth;
            float _TileSize;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
                float4 color  : COLOR;
            };

            struct v2f
            {
                float4 pos      : SV_POSITION;
                float2 uv       : TEXCOORD0;
                float4 color    : COLOR;
                float3 worldPos : TEXCOORD1;
            };

            // ----- Cheap hash/value-noise (fast, deterministic) -----
            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float valueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);

                float2 u = f * f * (3.0 - 2.0 * f);

                float a = hash21(i + float2(0, 0));
                float b = hash21(i + float2(1, 0));
                float c = hash21(i + float2(0, 1));
                float d = hash21(i + float2(1, 1));

                float x1 = lerp(a, b, u.x);
                float x2 = lerp(c, d, u.x);
                return lerp(x1, x2, u.y);
            }

            float fbm(float2 p, int octaves, float persistence, float lacunarity)
            {
                float amp = 1.0;
                float freq = 1.0;
                float sum = 0.0;
                float norm = 0.0;

                [unroll(6)]
                for (int o = 0; o < 6; o++)
                {
                    if (o >= octaves) break;
                    sum += amp * valueNoise(p * freq);
                    norm += amp;
                    amp *= persistence;
                    freq *= lacunarity;
                }

                return (norm > 0.0) ? (sum / norm) : sum;
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            float heightAt(float2 worldXY)
            {
                // Larger _NoiseScale => larger features (lower frequency).
                float2 p = worldXY / max(_NoiseScale, 0.0001);

                int oct = (int)round(_NoiseOctaves);
                oct = clamp(oct, 1, 6);

                float h = fbm(p, oct, _NoisePersistence, _NoiseLacunarity);

                // Shape to help readability
                h = (h + _HeightBias);
                h = (h - 0.5) * _HeightContrast + 0.5;

                return saturate(h);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv) * i.color;

                // Mesh is on XY plane; Z is layer depth.
                float2 p = float2(i.worldPos.x, i.worldPos.y);

                // Sample step in world units. For cellSize=1, 0.25 reads well.
                float eps = 0.25;

                // Heights
                float hC = heightAt(p);
                float hL = heightAt(p - float2(eps, 0));
                float hR = heightAt(p + float2(eps, 0));
                float hD = heightAt(p - float2(0, eps));
                float hU = heightAt(p + float2(0, eps));

                // --- Curvature AO (terrain-like depth) ---
                if (_CurvatureAO > 0.0)
                {
                    float lap = (hL + hR + hD + hU - 4.0 * hC);
                    float aoCurv = 1.0 - saturate(abs(lap) * _CurvatureAOGain);
                    col.rgb *= lerp(1.0, aoCurv, _CurvatureAO);
                }

                // --- Tile Edge AO (structured tiles / grout) ---
                if (_TileEdgeAO > 0.0)
                {
                    float ts = max(_TileSize, 0.0001);
                    float2 tileUV = frac(p / ts);

                    float edge = min(min(tileUV.x, 1.0 - tileUV.x), min(tileUV.y, 1.0 - tileUV.y));
                    float w = max(_TileEdgeWidth / ts, 0.0001);
                    float edgeMask = smoothstep(0.0, w, edge);

                    float aoEdge = lerp(0.75, 1.0, edgeMask);

                    // Optionally scale edge AO with relief strength (keeps it subtle when relief is off)
                    float edgeStrength = _TileEdgeAO * saturate(_ReliefStrength / 4.0);
                    col.rgb *= lerp(1.0, aoEdge, edgeStrength);
                }

                // Relief lighting (skip if strength is 0)
                if (_ReliefStrength > 0.0)
                {
                    // Proper central-difference derivative (critical)
                    float ddx = (hR - hL) / (2.0 * eps);
                    float ddy = (hU - hD) / (2.0 * eps);

                    float3 n = normalize(float3(-ddx * _ReliefStrength, -ddy * _ReliefStrength, 1.0));
                    float3 L = normalize(_FakeLightDir.xyz);

                    // Wrap a bit so top-down reads softer (less harsh "normal map" feel)
                    float ndl = dot(n, L);
                    ndl = saturate(ndl * 0.7 + 0.3);

                    float shade = lerp(_Ambient, 1.0, ndl);
                    col.rgb *= shade;
                }

                return col;
            }
            ENDCG
        }
    }
}

Shader "Custom/GrassInstanced_BuiltIn"
{
    Properties
    {
        _MainTex ("Albedo (RGBA)", 2D) = "white" {}
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.5
        _Color ("Grass Tint", Color) = (0.35, 0.75, 0.35, 1)

        // Wind field
        _WindAmp ("Wind Amplitude", Range(0,1)) = 0.25
        _WindFreq ("Wind Frequency", Range(0,8)) = 2
        _WindDir ("Wind Direction", Vector) = (1,0,0,0)
        _WindWorldScale ("Wind World Scale", Range(0.001, 0.2)) = 0.03

        // Bend shaping
        _BaseStiffness ("Base Stiffness", Range(0,0.6)) = 0.2
        _BendExponent ("Bend Exponent", Range(1,6)) = 3

        // easy tuning knobs
        _SwayStrength ("Sway Strength", Range(0,2)) = 1
        _TipBoost ("Tip Boost", Range(0,2)) = 0.5

        // Patch tint
        _PatchColorA ("Patch Color A", Color) = (0.34, 0.78, 0.32, 1)
        _PatchColorB ("Patch Color B", Color) = (0.16, 0.48, 0.18, 1)
        _PatchScale ("Patch Scale", Range(0.001, 0.2)) = 0.04
        _PatchStrength ("Patch Strength", Range(0, 1)) = 0.45

        _BaseTint ("Base Tint", Color) = (0.12, 0.30, 0.12, 1)
        _TipTint  ("Tip Tint",  Color) = (0.45, 0.95, 0.45, 1)

        _GradientStrength ("Blade Gradient Strength", Range(0,1)) = 1
        _GradientExponent ("Blade Gradient Exponent", Range(0.5, 6)) = 2.5


        // 0 = XY (Unity 2D default), 1 = XZ (3D ground plane)
        _UseXZPlane ("Use XZ Plane", Range(0,1)) = 0

        _EmissionStrength ("Emission Strength", Range(0,1)) = 1

    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Overlay" }
        LOD 200
        Cull Off

        // painter-style ordering
        ZWrite Off
        ZTest Always

        CGPROGRAM
        #pragma surface surf Lambert vertex:vert addshadow
        #pragma target 3.0

        sampler2D _MainTex;
        half _Cutoff;
        fixed4 _Color;

        fixed4 _BaseTint;
        fixed4 _TipTint;
        float _GradientStrength;
        float _GradientExponent;

        float _WindAmp;
        float _WindFreq;
        float4 _WindDir;
        float _WindWorldScale;

        float _BaseStiffness;
        float _BendExponent;

        float _SwayStrength;
        float _TipBoost;

        fixed4 _PatchColorA;
        fixed4 _PatchColorB;
        float _PatchScale;
        float _PatchStrength;

        float _UseXZPlane;

        int _InfluencerCount;
        float4 _Influencers[32];
        float  _InfluencerStrength[32];

        float _EmissionStrength;

        struct Input
        {
            float2 uv_MainTex;
            float3 worldPos;
        };

        float hash11(float n) { return frac(sin(n) * 43758.5453); }

        float valueNoise2D(float2 p)
        {
            float2 i = floor(p);
            float2 f = frac(p);

            float a = hash11(dot(i, float2(127.1, 311.7)));
            float b = hash11(dot(i + float2(1,0), float2(127.1, 311.7)));
            float c = hash11(dot(i + float2(0,1), float2(127.1, 311.7)));
            float d = hash11(dot(i + float2(1,1), float2(127.1, 311.7)));

            float2 u = f * f * (3.0 - 2.0 * f);
            return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
        }

        float2 WorldPlane2D(float3 wp)
        {
            return (_UseXZPlane > 0.5) ? wp.xz : wp.xy;
        }

        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.uv_MainTex = v.texcoord.xy;

            float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;

            // Base bend weight from height (local +Y)
            float hn = saturate(v.vertex.y);

            // Stiff base, flexible tip
            float h = saturate((hn - _BaseStiffness) / max(1e-4, (1.0 - _BaseStiffness)));
            h = pow(h, _BendExponent);

            // NEW: master sway multiplier + extra tip looseness
            float sway = _SwayStrength * lerp(1.0, 1.0 + _TipBoost, h);

            // Coherent wind
            float2 wp2 = WorldPlane2D(wp) * _WindWorldScale;
            float n = valueNoise2D(wp2);
            float phase = n * 6.2831853;

            float gust1 = sin((_Time.y * _WindFreq) + phase);
            float gust2 = sin((_Time.y * (_WindFreq * 0.35)) + phase * 0.6);
            float w = (gust1 * 0.7) + (gust2 * 0.3);

            float3 windDir = normalize(_WindDir.xyz + 1e-5);
            float3 windOffset = windDir * (w * _WindAmp) * h * sway;

            // Influencer push
            float3 push = 0;
            int count = clamp(_InfluencerCount, 0, 32);

            for (int i = 0; i < count; i++)
            {
                float3 ip = _Influencers[i].xyz;
                float radius = _Influencers[i].w;
                float strength = _InfluencerStrength[i];

                float3 d = wp - ip;
                float dist = length(d);
                if (dist < radius && dist > 1e-4)
                {
                    float t = 1.0 - saturate(dist / radius);
                    t = t * t * (3.0 - 2.0 * t);
                    push += (d / dist) * (t * strength) * h * sway;
                }
            }

            float3 total = windOffset + push;

            float3 localOffset = mul(unity_WorldToObject, float4(total, 0)).xyz;
            v.vertex.xyz += localOffset;

            o.worldPos = wp + total;
        }

        void surf(Input IN, inout SurfaceOutput o)
        {
            fixed4 tex = tex2D(_MainTex, IN.uv_MainTex);

            float2 pc = WorldPlane2D(IN.worldPos) * _PatchScale;

            float p1 = valueNoise2D(pc);
            float p2 = valueNoise2D(pc * 2.0);
            float patch = saturate(p1 * 0.7 + p2 * 0.3);

            fixed3 patchColor = lerp(_PatchColorA.rgb, _PatchColorB.rgb, patch);

            fixed3 base = tex.rgb * _Color.rgb;
            fixed3 patched = base * patchColor;

            fixed3 outRgb = lerp(base, patched, _PatchStrength);

            float bn = valueNoise2D(pc * 4.0);
            float brighten = lerp(0.99, 1.01, bn);
            outRgb *= brighten;

            clip(tex.a - _Cutoff);


            // --- Vertical blade gradient (base -> tip) ---
            float bladeH = saturate(IN.uv_MainTex.y);
            bladeH = pow(bladeH, _GradientExponent);

            // Blend between base and tip tint
            float3 bladeTint = lerp(_BaseTint.rgb, _TipTint.rgb, bladeH);

            // Apply gradient to the already-patched final color
            outRgb = lerp(outRgb, outRgb * bladeTint, _GradientStrength);


            o.Albedo = outRgb;
            o.Alpha = tex.a;
            // Make shadows visible when EmissionStrength < 1
            o.Emission = outRgb * _EmissionStrength;

        }
        ENDCG
    }

    FallBack "Diffuse"
}

Shader "Custom/GrassInstanced_BuiltIn"
{
    Properties
    {
        _MainTex ("Albedo (RGBA)", 2D) = "white" {}
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.5
        _Color ("Grass Tint", Color) = (0.35, 0.75, 0.35, 1)

        _WindAmp ("Wind Amplitude", Range(0,1)) = 0.25
        _WindFreq ("Wind Frequency", Range(0,8)) = 2
        _WindDir ("Wind Direction", Vector) = (1,0,0,0)
        _WindWorldScale ("Wind World Scale", Range(0.001, 0.2)) = 0.03

        _PatchColorA ("Patch Color A", Color) = (0.30, 0.70, 0.30, 1)
        _PatchColorB ("Patch Color B", Color) = (0.20, 0.55, 0.20, 1)
        _PatchScale ("Patch Scale", Range(0.001, 0.2)) = 0.02
        _PatchStrength ("Patch Strength", Range(0, 1)) = 0.35

        // 0 = XY (Unity 2D default), 1 = XZ (3D ground plane)
        _UseXZPlane ("Use XZ Plane", Range(0,1)) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Overlay" }
        LOD 200
        Cull Off

        // Painter-style ordering (your renderer sorts by Y)
        ZWrite Off
        ZTest Always

        CGPROGRAM
        #pragma surface surf Lambert vertex:vert addshadow
        #pragma target 3.0

        sampler2D _MainTex;
        half _Cutoff;
        fixed4 _Color;

        float _WindAmp;
        float _WindFreq;
        float4 _WindDir;
        float _WindWorldScale;

        fixed4 _PatchColorA;
        fixed4 _PatchColorB;
        float _PatchScale;
        float _PatchStrength;

        float _UseXZPlane;

        int _InfluencerCount;
        float4 _Influencers[32];          // xyz + radius
        float  _InfluencerStrength[32];

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
            // choose XY or XZ based on _UseXZPlane
            return (_UseXZPlane > 0.5) ? wp.xz : wp.xy;
        }

        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.uv_MainTex = v.texcoord.xy;

            float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;

            // Assumes grass height is local +Y
            float h = saturate(v.vertex.y);

            // --- Coherent world wind ---
            float2 wp2 = WorldPlane2D(wp) * _WindWorldScale;
            float n = valueNoise2D(wp2);
            float phase = n * 6.2831853;

            float gust1 = sin((_Time.y * _WindFreq) + phase);
            float gust2 = sin((_Time.y * (_WindFreq * 0.35)) + phase * 0.6);
            float w = (gust1 * 0.7) + (gust2 * 0.3);

            float3 windDir = normalize(_WindDir.xyz + 1e-5);
            float3 windOffset = windDir * (w * _WindAmp) * h;

            // --- Influencer push (optional) ---
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
                    push += (d / dist) * (t * strength) * h;
                }
            }

            float3 total = windOffset + push;

            float3 localOffset = mul(unity_WorldToObject, float4(total, 0)).xyz;
            v.vertex.xyz += localOffset;

            o.worldPos = wp + total;
        }

        void surf(Input IN, inout SurfaceOutput o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex);

            // --- Low-frequency patch tint (cohesion) ---
            float2 pcoord = WorldPlane2D(IN.worldPos) * _PatchScale;
            float p1 = valueNoise2D(pcoord);
            float p2 = valueNoise2D(pcoord * 2.0);
            float patch = saturate(p1 * 0.7 + p2 * 0.3);

            fixed3 patchTint = lerp(_PatchColorA.rgb, _PatchColorB.rgb, patch);

            // Stronger, more perceptible patching:
            c.rgb = lerp(c.rgb, c.rgb * patchTint, _PatchStrength);


            // tiny brighten noise (optional subtle)
            float bn = valueNoise2D(pcoord * 4.0);
            float brighten = lerp(0.985, 1.015, bn);

            c.rgb *= _Color.rgb * brighten;

            clip(c.a - _Cutoff);

            o.Albedo = c.rgb;
            o.Alpha = c.a;

            // Self-lit for stable top-down look
            o.Emission = c.rgb;
        }
        ENDCG
    }

    FallBack "Diffuse"
}

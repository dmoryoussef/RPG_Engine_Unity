Shader "Custom/Grass_Clean_UnlitSurface"
{
    Properties
    {
        _MainTex ("Albedo (RGBA)", 2D) = "white" {}
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.5
        _Color ("Grass Tint", Color) = (0.35, 0.75, 0.35, 1)

        _WindAmp ("Wind Amplitude", Range(0,1)) = 0.25
        _WindFreq ("Wind Frequency", Range(0,8)) = 2
        _WindDir ("Wind Direction", Vector) = (1,0,0,0)
    }

    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
        LOD 200
        Cull Off

        CGPROGRAM
        #pragma surface surf Lambert vertex:vert addshadow
        #pragma target 3.0

        sampler2D _MainTex;
        half _Cutoff;
        fixed4 _Color;

        float _WindAmp;
        float _WindFreq;
        float4 _WindDir;

        int _InfluencerCount;
        float4 _Influencers[32];
        float  _InfluencerStrength[32];

        struct Input
        {
            float2 uv_MainTex;
            float3 worldPos;
        };

        // Low-frequency stable noise
        float hash21(float2 p)
        {
            p = frac(p * float2(123.34, 456.21));
            p += dot(p, p + 34.345);
            return frac(p.x * p.y);
        }

        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.uv_MainTex = v.texcoord.xy;

            float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;

            // assumes grass grows along +Y
            float h = saturate(v.vertex.y);

            // wind
            float2 nPos = wp.xz * 0.15 + wp.xy * 0.05;
            float n = hash21(nPos);
            float w = sin((_Time.y * _WindFreq) + n * 6.28318);

            float3 windDir = normalize(_WindDir.xyz + 1e-5);
            float3 windOffset = windDir * (w * _WindAmp) * h;

            // influencer push
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

            // very subtle patch variation
            float n = hash21(IN.worldPos.xz * 0.08);
            float brighten = lerp(0.97, 1.03, n);

            c.rgb *= _Color.rgb * brighten;

            clip(c.a - _Cutoff);

            o.Albedo = c.rgb;
            o.Alpha = c.a;

            // 👇 THIS is the key: self-lit grass
            o.Emission = c.rgb;
        }
        ENDCG
    }

    FallBack "Diffuse"
}

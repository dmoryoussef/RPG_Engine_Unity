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

        // Height bend profile
        _BaseStiffness ("Base Stiffness", Range(0,1)) = 0.2
        _BendExponent ("Bend Exponent", Range(0.2,8)) = 2.0

        // Lateral sway profile
        _SwayStrength ("Sway Strength", Range(0,1)) = 0.6
        _TipBoost ("Tip Boost", Range(0,3)) = 1.0

        // World scale for coherent gusts
        _WindWorldScale ("Wind World Scale", Range(0.02,4)) = 1.0

        // XZ plane support (when generating meshes on XZ instead of XY)
        _UseXZPlane ("Use XZ Plane", Float) = 0
    }

    SubShader
    {
        Tags { "Queue"="AlphaTest" "RenderType"="TransparentCutout" "IgnoreProjector"="True" }
        LOD 200
        Cull Off

        // For cutout you typically want depth writes ON so it sorts correctly.
        ZWrite On

        CGPROGRAM
        #pragma surface surf Lambert vertex:vert addshadow
        #pragma target 3.0
        #pragma multi_compile_instancing
        #pragma instancing_options assumeuniformscaling

        sampler2D _MainTex;
        fixed4 _Color;
        float _Cutoff;

        float _WindAmp;
        float _WindFreq;
        float4 _WindDir;

        float _BaseStiffness;
        float _BendExponent;

        float _SwayStrength;
        float _TipBoost;

        float _WindWorldScale;
        float _UseXZPlane;

        UNITY_INSTANCING_BUFFER_START(Props)
            // (no per-instance props currently)
        UNITY_INSTANCING_BUFFER_END(Props)

        // IMPORTANT: custom appdata that carries instance ID (fixes "72 instances but 1 visible")
        struct appdata
        {
            float4 vertex   : POSITION;
            float3 normal   : NORMAL;
            float4 tangent  : TANGENT;
            float2 texcoord : TEXCOORD0;
            float4 color    : COLOR;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct Input
        {
            float2 uv_MainTex;
            float3 worldPos;
            UNITY_VERTEX_INPUT_INSTANCE_ID
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
            return lerp(a, b, u.x) + (c - a) * u.y * (1.0 - u.x) + (d - b) * u.x * u.y;
        }

        float2 WorldPlane2D(float3 wp)
        {
            // If mesh is XY plane, use world XY. If mesh is XZ plane, use world XZ.
            return (_UseXZPlane >= 0.5) ? float2(wp.x, wp.z) : float2(wp.x, wp.y);
        }

        void vert(inout appdata v, out Input o)
        {
            UNITY_SETUP_INSTANCE_ID(v);
            UNITY_INITIALIZE_OUTPUT(Input, o);
            UNITY_TRANSFER_INSTANCE_ID(v, o);

            o.uv_MainTex = v.texcoord.xy;

            float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;

            // Base bend weight from height (local +Y)
            float hn = saturate(v.vertex.y);

            // Stiff base, flexible tip
            float h = saturate((hn - _BaseStiffness) / max(1e-4, (1.0 - _BaseStiffness)));
            h = pow(h, _BendExponent);

            // master sway multiplier + extra tip looseness
            float sway = _SwayStrength * lerp(1.0, 1.0 + _TipBoost, h);

            // Coherent wind
            float2 wp2 = WorldPlane2D(wp) * _WindWorldScale;
            float gust = valueNoise2D(wp2 + _Time.y * 0.25);

            float2 dir = normalize(_WindDir.xy);
            float windPhase = dot(wp2, dir) * _WindFreq + _Time.y * _WindFreq;

            float wind = sin(windPhase) * 0.5 + 0.5;
            wind = lerp(wind, gust, 0.5);

            float amp = _WindAmp * sway * wind;

            // Bend direction in world-plane
            float2 bend2 = dir * amp;

            // Apply bend in object space by offsetting vertex in plane direction
            // If mesh is XY plane: offset X/Y. If XZ plane: offset X/Z.
            if (_UseXZPlane >= 0.5)
            {
                v.vertex.x += bend2.x;
                v.vertex.z += bend2.y;
            }
            else
            {
                v.vertex.x += bend2.x;
                v.vertex.y += bend2.y;
            }

            // Store worldPos after deformation
            o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
        }

        void surf(Input IN, inout SurfaceOutput o)
        {
            UNITY_SETUP_INSTANCE_ID(IN);

            fixed4 tex = tex2D(_MainTex, IN.uv_MainTex);

            // Cutout alpha
            clip(tex.a - _Cutoff);

            fixed4 c = tex * _Color;

            o.Albedo = c.rgb;
            o.Alpha = tex.a;
        }
        ENDCG
    }

    FallBack "Transparent/Cutout/Diffuse"
}

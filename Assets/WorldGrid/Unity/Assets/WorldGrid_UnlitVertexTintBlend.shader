Shader "WorldGrid/Unlit Vertex Tint Blend"
{
    Properties
    {
        _MainTex ("Main Tex", 2D) = "white" {}
        _BaseMap ("Base Map (Compat)", 2D) = "white" {}
        _Color ("Global Color", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            sampler2D _BaseMap;
            fixed4 _Color;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
                fixed4 color  : COLOR;
            };

            struct v2f
            {
                float4 pos   : SV_POSITION;
                float2 uv    : TEXCOORD0;
                fixed4 color : COLOR;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 a = tex2D(_MainTex, i.uv);
                fixed4 b = tex2D(_BaseMap, i.uv);

                // Use _BaseMap if _MainTex looks unbound/white
                fixed useBase = step(0.999, a.r) * step(0.999, a.g) * step(0.999, a.b);
                fixed4 tex = lerp(a, b, useBase);

                fixed strength = i.color.a;      // <-- blend
                fixed3 tint = i.color.rgb;

                fixed3 tinted = tex.rgb * tint;
                fixed3 outRgb = lerp(tex.rgb, tinted, strength);

                return fixed4(outRgb, tex.a);
            }
            ENDCG
        }
    }
}

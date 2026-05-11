Shader "ElectroOptics/DotTracking"
{
    Properties
    {
        _DotRadius ("Dot Radius", Range(0.01, 0.2)) = 0.05
        _DotBrightness ("Dot Brightness", Range(0.5, 3)) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            float4 _HitUV;
            float _DotIntensity;
            float _DotRadius;
            float _DotBrightness;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float dist = length(i.uv - _HitUV.xy);
                float soft = 1.0 - smoothstep(0, _DotRadius, dist);
                float3 bg = float3(0.0, 0.0, 0.0);
                float3 fg = float3(1.0, 0.0, 0.0);
                float effective = _DotIntensity * _DotBrightness;
                float3 color = lerp(bg, fg, saturate(effective) * soft);
                return float4(color, 1.0);
            }
            ENDCG
        }
    }
}

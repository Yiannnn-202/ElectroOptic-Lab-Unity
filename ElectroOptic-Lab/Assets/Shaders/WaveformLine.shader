Shader "UI/WaveformLine"
{
    Properties
    {
        [PerRendererData] _MainTex ("Wave Data", 2D) = "black" {}
        _LineColor ("Line Color", Color) = (0.2, 0.85, 1, 1)
        _LineWidth ("Line Width (px)", Range(0.5, 8)) = 3
        _GlowWidth ("Glow Width (px)", Range(2, 60)) = 20
        _Intensity ("Intensity", Range(0.5, 3)) = 1.5

        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15
        [HideInInspector] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        ColorMask [_ColorMask]
        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                float4 worldPos : TEXCOORD1;
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            fixed4 _LineColor;
            float _LineWidth;
            float _GlowWidth;
            float _Intensity;
            float4 _ClipRect;

            v2f vert(appdata v)
            {
                v2f o;
                o.worldPos = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // RectMask2D clipping
                #ifdef UNITY_UI_CLIP_RECT
                i.color.a *= UnityGet2DClipping(i.worldPos.xy, _ClipRect);
                #endif

                float x = i.uv.x;
                float y = i.uv.y;

                // Sample waveform Y at current X (bilinear filtering = smooth interpolation)
                float waveY = tex2D(_MainTex, float2(x, 0.5)).r;

                // Vertical distance to waveform line (UV space)
                float dist = abs(y - waveY);

                // Screen-space pixel size for resolution-independent rendering
                float pixelSize = fwidth(y);

                // Core line: sharp anti-aliased edge
                float halfLinePx = _LineWidth * 0.5;
                float halfLineUv = halfLinePx * pixelSize;
                float aaWidth = pixelSize * 1.2;
                float lineAlpha = 1.0 - smoothstep(halfLineUv - aaWidth, halfLineUv + aaWidth, dist);

                // Outer glow: exponential falloff from line edge
                float glowDist = max(0.0, dist - halfLineUv);
                float glowUv = _GlowWidth * pixelSize;
                float glowAlpha = glowUv > 0.0001 ? exp(-glowDist / glowUv) : 0.0;

                // Combine: line is solid, glow fades out
                float alpha = saturate(lineAlpha + glowAlpha * 0.65) * _Intensity;
                // Boost color saturation in glow region
                float glowBoost = 1.0 + glowAlpha * 2.0;
                fixed3 rgb = _LineColor.rgb * glowBoost;

                return fixed4(rgb, saturate(alpha) * i.color.a);
            }
            ENDCG
        }
    }
    Fallback "UI/Default"
}

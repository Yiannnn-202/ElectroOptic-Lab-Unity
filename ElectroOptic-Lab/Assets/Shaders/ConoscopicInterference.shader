Shader "ElectroOptics/ConoscopicInterference"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1, 0, 0, 1)
        _FOV ("Field of View", Range(1, 179)) = 10.0
        
        // 内部驱动变量
        _RefractiveIndices ("Refractive Indices", Vector) = (2.286, 2.286, 2.200, 0)
        _CrystalLength ("Crystal Length", Float) = 0.02
        _Wavelength ("Wavelength", Float) = 0.000000633
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

            float4 _BaseColor;
            float _FOV;
            float3 _RefractiveIndices;
            float _CrystalLength;
            float _Wavelength;
            float4x4 _RotationMatrix;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            // 求解菲涅尔方程
            float2 SolveFresnel(float3 s, float3 n_principal)
            {
                float3 n2 = n_principal * n_principal;
                float3 A = 1.0 / (n2 + 1e-9);
                
                float sx2 = s.x * s.x;
                float sy2 = s.y * s.y;
                float sz2 = s.z * s.z;

                float a = 1.0;
                float b = -(sx2*(A.y + A.z) + sy2*(A.x + A.z) + sz2*(A.x + A.y));
                float c = sx2*(A.y * A.z) + sy2*(A.x * A.z) + sz2*(A.x * A.y);

                float delta = b*b - 4*a*c;
                if (delta < 0) return float2(0, 0);

                float sqrtDelta = sqrt(delta);
                float X1 = (-b + sqrtDelta) / (2 * a);
                float X2 = (-b - sqrtDelta) / (2 * a);

                return float2(1.0/sqrt(X1), 1.0/sqrt(X2));
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 p = (i.uv - 0.5) * 2.0;
                float halfSize = tan(radians(_FOV) * 0.5);
                float3 rayView = normalize(float3(p.x * halfSize, p.y * halfSize, 1.0));

                // 坐标逆变换
                float3 rayOptical = mul(rayView, (float3x3)_RotationMatrix);

                // 求解双折射
                float2 n_sols = SolveFresnel(rayOptical, _RefractiveIndices);
                float delta_n = abs(n_sols.x - n_sols.y);

                // 干涉计算
                float pathLength = _CrystalLength / rayView.z;
                float PI = 3.14159265359;
                float gamma = (2.0 * PI * pathLength * delta_n) / _Wavelength;

                // 黑十字
                float phi = atan2(p.y, p.x);
                float crossPattern = pow(sin(2.0 * phi), 2.0);
                if (length(p) < 0.01) crossPattern = 0;

                // 最终颜色
                float intensity = crossPattern * pow(sin(gamma * 0.5), 2.0);
                return _BaseColor * intensity;
            }
            ENDCG
        }
    }
}
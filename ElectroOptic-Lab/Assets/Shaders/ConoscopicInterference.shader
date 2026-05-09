Shader "ElectroOptics/ConoscopicInterference"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1, 0, 0, 1)
        _FOV ("Field of View", Range(1, 179)) = 10.0

        // Driven by the runtime renderer.
        _RefractiveIndices ("Refractive Indices", Vector) = (2.286, 2.286, 2.200, 0)
        [HideInInspector] _OpticAxisView ("Optic Axis View", Vector) = (0, 0, 1, 0)
        [HideInInspector] _BiaxialAxesView ("Biaxial Axes View", Vector) = (0, 0, 0, 0)
        _InitialMelatopeOffset ("Initial Melatope Offset", Vector) = (0.035, -0.025, 0, 0)
        _CrystalLength ("Crystal Length", Float) = 0.02
        _Wavelength ("Wavelength", Float) = 0.000000633
        _PhaseScale ("Phase Scale", Range(0.01, 5)) = 0.1

        // Display shaping only; the physical inputs above are still supplied by Scene2.
        _DisplayGamma ("Display Gamma", Range(0.2, 3)) = 1.25
        _BlackCutoff ("Black Cutoff", Range(0, 0.25)) = 0.012
        _RingSharpness ("Ring Sharpness", Range(0.25, 4)) = 1.0
        _CrossWidth ("Cross Width", Range(0.01, 0.35)) = 0.16
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
            #pragma target 3.0
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
            float _PhaseScale;
            float _DisplayGamma;
            float _BlackCutoff;
            float _RingSharpness;
            float _CrossWidth;
            float4x4 _RotationMatrix;
            float4 _OpticAxisView;
            float4 _BiaxialAxesView;
            float4 _InitialMelatopeOffset;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

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

            float3 GetOpticAxisView()
            {
                float3 opticAxisView = _OpticAxisView.xyz;
                float axisLengthSqr = dot(opticAxisView, opticAxisView);
                opticAxisView = axisLengthSqr > 1e-8 ? opticAxisView * rsqrt(axisLengthSqr) : float3(0.0, 0.0, 1.0);

                if (opticAxisView.z < 0.0)
                {
                    opticAxisView = -opticAxisView;
                }

                return opticAxisView;
            }

            float2 GetMelatopeOffset(float3 opticAxisView, float halfSize)
            {
                float axisZ = max(opticAxisView.z, 0.05);
                float projectionScale = axisZ * max(halfSize, 0.0001);
                return opticAxisView.xy / projectionScale;
            }

            float3 GetAxisFromMelatopeOffset(float2 melatopeOffset, float halfSize)
            {
                return normalize(float3(melatopeOffset.x * halfSize, melatopeOffset.y * halfSize, 1.0));
            }

            float GetExtinctionPattern(float3 rayView, float3 opticAxisView, float2 localP)
            {
                float3 projectedAxis = opticAxisView - rayView * dot(opticAxisView, rayView);
                float2 polarizationDirection = projectedAxis.xy;
                float directionLengthSqr = dot(polarizationDirection, polarizationDirection);

                if (directionLengthSqr < 1e-8)
                {
                    polarizationDirection = localP;
                    directionLengthSqr = dot(polarizationDirection, polarizationDirection);
                }

                float2 direction = polarizationDirection * rsqrt(max(directionLengthSqr, 1e-8));
                float crossSignal = 2.0 * direction.x * direction.y;
                float crossPower = lerp(0.8, 2.4, saturate(_CrossWidth / 0.35));
                float crossPattern = pow(saturate(crossSignal * crossSignal), crossPower);
                float melatope = smoothstep(0.025, 0.14, length(localP));

                return crossPattern * melatope;
            }

            float3 GetBiaxialAxisView(float2 axisXY)
            {
                float zSqr = max(1.0 - dot(axisXY, axisXY), 0.0025);
                return normalize(float3(axisXY.x, axisXY.y, sqrt(zSqr)));
            }

            float2 ClampBiaxialMelatopeOffset(float2 offset)
            {
                float maxRadius = 0.42;
                float offsetRadius = length(offset);
                if (offsetRadius > maxRadius)
                {
                    return offset * (maxRadius / max(offsetRadius, 0.0001));
                }

                return offset;
            }

            float GetBiaxialExtinctionPattern(float3 rayView, float halfSize, float2 p)
            {
                float hasAxes = step(0.000001, dot(_BiaxialAxesView, _BiaxialAxesView));
                if (hasAxes <= 0.0)
                {
                    float3 opticAxisView = GetOpticAxisView();
                    float2 melatopeOffset = GetMelatopeOffset(opticAxisView, halfSize) + _InitialMelatopeOffset.xy;
                    float2 localP = p - melatopeOffset;
                    float3 extinctionAxisView = GetAxisFromMelatopeOffset(melatopeOffset, halfSize);
                    return GetExtinctionPattern(rayView, extinctionAxisView, localP);
                }

                float3 axisA = GetBiaxialAxisView(_BiaxialAxesView.xy);
                float3 axisB = GetBiaxialAxisView(_BiaxialAxesView.zw);
                float2 offsetA = ClampBiaxialMelatopeOffset(GetMelatopeOffset(axisA, halfSize)) + _InitialMelatopeOffset.xy;
                float2 offsetB = ClampBiaxialMelatopeOffset(GetMelatopeOffset(axisB, halfSize)) + _InitialMelatopeOffset.xy;
                float3 displayAxisA = GetAxisFromMelatopeOffset(offsetA - _InitialMelatopeOffset.xy, halfSize);
                float3 displayAxisB = GetAxisFromMelatopeOffset(offsetB - _InitialMelatopeOffset.xy, halfSize);
                float patternA = GetExtinctionPattern(rayView, displayAxisA, p - offsetA);
                float patternB = GetExtinctionPattern(rayView, displayAxisB, p - offsetB);

                return min(patternA, patternB);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 p = (i.uv - 0.5) * 2.0;
                float radius = length(p);
                float aperture = 1.0 - smoothstep(0.96, 1.0, radius);
                if (aperture <= 0.0)
                {
                    return float4(1.0, 1.0, 1.0, 1.0);
                }

                float halfSize = max(tan(radians(_FOV) * 0.5), 0.0001);
                float3 rayView = normalize(float3(p.x * halfSize, p.y * halfSize, 1.0));

                float3 rayOptical = mul(rayView, (float3x3)_RotationMatrix);

                float2 n_sols = SolveFresnel(rayOptical, _RefractiveIndices);
                float delta_n = abs(n_sols.x - n_sols.y);

                float pathLength = _CrystalLength / rayView.z;
                float PI = 3.14159265359;
                float gamma = ((2.0 * PI * pathLength * delta_n) / _Wavelength) * _PhaseScale;

                float crossPattern = GetBiaxialExtinctionPattern(rayView, halfSize, p);

                float phase = gamma * 0.5;
                float gammaWidth = max(fwidth(gamma), 0.0001);
                float visibility = exp2((-0.75 * gammaWidth * gammaWidth) / max(_RingSharpness, 0.0001));
                float ringPattern = 0.5 - 0.5 * cos(2.0 * phase) * saturate(visibility);

                float intensity = saturate(crossPattern * ringPattern * aperture);
                intensity = smoothstep(_BlackCutoff, 1.0, intensity);
                intensity = pow(intensity, 1.0 / max(_DisplayGamma, 0.0001));

                float3 color = lerp(float3(1.0, 1.0, 1.0), _BaseColor.rgb, intensity);
                return float4(color, 1.0);
            }
            ENDCG
        }
    }
}

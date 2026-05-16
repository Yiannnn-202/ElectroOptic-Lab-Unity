Shader "ElectroOptics/ConoscopicJonesIntensity"
{
    Properties
    {
        _WavelengthM ("Wavelength (m)", Float) = 0.0000006328
        _ThicknessM ("Thickness (m)", Float) = 0.02
        _OrdinaryIndexNo ("Ordinary Index no", Float) = 2.286
        _ExtraordinaryIndexNe ("Extraordinary Index ne", Float) = 2.200
        _PrincipalIndices ("Principal Indices", Vector) = (2.286, 2.286, 2.200, 0)
        _ScreenDistanceM ("Screen Distance (m)", Float) = 0.7
        _ScreenHalfSizeM ("Screen Half Size (m)", Float) = 0.08
        _InitialIntensity ("Initial Intensity", Float) = 1.0
        _PhaseAntiAliasStrength ("Phase Anti Alias Strength", Float) = 1.0
        _PolarizerAngleRad ("Polarizer Angle (rad)", Float) = 0.0
        _AnalyzerAngleRad ("Analyzer Angle (rad)", Float) = 1.5707963
        _CrystalAxisAngleRad ("Crystal Axis Angle (rad)", Float) = 0.7853982
        _OpticAxisTiltRad ("Optic Axis Tilt (rad)", Float) = 0.0
        _OpticAxisAzimuthRad ("Optic Axis Azimuth (rad)", Float) = 0.0
        _ElectricFieldStrength ("Electric Field Strength", Float) = 0.0
        _ElectroOpticCoefficientR22 ("Electro Optic r22", Float) = 0.0
        _ApertureRadius ("Aperture Radius", Float) = 1.0
        [HideInInspector] _BiaxialAxesView ("Biaxial Axes View", Vector) = (0, 0, 0, 0)
        _InitialMelatopeOffset ("Initial Melatope Offset", Vector) = (0.035, -0.025, 0, 0)
        _PhaseScale ("Biaxial Phase Scale", Range(0.01, 5)) = 0.1
        _RingSharpness ("Ring Sharpness", Range(0.25, 4)) = 1.0
        _CrossWidth ("Cross Width", Range(0.01, 0.35)) = 0.16
        _BlackCutoff ("Black Cutoff", Range(0, 0.25)) = 0.012
        _DisplayGamma ("Display Gamma", Range(0.2, 3)) = 1.25
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            float _WavelengthM;
            float _ThicknessM;
            float _OrdinaryIndexNo;
            float _ExtraordinaryIndexNe;
            float3 _PrincipalIndices;
            float4x4 _WorldToPrincipalMatrix;
            float _UseBiaxial;
            float _BiaxialDisplayMode;
            float4 _BiaxialAxesView;
            float4 _InitialMelatopeOffset;
            float _PhaseScale;
            float _RingSharpness;
            float _CrossWidth;
            float _BlackCutoff;
            float _DisplayGamma;
            float _UniaxialEpsilon;
            float _ScreenDistanceM;
            float _ScreenHalfSizeM;
            float _InitialIntensity;
            float _PhaseAntiAliasStrength;
            float _PolarizerAngleRad;
            float _AnalyzerAngleRad;
            float _CrystalAxisAngleRad;
            float _OpticAxisTiltRad;
            float _OpticAxisAzimuthRad;
            float _ElectricFieldStrength;
            float _ElectroOpticCoefficientR22;
            float _ApertureRadius;

            float3 ProjectOntoWavefront(float3 direction, float3 rayDir)
            {
                return direction - rayDir * dot(direction, rayDir);
            }

            float3 SafeNormalizeOnWavefront(float3 direction, float3 rayDir, float3 fallback)
            {
                direction = ProjectOntoWavefront(direction, rayDir);
                float lenSqr = dot(direction, direction);
                if (lenSqr < 1e-8)
                {
                    direction = ProjectOntoWavefront(fallback, rayDir);
                    lenSqr = max(dot(direction, direction), 1e-8);
                }

                return direction * rsqrt(lenSqr);
            }

            float EffectiveExtraordinaryIndex(float noIndex, float neIndex, float cosTheta)
            {
                float sinThetaSqr = max(1.0 - cosTheta * cosTheta, 0.0);
                float denominator = sqrt(neIndex * neIndex * cosTheta * cosTheta + noIndex * noIndex * sinThetaSqr);
                return noIndex * neIndex / max(denominator, 1e-6);
            }

            float3 AngleVector(float angleRad, float3 rayDir)
            {
                float3 direction = float3(cos(angleRad), sin(angleRad), 0.0);
                return SafeNormalizeOnWavefront(direction, rayDir, float3(0.0, 1.0, 0.0));
            }

            float3 OpticAxis()
            {
                float sinTilt = sin(_OpticAxisTiltRad);
                return normalize(float3(
                    sinTilt * cos(_OpticAxisAzimuthRad),
                    sinTilt * sin(_OpticAxisAzimuthRad),
                    cos(_OpticAxisTiltRad)));
            }

            float3 CrystalReferenceAxis()
            {
                return normalize(float3(cos(_CrystalAxisAngleRad), sin(_CrystalAxisAngleRad), 0.0));
            }

            float WeightedDot(float3 a, float3 b, float3 weights)
            {
                return a.x * b.x * weights.x + a.y * b.y * weights.y + a.z * b.z * weights.z;
            }

            float2 SmallestEigenVector2(float a, float b, float d)
            {
                float root = sqrt(max((a - d) * (a - d) + 4.0 * b * b, 0.0));
                float lambda = (a + d - root) * 0.5;
                float2 eigenVec;
                if (abs(b) > 1e-6)
                {
                    eigenVec = float2(b, lambda - a);
                }
                else if (a <= d)
                {
                    eigenVec = float2(1.0, 0.0);
                }
                else
                {
                    eigenVec = float2(0.0, 1.0);
                }

                float lenSqr = dot(eigenVec, eigenVec);
                if (lenSqr > 1e-8)
                {
                    eigenVec *= rsqrt(lenSqr);
                }
                else
                {
                    eigenVec = float2(1.0, 0.0);
                }

                if (eigenVec.x < -1e-6 || (abs(eigenVec.x) <= 1e-6 && eigenVec.y < 0.0))
                {
                    eigenVec = -eigenVec;
                }

                return eigenVec;
            }

            bool SolveBiaxialFresnel(float3 rayPrincipal, float3 indices, out float n1, out float n2)
            {
                float3 n2Terms = max(indices * indices, 1e-6);
                float3 aTerms = 1.0 / n2Terms;
                float sx2 = rayPrincipal.x * rayPrincipal.x;
                float sy2 = rayPrincipal.y * rayPrincipal.y;
                float sz2 = rayPrincipal.z * rayPrincipal.z;
                float b = -(sx2 * (aTerms.y + aTerms.z)
                            + sy2 * (aTerms.x + aTerms.z)
                            + sz2 * (aTerms.x + aTerms.y));
                float c = sx2 * aTerms.y * aTerms.z
                          + sy2 * aTerms.x * aTerms.z
                          + sz2 * aTerms.x * aTerms.y;
                float discriminant = b * b - 4.0 * c;
                if (discriminant < 0.0)
                {
                    n1 = indices.x;
                    n2 = indices.y;
                    return false;
                }

                float root = sqrt(discriminant);
                float x1 = (-b + root) * 0.5;
                float x2 = (-b - root) * 0.5;
                if (x1 <= 1e-6 || x2 <= 1e-6)
                {
                    n1 = indices.x;
                    n2 = indices.y;
                    return false;
                }

                n1 = rsqrt(x1);
                n2 = rsqrt(x2);
                return n1 > 0.0 && n2 > 0.0 && abs(n1) < 10000.0 && abs(n2) < 10000.0;
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
                    float3 opticAxisView = OpticAxis();
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

            bool TryEvaluateBiaxialTeaching(float2 p, float3 rayDir, out float intensity)
            {
                intensity = 0.0;
                float3 indices = _PrincipalIndices;
                float minDiff = min(abs(indices.x - indices.y), min(abs(indices.y - indices.z), abs(indices.x - indices.z)));
                if (_UseBiaxial < 0.5 || minDiff < max(_UniaxialEpsilon, 1e-6))
                {
                    return false;
                }

                float halfSize = _ScreenHalfSizeM / max(_ScreenDistanceM, 1e-6);
                float3 rayView = normalize(float3(p.x * halfSize, p.y * halfSize, 1.0));
                float3 rayPrincipal = normalize(mul(rayDir, (float3x3)_WorldToPrincipalMatrix));
                float n1;
                float n2;
                if (!SolveBiaxialFresnel(rayPrincipal, indices, n1, n2))
                {
                    return false;
                }

                float wavelength = max(_WavelengthM, 1e-12);
                float pathLength = _ThicknessM / max(rayView.z, 0.05);
                float gamma = 6.28318530718 * pathLength * abs(n1 - n2) / wavelength * _PhaseScale;
                float gammaWidth = max(fwidth(gamma), 0.0001);
                float visibility = exp2((-0.75 * gammaWidth * gammaWidth) / max(_RingSharpness, 0.0001));
                float ringPattern = 0.5 - 0.5 * cos(gamma) * saturate(visibility);
                float crossPattern = GetBiaxialExtinctionPattern(rayView, halfSize, p);
                intensity = saturate(crossPattern * ringPattern);
                intensity = smoothstep(_BlackCutoff, 1.0, intensity);
                intensity = pow(intensity, 1.0 / max(_DisplayGamma, 0.0001));
                return true;
            }

            bool TryGetBiaxialEigenSystem(float3 rayDir, out float3 eigenA, out float3 eigenB, out float delta)
            {
                eigenA = float3(1.0, 0.0, 0.0);
                eigenB = float3(0.0, 1.0, 0.0);
                delta = 0.0;

                float3 indices = _PrincipalIndices;
                float minDiff = min(abs(indices.x - indices.y), min(abs(indices.y - indices.z), abs(indices.x - indices.z)));
                if (_UseBiaxial < 0.5 || minDiff < max(_UniaxialEpsilon, 1e-6))
                {
                    return false;
                }

                float3 rayPrincipal = normalize(mul(rayDir, (float3x3)_WorldToPrincipalMatrix));
                float n1;
                float n2;
                if (!SolveBiaxialFresnel(rayPrincipal, indices, n1, n2))
                {
                    return false;
                }

                float3 tangentU;
                if (abs(rayPrincipal.z) < 0.9)
                {
                    tangentU = normalize(cross(float3(0.0, 0.0, 1.0), rayPrincipal));
                }
                else
                {
                    tangentU = normalize(cross(float3(0.0, 1.0, 0.0), rayPrincipal));
                }
                float3 tangentV = normalize(cross(rayPrincipal, tangentU));
                float3 weights = 1.0 / max(indices * indices, 1e-6);
                float m00 = WeightedDot(tangentU, tangentU, weights);
                float m01 = WeightedDot(tangentU, tangentV, weights);
                float m11 = WeightedDot(tangentV, tangentV, weights);
                float2 eigen2 = SmallestEigenVector2(m00, m01, m11);
                float3 eigenPrincipal = normalize(tangentU * eigen2.x + tangentV * eigen2.y);
                float3x3 principalToView = transpose((float3x3)_WorldToPrincipalMatrix);
                eigenA = SafeNormalizeOnWavefront(mul(eigenPrincipal, principalToView), rayDir, CrystalReferenceAxis());

                float3 reference = ProjectOntoWavefront(CrystalReferenceAxis(), rayDir);
                if (dot(reference, reference) > 1e-8 && dot(eigenA, normalize(reference)) < 0.0)
                {
                    eigenA = -eigenA;
                }

                eigenB = normalize(cross(rayDir, eigenA));
                float wavelength = max(_WavelengthM, 1e-12);
                float pathFactor = 1.0 / max(abs(rayDir.z), 0.05);
                delta = 6.28318530718 * _ThicknessM * abs(n1 - n2) * pathFactor / wavelength;
                return abs(delta) < 1e12;
            }

            fixed4 frag(v2f_img i) : SV_Target
            {
                float2 p = i.uv * 2.0 - 1.0;
                float radius = length(p);
                if (radius > _ApertureRadius)
                {
                    return float4(0.0, 0.0, 0.0, 1.0);
                }

                float2 screenPoint = p * _ScreenHalfSizeM;
                float3 rayDir = normalize(float3(screenPoint.x, screenPoint.y, max(_ScreenDistanceM, 1e-6)));
                float3 opticAxis = OpticAxis();
                float3 eDirection;
                float3 oDirection;
                float delta;
                if (_BiaxialDisplayMode < 0.5 || _BiaxialDisplayMode > 1.5)
                {
                    float biaxialIntensity;
                    if (TryEvaluateBiaxialTeaching(p, rayDir, biaxialIntensity))
                    {
                        return float4(biaxialIntensity, biaxialIntensity, biaxialIntensity, 1.0);
                    }
                }

                bool hasBiaxial = TryGetBiaxialEigenSystem(rayDir, eDirection, oDirection, delta);
                if (!hasBiaxial)
                {
                    eDirection = SafeNormalizeOnWavefront(opticAxis, rayDir, CrystalReferenceAxis());
                    oDirection = normalize(cross(rayDir, eDirection));
                    float cosTheta = saturate(abs(dot(rayDir, opticAxis)));
                    float nEffective = EffectiveExtraordinaryIndex(_OrdinaryIndexNo, _ExtraordinaryIndexNe, cosTheta);

                    // r22/E are uploaded in v1 so the interface is stable; the physical EO perturbation is deferred.
                    float wavelength = max(_WavelengthM, 1e-12);
                    float pathFactor = 1.0 / max(abs(rayDir.z), 0.05);
                    delta = 6.28318530718 * _ThicknessM * (nEffective - _OrdinaryIndexNo) * pathFactor / wavelength;
                }

                float3 polarizer = AngleVector(_PolarizerAngleRad, rayDir);
                float3 analyzer = AngleVector(_AnalyzerAngleRad, rayDir);
                float oAmplitude = dot(polarizer, oDirection);
                float eAmplitude = dot(polarizer, eDirection);
                float analyzerO = dot(analyzer, oDirection);
                float analyzerE = dot(analyzer, eDirection);
                float oTerm = analyzerO * oAmplitude;
                float eTerm = analyzerE * eAmplitude;
                float deltaWidth = fwidth(delta);
                float visibility = _PhaseAntiAliasStrength > 0.0
                    ? exp(-0.5 * pow(deltaWidth * _PhaseAntiAliasStrength, 2.0))
                    : 1.0;
                float intensity = saturate(_InitialIntensity * (oTerm * oTerm + eTerm * eTerm + 2.0 * oTerm * eTerm * cos(delta) * visibility));
                return float4(intensity, intensity, intensity, 1.0);
            }
            ENDCG
        }
    }
}

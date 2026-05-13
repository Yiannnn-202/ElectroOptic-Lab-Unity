Shader "ElectroOptics/ConoscopicJonesIntensity"
{
    Properties
    {
        _WavelengthM ("Wavelength (m)", Float) = 0.0000006328
        _ThicknessM ("Thickness (m)", Float) = 0.02
        _OrdinaryIndexNo ("Ordinary Index no", Float) = 2.286
        _ExtraordinaryIndexNe ("Extraordinary Index ne", Float) = 2.200
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
                float3 eDirection = SafeNormalizeOnWavefront(opticAxis, rayDir, CrystalReferenceAxis());
                float3 oDirection = normalize(cross(rayDir, eDirection));

                float3 polarizer = AngleVector(_PolarizerAngleRad, rayDir);
                float3 analyzer = AngleVector(_AnalyzerAngleRad, rayDir);

                float oAmplitude = dot(polarizer, oDirection);
                float eAmplitude = dot(polarizer, eDirection);
                float cosTheta = saturate(abs(dot(rayDir, opticAxis)));
                float nEffective = EffectiveExtraordinaryIndex(_OrdinaryIndexNo, _ExtraordinaryIndexNe, cosTheta);

                // r22/E are uploaded in v1 so the interface is stable; the physical EO perturbation is deferred.
                float wavelength = max(_WavelengthM, 1e-12);
                float pathFactor = 1.0 / max(abs(rayDir.z), 0.05);
                float delta = 6.28318530718 * _ThicknessM * (nEffective - _OrdinaryIndexNo) * pathFactor / wavelength;

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

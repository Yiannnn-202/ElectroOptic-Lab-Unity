using UnityEngine;

namespace ElectroOptics.ConoscopicAnalysis
{
    public static class ConoscopicJonesCpuReference
    {
        private const float TwoPi = 6.28318530718f;
        private const float Epsilon = 0.000001f;

        public static float EvaluateIntensity(Vector2 normalizedPoint, ConoscopicJonesParameters parameters)
        {
            return EvaluateIntensity(normalizedPoint, parameters, 0f);
        }

        public static float EvaluateIntensity(Vector2 normalizedPoint, ConoscopicJonesParameters parameters, float deltaWidth)
        {
            if (parameters == null)
            {
                return 0f;
            }

            parameters.Clamp();
            if (normalizedPoint.magnitude > parameters.apertureRadius)
            {
                return 0f;
            }

            Vector3 rayDir = new Vector3(
                normalizedPoint.x * parameters.screenHalfSizeM,
                normalizedPoint.y * parameters.screenHalfSizeM,
                Mathf.Max(parameters.screenDistanceM, Epsilon)).normalized;

            Vector3 opticAxis = GetOpticAxis(parameters);
            Vector3 eDirection = ProjectOntoWavefront(opticAxis, rayDir);
            if (eDirection.sqrMagnitude < Epsilon)
            {
                eDirection = ProjectOntoWavefront(GetCrystalReferenceAxis(parameters), rayDir);
            }

            eDirection.Normalize();
            Vector3 oDirection = Vector3.Cross(rayDir, eDirection).normalized;

            Vector3 polarizer = GetProjectedAngleVector(parameters.polarizerAngleDeg, rayDir);
            Vector3 analyzer = GetProjectedAngleVector(parameters.analyzerAngleDeg, rayDir);
            float oAmplitude = Vector3.Dot(polarizer, oDirection);
            float eAmplitude = Vector3.Dot(polarizer, eDirection);

            float cosTheta = Mathf.Clamp(Mathf.Abs(Vector3.Dot(rayDir, opticAxis)), 0f, 1f);
            float nEffective = EffectiveExtraordinaryIndex(parameters.ordinaryIndexNo, parameters.extraordinaryIndexNe, cosTheta);
            float thicknessM = parameters.thicknessMm * 1e-3f;
            float wavelengthM = Mathf.Max(parameters.wavelengthNm * 1e-9f, 1e-12f);
            float pathFactor = 1f / Mathf.Max(Mathf.Abs(rayDir.z), 0.05f);
            float delta = TwoPi * thicknessM * (nEffective - parameters.ordinaryIndexNo) * pathFactor / wavelengthM;

            float analyzerO = Vector3.Dot(analyzer, oDirection);
            float analyzerE = Vector3.Dot(analyzer, eDirection);
            float intensity = EvaluateJonesIntensity(
                oAmplitude,
                eAmplitude,
                analyzerO,
                analyzerE,
                delta,
                deltaWidth,
                parameters);
            return Mathf.Clamp01(intensity);
        }

        public static float EvaluateIntensityWithFiniteDifference(
            int x,
            int y,
            int resolution,
            ConoscopicJonesParameters parameters)
        {
            if (parameters == null)
            {
                return 0f;
            }

            Vector2 center = GetPixelCenterCoordinate(x, y, resolution);
            float delta = EvaluateDelta(center, parameters);
            float deltaDx = Mathf.Abs(
                EvaluateDelta(GetPixelCenterCoordinate(Mathf.Min(x + 1, resolution - 1), y, resolution), parameters)
                - EvaluateDelta(GetPixelCenterCoordinate(Mathf.Max(x - 1, 0), y, resolution), parameters)) * 0.5f;
            float deltaDy = Mathf.Abs(
                EvaluateDelta(GetPixelCenterCoordinate(x, Mathf.Min(y + 1, resolution - 1), resolution), parameters)
                - EvaluateDelta(GetPixelCenterCoordinate(x, Mathf.Max(y - 1, 0), resolution), parameters)) * 0.5f;

            return EvaluateIntensity(center, parameters, deltaDx + deltaDy);
        }

        public static float EvaluateDelta(Vector2 normalizedPoint, ConoscopicJonesParameters parameters)
        {
            if (parameters == null)
            {
                return 0f;
            }

            parameters.Clamp();
            Vector3 rayDir = new Vector3(
                normalizedPoint.x * parameters.screenHalfSizeM,
                normalizedPoint.y * parameters.screenHalfSizeM,
                Mathf.Max(parameters.screenDistanceM, Epsilon)).normalized;

            Vector3 opticAxis = GetOpticAxis(parameters);
            float cosTheta = Mathf.Clamp(Mathf.Abs(Vector3.Dot(rayDir, opticAxis)), 0f, 1f);
            float nEffective = EffectiveExtraordinaryIndex(parameters.ordinaryIndexNo, parameters.extraordinaryIndexNe, cosTheta);
            float thicknessM = parameters.thicknessMm * 1e-3f;
            float wavelengthM = Mathf.Max(parameters.wavelengthNm * 1e-9f, 1e-12f);
            float pathFactor = 1f / Mathf.Max(Mathf.Abs(rayDir.z), 0.05f);
            return TwoPi * thicknessM * (nEffective - parameters.ordinaryIndexNo) * pathFactor / wavelengthM;
        }

        public static Vector2 GetCoordinate(int x, int y, int resolution)
        {
            if (resolution <= 1)
            {
                return Vector2.zero;
            }

            return new Vector2(
                Mathf.Lerp(-1f, 1f, x / (float)(resolution - 1)),
                Mathf.Lerp(-1f, 1f, y / (float)(resolution - 1)));
        }

        public static Vector2 GetPixelCenterCoordinate(int x, int y, int resolution)
        {
            if (resolution <= 0)
            {
                return Vector2.zero;
            }

            float u = (x + 0.5f) / resolution;
            float v = (y + 0.5f) / resolution;
            return new Vector2(u * 2f - 1f, v * 2f - 1f);
        }

        private static float EffectiveExtraordinaryIndex(float no, float ne, float cosTheta)
        {
            float sinThetaSqr = Mathf.Max(1f - cosTheta * cosTheta, 0f);
            float denominator = Mathf.Sqrt(ne * ne * cosTheta * cosTheta + no * no * sinThetaSqr);
            return no * ne / Mathf.Max(denominator, Epsilon);
        }

        private static float EvaluateJonesIntensity(
            float oAmplitude,
            float eAmplitude,
            float analyzerO,
            float analyzerE,
            float delta,
            float deltaWidth,
            ConoscopicJonesParameters parameters)
        {
            float oTerm = analyzerO * oAmplitude;
            float eTerm = analyzerE * eAmplitude;
            float visibility = 1f;
            if (parameters.phaseAntiAliasStrength > 0f && deltaWidth > 0f)
            {
                float width = deltaWidth * parameters.phaseAntiAliasStrength;
                visibility = Mathf.Exp(-0.5f * width * width);
            }

            return parameters.initialIntensity * (oTerm * oTerm + eTerm * eTerm + 2f * oTerm * eTerm * Mathf.Cos(delta) * visibility);
        }

        private static Vector3 GetProjectedAngleVector(float angleDeg, Vector3 rayDir)
        {
            float angleRad = angleDeg * Mathf.Deg2Rad;
            Vector3 direction = new Vector3(Mathf.Cos(angleRad), Mathf.Sin(angleRad), 0f);
            direction = ProjectOntoWavefront(direction, rayDir);
            if (direction.sqrMagnitude < Epsilon)
            {
                direction = ProjectOntoWavefront(Vector3.up, rayDir);
            }

            return direction.normalized;
        }

        private static Vector3 ProjectOntoWavefront(Vector3 direction, Vector3 rayDir)
        {
            return direction - rayDir * Vector3.Dot(direction, rayDir);
        }

        private static Vector3 GetOpticAxis(ConoscopicJonesParameters parameters)
        {
            float tiltRad = parameters.opticAxisTiltDeg * Mathf.Deg2Rad;
            float azimuthRad = parameters.opticAxisAzimuthDeg * Mathf.Deg2Rad;
            float sinTilt = Mathf.Sin(tiltRad);
            return new Vector3(
                sinTilt * Mathf.Cos(azimuthRad),
                sinTilt * Mathf.Sin(azimuthRad),
                Mathf.Cos(tiltRad)).normalized;
        }

        private static Vector3 GetCrystalReferenceAxis(ConoscopicJonesParameters parameters)
        {
            float axisRad = parameters.crystalAxisAngleDeg * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(axisRad), Mathf.Sin(axisRad), 0f).normalized;
        }
    }
}

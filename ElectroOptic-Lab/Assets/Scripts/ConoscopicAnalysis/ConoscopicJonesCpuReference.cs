using UnityEngine;

namespace ElectroOptics.ConoscopicAnalysis
{
    public static class ConoscopicJonesCpuReference
    {
        private const float TwoPi = 6.28318530718f;
        private const float Epsilon = 0.000001f;

        private struct AxisIndex
        {
            public float Index;
            public Vector3 PrincipalAxis;

            public AxisIndex(float index, Vector3 principalAxis)
            {
                Index = index;
                PrincipalAxis = principalAxis;
            }
        }

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

            if (parameters.IsBiaxial()
                && TryEvaluateBiaxialIntensity(normalizedPoint, rayDir, parameters, deltaWidth, out float biaxialIntensity))
            {
                return Mathf.Clamp01(biaxialIntensity);
            }

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
                - delta);
            float deltaDy = Mathf.Abs(
                EvaluateDelta(GetPixelCenterCoordinate(x, Mathf.Min(y + 1, resolution - 1), resolution), parameters)
                - delta);

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

            if (parameters.IsBiaxial()
                && TryEvaluateBiaxialDelta(rayDir, parameters, out float biaxialDelta))
            {
                return biaxialDelta;
            }

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

        private static bool TryEvaluateBiaxialIntensity(
            Vector2 normalizedPoint,
            Vector3 rayDir,
            ConoscopicJonesParameters parameters,
            float deltaWidth,
            out float intensity)
        {
            intensity = 0f;
            if ((parameters.biaxialDisplayMode == ConoscopicBiaxialDisplayMode.ConoscopicTeaching
                 || parameters.biaxialDisplayMode == ConoscopicBiaxialDisplayMode.PaperKtp1)
                && TryEvaluateBiaxialTeaching(normalizedPoint, rayDir, parameters, deltaWidth, out intensity))
            {
                return true;
            }

            if (!TryGetBiaxialEigenSystem(rayDir, parameters, out Vector3 eigenA, out Vector3 eigenB, out float delta))
            {
                return false;
            }

            Vector3 polarizer = GetProjectedAngleVector(parameters.polarizerAngleDeg, rayDir);
            Vector3 analyzer = GetProjectedAngleVector(parameters.analyzerAngleDeg, rayDir);
            float aAmplitude = Vector3.Dot(polarizer, eigenA);
            float bAmplitude = Vector3.Dot(polarizer, eigenB);
            float analyzerA = Vector3.Dot(analyzer, eigenA);
            float analyzerB = Vector3.Dot(analyzer, eigenB);

            intensity = EvaluateJonesIntensity(
                aAmplitude,
                bAmplitude,
                analyzerA,
                analyzerB,
                delta,
                deltaWidth,
                parameters);
            return true;
        }

        private static bool TryEvaluateBiaxialTeaching(
            Vector2 normalizedPoint,
            Vector3 rayDir,
            ConoscopicJonesParameters parameters,
            float gammaWidth,
            out float intensity)
        {
            intensity = 0f;
            Vector3 unusedEigen;
            Vector3 rayPrincipal;
            if (!TrySolveBiaxialFresnel(rayDir, parameters, out float n1, out float n2, out unusedEigen, out rayPrincipal))
            {
                return false;
            }

            float halfSize = parameters.screenHalfSizeM / Mathf.Max(parameters.screenDistanceM, Epsilon);
            Vector3 rayView = new Vector3(normalizedPoint.x * halfSize, normalizedPoint.y * halfSize, 1f).normalized;
            float thicknessM = parameters.thicknessMm * 1e-3f;
            float wavelengthM = Mathf.Max(parameters.wavelengthNm * 1e-9f, 1e-12f);
            float pathLength = thicknessM / Mathf.Max(rayView.z, 0.05f);
            float gamma = TwoPi * pathLength * Mathf.Abs(n1 - n2) / wavelengthM * parameters.phaseScale;
            float visibility = 1f;
            if (gammaWidth > 0f)
            {
                float width = Mathf.Max(gammaWidth, 0.0001f);
                visibility = Mathf.Pow(2f, (-0.75f * width * width) / Mathf.Max(parameters.ringSharpness, 0.0001f));
            }

            float ringPattern = 0.5f - 0.5f * Mathf.Cos(gamma) * Mathf.Clamp01(visibility);
            Vector4 axesView = DeriveBiaxialAxesView(
                new Vector3(parameters.principalIndexNx, parameters.principalIndexNy, parameters.principalIndexNz),
                parameters.worldToPrincipalMatrix);
            float crossPattern = GetBiaxialExtinctionPattern(rayView, halfSize, normalizedPoint, axesView, parameters);
            intensity = Mathf.Clamp01(crossPattern * ringPattern);
            intensity = SmoothStep(parameters.blackCutoff, 1f, intensity);
            intensity = Mathf.Pow(intensity, 1f / Mathf.Max(parameters.displayGamma, 0.0001f));
            return !float.IsNaN(intensity) && !float.IsInfinity(intensity);
        }

        private static bool TryEvaluateBiaxialDelta(
            Vector3 rayDir,
            ConoscopicJonesParameters parameters,
            out float delta)
        {
            delta = 0f;
            Vector3 unusedEigen;
            Vector3 unusedRay;
            if (!TrySolveBiaxialFresnel(rayDir, parameters, out float n1, out float n2, out unusedEigen, out unusedRay))
            {
                return false;
            }

            float thicknessM = parameters.thicknessMm * 1e-3f;
            float wavelengthM = Mathf.Max(parameters.wavelengthNm * 1e-9f, 1e-12f);
            float pathFactor = 1f / Mathf.Max(Mathf.Abs(rayDir.z), 0.05f);
            delta = TwoPi * thicknessM * Mathf.Abs(n1 - n2) * pathFactor / wavelengthM;
            if (parameters.biaxialDisplayMode == ConoscopicBiaxialDisplayMode.ConoscopicTeaching
                || parameters.biaxialDisplayMode == ConoscopicBiaxialDisplayMode.PaperKtp1)
            {
                delta *= parameters.phaseScale;
            }

            return !float.IsNaN(delta) && !float.IsInfinity(delta);
        }

        private static bool TryGetBiaxialEigenSystem(
            Vector3 rayDir,
            ConoscopicJonesParameters parameters,
            out Vector3 eigenAView,
            out Vector3 eigenBView,
            out float delta)
        {
            eigenAView = Vector3.right;
            eigenBView = Vector3.up;
            delta = 0f;

            Vector3 unusedRay;
            if (!TrySolveBiaxialFresnel(rayDir, parameters, out float n1, out float n2, out Vector3 eigenPrincipal, out unusedRay))
            {
                return false;
            }

            Matrix4x4 principalToView = parameters.worldToPrincipalMatrix.transpose;
            eigenAView = principalToView.MultiplyVector(eigenPrincipal);
            eigenAView = ProjectOntoWavefront(eigenAView, rayDir);
            if (eigenAView.sqrMagnitude < Epsilon)
            {
                eigenAView = ProjectOntoWavefront(GetCrystalReferenceAxis(parameters), rayDir);
            }

            if (eigenAView.sqrMagnitude < Epsilon)
            {
                return false;
            }

            eigenAView.Normalize();
            Vector3 reference = ProjectOntoWavefront(GetCrystalReferenceAxis(parameters), rayDir);
            if (reference.sqrMagnitude > Epsilon && Vector3.Dot(eigenAView, reference.normalized) < 0f)
            {
                eigenAView = -eigenAView;
            }

            eigenBView = Vector3.Cross(rayDir, eigenAView);
            if (eigenBView.sqrMagnitude < Epsilon)
            {
                return false;
            }

            eigenBView.Normalize();
            float thicknessM = parameters.thicknessMm * 1e-3f;
            float wavelengthM = Mathf.Max(parameters.wavelengthNm * 1e-9f, 1e-12f);
            float pathFactor = 1f / Mathf.Max(Mathf.Abs(rayDir.z), 0.05f);
            delta = TwoPi * thicknessM * Mathf.Abs(n1 - n2) * pathFactor / wavelengthM;
            return !float.IsNaN(delta) && !float.IsInfinity(delta);
        }

        private static bool TrySolveBiaxialFresnel(
            Vector3 rayDir,
            ConoscopicJonesParameters parameters,
            out float n1,
            out float n2,
            out Vector3 eigenDirectionPrincipal,
            out Vector3 rayPrincipal)
        {
            n1 = parameters.principalIndexNx;
            n2 = parameters.principalIndexNy;
            eigenDirectionPrincipal = Vector3.right;
            rayPrincipal = RowMultiply(rayDir, parameters.worldToPrincipalMatrix);
            if (rayPrincipal.sqrMagnitude < Epsilon)
            {
                return false;
            }

            rayPrincipal.Normalize();
            Vector3 indices = new Vector3(
                parameters.principalIndexNx,
                parameters.principalIndexNy,
                parameters.principalIndexNz);
            if (!SolveFresnel(rayPrincipal, indices, out n1, out n2))
            {
                return false;
            }

            Vector3 tangentU = Mathf.Abs(rayPrincipal.z) < 0.9f
                ? Vector3.Cross(Vector3.forward, rayPrincipal).normalized
                : Vector3.Cross(Vector3.up, rayPrincipal).normalized;
            Vector3 tangentV = Vector3.Cross(rayPrincipal, tangentU).normalized;
            Vector3 aTerms = new Vector3(
                1f / Mathf.Max(indices.x * indices.x, Epsilon),
                1f / Mathf.Max(indices.y * indices.y, Epsilon),
                1f / Mathf.Max(indices.z * indices.z, Epsilon));

            float m00 = WeightedDot(tangentU, tangentU, aTerms);
            float m01 = WeightedDot(tangentU, tangentV, aTerms);
            float m11 = WeightedDot(tangentV, tangentV, aTerms);
            Vector2 eigen2 = SmallestEigenVector2(m00, m01, m11);
            eigenDirectionPrincipal = (tangentU * eigen2.x + tangentV * eigen2.y).normalized;
            return eigenDirectionPrincipal.sqrMagnitude >= Epsilon;
        }

        private static bool SolveFresnel(Vector3 s, Vector3 indices, out float n1, out float n2)
        {
            n1 = indices.x;
            n2 = indices.y;
            Vector3 nSqr = new Vector3(indices.x * indices.x, indices.y * indices.y, indices.z * indices.z);
            Vector3 aTerms = new Vector3(
                1f / Mathf.Max(nSqr.x, Epsilon),
                1f / Mathf.Max(nSqr.y, Epsilon),
                1f / Mathf.Max(nSqr.z, Epsilon));

            float sx2 = s.x * s.x;
            float sy2 = s.y * s.y;
            float sz2 = s.z * s.z;
            float b = -(sx2 * (aTerms.y + aTerms.z)
                        + sy2 * (aTerms.x + aTerms.z)
                        + sz2 * (aTerms.x + aTerms.y));
            float c = sx2 * aTerms.y * aTerms.z
                      + sy2 * aTerms.x * aTerms.z
                      + sz2 * aTerms.x * aTerms.y;
            float discriminant = b * b - 4f * c;
            if (discriminant < 0f)
            {
                return false;
            }

            float sqrtDiscriminant = Mathf.Sqrt(discriminant);
            float x1 = (-b + sqrtDiscriminant) * 0.5f;
            float x2 = (-b - sqrtDiscriminant) * 0.5f;
            if (x1 <= Epsilon || x2 <= Epsilon)
            {
                return false;
            }

            n1 = 1f / Mathf.Sqrt(x1);
            n2 = 1f / Mathf.Sqrt(x2);
            return !float.IsNaN(n1) && !float.IsInfinity(n1) && !float.IsNaN(n2) && !float.IsInfinity(n2);
        }

        private static float WeightedDot(Vector3 a, Vector3 b, Vector3 weights)
        {
            return a.x * b.x * weights.x + a.y * b.y * weights.y + a.z * b.z * weights.z;
        }

        private static Vector2 SmallestEigenVector2(float a, float b, float d)
        {
            float trace = a + d;
            float root = Mathf.Sqrt(Mathf.Max((a - d) * (a - d) + 4f * b * b, 0f));
            float lambda = (trace - root) * 0.5f;
            Vector2 vector = Mathf.Abs(b) > Epsilon
                ? new Vector2(b, lambda - a)
                : (a <= d ? Vector2.right : Vector2.up);

            if (vector.sqrMagnitude < Epsilon)
            {
                vector = Vector2.right;
            }

            vector.Normalize();
            if (vector.x < -Epsilon || (Mathf.Abs(vector.x) <= Epsilon && vector.y < 0f))
            {
                vector = -vector;
            }

            return vector;
        }

        private static Vector3 RowMultiply(Vector3 vector, Matrix4x4 matrix)
        {
            return new Vector3(
                vector.x * matrix.m00 + vector.y * matrix.m10 + vector.z * matrix.m20,
                vector.x * matrix.m01 + vector.y * matrix.m11 + vector.z * matrix.m21,
                vector.x * matrix.m02 + vector.y * matrix.m12 + vector.z * matrix.m22);
        }

        private static Vector4 DeriveBiaxialAxesView(Vector3 indices, Matrix4x4 viewToPrincipalMatrix)
        {
            float nx = indices.x;
            float ny = indices.y;
            float nz = indices.z;
            const float epsilon = ConoscopicJonesParameters.DefaultUniaxialEpsilon;
            if (Mathf.Abs(nx - ny) < epsilon
                || Mathf.Abs(ny - nz) < epsilon
                || Mathf.Abs(nx - nz) < epsilon)
            {
                return Vector4.zero;
            }

            AxisIndex[] sorted =
            {
                new AxisIndex(nx, Vector3.right),
                new AxisIndex(ny, Vector3.up),
                new AxisIndex(nz, Vector3.forward)
            };
            System.Array.Sort(sorted, (a, b) => a.Index.CompareTo(b.Index));

            float min2 = sorted[0].Index * sorted[0].Index;
            float mid2 = sorted[1].Index * sorted[1].Index;
            float max2 = sorted[2].Index * sorted[2].Index;
            float span = Mathf.Max(max2 - min2, Epsilon);
            float cosFromMax = Mathf.Sqrt(Mathf.Clamp01((mid2 - min2) / span));
            float sinFromMax = Mathf.Sqrt(Mathf.Clamp01(1f - cosFromMax * cosFromMax));

            Vector3 minAxis = sorted[0].PrincipalAxis;
            Vector3 maxAxis = sorted[2].PrincipalAxis;
            Vector3 axisA = (minAxis * sinFromMax + maxAxis * cosFromMax).normalized;
            Vector3 axisB = (-minAxis * sinFromMax + maxAxis * cosFromMax).normalized;

            Matrix4x4 principalToView = viewToPrincipalMatrix.transpose;
            axisA = principalToView.MultiplyVector(axisA).normalized;
            axisB = principalToView.MultiplyVector(axisB).normalized;

            if (axisA.z < 0f) axisA = -axisA;
            if (axisB.z < 0f) axisB = -axisB;
            return new Vector4(axisA.x, axisA.y, axisB.x, axisB.y);
        }

        private static float GetBiaxialExtinctionPattern(
            Vector3 rayView,
            float halfSize,
            Vector2 normalizedPoint,
            Vector4 biaxialAxesView,
            ConoscopicJonesParameters parameters)
        {
            bool hasAxes = Vector4.Dot(biaxialAxesView, biaxialAxesView) > Epsilon;
            if (!hasAxes)
            {
                Vector3 opticAxisView = GetOpticAxis(parameters);
                Vector2 melatopeOffset = GetMelatopeOffset(opticAxisView, halfSize) + parameters.initialMelatopeOffset;
                Vector2 localP = normalizedPoint - melatopeOffset;
                Vector3 extinctionAxisView = GetAxisFromMelatopeOffset(melatopeOffset, halfSize);
                return GetExtinctionPattern(rayView, extinctionAxisView, localP, parameters.crossWidth);
            }

            Vector3 axisA = GetBiaxialAxisView(new Vector2(biaxialAxesView.x, biaxialAxesView.y));
            Vector3 axisB = GetBiaxialAxisView(new Vector2(biaxialAxesView.z, biaxialAxesView.w));
            Vector2 offsetA = ClampBiaxialMelatopeOffset(GetMelatopeOffset(axisA, halfSize)) + parameters.initialMelatopeOffset;
            Vector2 offsetB = ClampBiaxialMelatopeOffset(GetMelatopeOffset(axisB, halfSize)) + parameters.initialMelatopeOffset;
            Vector3 displayAxisA = GetAxisFromMelatopeOffset(offsetA - parameters.initialMelatopeOffset, halfSize);
            Vector3 displayAxisB = GetAxisFromMelatopeOffset(offsetB - parameters.initialMelatopeOffset, halfSize);
            float patternA = GetExtinctionPattern(rayView, displayAxisA, normalizedPoint - offsetA, parameters.crossWidth);
            float patternB = GetExtinctionPattern(rayView, displayAxisB, normalizedPoint - offsetB, parameters.crossWidth);
            return Mathf.Min(patternA, patternB);
        }

        private static float GetExtinctionPattern(Vector3 rayView, Vector3 opticAxisView, Vector2 localP, float crossWidth)
        {
            Vector3 projectedAxis = opticAxisView - rayView * Vector3.Dot(opticAxisView, rayView);
            Vector2 polarizationDirection = new Vector2(projectedAxis.x, projectedAxis.y);
            float directionLengthSqr = polarizationDirection.sqrMagnitude;
            if (directionLengthSqr < Epsilon)
            {
                polarizationDirection = localP;
                directionLengthSqr = polarizationDirection.sqrMagnitude;
            }

            Vector2 direction = polarizationDirection / Mathf.Sqrt(Mathf.Max(directionLengthSqr, Epsilon));
            float crossSignal = 2f * direction.x * direction.y;
            float crossPower = Mathf.Lerp(0.8f, 2.4f, Mathf.Clamp01(crossWidth / 0.35f));
            float crossPattern = Mathf.Pow(Mathf.Clamp01(crossSignal * crossSignal), crossPower);
            float melatope = SmoothStep(0.025f, 0.14f, localP.magnitude);
            return crossPattern * melatope;
        }

        private static Vector2 GetMelatopeOffset(Vector3 opticAxisView, float halfSize)
        {
            float axisZ = Mathf.Max(opticAxisView.z, 0.05f);
            float projectionScale = axisZ * Mathf.Max(halfSize, 0.0001f);
            return new Vector2(opticAxisView.x / projectionScale, opticAxisView.y / projectionScale);
        }

        private static Vector3 GetAxisFromMelatopeOffset(Vector2 melatopeOffset, float halfSize)
        {
            return new Vector3(melatopeOffset.x * halfSize, melatopeOffset.y * halfSize, 1f).normalized;
        }

        private static Vector3 GetBiaxialAxisView(Vector2 axisXY)
        {
            float zSqr = Mathf.Max(1f - Vector2.Dot(axisXY, axisXY), 0.0025f);
            return new Vector3(axisXY.x, axisXY.y, Mathf.Sqrt(zSqr)).normalized;
        }

        private static Vector2 ClampBiaxialMelatopeOffset(Vector2 offset)
        {
            const float maxRadius = 0.42f;
            float offsetRadius = offset.magnitude;
            return offsetRadius > maxRadius ? offset * (maxRadius / Mathf.Max(offsetRadius, 0.0001f)) : offset;
        }

        private static float SmoothStep(float edge0, float edge1, float value)
        {
            float t = Mathf.Clamp01((value - edge0) / Mathf.Max(edge1 - edge0, Epsilon));
            return t * t * (3f - 2f * t);
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

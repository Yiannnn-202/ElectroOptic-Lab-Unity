using UnityEngine;
using ElectroOptics;

namespace ElectroOptics.ConoscopicAnalysis
{
    public static class ConoscopicIntensityCalculator
    {
        private const float MinOpticAxisSqrMagnitude = 0.000001f;
        private const float UniaxialEpsilon = 0.0005f;
        private const float Pi = 3.14159265359f;

        private struct SampleCache
        {
            public float Gamma;
            public float CrossPattern;
            public float Aperture;
            public bool IsValid;
        }

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

        public static void Compute(
            CrystalPhysicalCore physicalCore,
            ConoscopicIntensityParameters parameters,
            ConoscopicIntensityResult result)
        {
            if (physicalCore == null || parameters == null || result == null)
            {
                return;
            }

            parameters.Clamp();
            int resolution = parameters.resolution;
            result.EnsureSize(resolution);

            var config = physicalCore.CurrentConfig;
            CrystalProfile profile = config.profile;
            if (profile == null)
            {
                result.MarkInvalid(null, parameters);
                return;
            }

            Vector3 indices = ResolveIndices(physicalCore.NewPrincipalIndices, profile);
            Matrix4x4 matrix = ResolveMatrix(physicalCore.ShaderWorldToPrincipalMatrix);
            float lengthMeters = Mathf.Max((float)(profile.defaultLength_mm * 1e-3), 0.000001f);
            float wavelengthMeters = Mathf.Max((float)(profile.defaultWavelength_nm * 1e-9), 1e-9f);
            Vector3 opticAxisView = DeriveOpticAxisView(matrix);
            Vector4 biaxialAxesView = DeriveBiaxialAxesView(indices, matrix);

            var cache = new SampleCache[resolution * resolution];
            float halfSize = Mathf.Max(Mathf.Tan(parameters.fov * Mathf.Deg2Rad * 0.5f), 0.0001f);

            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    int index = y * resolution + x;
                    Vector2 p = GetCoordinate(x, y, resolution);
                    cache[index] = ComputeSampleCache(
                        p,
                        halfSize,
                        indices,
                        matrix,
                        opticAxisView,
                        biaxialAxesView,
                        lengthMeters,
                        wavelengthMeters,
                        parameters);
                }
            }

            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    int index = y * resolution + x;
                    SampleCache sample = cache[index];
                    result.Intensities[index] = sample.IsValid
                        ? ComputeFinalIntensity(sample, EstimateGammaWidth(cache, resolution, x, y), parameters)
                        : 0f;
                }
            }

            result.SetSnapshot(profile, parameters, true);
        }

        private static SampleCache ComputeSampleCache(
            Vector2 p,
            float halfSize,
            Vector3 indices,
            Matrix4x4 matrix,
            Vector3 opticAxisView,
            Vector4 biaxialAxesView,
            float lengthMeters,
            float wavelengthMeters,
            ConoscopicIntensityParameters parameters)
        {
            float radius = p.magnitude;
            float aperture = 1f - SmoothStep(0.96f, 1f, radius);
            if (aperture <= 0f)
            {
                return new SampleCache { IsValid = false };
            }

            Vector3 rayView = new Vector3(p.x * halfSize, p.y * halfSize, 1f).normalized;
            Vector3 rayOptical = RowMultiply(rayView, matrix);
            Vector2 nSols = SolveFresnel(rayOptical, indices);
            float deltaN = Mathf.Abs(nSols.x - nSols.y);
            float pathLength = lengthMeters / Mathf.Max(rayView.z, 0.0001f);
            float gamma = ((2f * Pi * pathLength * deltaN) / wavelengthMeters) * parameters.phaseScale;
            float crossPattern = GetBiaxialExtinctionPattern(
                rayView,
                halfSize,
                p,
                opticAxisView,
                biaxialAxesView,
                parameters.initialMelatopeOffset,
                parameters.crossWidth);

            return new SampleCache
            {
                Gamma = gamma,
                CrossPattern = crossPattern,
                Aperture = aperture,
                IsValid = true
            };
        }

        private static float ComputeFinalIntensity(
            SampleCache sample,
            float gammaWidth,
            ConoscopicIntensityParameters parameters)
        {
            float phase = sample.Gamma * 0.5f;
            gammaWidth = Mathf.Max(gammaWidth, 0.0001f);
            float visibility = Mathf.Pow(2f, (-0.75f * gammaWidth * gammaWidth) / Mathf.Max(parameters.ringSharpness, 0.0001f));
            float ringPattern = 0.5f - 0.5f * Mathf.Cos(2f * phase) * Saturate(visibility);
            float intensity = Saturate(sample.CrossPattern * ringPattern * sample.Aperture);
            intensity = SmoothStep(parameters.blackCutoff, 1f, intensity);
            return Mathf.Pow(intensity, 1f / Mathf.Max(parameters.displayGamma, 0.0001f));
        }

        private static float EstimateGammaWidth(SampleCache[] cache, int resolution, int x, int y)
        {
            int index = y * resolution + x;
            if (!cache[index].IsValid)
            {
                return 0.0001f;
            }

            float center = cache[index].Gamma;
            float left = GetNeighborGamma(cache, resolution, x - 1, y, center);
            float right = GetNeighborGamma(cache, resolution, x + 1, y, center);
            float down = GetNeighborGamma(cache, resolution, x, y - 1, center);
            float up = GetNeighborGamma(cache, resolution, x, y + 1, center);
            float gammaDx = Mathf.Abs(right - left) * 0.5f;
            float gammaDy = Mathf.Abs(up - down) * 0.5f;
            return Mathf.Max(gammaDx + gammaDy, 0.0001f);
        }

        private static float GetNeighborGamma(SampleCache[] cache, int resolution, int x, int y, float fallback)
        {
            if (x < 0 || x >= resolution || y < 0 || y >= resolution)
            {
                return fallback;
            }

            SampleCache sample = cache[y * resolution + x];
            return sample.IsValid ? sample.Gamma : fallback;
        }

        private static Vector2 SolveFresnel(Vector3 s, Vector3 nPrincipal)
        {
            Vector3 n2 = new Vector3(
                nPrincipal.x * nPrincipal.x,
                nPrincipal.y * nPrincipal.y,
                nPrincipal.z * nPrincipal.z);
            Vector3 aTerms = new Vector3(
                1f / (n2.x + 1e-9f),
                1f / (n2.y + 1e-9f),
                1f / (n2.z + 1e-9f));

            float sx2 = s.x * s.x;
            float sy2 = s.y * s.y;
            float sz2 = s.z * s.z;

            float b = -(sx2 * (aTerms.y + aTerms.z)
                        + sy2 * (aTerms.x + aTerms.z)
                        + sz2 * (aTerms.x + aTerms.y));
            float c = sx2 * (aTerms.y * aTerms.z)
                      + sy2 * (aTerms.x * aTerms.z)
                      + sz2 * (aTerms.x * aTerms.y);
            float delta = b * b - 4f * c;
            if (delta < 0f)
            {
                return Vector2.zero;
            }

            float sqrtDelta = Mathf.Sqrt(delta);
            float x1 = (-b + sqrtDelta) * 0.5f;
            float x2 = (-b - sqrtDelta) * 0.5f;
            return new Vector2(1f / Mathf.Sqrt(x1), 1f / Mathf.Sqrt(x2));
        }

        private static Vector3 ResolveIndices(Vector3 indices, CrystalProfile profile)
        {
            if (IsFinitePositive(indices.x) && IsFinitePositive(indices.y) && IsFinitePositive(indices.z))
            {
                return indices;
            }

            return new Vector3((float)profile.n_x, (float)profile.n_y, (float)profile.n_z);
        }

        private static Matrix4x4 ResolveMatrix(Matrix4x4 matrix)
        {
            Vector3 basisX = new Vector3(matrix.m00, matrix.m01, matrix.m02);
            Vector3 basisY = new Vector3(matrix.m10, matrix.m11, matrix.m12);
            Vector3 basisZ = new Vector3(matrix.m20, matrix.m21, matrix.m22);
            float magnitude = basisX.sqrMagnitude + basisY.sqrMagnitude + basisZ.sqrMagnitude;
            return magnitude > 0.000001f ? matrix : Matrix4x4.identity;
        }

        private static bool IsFinitePositive(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
        }

        private static float GetBiaxialExtinctionPattern(
            Vector3 rayView,
            float halfSize,
            Vector2 p,
            Vector3 opticAxisView,
            Vector4 biaxialAxesView,
            Vector2 initialMelatopeOffset,
            float crossWidth)
        {
            bool hasAxes = Vector4.Dot(biaxialAxesView, biaxialAxesView) > 0.000001f;
            if (!hasAxes)
            {
                Vector2 melatopeOffset = GetMelatopeOffset(opticAxisView, halfSize) + initialMelatopeOffset;
                Vector2 localP = p - melatopeOffset;
                Vector3 extinctionAxisView = GetAxisFromMelatopeOffset(melatopeOffset, halfSize);
                return GetExtinctionPattern(rayView, extinctionAxisView, localP, crossWidth);
            }

            Vector3 axisA = GetBiaxialAxisView(new Vector2(biaxialAxesView.x, biaxialAxesView.y));
            Vector3 axisB = GetBiaxialAxisView(new Vector2(biaxialAxesView.z, biaxialAxesView.w));
            Vector2 offsetA = ClampBiaxialMelatopeOffset(GetMelatopeOffset(axisA, halfSize)) + initialMelatopeOffset;
            Vector2 offsetB = ClampBiaxialMelatopeOffset(GetMelatopeOffset(axisB, halfSize)) + initialMelatopeOffset;
            Vector3 displayAxisA = GetAxisFromMelatopeOffset(offsetA - initialMelatopeOffset, halfSize);
            Vector3 displayAxisB = GetAxisFromMelatopeOffset(offsetB - initialMelatopeOffset, halfSize);
            float patternA = GetExtinctionPattern(rayView, displayAxisA, p - offsetA, crossWidth);
            float patternB = GetExtinctionPattern(rayView, displayAxisB, p - offsetB, crossWidth);
            return Mathf.Min(patternA, patternB);
        }

        private static float GetExtinctionPattern(Vector3 rayView, Vector3 opticAxisView, Vector2 localP, float crossWidth)
        {
            Vector3 projectedAxis = opticAxisView - rayView * Vector3.Dot(opticAxisView, rayView);
            Vector2 polarizationDirection = new Vector2(projectedAxis.x, projectedAxis.y);
            float directionLengthSqr = polarizationDirection.sqrMagnitude;
            if (directionLengthSqr < 1e-8f)
            {
                polarizationDirection = localP;
                directionLengthSqr = polarizationDirection.sqrMagnitude;
            }

            Vector2 direction = polarizationDirection / Mathf.Sqrt(Mathf.Max(directionLengthSqr, 1e-8f));
            float crossSignal = 2f * direction.x * direction.y;
            float crossPower = Mathf.Lerp(0.8f, 2.4f, Saturate(crossWidth / 0.35f));
            float crossPattern = Mathf.Pow(Saturate(crossSignal * crossSignal), crossPower);
            float melatope = SmoothStep(0.025f, 0.14f, localP.magnitude);
            return crossPattern * melatope;
        }

        private static Vector3 DeriveOpticAxisView(Matrix4x4 worldToPrincipalMatrix)
        {
            Vector3 opticAxisView = new Vector3(
                worldToPrincipalMatrix.m02,
                worldToPrincipalMatrix.m12,
                worldToPrincipalMatrix.m22);

            if (opticAxisView.sqrMagnitude < MinOpticAxisSqrMagnitude)
            {
                return Vector3.forward;
            }

            opticAxisView.Normalize();
            return opticAxisView.z < 0f ? -opticAxisView : opticAxisView;
        }

        private static Vector4 DeriveBiaxialAxesView(Vector3 indices, Matrix4x4 viewToPrincipalMatrix)
        {
            float nx = indices.x;
            float ny = indices.y;
            float nz = indices.z;
            if (Mathf.Abs(nx - ny) < UniaxialEpsilon
                || Mathf.Abs(ny - nz) < UniaxialEpsilon
                || Mathf.Abs(nx - nz) < UniaxialEpsilon)
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
            float span = Mathf.Max(max2 - min2, 0.000001f);
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

        private static Vector2 GetCoordinate(int x, int y, int resolution)
        {
            float u = x / (float)(resolution - 1);
            float v = y / (float)(resolution - 1);
            return new Vector2(Mathf.Lerp(-1f, 1f, u), Mathf.Lerp(-1f, 1f, v));
        }

        private static Vector3 RowMultiply(Vector3 vector, Matrix4x4 matrix)
        {
            return new Vector3(
                vector.x * matrix.m00 + vector.y * matrix.m10 + vector.z * matrix.m20,
                vector.x * matrix.m01 + vector.y * matrix.m11 + vector.z * matrix.m21,
                vector.x * matrix.m02 + vector.y * matrix.m12 + vector.z * matrix.m22);
        }

        private static float SmoothStep(float edge0, float edge1, float value)
        {
            if (Mathf.Approximately(edge0, edge1))
            {
                return value < edge0 ? 0f : 1f;
            }

            float t = Saturate((value - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        private static float Saturate(float value)
        {
            return Mathf.Clamp01(value);
        }
    }
}

using ElectroOptics;
using ElectroOptics.ConoscopicAnalysis;
using ElectroOptics.DataTransfer;
using UnityEngine;

[DisallowMultipleComponent]
public class CrystalRetarderPhysics : MonoBehaviour, IOpticalReceiver
{
    private const float TwoPi = 6.28318530718f;
    private const float VisibleIntensityThreshold = 0.001f;
    private const float MinAxisLengthSqr = 0.000001f;

    [Header("Direct Red-Dot Retarder")]
    public CrystalPhysicalCore physicalCore;
    public OpticalComponent opticalComponent;
    [Min(0f)] public float retardanceScale = 1f;
    public float rayExitOffset = 0.06f;
    public float maxRayDistance = 50f;

    private LineRenderer lineRenderer;
    private bool gotLight;
    private bool warnedUnavailable;

    private void Start()
    {
        ResolveReferences();
        EnsureLineRenderer();
    }

    private void Update()
    {
        if (!gotLight && lineRenderer != null)
        {
            lineRenderer.enabled = false;
        }

        gotLight = false;
    }

    public void Bind(CrystalPhysicalCore core, OpticalComponent component)
    {
        physicalCore = core;
        opticalComponent = component;
    }

    public void ReceiveLight(LightData lightIn, Vector3 hitPoint, Vector3 direction)
    {
        gotLight = true;
        ResolveReferences();
        EnsureLineRenderer();

        LightData lightOut = lightIn;
        bool appliedRetardance = false;
        string failureReason = "not-on-rail";
        if (IsOnRail())
        {
            if (TryApplyCrystalRetardance(lightIn, direction, out LightData transformed, out failureReason))
            {
                lightOut = transformed;
                appliedRetardance = true;
                failureReason = "none";
                warnedUnavailable = false;
            }
            else
            {
                WarnUnavailableOnce(failureReason);
            }
        }
        Debug.Log($"[CrystalRetarder] onRail={IsOnRail()} applied={appliedRetardance} reason={failureReason} inI={lightIn.intensity:F4}(S1={lightIn.stokesQ:F4},S2={lightIn.stokesU:F4},S3={lightIn.stokesV:F4}) outI={lightOut.intensity:F4}(S1={lightOut.stokesQ:F4},S2={lightOut.stokesU:F4},S3={lightOut.stokesV:F4})");

        PropagateLight(lightOut, hitPoint, direction.normalized);
    }

    public static LightData ApplyLinearRetarder(LightData light, float axisAngleDeg, float retardanceRad)
    {
        if (light.intensity <= 0f)
        {
            return LightData.FromStokes(0f, 0f, 0f, 0f);
        }

        float axisRad = axisAngleDeg * Mathf.Deg2Rad;
        float cos2Axis = Mathf.Cos(2f * axisRad);
        float sin2Axis = Mathf.Sin(2f * axisRad);

        float qAxis = light.stokesQ * cos2Axis + light.stokesU * sin2Axis;
        float uAxis = -light.stokesQ * sin2Axis + light.stokesU * cos2Axis;
        float vAxis = light.stokesV;

        float phase = NormalizePhase(retardanceRad);
        float cosDelta = Mathf.Cos(phase);
        float sinDelta = Mathf.Sin(phase);

        float uDelayed = uAxis * cosDelta - vAxis * sinDelta;
        float vDelayed = uAxis * sinDelta + vAxis * cosDelta;

        float qOut = qAxis * cos2Axis - uDelayed * sin2Axis;
        float uOut = qAxis * sin2Axis + uDelayed * cos2Axis;
        return LightData.FromStokes(light.intensity, qOut, uOut, vDelayed);
    }

    private bool TryApplyCrystalRetardance(LightData lightIn, Vector3 direction, out LightData lightOut, out string failureReason)
    {
        lightOut = lightIn;
        failureReason = "none";
        if (physicalCore == null)
        {
            failureReason = "missing physicalCore";
            return false;
        }

        CrystalConfig config = physicalCore.CurrentConfig;
        CrystalProfile profile = ResolveProfile(config);
        if (profile == null)
        {
            failureReason = "missing profile";
            return false;
        }

        ConoscopicJonesParameters parameters = BuildJonesParameters(physicalCore, profile);
        Vector3 rayDir = config.worldLightDirection.sqrMagnitude > MinAxisLengthSqr
            ? config.worldLightDirection.normalized
            : direction.normalized;

        if (!ConoscopicJonesCpuReference.TryGetRetarderEigenSystem(
                rayDir,
                parameters,
                out Vector3 eigenA,
                out Vector3 _,
                out float delta))
        {
            failureReason = $"eigensystem failed profile={profile.crystalName} ray={FormatVector(rayDir)} indices={FormatVector(new Vector3(parameters.principalIndexNx, parameters.principalIndexNy, parameters.principalIndexNz))}";
            return false;
        }

        if (!TryGetAxisAngle(eigenA, rayDir, out float axisAngleDeg))
        {
            failureReason = $"axis angle failed eigen={FormatVector(eigenA)} ray={FormatVector(rayDir)}";
            return false;
        }

        Debug.Log($"[CrystalRetarder] delta={delta:F6}rad({delta/TwoPi:F3}*2π) scale={retardanceScale} axis={axisAngleDeg:F1}° rayDir={rayDir}");
        lightOut = ApplyLinearRetarder(lightIn, axisAngleDeg, delta * retardanceScale);
        return true;
    }

    private static CrystalProfile ResolveProfile(CrystalConfig config)
    {
        if (config.profile != null)
        {
            return config.profile;
        }

        return CrystalRuntime.Controller != null ? CrystalRuntime.Controller.GetProfile() : null;
    }

    private static ConoscopicJonesParameters BuildJonesParameters(CrystalPhysicalCore core, CrystalProfile profile)
    {
        Vector3 indices = core.NewPrincipalIndices;
        if (!IsFinitePositive(indices.x) || !IsFinitePositive(indices.y) || !IsFinitePositive(indices.z))
        {
            indices = new Vector3((float)profile.n_x, (float)profile.n_y, (float)profile.n_z);
        }

        var parameters = new ConoscopicJonesParameters
        {
            wavelengthNm = SafePositive((float)profile.defaultWavelength_nm, 633f),
            thicknessMm = SafePositive((float)profile.defaultLength_mm, 20f),
            worldToPrincipalMatrix = core.ShaderWorldToPrincipalMatrix,
            biaxialDisplayMode = ConoscopicBiaxialDisplayMode.RawJones,
            forceUniaxial = false,
            phaseScale = 1f
        };
        parameters.ApplyPrincipalIndices(indices);
        parameters.Clamp();
        return parameters;
    }

    private void PropagateLight(LightData light, Vector3 hitPoint, Vector3 direction)
    {
        bool drawLine = light.intensity > VisibleIntensityThreshold;
        if (lineRenderer != null)
        {
            lineRenderer.enabled = drawLine;
            if (drawLine)
            {
                float alpha = Mathf.Clamp01(light.intensity);
                lineRenderer.startColor = new Color(1f, 0f, 0f, alpha);
                lineRenderer.endColor = new Color(1f, 0f, 0f, alpha);
            }
        }

        Vector3 start = hitPoint + direction * Mathf.Max(rayExitOffset, 0.001f);
        Vector3 end = start + direction * maxRayDistance;
        RaycastHit[] hits = Physics.RaycastAll(start, direction, maxRayDistance);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            IOpticalReceiver next = hit.collider.GetComponent<IOpticalReceiver>();
            if (next == null) next = hit.collider.GetComponentInParent<IOpticalReceiver>();
            if (next == null || ReferenceEquals(next, this))
            {
                continue;
            }

            end = hit.point;
            next.ReceiveLight(light, hit.point, direction);
            break;
        }

        if (lineRenderer != null && lineRenderer.enabled)
        {
            lineRenderer.SetPosition(0, start);
            lineRenderer.SetPosition(1, end);
        }
    }

    private void ResolveReferences()
    {
        if (physicalCore == null)
        {
            physicalCore = GetComponent<CrystalPhysicalCore>();
            if (physicalCore == null) physicalCore = GetComponentInParent<CrystalPhysicalCore>();
            if (physicalCore == null) physicalCore = CrystalRuntime.PhysicalCore;
        }

        if (opticalComponent == null)
        {
            opticalComponent = GetComponent<OpticalComponent>();
            if (opticalComponent == null) opticalComponent = GetComponentInParent<OpticalComponent>();
            if (opticalComponent == null) opticalComponent = GetComponentInChildren<OpticalComponent>();
        }
    }

    private bool IsOnRail()
    {
        return opticalComponent != null
               && opticalComponent.gameObject.activeInHierarchy
               && opticalComponent.isOnRail;
    }

    private void EnsureLineRenderer()
    {
        if (lineRenderer != null)
        {
            return;
        }

        lineRenderer = gameObject.AddComponent<LineRenderer>();
        lineRenderer.positionCount = 2;
        lineRenderer.startWidth = 0.02f;
        lineRenderer.endWidth = 0.02f;
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startColor = new Color(1f, 0f, 0f, 0.5f);
        lineRenderer.endColor = new Color(1f, 0f, 0f, 0.5f);
        lineRenderer.enabled = false;
    }

    private void WarnUnavailableOnce(string reason)
    {
        if (warnedUnavailable)
        {
            return;
        }

        Debug.LogWarning($"[CrystalRetarderPhysics] Crystal is on rail, but retarder parameters are unavailable ({reason}). Passing light through.");
        warnedUnavailable = true;
    }

    private static string FormatVector(Vector3 value)
    {
        return $"({value.x:F4},{value.y:F4},{value.z:F4})";
    }

    private static bool TryGetAxisAngle(Vector3 axis, Vector3 rayDir, out float angleDeg)
    {
        GetTransverseBasis(rayDir, out Vector3 basis0, out Vector3 basis90);

        Vector2 transverse = new Vector2(Vector3.Dot(axis, basis0), Vector3.Dot(axis, basis90));
        if (transverse.sqrMagnitude < MinAxisLengthSqr)
        {
            angleDeg = 0f;
            return false;
        }

        angleDeg = Mathf.Repeat(Mathf.Atan2(transverse.y, transverse.x) * Mathf.Rad2Deg, 180f);
        return true;
    }

    private static void GetTransverseBasis(Vector3 rayDir, out Vector3 basis0, out Vector3 basis90)
    {
        Vector3 dir = rayDir.sqrMagnitude > MinAxisLengthSqr ? rayDir.normalized : Vector3.forward;
        Vector3 abs = new Vector3(Mathf.Abs(dir.x), Mathf.Abs(dir.y), Mathf.Abs(dir.z));

        if (abs.x >= abs.y && abs.x >= abs.z)
        {
            basis0 = Vector3.up;
            basis90 = Vector3.forward;
            return;
        }

        if (abs.y >= abs.x && abs.y >= abs.z)
        {
            basis0 = Vector3.right;
            basis90 = Vector3.forward;
            return;
        }

        basis0 = Vector3.right;
        basis90 = Vector3.up;
    }

    private static float NormalizePhase(float phase)
    {
        return Mathf.Repeat(phase + Mathf.PI, TwoPi) - Mathf.PI;
    }

    private static float SafePositive(float value, float fallback)
    {
        return IsFinitePositive(value) ? value : fallback;
    }

    private static bool IsFinitePositive(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
    }
}

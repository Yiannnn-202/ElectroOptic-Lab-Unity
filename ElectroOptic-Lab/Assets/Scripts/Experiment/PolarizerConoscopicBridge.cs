using ElectroOptics.Experiment.Renderer;
using UnityEngine;
using ElectroOptics.WebStreaming;

/// <summary>
/// Bridges Scene2 polarizer/analyzer rotation to the conoscopic texture renderer.
///
/// Usage:
///   1. Drag the polarizer and analyzer GameObjects into the Inspector fields below.
///   2. At runtime the bridge reads their Z-axis rotation and pushes it to the shader.
///   3. If no objects are bound, use keyboard shortcuts to test:
///        Q / E  →  rotate polarizer ±5°/frame
///        Z / X  →  rotate analyzer  ±5°/frame
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-100)]
public class PolarizerConoscopicBridge : MonoBehaviour
{
    private const float KeyboardStep = 5f;

    [Header("Manual Bindings")]
    [Tooltip("Drag the polarizer (起偏器) GameObject here. Its Z rotation is read as the transmission axis angle.")]
    [SerializeField] private Transform polarizerTransform;

    [Tooltip("Drag the analyzer (检偏器) GameObject here. Its Z rotation is read as the transmission axis angle.")]
    [SerializeField] private Transform analyzerTransform;

    [Tooltip("Conoscopic renderer to push angles to. Auto-found if left empty.")]
    [SerializeField] private ConoscopicTextureRenderer conoscopicRenderer;

    [Header("Angle Offset")]
    [Tooltip("Added to the polarizer's Z rotation. Tune if the model's 0° doesn't match the optic axis reference.")]
    [SerializeField] private float polarizerAngleOffset = 0f;

    [Tooltip("Added to the analyzer's Z rotation.")]
    [SerializeField] private float analyzerAngleOffset = 0f;

    [Header("Keyboard Test (when no objects bound)")]
    [Tooltip("When enabled, Q/E adjusts polarizer angle and Z/X adjusts analyzer angle if no transforms are bound.")]
    [SerializeField] private bool enableKeyboardTest = true;

    [Header("Debug")]
    [SerializeField] private bool logAngleChanges = false;

    // Internal keyboard-driven test angles
    private float _testPolarizerAngle = 0f;
    private float _testAnalyzerAngle = 90f;

    private float _lastPolarizerAngle = float.NaN;
    private float _lastAnalyzerAngle = float.NaN;

    private void Start()
    {
        if (conoscopicRenderer == null)
        {
            // Same GameObject first (CrystalComponentInitializer adds it here)
            conoscopicRenderer = GetComponent<ConoscopicTextureRenderer>();
        }
        if (conoscopicRenderer == null)
        {
            conoscopicRenderer = FindFirstObjectByType<ConoscopicTextureRenderer>();
        }

        LogBindingStatus();
    }

    private void Update()
    {
        // Retry finding renderer each frame — CrystalComponentInitializer may add it after our Start()
        if (conoscopicRenderer == null)
        {
            conoscopicRenderer = GetComponent<ConoscopicTextureRenderer>()
                              ?? FindFirstObjectByType<ConoscopicTextureRenderer>();
            if (conoscopicRenderer == null) return;
        }

        HandleKeyboardTestInput();

        float polarizerAngle = ReadPolarizerAngle();
        float analyzerAngle = ReadAnalyzerAngle();

        conoscopicRenderer.SetPolarizerAngleDeg(polarizerAngle);
        conoscopicRenderer.SetAnalyzerAngleDeg(analyzerAngle);

        if (logAngleChanges)
        {
            LogIfChanged(polarizerAngle, analyzerAngle);
        }
    }

    // ── Angle reading ──────────────────────────────────────────────

    private float ReadPolarizerAngle()
    {
        return ReadAngle(polarizerTransform, polarizerAngleOffset, _testPolarizerAngle);
    }

    private float ReadAnalyzerAngle()
    {
        return ReadAngle(analyzerTransform, analyzerAngleOffset, _testAnalyzerAngle);
    }

    private float ReadAngle(Transform boundTransform, float offset, float testFallback)
    {
        // Priority 1: RotateStandController on bound transform or its parents
        RotateStandController stand = boundTransform != null
            ? boundTransform.GetComponentInParent<RotateStandController>()
            : null;

        if (stand != null)
        {
            return Mathf.Repeat(stand.GetCurrentRotateAngle() + offset, 360f);
        }

        // Priority 2: Direct Z rotation from bound transform
        if (boundTransform != null)
        {
            return Mathf.Repeat(boundTransform.eulerAngles.z + offset, 360f);
        }

        // Priority 3: Keyboard test angle
        return Mathf.Repeat(testFallback + offset, 360f);
    }

    private bool HasPolarizerBinding()
    {
        return polarizerTransform != null || analyzerTransform != null;
    }

    // ── Keyboard test input ────────────────────────────────────────

    private void HandleKeyboardTestInput()
    {
        if (!enableKeyboardTest || HasPolarizerBinding()) return;

        float step = KeyboardStep;
        // Speed up with Shift
        if (RemoteInputRelay.GetKey(KeyCode.LeftShift) || RemoteInputRelay.GetKey(KeyCode.RightShift))
        {
            step *= 6f;
        }

        if (RemoteInputRelay.GetKey(KeyCode.Q)) _testPolarizerAngle -= step;
        if (RemoteInputRelay.GetKey(KeyCode.E)) _testPolarizerAngle += step;
        if (RemoteInputRelay.GetKey(KeyCode.Z)) _testAnalyzerAngle -= step;
        if (RemoteInputRelay.GetKey(KeyCode.X)) _testAnalyzerAngle += step;

        // Preset shortcuts
        if (RemoteInputRelay.GetKeyDown(KeyCode.Alpha1)) { _testPolarizerAngle = 0f; _testAnalyzerAngle = 90f; }   // crossed
        if (RemoteInputRelay.GetKeyDown(KeyCode.Alpha2)) { _testPolarizerAngle = 0f; _testAnalyzerAngle = 0f; }    // parallel
        if (RemoteInputRelay.GetKeyDown(KeyCode.Alpha3)) { _testPolarizerAngle = 45f; _testAnalyzerAngle = 135f; }  // crossed at 45°
    }

    // ── Logging ────────────────────────────────────────────────────

    private void LogBindingStatus()
    {
        int sourceCount = 0;
        if (polarizerTransform != null) sourceCount++;
        if (analyzerTransform != null) sourceCount++;

        if (sourceCount > 0)
        {
            Debug.Log($"[PolarizerConoscopicBridge] Bound {sourceCount} transform(s) — " +
                      $"polarizer='{(polarizerTransform != null ? polarizerTransform.name : "unbound")}', " +
                      $"analyzer='{(analyzerTransform != null ? analyzerTransform.name : "unbound")}', " +
                      $"renderer={(conoscopicRenderer != null ? "ok" : "MISSING")}");
        }
        else
        {
            Debug.Log($"[PolarizerConoscopicBridge] No transforms bound. " +
                      $"Keyboard test mode: Q/E adjusts polarizer, Z/X adjusts analyzer. " +
                      $"renderer={(conoscopicRenderer != null ? "ok" : "MISSING")}");
        }
    }

    private void LogIfChanged(float polarizerAngle, float analyzerAngle)
    {
        if (!Mathf.Approximately(polarizerAngle, _lastPolarizerAngle)
            || !Mathf.Approximately(analyzerAngle, _lastAnalyzerAngle))
        {
            Debug.Log($"[PolarizerConoscopicBridge] Polarizer={polarizerAngle:F1}°, " +
                      $"Analyzer={analyzerAngle:F1}°");
            _lastPolarizerAngle = polarizerAngle;
            _lastAnalyzerAngle = analyzerAngle;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        polarizerAngleOffset = Mathf.Clamp(polarizerAngleOffset, -180f, 180f);
        analyzerAngleOffset = Mathf.Clamp(analyzerAngleOffset, -180f, 180f);
    }

    [ContextMenu("Print Status")]
    private void ContextMenuPrintStatus()
    {
        Debug.Log($"[PolarizerConoscopicBridge] Status:\n" +
                  $"  - Polarizer transform: {(polarizerTransform != null ? polarizerTransform.name : "null")}\n" +
                  $"  - Analyzer transform: {(analyzerTransform != null ? analyzerTransform.name : "null")}\n" +
                  $"  - Conoscopic renderer: {(conoscopicRenderer != null ? "ok" : "MISSING")}\n" +
                  $"  - Effective polarizer angle: {ReadPolarizerAngle():F1}°\n" +
                  $"  - Effective analyzer angle: {ReadAnalyzerAngle():F1}°\n" +
                  $"  - Keyboard test: {(enableKeyboardTest && !HasPolarizerBinding() ? "active" : "inactive")}");
    }
#endif
}

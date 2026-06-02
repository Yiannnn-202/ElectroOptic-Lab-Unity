using UnityEngine;

namespace ElectroOptics.UI.ScreenDisplay
{
    /// <summary>
    /// 3D 发光边框 — 在 Camera.main 下创建 Quad + QuickOutline，
    /// 位置对齐 UI 面板的屏幕区域，利用 HDR + Bloom 实现真正发光。
    /// 同时 Quad 自带 emissive 材质作为保底（无 Bloom 时也能看到）。
    /// [ExecuteAlways]
    /// </summary>
    [ExecuteAlways]
    public class Panel3DOutline : MonoBehaviour
    {
        [Header("外观")]
        [Tooltip("边框颜色 (HDR)")]
        [ColorUsage(true, true)]
        public Color outlineColor = new Color(2f, 1.5f, 0.3f, 1f);

        [Tooltip("边框宽度")]
        [Range(1f, 100f)]
        public float outlineWidth = 15f;

        [Tooltip("Quad 距离相机的距离")]
        [Range(0.5f, 50f)]
        public float distanceFromCamera = 2f;

        [Tooltip("Quad 透明度（非高亮时 0，高亮时可微调看到底色）")]
        [Range(0f, 0.3f)]
        public float quadAlpha = 0.05f;

        private GameObject _quadObject;
        private Outline _quickOutline;
        private MeshRenderer _quadRenderer;
        private Material _quadMaterial;
        private RectTransform _panelRect;
        private Camera _targetCamera;
        private bool _isHighlighted;
        private bool _quadReady;

        private const string QuadName = "Panel3DOutline_Quad";

        public bool IsHighlighted
        {
            get => _isHighlighted;
            set
            {
                _isHighlighted = value;
                ApplyHighlight();
            }
        }

        // ────────── 生命周期 ──────────

        private void OnEnable()
        {
            _panelRect = transform.parent?.GetComponent<RectTransform>();
            ResolveCamera();
            if (_targetCamera == null) return;

            Transform existing = _targetCamera.transform.Find(QuadName);
            if (existing != null)
            {
                _quadObject = existing.gameObject;
                _quadRenderer = _quadObject.GetComponent<MeshRenderer>();
                _quickOutline = _quadObject.GetComponent<Outline>();
                if (_quickOutline == null) _quickOutline = _quadObject.AddComponent<Outline>();
                _quadMaterial = _quadRenderer.sharedMaterial;
                SetupQuad();
                _quadReady = true;
                return;
            }

            CreateQuad();
        }

        private void Update()
        {
            if (!_quadReady) return;
            ResolveCamera();
            if (_targetCamera == null) return;
            UpdateQuadTransform();
        }

        private void OnDisable()
        {
            if (_quadObject != null) _quadObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_quadObject != null)
            {
                if (Application.isPlaying) Destroy(_quadObject);
                else DestroyImmediate(_quadObject);
            }
            if (_quadMaterial != null)
            {
                if (Application.isPlaying) Destroy(_quadMaterial);
                else DestroyImmediate(_quadMaterial);
            }
        }

        // ────────── 创建 ──────────

        private void ResolveCamera()
        {
            if (_targetCamera != null) return;
            _targetCamera = Camera.main;
        }

        private void CreateQuad()
        {
            if (_targetCamera == null) return;

            _quadObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _quadObject.name = QuadName;
            _quadObject.transform.SetParent(_targetCamera.transform, false);
            _quadObject.layer = LayerMask.NameToLayer("UI"); // 避免遮挡实验物体

            _quadRenderer = _quadObject.GetComponent<MeshRenderer>();

            // 自发光材质（保底：即使没有 Bloom 也能看到微光）
            _quadMaterial = new Material(Shader.Find("Standard"));
            _quadMaterial.SetFloat("_Mode", 3); // Transparent
            _quadMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _quadMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            _quadMaterial.SetInt("_ZWrite", 0);
            _quadMaterial.DisableKeyword("_ALPHATEST");
            _quadMaterial.EnableKeyword("_ALPHABLEND");
            _quadMaterial.SetColor("_EmissionColor", outlineColor * 0.3f);
            _quadMaterial.EnableKeyword("_EMISSION");
            _quadMaterial.renderQueue = 3000;
            _quadRenderer.sharedMaterial = _quadMaterial;
            _quadRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _quadRenderer.receiveShadows = false;

            // 移除碰撞体
            var collider = _quadObject.GetComponent<MeshCollider>();
            if (collider != null) DestroyImmediate(collider);

            // QuickOutline
            _quickOutline = _quadObject.AddComponent<Outline>();
            SetupQuad();

            UpdateQuadTransform();
            _quadReady = true;

            Debug.Log($"[Panel3DOutline] Quad 已创建 (Camera: {_targetCamera.name}, distance: {distanceFromCamera})");
        }

        private void SetupQuad()
        {
            if (_quickOutline == null) return;
            _quickOutline.OutlineColor = outlineColor;
            _quickOutline.OutlineWidth = outlineWidth;
            _quickOutline.OutlineMode = Outline.Mode.OutlineAll; // 无视深度遮挡
        }

        private void ApplyHighlight()
        {
            if (!_quadReady) return;

            // QuickOutline
            if (_quickOutline != null)
                _quickOutline.enabled = _isHighlighted;

            // Quad 自身：高亮时微微可见底色，让发光更明显
            if (_quadMaterial != null)
            {
                Color c = outlineColor;
                _quadMaterial.SetColor("_EmissionColor", _isHighlighted ? c : c * 0.1f);
                _quadMaterial.color = new Color(c.r, c.g, c.b, _isHighlighted ? 0.1f : 0f);
            }

            Debug.Log($"[Panel3DOutline] IsHighlighted = {_isHighlighted}, outlineEnabled = {_quickOutline?.enabled}");
        }

        // ────────── 定位 ──────────

        private void UpdateQuadTransform()
        {
            if (_panelRect == null || _targetCamera == null || _quadObject == null) return;

            Vector3[] corners = new Vector3[4];
            _panelRect.GetWorldCorners(corners);

            Vector2 screenCenter = (corners[0] + corners[2]) / 2f;
            float screenWidth  = Vector2.Distance(corners[0], corners[3]);
            float screenHeight = Vector2.Distance(corners[0], corners[1]);

            Vector3 worldCenter = _targetCamera.ScreenToWorldPoint(
                new Vector3(screenCenter.x, screenCenter.y, distanceFromCamera));
            Vector3 worldCorner = _targetCamera.ScreenToWorldPoint(
                new Vector3(screenCenter.x + screenWidth / 2f,
                            screenCenter.y + screenHeight / 2f,
                            distanceFromCamera));

            float worldWidth  = Mathf.Abs(worldCorner.x - worldCenter.x) * 2f;
            float worldHeight = Mathf.Abs(worldCorner.y - worldCenter.y) * 2f;

            _quadObject.transform.localPosition = _targetCamera.transform.InverseTransformPoint(worldCenter);
            _quadObject.transform.localRotation = Quaternion.identity;
            _quadObject.transform.localScale = new Vector3(worldWidth, worldHeight, 1f);
        }

        // ────────── Editor ──────────

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_quickOutline != null)
            {
                _quickOutline.OutlineColor = outlineColor;
                _quickOutline.OutlineWidth = outlineWidth;
                _quickOutline.OutlineMode = Outline.Mode.OutlineAll;
            }
            if (_quadMaterial != null)
            {
                _quadMaterial.SetColor("_EmissionColor", outlineColor * 0.3f);
            }

            if (_quadObject != null)
            {
                UnityEditor.EditorApplication.delayCall += () =>
                {
                    if (this == null || _quadObject == null) return;
                    ResolveCamera();
                    UpdateQuadTransform();
                };
            }
        }
#endif
    }
}

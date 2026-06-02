using UnityEngine;
using UnityEngine.UI;

namespace ElectroOptics.UI.ScreenDisplay
{
    /// <summary>
    /// UI 版周边高光组件（等效于 3D 物体的 QuickOutline）
    /// 在 RectTransform 四边创建彩色边框条，通过 IsHighlighted 控制显示/隐藏
    /// 使用方式与 LaserStateController/RotateStandController 中的 Outline 组件一致：
    ///   _uiOutline.IsHighlighted = true/false;
    /// </summary>
    [DisallowMultipleComponent]
    public class UIOutline : MonoBehaviour
    {
        [Header("高光外观")]
        [Tooltip("边框颜色")]
        public Color outlineColor = new Color(1f, 0.85f, 0.2f, 1f);

        [Tooltip("边框宽度（像素）")]
        [Range(1f, 20f)]
        public float outlineWidth = 4f;

        // 四条边框
        private GameObject _topBorder;
        private GameObject _bottomBorder;
        private GameObject _leftBorder;
        private GameObject _rightBorder;

        private Image _topImage;
        private Image _bottomImage;
        private Image _leftImage;
        private Image _rightImage;

        private bool _isHighlighted;

        /// <summary>
        /// 高亮开关（与 QuickOutline.enabled 用法一致）
        /// </summary>
        public bool IsHighlighted
        {
            get => _isHighlighted;
            set
            {
                _isHighlighted = value;
                ApplyState();
            }
        }

        private void Awake()
        {
            CreateBorders();
        }

        private void OnEnable()  { ApplyState(); }
        private void OnDisable() { ApplyState(); }

        private void OnDestroy()
        {
            DestroyBorder(ref _topBorder);
            DestroyBorder(ref _bottomBorder);
            DestroyBorder(ref _leftBorder);
            DestroyBorder(ref _rightBorder);
        }

        private void CreateBorders()
        {
            // 防止 Edit 模式重复创建
            Transform existing = transform.Find("UIOutline_Top");
            if (existing != null)
            {
                _topBorder    = existing.gameObject;
                _bottomBorder = transform.Find("UIOutline_Bottom")?.gameObject;
                _leftBorder   = transform.Find("UIOutline_Left")?.gameObject;
                _rightBorder  = transform.Find("UIOutline_Right")?.gameObject;
                if (_topBorder != null && _bottomBorder != null && _leftBorder != null && _rightBorder != null)
                {
                    _topImage    = _topBorder.GetComponent<Image>();
                    _bottomImage = _bottomBorder.GetComponent<Image>();
                    _leftImage   = _leftBorder.GetComponent<Image>();
                    _rightImage  = _rightBorder.GetComponent<Image>();
                    ApplyAll();
                    return;
                }
                DestroyAllBorders();
            }

            _topBorder    = CreateBorderStrip("UIOutline_Top");
            _bottomBorder = CreateBorderStrip("UIOutline_Bottom");
            _leftBorder   = CreateBorderStrip("UIOutline_Left");
            _rightBorder  = CreateBorderStrip("UIOutline_Right");

            _topImage    = _topBorder.GetComponent<Image>();
            _bottomImage = _bottomBorder.GetComponent<Image>();
            _leftImage   = _leftBorder.GetComponent<Image>();
            _rightImage  = _rightBorder.GetComponent<Image>();

            SetupEdge(_topBorder,    edge: "top");
            SetupEdge(_bottomBorder, edge: "bottom");
            SetupEdge(_leftBorder,   edge: "left");
            SetupEdge(_rightBorder,  edge: "right");

            ApplyAll();
        }

        private GameObject CreateBorderStrip(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.AddComponent<RectTransform>();
            var img = go.AddComponent<Image>();
            img.raycastTarget = false;
            return go;
        }

        private void SetupEdge(GameObject strip, string edge)
        {
            var rt = strip.GetComponent<RectTransform>();
            switch (edge)
            {
                case "top":
                    rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1);
                    rt.pivot = new Vector2(0.5f, 0); rt.anchoredPosition = Vector2.zero;
                    rt.sizeDelta = new Vector2(0, outlineWidth);
                    break;
                case "bottom":
                    rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(1, 0);
                    rt.pivot = new Vector2(0.5f, 1); rt.anchoredPosition = Vector2.zero;
                    rt.sizeDelta = new Vector2(0, outlineWidth);
                    break;
                case "left":
                    rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(0, 1);
                    rt.pivot = new Vector2(1, 0.5f); rt.anchoredPosition = Vector2.zero;
                    rt.sizeDelta = new Vector2(outlineWidth, 0);
                    break;
                case "right":
                    rt.anchorMin = new Vector2(1, 0); rt.anchorMax = new Vector2(1, 1);
                    rt.pivot = new Vector2(0, 0.5f); rt.anchoredPosition = Vector2.zero;
                    rt.sizeDelta = new Vector2(outlineWidth, 0);
                    break;
            }
        }

        private void ApplyAll()
        {
            if (_topImage != null)    _topImage.color    = outlineColor;
            if (_bottomImage != null) _bottomImage.color = outlineColor;
            if (_leftImage != null)   _leftImage.color   = outlineColor;
            if (_rightImage != null)  _rightImage.color  = outlineColor;
            ApplyState();
        }

        private void ApplyState()
        {
            bool show = _isHighlighted && isActiveAndEnabled;
            if (_topBorder != null)    _topBorder.SetActive(show);
            if (_bottomBorder != null) _bottomBorder.SetActive(show);
            if (_leftBorder != null)   _leftBorder.SetActive(show);
            if (_rightBorder != null)  _rightBorder.SetActive(show);
        }

        private void DestroyAllBorders()
        {
            DestroyBorder(ref _topBorder);
            DestroyBorder(ref _bottomBorder);
            DestroyBorder(ref _leftBorder);
            DestroyBorder(ref _rightBorder);
        }

        private static void DestroyBorder(ref GameObject go)
        {
            if (go != null) { Destroy(go); go = null; }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_topBorder == null) return;

            if (!Application.isPlaying)
            {
                UnityEditor.EditorApplication.delayCall += () =>
                {
                    if (this == null) return;
                    SetupEdge(_topBorder, "top");
                    SetupEdge(_bottomBorder, "bottom");
                    SetupEdge(_leftBorder, "left");
                    SetupEdge(_rightBorder, "right");
                    ApplyAll();
                };
            }
            else
            {
                SetupEdge(_topBorder, "top");
                SetupEdge(_bottomBorder, "bottom");
                SetupEdge(_leftBorder, "left");
                SetupEdge(_rightBorder, "right");
                ApplyAll();
            }
        }
#endif
    }
}

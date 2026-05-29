using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 偏振片刻度盘特写窗口
/// 挂载在 CloseUpUI_Polarizer 上，进入特写视角时渐变弹出，
/// 展示对准刻度盘的专用相机画面（RenderTexture）
/// </summary>
public class PolarizerDialWindow : MonoBehaviour
{
    [Header("相机画面")]
    [Tooltip("对准刻度盘的 RenderTexture")]
    public RenderTexture dialRenderTexture;

    [Header("窗口外观")]
    public Vector2 windowSize = new Vector2(400, 400);
    public Vector2 windowPosition = new Vector2(1500, 200);
    public string windowTitle = "刻度盘";
    public float fadeDuration = 0.3f;

    [Header("画面边距")]
    [Tooltip("画面与窗口边框的距离（上下左右，像素）")]
    public RectOffset dialPadding = new RectOffset(10, 10, 35, 10);

    private GameObject _windowObj;
    private CanvasGroup _windowGroup;
    private bool _windowVisible;
    private Coroutine _fadeCoroutine;

    private void OnEnable()
    {
        Debug.Log($"[PolarizerDialWindow] OnEnable on '{gameObject.name}' (ID:{GetInstanceID()}), _windowObj is null: {_windowObj == null}");

        if (_windowObj == null) CreateWindow();

        Debug.Log($"[PolarizerDialWindow] Starting fade-in, _windowObj active: {_windowObj != null && _windowObj.activeSelf}");

        if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
        _fadeCoroutine = StartCoroutine(FadeWindow(true));
    }

    private void OnDisable()
    {
        Debug.Log($"[PolarizerDialWindow] OnDisable on '{gameObject.name}' (ID:{GetInstanceID()}), _windowVisible: {_windowVisible}");

        // 停止可能的淡入协程
        if (_fadeCoroutine != null)
        {
            StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = null;
        }

        // 窗口是 WindowsCanvas 的子物体，不会随 CloseUpUI 关闭而被销毁
        // 给它挂一个独立 runner 执行淡出
        if (_windowObj != null && _windowVisible)
        {
            _windowVisible = false;
            var runner = _windowObj.AddComponent<FadeOutRunner>();
            runner.Run(_windowGroup, fadeDuration);
        }
    }

    private void CreateWindow()
    {
        Canvas canvas = GetOrCreateWindowsCanvas();
        Debug.Log($"[PolarizerDialWindow] CreateWindow, canvas found: {canvas != null}");

        // 用 instance ID 保证唯一性
        string windowName = $"DialWindow_{GetInstanceID()}";
        _windowObj = new GameObject(windowName);
        _windowObj.transform.SetParent(canvas.transform, false);
        _windowObj.SetActive(false);

        RectTransform windowRect = _windowObj.AddComponent<RectTransform>();
        // 居中锚点：windowPosition 从屏幕中心算起，方便找位置
        windowRect.anchorMin = new Vector2(0.5f, 0.5f);
        windowRect.anchorMax = new Vector2(0.5f, 0.5f);
        windowRect.pivot = new Vector2(0.5f, 0.5f);
        windowRect.anchoredPosition = windowPosition;
        windowRect.sizeDelta = windowSize;

        Image bg = _windowObj.AddComponent<Image>();
        bg.color = new Color(0.1f, 0.1f, 0.15f, 0.95f);

        _windowGroup = _windowObj.AddComponent<CanvasGroup>();
        _windowGroup.alpha = 0f;

        CreateTitleBar(windowRect);
        CreateDialImage(windowRect);

        Debug.Log($"[PolarizerDialWindow] Window '{windowName}' created at position {windowPosition}");
    }

    private void CreateTitleBar(RectTransform parent)
    {
        GameObject titleBar = new GameObject("TitleBar");
        titleBar.transform.SetParent(parent, false);

        RectTransform titleRect = titleBar.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0, 1);
        titleRect.anchorMax = new Vector2(1, 1);
        titleRect.pivot = new Vector2(0.5f, 1);
        titleRect.sizeDelta = new Vector2(0, 30);

        Image titleBg = titleBar.AddComponent<Image>();
        titleBg.color = new Color(0.2f, 0.2f, 0.2f, 1f);

        GameObject titleText = new GameObject("TitleText");
        titleText.transform.SetParent(titleBar.transform, false);
        RectTransform textRect = titleText.AddComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0, 0);
        textRect.anchorMax = new Vector2(1, 1);
        textRect.offsetMin = new Vector2(8, 2);
        textRect.offsetMax = new Vector2(-8, -2);

        Text text = titleText.AddComponent<Text>();
        text.text = windowTitle;
        text.color = Color.white;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 14;
        text.alignment = TextAnchor.MiddleLeft;
    }

    private void CreateDialImage(RectTransform parent)
    {
        GameObject dialObj = new GameObject("DialView");
        dialObj.transform.SetParent(parent, false);

        RectTransform dialRect = dialObj.AddComponent<RectTransform>();
        dialRect.anchorMin = Vector2.zero;
        dialRect.anchorMax = Vector2.one;
        dialRect.offsetMin = new Vector2(dialPadding.left, dialPadding.bottom);
        dialRect.offsetMax = new Vector2(-dialPadding.right, -dialPadding.top);

        RawImage rawImage = dialObj.AddComponent<RawImage>();
        rawImage.texture = dialRenderTexture;
        rawImage.color = Color.white;
        rawImage.raycastTarget = false;
    }

    private IEnumerator FadeWindow(bool show)
    {
        Debug.Log($"[PolarizerDialWindow] FadeWindow show={show}, _windowObj null? {_windowObj == null}");

        if (_windowObj == null) yield break;

        if (show)
        {
            _windowObj.SetActive(true);
            _windowVisible = true;
        }

        float elapsed = 0f;
        float startAlpha = _windowGroup.alpha;
        float targetAlpha = show ? 1f : 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            _windowGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / fadeDuration);
            yield return null;
        }

        _windowGroup.alpha = targetAlpha;

        if (!show)
        {
            _windowObj.SetActive(false);
            _windowVisible = false;
        }

        _fadeCoroutine = null;
        Debug.Log($"[PolarizerDialWindow] FadeWindow complete, alpha={_windowGroup.alpha}");
    }

    private static Canvas GetOrCreateWindowsCanvas()
    {
        GameObject canvasObj = GameObject.Find("WindowsCanvas");
        if (canvasObj != null) return canvasObj.GetComponent<Canvas>();

        canvasObj = new GameObject("WindowsCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObj.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    /// <summary>
    /// 独立淡出执行器。挂在窗口 GameObject 上，
    /// 不受 CloseUpUI 关闭影响，渐变完成后自动销毁自身。
    /// </summary>
    private class FadeOutRunner : MonoBehaviour
    {
        private CanvasGroup _group;
        private float _duration;

        public void Run(CanvasGroup group, float duration)
        {
            _group = group;
            _duration = duration;
            StartCoroutine(FadeOutAndDestroy());
        }

        private System.Collections.IEnumerator FadeOutAndDestroy()
        {
            float elapsed = 0f;
            float startAlpha = _group.alpha;

            while (elapsed < _duration)
            {
                elapsed += Time.deltaTime;
                _group.alpha = Mathf.Lerp(startAlpha, 0f, elapsed / _duration);
                yield return null;
            }

            _group.alpha = 0f;
            gameObject.SetActive(false);
            Destroy(this);
        }
    }
}

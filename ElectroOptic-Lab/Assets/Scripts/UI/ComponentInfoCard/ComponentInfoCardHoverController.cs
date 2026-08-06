using UnityEngine;
using UnityEngine.EventSystems;

namespace ElectroOptics.UI.ComponentInfoCard
{
    /// <summary>
    /// Scene2 元件介绍卡的集中式悬停检测、延迟、定位与淡入淡出控制器。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ComponentInfoCardHoverController : MonoBehaviour
    {
        private const int HitBufferSize = 32;

        [Header("References")]
        [SerializeField] private Camera hoverCamera;
        [SerializeField] private Canvas targetCanvas;
        [SerializeField] private ComponentInfoCardView cardView;

        [Header("Hover Detection")]
        [SerializeField] private LayerMask hoverLayerMask = 1 << 7;
        [SerializeField, Min(0f)] private float hoverDelay = 0.6f;
        [SerializeField] private bool disableInCloseUp = true;

        [Header("Presentation")]
        [SerializeField, Min(0f)] private float fadeDuration = 0.18f;
        [SerializeField] private Vector2 screenOffset = new Vector2(24f, 18f);
        [SerializeField, Min(0f)] private float edgePadding = 20f;

        private readonly RaycastHit[] hitBuffer = new RaycastHit[HitBufferSize];
        private ComponentInfoCardTarget hoveredTarget;
        private ComponentInfoCardTarget visibleTarget;
        private float hoverElapsed;
        private float desiredAlpha;
        private bool hasApplicationFocus = true;
        private int lastScreenWidth;
        private int lastScreenHeight;

        public ComponentInfoCardTarget HoveredTarget => hoveredTarget;
        public ComponentInfoCardTarget VisibleTarget => visibleTarget;
        public float HoverDelay => hoverDelay;
        public float FadeDuration => fadeDuration;
        public bool IsCardVisible => cardView != null
                                     && cardView.RootGroup != null
                                     && cardView.RootGroup.alpha > 0f;

        private void Awake()
        {
            ResolveReferences();
            PrepareHiddenState();
        }

        private void Update()
        {
            float deltaTime = Time.unscaledDeltaTime;

            if (Screen.width != lastScreenWidth || Screen.height != lastScreenHeight)
            {
                lastScreenWidth = Screen.width;
                lastScreenHeight = Screen.height;
                ClampLockedPosition();
            }

            if (!CanDetectHover())
            {
                ResetHoverState(false);
                UpdateFade(deltaTime);
                return;
            }

            ProcessDetectedTarget(FindHoveredTarget(), deltaTime);
            UpdateFade(deltaTime);
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            hasApplicationFocus = hasFocus;
            if (!hasFocus)
                ResetHoverState(true);
        }

        private void OnDisable()
        {
            ResetHoverState(true);
        }

        private void ResolveReferences()
        {
            if (hoverCamera == null)
                hoverCamera = Camera.main;

            if (cardView != null && targetCanvas == null)
                targetCanvas = cardView.GetComponentInParent<Canvas>();
        }

        private void PrepareHiddenState()
        {
            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;
            desiredAlpha = 0f;

            if (cardView == null || cardView.RootGroup == null)
                return;

            cardView.RootGroup.alpha = 0f;
            cardView.RootGroup.interactable = false;
            cardView.RootGroup.blocksRaycasts = false;
            cardView.gameObject.SetActive(false);
        }

        private bool CanDetectHover()
        {
            if (!hasApplicationFocus || cardView == null || !cardView.IsValid)
                return false;

            if (disableInCloseUp && global::ExperimentCameraController.IsInCloseUpView)
                return false;

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return false;

            Vector3 mousePosition = Input.mousePosition;
            return mousePosition.x >= 0f
                   && mousePosition.y >= 0f
                   && mousePosition.x <= Screen.width
                   && mousePosition.y <= Screen.height;
        }

        private ComponentInfoCardTarget FindHoveredTarget()
        {
            Camera cameraToUse = hoverCamera != null ? hoverCamera : Camera.main;
            if (cameraToUse == null)
                return null;

            Ray ray = cameraToUse.ScreenPointToRay(Input.mousePosition);
            int hitCount = Physics.RaycastNonAlloc(
                ray,
                hitBuffer,
                cameraToUse.farClipPlane,
                hoverLayerMask,
                QueryTriggerInteraction.Ignore);

            ComponentInfoCardTarget nearestTarget = null;
            float nearestDistance = float.PositiveInfinity;
            for (int index = 0; index < hitCount; index++)
            {
                RaycastHit hit = hitBuffer[index];
                if (hit.collider == null || hit.distance >= nearestDistance)
                    continue;

                ComponentInfoCardTarget target = hit.collider.GetComponentInParent<ComponentInfoCardTarget>();
                if (target == null || !target.isActiveAndEnabled || !target.IsValid)
                    continue;

                nearestTarget = target;
                nearestDistance = hit.distance;
            }

            return nearestTarget;
        }

        private void ProcessDetectedTarget(ComponentInfoCardTarget detectedTarget, float deltaTime)
        {
            if (detectedTarget != hoveredTarget)
            {
                hoveredTarget = detectedTarget;
                visibleTarget = null;
                hoverElapsed = 0f;
                BeginFadeOut();
            }

            if (hoveredTarget == null || !hoveredTarget.isActiveAndEnabled || !hoveredTarget.IsValid)
                return;

            if (visibleTarget == hoveredTarget)
                return;

            hoverElapsed += Mathf.Max(0f, deltaTime);
            if (hoverElapsed + 0.0001f < hoverDelay)
                return;

            ShowTarget(hoveredTarget);
        }

        private void ShowTarget(ComponentInfoCardTarget target)
        {
            if (target == null || target.Content == null || !TryLockCardPosition(target))
            {
                ResetHoverState(false);
                return;
            }

            cardView.SetContent(
                target.Content.ComponentName,
                target.Content.Description,
                target.Content.PreviewSprite);
            cardView.transform.SetAsLastSibling();
            cardView.gameObject.SetActive(true);
            visibleTarget = target;
            desiredAlpha = 1f;
        }

        private bool TryLockCardPosition(ComponentInfoCardTarget target)
        {
            if (targetCanvas == null || cardView == null || cardView.CardRect == null)
                return false;

            if (!target.TryGetWorldBounds(out Bounds bounds))
                return false;

            Camera cameraToUse = hoverCamera != null ? hoverCamera : Camera.main;
            if (cameraToUse == null)
                return false;

            Vector3 worldAnchor = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
            Vector3 screenPoint = cameraToUse.WorldToScreenPoint(worldAnchor);
            if (screenPoint.z <= 0f)
                return false;

            RectTransform canvasRect = targetCanvas.transform as RectTransform;
            if (canvasRect == null)
                return false;

            Camera canvasCamera = targetCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : targetCanvas.worldCamera;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect,
                    screenPoint,
                    canvasCamera,
                    out Vector2 localAnchor))
            {
                return false;
            }

            RectTransform cardRect = cardView.CardRect;
            Vector2 cardSize = cardRect.rect.size;
            Vector2 pivot = cardRect.pivot;

            Vector2 position = new Vector2(
                localAnchor.x + screenOffset.x + cardSize.x * pivot.x,
                localAnchor.y + screenOffset.y + cardSize.y * pivot.y);

            Rect canvasBounds = canvasRect.rect;
            float rightEdge = position.x + cardSize.x * (1f - pivot.x);
            if (rightEdge > canvasBounds.xMax - edgePadding)
            {
                position.x = localAnchor.x - screenOffset.x - cardSize.x * (1f - pivot.x);
            }

            float topEdge = position.y + cardSize.y * (1f - pivot.y);
            if (topEdge > canvasBounds.yMax - edgePadding)
            {
                position.y = localAnchor.y - screenOffset.y - cardSize.y * (1f - pivot.y);
            }

            cardRect.anchoredPosition = ClampToCanvas(position);
            return true;
        }

        private void BeginFadeOut()
        {
            desiredAlpha = 0f;
        }

        private void UpdateFade(float deltaTime)
        {
            if (cardView == null || cardView.RootGroup == null)
                return;

            CanvasGroup group = cardView.RootGroup;
            if (desiredAlpha > 0f && !cardView.gameObject.activeSelf)
                cardView.gameObject.SetActive(true);

            if (fadeDuration <= 0f)
            {
                group.alpha = desiredAlpha;
            }
            else
            {
                group.alpha = Mathf.MoveTowards(
                    group.alpha,
                    desiredAlpha,
                    Mathf.Max(0f, deltaTime) / fadeDuration);
            }

            if (desiredAlpha <= 0f && group.alpha <= 0f && cardView.gameObject.activeSelf)
                cardView.gameObject.SetActive(false);
        }

        private void ResetHoverState(bool immediate)
        {
            hoveredTarget = null;
            visibleTarget = null;
            hoverElapsed = 0f;
            desiredAlpha = 0f;

            if (!immediate || cardView == null || cardView.RootGroup == null)
                return;

            cardView.RootGroup.alpha = 0f;
            cardView.gameObject.SetActive(false);
        }

        private void ClampLockedPosition()
        {
            if (cardView == null || cardView.CardRect == null || targetCanvas == null)
                return;

            cardView.CardRect.anchoredPosition = ClampToCanvas(cardView.CardRect.anchoredPosition);
        }

        private Vector2 ClampToCanvas(Vector2 position)
        {
            RectTransform canvasRect = targetCanvas != null ? targetCanvas.transform as RectTransform : null;
            RectTransform cardRect = cardView != null ? cardView.CardRect : null;
            if (canvasRect == null || cardRect == null)
                return position;

            Rect canvasBounds = canvasRect.rect;
            Vector2 cardSize = cardRect.rect.size;
            Vector2 pivot = cardRect.pivot;

            float minX = canvasBounds.xMin + edgePadding + cardSize.x * pivot.x;
            float maxX = canvasBounds.xMax - edgePadding - cardSize.x * (1f - pivot.x);
            float minY = canvasBounds.yMin + edgePadding + cardSize.y * pivot.y;
            float maxY = canvasBounds.yMax - edgePadding - cardSize.y * (1f - pivot.y);

            position.x = minX <= maxX ? Mathf.Clamp(position.x, minX, maxX) : canvasBounds.center.x;
            position.y = minY <= maxY ? Mathf.Clamp(position.y, minY, maxY) : canvasBounds.center.y;
            return position;
        }
    }
}

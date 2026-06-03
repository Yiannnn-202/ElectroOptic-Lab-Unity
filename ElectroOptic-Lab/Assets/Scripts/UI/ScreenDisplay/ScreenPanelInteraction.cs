using UnityEngine;
using UnityEngine.EventSystems;

namespace ElectroOptics.UI.ScreenDisplay
{
    /// <summary>
    /// 统一光屏面板的点击交互组件
    /// 挂在 ClickOverlay 子物体上（透明覆盖层），双击加载附加实验场景
    /// </summary>
    public class ScreenPanelInteraction : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("双击配置")]
        [Tooltip("双击间隔阈值（秒）")]
        public float doubleClickInterval = 0.3f;

        [Header("场景跳转")]
        [Tooltip("双击后加载的目标场景名")]
        public string targetSceneName = "Scene_additional_exp";

        private float _lastClickTime;
        private UIOutline _uiOutline;

        public void OnPointerClick(PointerEventData eventData)
        {
            float currentTime = Time.time;

            if (currentTime - _lastClickTime <= doubleClickInterval)
            {
                _lastClickTime = 0f;
                OnDoubleClick();
            }
            else
            {
                _lastClickTime = currentTime;
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            SetHighlight(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            SetHighlight(false);
        }

        private void OnDisable()
        {
            SetHighlight(false);
        }

        private void OnDoubleClick()
        {
            string scenePath = $"Assets/Scenes/{targetSceneName}.unity";
            Debug.Log($"[ScreenPanelInteraction] 双击触发，加载场景: {targetSceneName}");

            if (Application.isPlaying)
            {
                global::Scene2AdditionalSceneNavigator.OpenAdditionalExperiment(targetSceneName);
            }
            else
            {
#if UNITY_EDITOR
                UnityEditor.SceneManagement.EditorSceneManager.LoadScene(scenePath);
#endif
            }
        }

        private void SetHighlight(bool highlighted)
        {
            UIOutline outline = GetOutline();
            if (outline != null)
            {
                outline.IsHighlighted = highlighted;
            }
        }

        private UIOutline GetOutline()
        {
            if (_uiOutline == null)
            {
                _uiOutline = GetComponent<UIOutline>();
            }

            return _uiOutline;
        }
    }
}

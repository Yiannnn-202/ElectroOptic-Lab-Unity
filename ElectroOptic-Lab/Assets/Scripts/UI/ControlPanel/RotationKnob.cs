using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System;

namespace ElectroOptics.UI.ControlPanel
{
    /// <summary>
    /// 旋钮控件
    /// 支持鼠标拖拽旋转，角度范围限制
    /// </summary>
    public class RotationKnob : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        #region 事件

        /// <summary>
        /// 角度变化事件
        /// </summary>
        public event Action<float> OnAngleChanged;

        #endregion

        #region 私有字段

        private float _minAngle = -15f;
        private float _maxAngle = 15f;
        private float _step = 0.5f;
        private int _axisIndex = 0; // 0 = X轴, 1 = Y轴

        private float _currentAngle = 0f;
        private bool _isDragging = false;
        private Vector2 _dragStartPos;
        private float _dragStartAngle;

        private RectTransform _rectTransform;
        private Image _knobImage;

        #endregion

        #region 初始化

        /// <summary>
        /// 初始化旋钮
        /// </summary>
        /// <param name="minAngle">最小角度</param>
        /// <param name="maxAngle">最大角度</param>
        /// <param name="step">角度步进</param>
        /// <param name="axisIndex">轴索引（0=X, 1=Y）</param>
        public void Initialize(float minAngle, float maxAngle, float step, int axisIndex)
        {
            _minAngle = minAngle;
            _maxAngle = maxAngle;
            _step = step;
            _axisIndex = axisIndex;

            _rectTransform = GetComponent<RectTransform>();
            _knobImage = GetComponent<Image>();

            Debug.Log($"[RotationKnob] 初始化完成 - 轴:{(_axisIndex == 0 ? "X" : "Y")}, 范围:[{_minAngle}, {_maxAngle}], 步进:{_step}");
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 设置角度（外部调用）
        /// </summary>
        /// <param name="angle">目标角度</param>
        public void SetAngle(float angle)
        {
            _currentAngle = ClampAngle(angle);
            UpdateVisualRotation();
        }

        /// <summary>
        /// 获取当前角度
        /// </summary>
        /// <returns>当前角度</returns>
        public float GetAngle()
        {
            return _currentAngle;
        }

        #endregion

        #region 拖拽接口实现

        public void OnBeginDrag(PointerEventData eventData)
        {
            _isDragging = true;
            _dragStartPos = eventData.position;
            _dragStartAngle = _currentAngle;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_isDragging)
                return;

            // 计算旋钮中心在屏幕上的位置
            Vector2 knobCenter;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _rectTransform.parent as RectTransform,
                _rectTransform.position,
                eventData.pressEventCamera,
                out knobCenter);

            // 计算鼠标相对于旋钮中心的位置
            Vector2 currentPos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _rectTransform.parent as RectTransform,
                eventData.position,
                eventData.pressEventCamera,
                out currentPos);

            // 计算起始位置的角度
            Vector2 startLocalPos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _rectTransform.parent as RectTransform,
                _dragStartPos,
                eventData.pressEventCamera,
                out startLocalPos);

            Vector2 startDir = startLocalPos - knobCenter;
            Vector2 currentDir = currentPos - knobCenter;

            // 计算角度变化（使用 atan2）
            float startAngle = Mathf.Atan2(startDir.y, startDir.x) * Mathf.Rad2Deg;
            float currentAngleRad = Mathf.Atan2(currentDir.y, currentDir.x) * Mathf.Rad2Deg;
            float angleDelta = currentAngleRad - startAngle;

            // 计算新角度
            float newAngle = _dragStartAngle + angleDelta;

            // 量化到步进值
            newAngle = Mathf.Round(newAngle / _step) * _step;

            // 限制范围
            newAngle = ClampAngle(newAngle);

            // 检查是否有变化
            if (Mathf.Abs(newAngle - _currentAngle) > 0.01f)
            {
                _currentAngle = newAngle;
                UpdateVisualRotation();
                OnAngleChanged?.Invoke(_currentAngle);
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            _isDragging = false;
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 限制角度在范围内
        /// </summary>
        private float ClampAngle(float angle)
        {
            return Mathf.Clamp(angle, _minAngle, _maxAngle);
        }

        /// <summary>
        /// 更新视觉旋转
        /// </summary>
        private void UpdateVisualRotation()
        {
            if (_knobImage != null)
            {
                // 旋钮图片旋转（Z轴）
                // 将角度映射到视觉旋转（放大显示效果）
                float visualRotation = -_currentAngle * 3f; // 放大3倍使旋转更明显
                _knobImage.rectTransform.localRotation = Quaternion.Euler(0, 0, visualRotation);
            }
        }

        #endregion
    }
}

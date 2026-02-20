using UnityEngine;
using UnityEngine.UI;

namespace ElectroOptics.UI.ControlPanel
{
    /// <summary>
    /// 角度显示组件
    /// 格式化显示角度值，带正负号
    /// </summary>
    [RequireComponent(typeof(Text))]
    public class AngleDisplay : MonoBehaviour
    {
        #region 私有字段

        private Text _displayText;
        private string _prefix = "";
        private float _currentAngle = 0f;

        #endregion

        #region 初始化

        /// <summary>
        /// 初始化显示组件
        /// </summary>
        /// <param name="prefix">前缀（如 "X: " 或 "Y: "）</param>
        public void Initialize(string prefix)
        {
            _prefix = prefix;
            _displayText = GetComponent<Text>();
            UpdateDisplay();
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 设置显示的角度值
        /// </summary>
        /// <param name="angle">角度值</param>
        public void SetAngle(float angle)
        {
            _currentAngle = angle;
            UpdateDisplay();
        }

        /// <summary>
        /// 获取当前显示的角度值
        /// </summary>
        /// <returns>角度值</returns>
        public float GetAngle()
        {
            return _currentAngle;
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 更新显示文本
        /// 格式：{prefix}{sign}{value:F1}°
        /// 例如：X: +5.0° 或 Y: -3.5°
        /// </summary>
        private void UpdateDisplay()
        {
            if (_displayText == null)
            {
                _displayText = GetComponent<Text>();
            }

            if (_displayText != null)
            {
                string sign = _currentAngle >= 0 ? "+" : "";
                _displayText.text = $"{_prefix}{sign}{_currentAngle:F1}°";
            }
        }

        #endregion

        #region Unity 生命周期

        private void Start()
        {
            if (_displayText == null)
            {
                _displayText = GetComponent<Text>();
            }
        }

        #endregion
    }
}

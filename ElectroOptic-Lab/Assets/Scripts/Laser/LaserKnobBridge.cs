using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace ElectroOptics.Experiment.Controller
{
    public class LaserKnobBridge : MonoBehaviour
    {
        [Header("控制目标")]
        [Tooltip("把激光器的外壳（或者FirePoint锚点）拖进来")]
        public Transform targetLaser;

        [Header("右上螺钉 (Top-Right)")]
        [SerializeField] private Button btn_TR_Clockwise;
        [SerializeField] private Button btn_TR_Counter;

        // 🎯 核心修改1：变量名改成 rotationAxisTR，强制 Unity 刷新缓存
        [Tooltip("右上螺钉的旋转轴向 (比如 X:0, Y:-0.5, Z:1)")]
        [SerializeField] private Vector3 rotationAxisTR = new Vector3(0, 0, 1);

        [Header("左下螺钉 (Bottom-Left)")]
        [SerializeField] private Button btn_BL_Clockwise;
        [SerializeField] private Button btn_BL_Counter;

        // 🎯 核心修改2：变量名改成 rotationAxisBL
        [Tooltip("左下螺钉的旋转轴向 (比如 X:0, Y:0.5, Z:1)")]
        [SerializeField] private Vector3 rotationAxisBL = new Vector3(0, 0, 1);

        [Header("全局旋转参数")]
        [SerializeField] private float degreesPerSecond = 1f;

        private bool _trCW_pressed;
        private bool _trCCW_pressed;
        private bool _blCW_pressed;
        private bool _blCCW_pressed;

        private void OnEnable() { SetupButtonEvents(); }

        private void OnDisable()
        {
            _trCW_pressed = false;
            _trCCW_pressed = false;
            _blCW_pressed = false;
            _blCCW_pressed = false;
        }

        private void Update()
        {
            if (targetLaser == null) return;

            // 🎯 核心修改3：下面计算旋转的地方，也要换成新的变量名
            if (_trCW_pressed)
                targetLaser.Rotate(rotationAxisTR * degreesPerSecond * Time.deltaTime, Space.Self);
            if (_trCCW_pressed)
                targetLaser.Rotate(-rotationAxisTR * degreesPerSecond * Time.deltaTime, Space.Self);

            if (_blCW_pressed)
                targetLaser.Rotate(rotationAxisBL * degreesPerSecond * Time.deltaTime, Space.Self);
            if (_blCCW_pressed)
                targetLaser.Rotate(-rotationAxisBL * degreesPerSecond * Time.deltaTime, Space.Self);
        }

        #region 核心：长按事件绑定逻辑
        private void SetupButtonEvents()
        {
            if (btn_TR_Clockwise != null) BindPressEvents(btn_TR_Clockwise.gameObject, (p) => _trCW_pressed = p);
            if (btn_TR_Counter != null) BindPressEvents(btn_TR_Counter.gameObject, (p) => _trCCW_pressed = p);
            if (btn_BL_Clockwise != null) BindPressEvents(btn_BL_Clockwise.gameObject, (p) => _blCW_pressed = p);
            if (btn_BL_Counter != null) BindPressEvents(btn_BL_Counter.gameObject, (p) => _blCCW_pressed = p);
        }

        private void BindPressEvents(GameObject target, System.Action<bool> setPressed)
        {
            var trigger = target.GetComponent<EventTrigger>();
            if (trigger == null) trigger = target.AddComponent<EventTrigger>();
            if (trigger.triggers.Exists(e => e.eventID == EventTriggerType.PointerDown)) return;

            var downEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            downEntry.callback.AddListener((_) => setPressed(true));
            trigger.triggers.Add(downEntry);

            var upEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
            upEntry.callback.AddListener((_) => setPressed(false));
            trigger.triggers.Add(upEntry);
        }
        #endregion
    }
}

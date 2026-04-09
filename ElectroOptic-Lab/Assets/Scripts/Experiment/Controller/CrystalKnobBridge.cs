using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using ElectroOptics.DataTransfer;

namespace ElectroOptics.Experiment.Controller
{
    /// <summary>
    /// 桥接晶体盒旋钮按钮与晶体物理系统
    /// 挂载在 CloseUpUI 上，按住按钮时持续调整晶体旋转角度
    /// </summary>
    public class CrystalKnobBridge : MonoBehaviour
    {
        #region Inspector 字段

        [Header("按钮引用")]
        [SerializeField] private Button btnLU_Clockwise;
        [SerializeField] private Button btnLU_AntiClockwise;
        [SerializeField] private Button btnRD_Clockwise;
        [SerializeField] private Button btnRD_AntiClockwise;

        [Header("轴映射")]
        [Tooltip("LU 旋钮控制的轴: 0=X轴(俯仰), 1=Y轴(偏航)")]
        [Range(0, 1)]
        [SerializeField] private int luAxisIndex = 0;

        [Tooltip("RD 旋钮控制的轴: 0=X轴(俯仰), 1=Y轴(偏航)")]
        [Range(0, 1)]
        [SerializeField] private int rdAxisIndex = 1;

        [Header("旋转速度")]
        [SerializeField] private float degreesPerSecond = 5f;

        #endregion

        #region 运行时状态

        private bool _luCW_pressed;
        private bool _luCCW_pressed;
        private bool _rdCW_pressed;
        private bool _rdCCW_pressed;

        private CrystalControllerWrapper _controller;
        private bool _controllerResolved;
        private Coroutine _waitCoroutine;

        #endregion

        #region Unity 生命周期

        private void OnEnable()
        {
            Debug.Log("[CrystalKnobBridge] OnEnable 被调用");

            if (!_controllerResolved)
            {
                TryResolveController();
                if (!_controllerResolved)
                {
                    Debug.Log("[CrystalKnobBridge] Controller 未就绪，启动等待协程");
                    _waitCoroutine = StartCoroutine(WaitForController());
                }
            }

            SetupButtonEvents();
        }

        private void OnDisable()
        {
            _luCW_pressed = false;
            _luCCW_pressed = false;
            _rdCW_pressed = false;
            _rdCCW_pressed = false;

            if (_waitCoroutine != null)
            {
                StopCoroutine(_waitCoroutine);
                _waitCoroutine = null;
            }
        }

        private void Update()
        {
            if (!_controllerResolved) return;

            Vector2 delta = Vector2.zero;

            // LU 旋钮组
            if (_luCW_pressed)  delta[luAxisIndex] += degreesPerSecond * Time.deltaTime;
            if (_luCCW_pressed) delta[luAxisIndex] -= degreesPerSecond * Time.deltaTime;

            // RD 旋钮组
            if (_rdCW_pressed)  delta[rdAxisIndex] += degreesPerSecond * Time.deltaTime;
            if (_rdCCW_pressed) delta[rdAxisIndex] -= degreesPerSecond * Time.deltaTime;

            if (delta != Vector2.zero)
            {
                _controller.AddRotation(delta);
                Debug.Log($"[CrystalKnobBridge] 旋转 delta=({delta.x:F3}, {delta.y:F3}), 当前角度={_controller.CurrentRotation}");
            }
        }

        #endregion

        #region Controller 获取

        private void TryResolveController()
        {
            if (CrystalRuntime.Controller != null && CrystalRuntime.Controller.IsInitialized())
            {
                _controller = CrystalRuntime.Controller;
                _controllerResolved = true;
                Debug.Log("[CrystalKnobBridge] Controller 已获取成功");
            }
        }

        private IEnumerator WaitForController()
        {
            while (!_controllerResolved)
            {
                TryResolveController();
                yield return null;
            }
            _waitCoroutine = null;
        }

        #endregion

        #region 按钮事件绑定

        private void SetupButtonEvents()
        {
            if (btnLU_Clockwise != null)
                BindPressEvents(btnLU_Clockwise.gameObject, (p) => _luCW_pressed = p);

            if (btnLU_AntiClockwise != null)
                BindPressEvents(btnLU_AntiClockwise.gameObject, (p) => _luCCW_pressed = p);

            if (btnRD_Clockwise != null)
                BindPressEvents(btnRD_Clockwise.gameObject, (p) => _rdCW_pressed = p);

            if (btnRD_AntiClockwise != null)
                BindPressEvents(btnRD_AntiClockwise.gameObject, (p) => _rdCCW_pressed = p);
        }

        private void BindPressEvents(GameObject target, System.Action<bool> setPressed)
        {
            var trigger = target.GetComponent<EventTrigger>();
            if (trigger == null)
                trigger = target.AddComponent<EventTrigger>();

            // 避免重复绑定：检查是否已有 PointerDown entry
            if (trigger.triggers.Exists(e => e.eventID == EventTriggerType.PointerDown))
                return;

            var downEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            downEntry.callback.AddListener((_) => {
                setPressed(true);
                Debug.Log($"[CrystalKnobBridge] 按钮按下: {target.name}");
            });
            trigger.triggers.Add(downEntry);

            var upEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
            upEntry.callback.AddListener((_) => {
                setPressed(false);
                Debug.Log($"[CrystalKnobBridge] 按钮松开: {target.name}");
            });
            trigger.triggers.Add(upEntry);
        }

        #endregion
    }
}

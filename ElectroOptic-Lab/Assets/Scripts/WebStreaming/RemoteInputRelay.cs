using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace ElectroOptics.WebStreaming
{
    /// <summary>
    /// 静态桥接层：将 Render Streaming 的 Input System 事件转发为
    /// 与旧版 Input Manager 兼容的 API，让现有脚本只需做机械替换即可
    /// 在 Web 端操作。
    ///
    /// 用法：
    ///   Input.GetAxis("Vertical")   → RemoteInputRelay.GetAxis("Vertical")
    ///   Input.GetKey(KeyCode.W)     → RemoteInputRelay.GetKey(KeyCode.W)
    ///   Input.GetKeyDown(key)       → RemoteInputRelay.GetKeyDown(key)
    ///   Input.mousePosition         → RemoteInputRelay.MousePosition
    ///   Input.GetMouseButtonDown(0) → RemoteInputRelay.GetMouseButtonDown(0)
    /// </summary>
    public static class RemoteInputRelay
    {
        // ── 状态 ──────────────────────────────────────────

        /// <summary>是否处于远程流式传输模式（由 WebStreamingSetup 设置）</summary>
        public static bool IsRemote { get; set; }

        // ── 键盘 ──────────────────────────────────────────

        private static HashSet<KeyCode> heldKeys = new HashSet<KeyCode>();
        private static HashSet<KeyCode> downKeysThisFrame = new HashSet<KeyCode>();
        private static HashSet<KeyCode> upKeysThisFrame = new HashSet<KeyCode>();
        private static HashSet<KeyCode> prevFrameKeys = new HashSet<KeyCode>();

        // ── 鼠标 ──────────────────────────────────────────

        private static Vector3 mousePos = Vector3.zero;
        private static bool mouse0Held, mouse0Down, mouse0Up;
        private static bool mouse1Held, mouse1Down, mouse1Up;
        private static bool prevMouse0, prevMouse1;

        // ── 轴映射（从新 Input System 键盘设备读取）───────

        private static Dictionary<string, (KeyCode positive, KeyCode negative)> axisBindings = new Dictionary<string, (KeyCode, KeyCode)>
        {
            { "Vertical",   (KeyCode.W, KeyCode.S) },
            { "Horizontal", (KeyCode.D, KeyCode.A) },
        };

        // ═══════════════════════════════════════════════════
        //  公共 API（替换 Input 类的方法）
        // ═══════════════════════════════════════════════════

        public static float GetAxis(string axisName)
        {
            if (!IsRemote)
                return Input.GetAxis(axisName);

            if (!axisBindings.TryGetValue(axisName, out var binding))
                return 0f;

            float val = 0f;
            if (heldKeys.Contains(binding.positive)) val += 1f;
            if (heldKeys.Contains(binding.negative)) val -= 1f;
            return val;
        }

        public static float GetAxisRaw(string axisName)
        {
            if (!IsRemote)
                return Input.GetAxisRaw(axisName);

            return GetAxis(axisName); // 远程模式下无平滑，直接返回原始值
        }

        public static bool GetKey(KeyCode key)
        {
            if (!IsRemote)
                return Input.GetKey(key);

            return heldKeys.Contains(key);
        }

        public static bool GetKeyDown(KeyCode key)
        {
            if (!IsRemote)
                return Input.GetKeyDown(key);

            return downKeysThisFrame.Contains(key);
        }

        public static bool GetKeyUp(KeyCode key)
        {
            if (!IsRemote)
                return Input.GetKeyUp(key);

            return upKeysThisFrame.Contains(key);
        }

        public static Vector3 MousePosition
        {
            get
            {
                if (!IsRemote)
                    return Input.mousePosition;

                return mousePos;
            }
        }

        public static bool GetMouseButton(int button)
        {
            if (!IsRemote)
                return Input.GetMouseButton(button);

            return button == 0 ? mouse0Held : (button == 1 ? mouse1Held : false);
        }

        public static bool GetMouseButtonDown(int button)
        {
            if (!IsRemote)
                return Input.GetMouseButtonDown(button);

            return button == 0 ? mouse0Down : (button == 1 ? mouse1Down : false);
        }

        public static bool GetMouseButtonUp(int button)
        {
            if (!IsRemote)
                return Input.GetMouseButtonUp(button);

            return button == 0 ? mouse0Up : (button == 1 ? mouse1Up : false);
        }

        // ═══════════════════════════════════════════════════
        //  内部：由 RemoteInputUpdater 每帧调用
        // ═══════════════════════════════════════════════════

        /// <summary>每帧开始时调用，清除单帧状态</summary>
        internal static void BeginFrame()
        {
            downKeysThisFrame.Clear();
            upKeysThisFrame.Clear();
            mouse0Down = false;
            mouse0Up = false;
            mouse1Down = false;
            mouse1Up = false;
        }

        /// <summary>从 Input System Keyboard 同步按键状态</summary>
        internal static void SyncKeyboard()
        {
            if (Keyboard.current == null) return;

            // 记录本帧按下的键
            var currentKeys = new HashSet<KeyCode>();
            foreach (var keyEvent in Keyboard.current.allKeys)
            {
                if (keyEvent.isPressed)
                {
                    var keyCode = KeyCodeFromInputSystemKey(keyEvent.keyCode);
                    currentKeys.Add(keyCode);
                }
            }

            // 对比上一帧，产生 down / up
            foreach (var k in currentKeys)
            {
                if (!prevFrameKeys.Contains(k))
                    downKeysThisFrame.Add(k);
            }
            foreach (var k in prevFrameKeys)
            {
                if (!currentKeys.Contains(k))
                    upKeysThisFrame.Add(k);
            }

            heldKeys = currentKeys;
            prevFrameKeys = new HashSet<KeyCode>(currentKeys);
        }

        /// <summary>从 Input System Mouse 同步鼠标状态</summary>
        internal static void SyncMouse()
        {
            if (Mouse.current == null)
            {
                if (Touchscreen.current != null)
                {
                    // 触摸屏映射为鼠标
                    var touch = Touchscreen.current;
                    if (touch.primaryTouch.press.isPressed)
                    {
                        var pos = touch.primaryTouch.position.ReadValue();
                        mousePos = new Vector3(pos.x, pos.y, 0f);
                        mouse0Held = true;
                        if (!prevMouse0) { mouse0Down = true; prevMouse0 = true; }
                        else mouse0Down = false;
                    }
                    else
                    {
                        mouse0Held = false;
                        if (prevMouse0) { mouse0Up = true; prevMouse0 = false; }
                        else mouse0Up = false;
                    }
                    return;
                }
                return;
            }

            var m = Mouse.current;
            var mPos = m.position.ReadValue();
            mousePos = new Vector3(mPos.x, mPos.y, 0f);

            // 左键
            bool m0 = m.leftButton.isPressed;
            mouse0Held = m0;
            if (m0 && !prevMouse0) { mouse0Down = true; prevMouse0 = true; }
            else if (!m0 && prevMouse0) { mouse0Up = true; prevMouse0 = false; }
            else mouse0Down = mouse0Up = false;

            // 右键
            bool m1 = m.rightButton.isPressed;
            mouse1Held = m1;
            if (m1 && !prevMouse1) { mouse1Down = true; prevMouse1 = true; }
            else if (!m1 && prevMouse1) { mouse1Up = true; prevMouse1 = false; }
            else mouse1Down = mouse1Up = false;
        }

        private static KeyCode KeyCodeFromInputSystemKey(Key key)
        {
            // Key enum 数值与 KeyCode enum 数值相同（Unity 内部设计）
            return (KeyCode)key;
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace ElectroOptics.Mobile
{
    /// <summary>
    /// Shared input bridge. Desktop keyboard remains active; mobile UI injects virtual keys.
    /// </summary>
    public static class MobileVirtualInput
    {
        private static readonly HashSet<KeyCode> HeldKeys = new HashSet<KeyCode>();
        private static readonly Dictionary<KeyCode, int> DownFrames = new Dictionary<KeyCode, int>();

        public static bool GetKey(KeyCode key)
        {
            return Input.GetKey(key) || HeldKeys.Contains(key);
        }

        public static bool GetKeyDown(KeyCode key)
        {
            return Input.GetKeyDown(key)
                   || (DownFrames.TryGetValue(key, out int frame) && frame == Time.frameCount);
        }

        public static float GetAxis(string axisName)
        {
            float virtualValue = GetVirtualAxis(axisName);
            return Mathf.Abs(virtualValue) > 0.01f ? virtualValue : Input.GetAxis(axisName);
        }

        public static float GetAxisRaw(string axisName)
        {
            float virtualValue = GetVirtualAxis(axisName);
            return Mathf.Abs(virtualValue) > 0.01f ? virtualValue : Input.GetAxisRaw(axisName);
        }

        public static void SetKey(KeyCode key, bool pressed)
        {
            if (pressed)
            {
                if (HeldKeys.Add(key))
                {
                    DownFrames[key] = Time.frameCount;
                }
            }
            else
            {
                HeldKeys.Remove(key);
            }
        }

        public static void ReleaseAll()
        {
            HeldKeys.Clear();
            DownFrames.Clear();
        }

        private static float GetVirtualAxis(string axisName)
        {
            switch (axisName)
            {
                case "Horizontal":
                    return GetDirection(KeyCode.A, KeyCode.LeftArrow, KeyCode.D, KeyCode.RightArrow);
                case "Vertical":
                    return GetDirection(KeyCode.S, KeyCode.DownArrow, KeyCode.W, KeyCode.UpArrow);
                default:
                    return 0f;
            }
        }

        private static float GetDirection(KeyCode negativeA, KeyCode negativeB, KeyCode positiveA, KeyCode positiveB)
        {
            float value = 0f;
            if (HeldKeys.Contains(negativeA) || HeldKeys.Contains(negativeB)) value -= 1f;
            if (HeldKeys.Contains(positiveA) || HeldKeys.Contains(positiveB)) value += 1f;
            return Mathf.Clamp(value, -1f, 1f);
        }
    }
}

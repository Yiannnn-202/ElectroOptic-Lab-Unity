using System.Collections;
using UnityEngine;

namespace ElectroOptics.UI.ScreenDisplay
{
    /// <summary>
    /// CanvasGroup 动画工具类
    /// 提供淡入、淡出、交叉淡入淡出等过渡效果
    /// </summary>
    public static class CanvasGroupTweener
    {
        /// <summary>
        /// 淡入（alpha 0 -> 1）
        /// </summary>
        public static IEnumerator FadeIn(CanvasGroup group, float duration)
        {
            float elapsed = 0f;
            group.alpha = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                group.alpha = Mathf.Clamp01(elapsed / duration);
                yield return null;
            }
            group.alpha = 1f;
        }

        /// <summary>
        /// 淡出（alpha 1 -> 0）
        /// </summary>
        public static IEnumerator FadeOut(CanvasGroup group, float duration)
        {
            float elapsed = 0f;
            group.alpha = 1f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                group.alpha = 1f - Mathf.Clamp01(elapsed / duration);
                yield return null;
            }
            group.alpha = 0f;
        }

        /// <summary>
        /// 交叉淡入淡出（并行：from 淡出，to 淡入）
        /// </summary>
        public static IEnumerator CrossFade(CanvasGroup from, CanvasGroup to, float duration)
        {
            float elapsed = 0f;
            from.alpha = 1f;
            to.alpha = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                from.alpha = 1f - t;
                to.alpha = t;
                yield return null;
            }
            from.alpha = 0f;
            to.alpha = 1f;
        }

        /// <summary>
        /// 立即设置 alpha
        /// </summary>
        public static void SetAlpha(CanvasGroup group, float alpha)
        {
            group.alpha = Mathf.Clamp01(alpha);
        }
    }
}

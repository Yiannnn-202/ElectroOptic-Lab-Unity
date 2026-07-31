using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ElectroOptics.Mobile
{
    /// <summary>
    /// Small mobile polish for Scene2-preview. Keeps the back button inside the 2560x1600 safe area.
    /// </summary>
    public class Scene2PreviewMobileLayoutAdapter : MonoBehaviour
    {
        private static bool _created;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_created || !MobileRuntime.IsActive)
            {
                return;
            }

            GameObject root = new GameObject("Scene2PreviewMobileLayoutAdapter");
            DontDestroyOnLoad(root);
            root.AddComponent<Scene2PreviewMobileLayoutAdapter>();
            _created = true;
        }

        private void LateUpdate()
        {
            if (!SceneManager.GetActiveScene().name.Contains("Scene2-preview"))
            {
                return;
            }

            Button button = FindBackButton();
            if (button == null)
            {
                return;
            }

            RectTransform rect = button.GetComponent<RectTransform>();
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(48f, -22f);
            rect.localScale = Vector3.one;
        }

        private static Button FindBackButton()
        {
            Button[] buttons = FindObjectsOfType<Button>();
            for (int i = 0; i < buttons.Length; i++)
            {
                Button button = buttons[i];
                if (button == null)
                {
                    continue;
                }

                if (ContainsBackText(button.transform))
                {
                    return button;
                }
            }

            return null;
        }

        private static bool ContainsBackText(Transform root)
        {
            TMP_Text tmp = root.GetComponentInChildren<TMP_Text>(true);
            if (tmp != null && tmp.text.Contains("返回"))
            {
                return true;
            }

            Text text = root.GetComponentInChildren<Text>(true);
            return text != null && text.text.Contains("返回");
        }
    }
}

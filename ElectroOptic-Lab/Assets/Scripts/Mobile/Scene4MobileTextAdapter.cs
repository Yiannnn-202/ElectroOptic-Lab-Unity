using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ElectroOptics.Mobile
{
    /// <summary>
    /// Hides desktop keyboard hints in Scene4 when mobile virtual controls are active.
    /// </summary>
    public class Scene4MobileTextAdapter : MonoBehaviour
    {
        private static bool _created;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_created || !MobileRuntime.IsActive)
            {
                return;
            }

            GameObject root = new GameObject("Scene4MobileTextAdapter");
            DontDestroyOnLoad(root);
            root.AddComponent<Scene4MobileTextAdapter>();
            _created = true;
        }

        private void LateUpdate()
        {
            if (!SceneManager.GetActiveScene().name.Contains("Scene4"))
            {
                return;
            }

            TMP_Text[] texts = FindObjectsOfType<TMP_Text>();
            for (int i = 0; i < texts.Length; i++)
            {
                TMP_Text text = texts[i];
                if (text != null && text.text.Contains("A / D") && text.text.Contains("调节电压"))
                {
                    text.gameObject.SetActive(false);
                }
            }
        }
    }
}

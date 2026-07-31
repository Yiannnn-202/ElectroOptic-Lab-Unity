using UnityEngine;
using UnityEngine.SceneManagement;

namespace ElectroOptics.Mobile
{
    /// <summary>
    /// Scene4-specific mobile layout for the oscilloscope area.
    /// Keeps CH1/CH2 display blocks aligned and leaves the key-point panel below them.
    /// </summary>
    public class Scene4MobileLayoutAdapter : MonoBehaviour
    {
        private const bool EnableRuntimeWaveGroupRelayout = true;
        private static bool _created;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_created || !MobileRuntime.IsActive)
            {
                return;
            }

            GameObject root = new GameObject("Scene4MobileLayoutAdapter");
            DontDestroyOnLoad(root);
            root.AddComponent<Scene4MobileLayoutAdapter>();
            _created = true;
        }

        private void LateUpdate()
        {
            if (!EnableRuntimeWaveGroupRelayout)
            {
                return;
            }

            if (!SceneManager.GetActiveScene().name.Contains("Scene4"))
            {
                return;
            }

            Transform dataCanvas = FindRoot("DataCanvas");
            if (dataCanvas == null)
            {
                return;
            }

            RectTransform wavePanel = FindRect(dataCanvas, "MainArea", "RightDisplayPanel", "WavePanel");
            RectTransform waveContent = FindRect(dataCanvas, "MainArea", "RightDisplayPanel", "WavePanel", "WaveContentArea");
            RectTransform ch1 = FindRect(dataCanvas, "MainArea", "RightDisplayPanel", "WavePanel", "WaveContentArea", "CH1Block");
            RectTransform ch2 = FindRect(dataCanvas, "MainArea", "RightDisplayPanel", "WavePanel", "WaveContentArea", "CH2Block");
            RectTransform resultPanel = FindRect(dataCanvas, "MainArea", "RightDisplayPanel", "ResultPanel");

            if (wavePanel != null)
            {
                Stretch(wavePanel, new Vector2(0f, 0.38f), Vector2.one, new Vector2(24f, 18f), new Vector2(-24f, -24f));
            }

            if (waveContent != null)
            {
                Stretch(waveContent, Vector2.zero, Vector2.one, new Vector2(24f, 24f), new Vector2(-24f, -72f));
            }

            if (ch1 != null)
            {
                Stretch(ch1, new Vector2(0f, 0.52f), Vector2.one, Vector2.zero, Vector2.zero);
            }

            if (ch2 != null)
            {
                Stretch(ch2, Vector2.zero, new Vector2(1f, 0.48f), Vector2.zero, Vector2.zero);
            }

            if (resultPanel != null)
            {
                Stretch(resultPanel, Vector2.zero, new Vector2(1f, 0.34f), new Vector2(48f, 18f), new Vector2(-48f, -18f));
            }
        }

        private static Transform FindRoot(string name)
        {
            Scene activeScene = SceneManager.GetActiveScene();
            GameObject[] roots = activeScene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i].name == name)
                {
                    return roots[i].transform;
                }
            }

            return null;
        }

        private static RectTransform FindRect(Transform root, params string[] path)
        {
            Transform current = root;
            for (int i = 0; i < path.Length && current != null; i++)
            {
                current = current.Find(path[i]);
            }

            return current as RectTransform;
        }

        private static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            rect.localScale = Vector3.one;
        }
    }
}

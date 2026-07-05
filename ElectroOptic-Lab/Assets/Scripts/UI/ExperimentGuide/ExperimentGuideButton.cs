using UnityEngine;
using UnityEngine.UI;

namespace ElectroOptics.UI.ExperimentGuide
{
    /// <summary>
    /// Button-facing entry point for the Scene2 experiment guide popup.
    /// Attach this to a UI Button and bind OpenGuide() in OnClick.
    /// </summary>
    public class ExperimentGuideButton : MonoBehaviour
    {
        [SerializeField] private ExperimentGuidePopup popup;
        [SerializeField] private string guideTitle = "实验操作指引";
        [SerializeField] private Sprite[] pages;
        [SerializeField] private bool createPopupIfMissing = true;
        [SerializeField] private bool bindButtonOnAwake = true;

        private Button _button;

        private void Awake()
        {
            if (!bindButtonOnAwake)
            {
                return;
            }

            _button = GetComponent<Button>();
            if (_button == null)
            {
                return;
            }

            _button.onClick.RemoveListener(OpenGuide);
            _button.onClick.AddListener(OpenGuide);
        }

        private void OnDestroy()
        {
            if (_button != null)
            {
                _button.onClick.RemoveListener(OpenGuide);
            }
        }

        public void OpenGuide()
        {
            ExperimentGuidePopup target = ResolvePopup();
            if (target == null)
            {
                Debug.LogError("[ExperimentGuideButton] No ExperimentGuidePopup found or created.");
                return;
            }

            target.SetTitle(guideTitle);
            if (pages != null && pages.Length > 0)
            {
                target.SetPages(pages);
            }

            target.Show();
        }

        private ExperimentGuidePopup ResolvePopup()
        {
            if (popup != null)
            {
                return popup;
            }

            ExperimentGuidePopup[] popups = FindObjectsOfType<ExperimentGuidePopup>(true);
            popup = popups.Length > 0 ? popups[0] : null;
            if (popup != null || !createPopupIfMissing)
            {
                return popup;
            }

            GameObject popupObject = new GameObject("ExperimentGuidePopup");
            popup = popupObject.AddComponent<ExperimentGuidePopup>();
            return popup;
        }
    }
}

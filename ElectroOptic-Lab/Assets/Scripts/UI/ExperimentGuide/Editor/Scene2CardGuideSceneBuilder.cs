#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ElectroOptics.UI.ExperimentGuide.Editor
{
    /// <summary>
    /// 在当前场景直接创建可编辑的卡片 UI 子物体。
    /// </summary>
    public static class Scene2CardGuideSceneBuilder
    {
        private const string SceneName = "Scene2.The Lab";
        private const string CanvasName = "WindowsCanvas";
        private const string RootName = "Scene2CardGuideRoot";

        private static readonly Color AccentColor = new Color(0.25f, 0.86f, 1f, 0.95f);
        private static readonly Color CardColor = new Color(0.03f, 0.10f, 0.16f, 0.62f);

        [MenuItem("ElectroOptics/Experiment Guide/Build Editable Card UI In Scene2")]
        public static void BuildEditableCardUi()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[Scene2CardGuide] 请先停止 Play Mode，再创建可编辑卡片。");
                return;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            if (!string.Equals(activeScene.name, SceneName, System.StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogWarning($"[Scene2CardGuide] 当前场景是 {activeScene.name}，建议在 {SceneName} 中执行。");
            }

            Scene2CardGuideView existing = Object.FindObjectOfType<Scene2CardGuideView>(true);
            if (existing != null)
            {
                Selection.activeGameObject = existing.gameObject;
                EditorGUIUtility.PingObject(existing.gameObject);
                Debug.Log("[Scene2CardGuide] 当前场景已经存在可编辑卡片。");
                return;
            }

            Canvas canvas = GetOrCreateCanvas();
            TMP_FontAsset font = FindChineseFont();

            GameObject root = CreateRectObject(RootName, canvas.transform);
            Undo.RegisterCreatedObjectUndo(root, "Create Editable Scene2 Card Guide");
            Stretch(root.GetComponent<RectTransform>());

            CanvasGroup rootGroup = root.AddComponent<CanvasGroup>();
            rootGroup.alpha = 1f;
            rootGroup.blocksRaycasts = false;
            rootGroup.interactable = false;

            Image overlay = root.AddComponent<Image>();
            overlay.color = new Color(0f, 0f, 0f, 0.025f);
            overlay.raycastTarget = false;

            Scene2CardGuideView view = root.AddComponent<Scene2CardGuideView>();

            RectTransform card = CreateRectObject("GuideCard", root.transform).GetComponent<RectTransform>();
            card.anchorMin = card.anchorMax = new Vector2(0f, 1f);
            card.pivot = new Vector2(0f, 1f);
            card.anchoredPosition = new Vector2(32f, -32f);
            card.sizeDelta = new Vector2(460f, 210f);

            Image cardImage = card.gameObject.AddComponent<Image>();
            cardImage.color = CardColor;
            cardImage.raycastTarget = true;
            UnityEngine.UI.Outline outline = card.gameObject.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = new Color(AccentColor.r, AccentColor.g, AccentColor.b, 0.25f);
            outline.effectDistance = new Vector2(1f, -1f);

            Image accent = CreateRectObject("AccentLine", card).AddComponent<Image>();
            RectTransform accentRect = accent.rectTransform;
            accentRect.anchorMin = new Vector2(0f, 1f);
            accentRect.anchorMax = new Vector2(1f, 1f);
            accentRect.pivot = new Vector2(0.5f, 1f);
            accentRect.sizeDelta = new Vector2(0f, 4f);
            accentRect.anchoredPosition = Vector2.zero;
            accent.color = AccentColor;

            TextMeshProUGUI step = CreateText("Step", card, "步骤 1 / 12", 16, AccentColor, TextAlignmentOptions.Left, font);
            SetRect(step.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(22f, -22f), new Vector2(180f, 22f));

            TextMeshProUGUI title = CreateText("Title", card, "第一步：放置光屏", 25, Color.white, TextAlignmentOptions.Left, font);
            SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(22f, -60f), new Vector2(-22f, -27f));
            title.fontStyle = FontStyles.Bold;

            TextMeshProUGUI body = CreateText("Body", card, "点击目标物体，然后按照卡片中的操作说明完成当前步骤。", 19, Color.white, TextAlignmentOptions.TopLeft, font);
            SetRect(body.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(22f, -145f), new Vector2(-22f, -74f));
            body.lineSpacing = 2f;

            Button close = CreateButton("CloseGuide", card, "退出引导", new Vector2(120f, 34f), AccentColor, font, out TextMeshProUGUI closeText);
            SetRect(close.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-142f, 18f), new Vector2(-22f, 52f));

            Button restart = CreateButton("RestartGuide", card, "重新开始", new Vector2(120f, 34f), new Color(0.12f, 0.30f, 0.40f, 0.78f), font, out _);
            SetRect(restart.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-272f, 18f), new Vector2(-152f, 52f));
            restart.gameObject.SetActive(false);

            RectTransform marker = CreateRectObject("TargetMarker", root.transform).GetComponent<RectTransform>();
            marker.anchorMin = marker.anchorMax = new Vector2(0.5f, 0.5f);
            marker.sizeDelta = new Vector2(76f, 76f);
            TextMeshProUGUI markerGlyph = CreateText("Glyph", marker, "O", 58, AccentColor, TextAlignmentOptions.Center, font);
            Stretch(markerGlyph.rectTransform);

            RectTransform line = CreateRectObject("GuideArrowLine", root.transform).GetComponent<RectTransform>();
            line.anchorMin = line.anchorMax = new Vector2(0.5f, 0.5f);
            line.pivot = new Vector2(0f, 0.5f);
            line.sizeDelta = new Vector2(1f, 4f);
            line.gameObject.AddComponent<Image>().color = AccentColor;

            RectTransform arrow = CreateRectObject("GuideArrowHead", root.transform).GetComponent<RectTransform>();
            arrow.anchorMin = arrow.anchorMax = new Vector2(0.5f, 0.5f);
            arrow.sizeDelta = new Vector2(48f, 48f);
            TextMeshProUGUI arrowGlyph = CreateText("Glyph", arrow, ">", 34, AccentColor, TextAlignmentOptions.Center, font);
            Stretch(arrowGlyph.rectTransform);

            RectTransform cursor = CreateRectObject("SimulatedHand", root.transform).GetComponent<RectTransform>();
            cursor.anchorMin = cursor.anchorMax = new Vector2(0.5f, 0.5f);
            cursor.sizeDelta = new Vector2(68f, 68f);
            Image cursorImage = cursor.gameObject.AddComponent<Image>();
            cursorImage.color = new Color(0.02f, 0.12f, 0.18f, 0.55f);
            cursorImage.raycastTarget = false;
            TextMeshProUGUI cursorGlyph = CreateText("Glyph", cursor, "+", 40, Color.white, TextAlignmentOptions.Center, font);
            Stretch(cursorGlyph.rectTransform);

            view.Configure(rootGroup, card, step, title, body, close, closeText, restart, line, arrow, marker, cursor);
            root.transform.SetAsLastSibling();

            EditorSceneManager.MarkSceneDirty(activeScene);
            Selection.activeGameObject = root;
            EditorGUIUtility.PingObject(root);
            Debug.Log("[Scene2CardGuide] 已创建可编辑卡片。请展开 WindowsCanvas/Scene2CardGuideRoot 修改子物体。");
        }

        [MenuItem("ElectroOptics/Experiment Guide/Clean Duplicate Card UI")]
        public static void CleanDuplicateCardUi()
        {
            GameObject canvasObject = GameObject.Find(CanvasName);
            if (canvasObject == null)
            {
                Debug.Log("[Scene2CardGuide] 没有找到 WindowsCanvas，无需清理。");
                return;
            }

            Scene2CardGuideView keepView = null;
            int removedCount = 0;

            for (int i = canvasObject.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = canvasObject.transform.GetChild(i);
                Scene2CardGuideView view = child.GetComponent<Scene2CardGuideView>();
                bool isGuideRoot = child.name == RootName || view != null;

                if (isGuideRoot)
                {
                    if (keepView == null && view != null)
                    {
                        keepView = view;
                        continue;
                    }

                    Undo.DestroyObjectImmediate(child.gameObject);
                    removedCount++;
                    continue;
                }

                // 清理以前可能直接挂在 WindowsCanvas 下的旧运行时子物体，
                // 不会触碰当前卡片 Root 内的同名子物体。
                if (child.name == "GuideCard"
                    || child.name == "TargetMarker"
                    || child.name == "GuideArrowLine"
                    || child.name == "GuideArrowHead"
                    || child.name == "SimulatedHand")
                {
                    Undo.DestroyObjectImmediate(child.gameObject);
                    removedCount++;
                }
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log($"[Scene2CardGuide] 已清理 {removedCount} 个重复/废弃卡片对象。");
        }

        private static Canvas GetOrCreateCanvas()
        {
            GameObject canvasObject = GameObject.Find(CanvasName);
            if (canvasObject == null)
            {
                canvasObject = new GameObject(CanvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                Canvas createdCanvas = canvasObject.GetComponent<Canvas>();
                createdCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                createdCanvas.sortingOrder = 100;

                CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
            }

            if (canvasObject.GetComponent<Canvas>() == null)
                canvasObject.AddComponent<Canvas>();
            if (canvasObject.GetComponent<CanvasScaler>() == null)
                canvasObject.AddComponent<CanvasScaler>();
            if (canvasObject.GetComponent<GraphicRaycaster>() == null)
                canvasObject.AddComponent<GraphicRaycaster>();

            return canvasObject.GetComponent<Canvas>();
        }

        private static TMP_FontAsset FindChineseFont()
        {
            string[] guids = AssetDatabase.FindAssets("SIMHEI SDF t:TMP_FontAsset");
            if (guids.Length > 0)
                return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guids[0]));

            return TMP_Settings.defaultFontAsset;
        }

        private static Button CreateButton(
            string name,
            Transform parent,
            string label,
            Vector2 size,
            Color color,
            TMP_FontAsset font,
            out TextMeshProUGUI labelText)
        {
            GameObject obj = CreateRectObject(name, parent);
            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.sizeDelta = size;

            Image image = obj.AddComponent<Image>();
            image.color = color;
            Button button = obj.AddComponent<Button>();
            button.targetGraphic = image;

            ColorBlock colors = button.colors;
            colors.normalColor = color;
            colors.highlightedColor = Color.Lerp(color, Color.white, 0.15f);
            colors.pressedColor = Color.Lerp(color, Color.black, 0.15f);
            button.colors = colors;

            labelText = CreateText("Label", obj.transform, label, 15, Color.white, TextAlignmentOptions.Center, font);
            Stretch(labelText.rectTransform);
            return button;
        }

        private static TextMeshProUGUI CreateText(
            string name,
            Transform parent,
            string text,
            int fontSize,
            Color color,
            TextAlignmentOptions alignment,
            TMP_FontAsset font)
        {
            GameObject obj = CreateRectObject(name, parent);
            TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.enableWordWrapping = true;
            tmp.raycastTarget = false;
            tmp.font = font;
            return tmp;
        }

        private static GameObject CreateRectObject(string name, Transform parent)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            return obj;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
        }

        private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }
    }
}
#endif

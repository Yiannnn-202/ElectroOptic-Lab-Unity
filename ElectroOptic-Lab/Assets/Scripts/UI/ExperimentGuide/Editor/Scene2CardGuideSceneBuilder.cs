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
    /// 创建或原位升级 Scene2 实时指引。工具可重复执行，不会保留重复的新旧卡片。
    /// </summary>
    public static class Scene2CardGuideSceneBuilder
    {
        private const string SceneName = "Scene2.The Lab";
        private const string ScenePath = "Assets/Scenes/Scene2.The Lab.unity";
        private const string CanvasName = "WindowsCanvas";
        private const string RootName = "Scene2CardGuideRoot";

        [MenuItem("ElectroOptics/Experiment Guide/Upgrade Realtime Guide UI In Scene2")]
        public static void UpgradeRealtimeGuideUi()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[Scene2RealtimeGuide] 请先停止 Play Mode。 ");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!string.Equals(scene.name, SceneName, System.StringComparison.OrdinalIgnoreCase))
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            UpgradeActiveScene(scene, true);
        }

        // 保留旧菜单入口，执行相同升级，避免团队成员仍使用旧工作流。
        [MenuItem("ElectroOptics/Experiment Guide/Build Editable Card UI In Scene2")]
        public static void BuildEditableCardUi()
        {
            UpgradeRealtimeGuideUi();
        }

        /// <summary>
        /// 供 Unity -batchmode -executeMethod 调用。
        /// </summary>
        public static void UpgradeScene2ForBatch()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            UpgradeActiveScene(scene, true);
        }

        /// <summary>
        /// 供 Unity -batchmode 验证保存后的 Scene2，避免在默认空场景中误报。
        /// </summary>
        public static void ValidateScene2ForBatch()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            ValidateRealtimeGuide();
        }

        [MenuItem("ElectroOptics/Experiment Guide/Validate Realtime Guide In Scene2")]
        public static void ValidateRealtimeGuide()
        {
            Scene2CardGuide[] controllers = Object.FindObjectsOfType<Scene2CardGuide>(true);
            Scene2CardGuideView[] views = Object.FindObjectsOfType<Scene2CardGuideView>(true);
            Scene2GuideStateProvider[] providers = Object.FindObjectsOfType<Scene2GuideStateProvider>(true);

            bool valid = controllers.Length == 1
                         && views.Length == 1
                         && views[0].IsValid
                         && providers.Length == 1
                         && providers[0].ResolveReferences(false)
                         && providers[0].LaserMover != null
                         && providers[0].LaserEmitter == providers[0].LaserMover.GetComponent<LaserEmitter>()
                         && providers[0].LaserStateController == providers[0].LaserMover.GetComponent<LaserStateController>()
                         && providers[0].PowerMeterSceneEntry != null
                         && providers[0].PowerMeterSceneEntry.transform.root.name == "功率计"
                         && providers[0].PowerMeterSceneEntry.targetSceneName == "Scene3_UIRebuild"
                         && providers[0].OscilloscopeSceneEntry != null
                         && providers[0].OscilloscopeSceneEntry.transform.root.name == "示波器"
                         && providers[0].OscilloscopeSceneEntry.targetSceneName == "Scene4_UIRebuild 1";
            if (valid)
                Debug.Log("[Scene2RealtimeGuide] Scene2 实时指引结构和关键引用验证通过。");
            else
                Debug.LogError(
                    $"[Scene2RealtimeGuide] 验证失败：controllers={controllers.Length}, views={views.Length}, providers={providers.Length}。");
        }

        private static void UpgradeActiveScene(Scene scene, bool saveScene)
        {
            Canvas canvas = GetOrCreateCanvas();
            RemoveOldAndDuplicateRoots(canvas.transform);

            TMP_FontAsset font = FindChineseFont();
            Scene2CardGuideView view = Scene2CardGuideView.Create(canvas.transform, font);
            view.name = RootName;
            view.transform.SetAsLastSibling();

            GameObject controllerObject = GameObject.Find("GameManager");
            Scene2CardGuide controller = Object.FindObjectOfType<Scene2CardGuide>(true);
            if (controller != null)
                controllerObject = controller.gameObject;
            if (controllerObject == null)
                controllerObject = new GameObject("GameManager");

            controller = GetOrAdd<Scene2CardGuide>(controllerObject);
            Scene2GuideStateProvider provider = GetOrAdd<Scene2GuideStateProvider>(controllerObject);
            Scene2GuideLaserCalibrationGate gate = GetOrAdd<Scene2GuideLaserCalibrationGate>(controllerObject);
            Scene2GuideCameraVisibility visibility = GetOrAdd<Scene2GuideCameraVisibility>(controllerObject);
            Scene2GuideInteractionLock interactionLock = GetOrAdd<Scene2GuideInteractionLock>(controllerObject);
            Scene2GuideGifPopup popup = GetOrAdd<Scene2GuideGifPopup>(controllerObject);

            SerializedObject serializedController = new SerializedObject(controller);
            serializedController.FindProperty("view").objectReferenceValue = view;
            serializedController.FindProperty("stateProvider").objectReferenceValue = provider;
            serializedController.FindProperty("calibrationGate").objectReferenceValue = gate;
            serializedController.FindProperty("cameraVisibility").objectReferenceValue = visibility;
            serializedController.FindProperty("interactionLock").objectReferenceValue = interactionLock;
            serializedController.FindProperty("gifPopup").objectReferenceValue = popup;
            serializedController.ApplyModifiedPropertiesWithoutUndo();

            provider.ResolveReferences(false);
            EditorUtility.SetDirty(controllerObject);
            EditorUtility.SetDirty(view);
            EditorUtility.SetDirty(provider);
            EditorSceneManager.MarkSceneDirty(scene);
            if (saveScene)
                EditorSceneManager.SaveScene(scene);

            Selection.activeGameObject = view.gameObject;
            Debug.Log("[Scene2RealtimeGuide] Scene2 实时指引已原位升级：六阶段卡片和运行时组件均已配置。 ");
        }

        private static void RemoveOldAndDuplicateRoots(Transform canvasTransform)
        {
            for (int i = canvasTransform.childCount - 1; i >= 0; i--)
            {
                Transform child = canvasTransform.GetChild(i);
                if (child.name == RootName
                    || child.GetComponent<Scene2CardGuideView>() != null
                    || child.name == "Scene2GuideGifModalRoot")
                {
                    Object.DestroyImmediate(child.gameObject);
                }
            }

            Scene2CardGuideView[] looseViews = Object.FindObjectsOfType<Scene2CardGuideView>(true);
            for (int i = 0; i < looseViews.Length; i++)
            {
                if (looseViews[i] != null)
                    Object.DestroyImmediate(looseViews[i].gameObject);
            }
        }

        private static T GetOrAdd<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            return component != null ? component : target.AddComponent<T>();
        }

        private static Canvas GetOrCreateCanvas()
        {
            GameObject canvasObject = GameObject.Find(CanvasName);
            if (canvasObject == null)
                canvasObject = new GameObject(CanvasName, typeof(RectTransform));

            Canvas canvas = GetOrAdd<Canvas>(canvasObject);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = Mathf.Max(canvas.sortingOrder, 100);

            CanvasScaler scaler = GetOrAdd<CanvasScaler>(canvasObject);
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            GetOrAdd<GraphicRaycaster>(canvasObject);
            return canvas;
        }

        private static TMP_FontAsset FindChineseFont()
        {
            string[] guids = AssetDatabase.FindAssets("SIMHEI SDF t:TMP_FontAsset");
            if (guids.Length > 0)
                return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guids[0]));

            TMP_FontAsset fallback = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                "Assets/Arts/Fonts/SourceHanSerifCN-Medium SDF 1.asset");
            return fallback != null ? fallback : TMP_Settings.defaultFontAsset;
        }
    }
}
#endif

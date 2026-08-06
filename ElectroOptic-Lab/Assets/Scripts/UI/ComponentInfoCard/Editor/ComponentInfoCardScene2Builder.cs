#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ElectroOptics.UI.ComponentInfoCard.Editor
{
    /// <summary>
    /// 将元件介绍卡悬停系统幂等安装到 Scene2，不修改既有交互脚本。
    /// </summary>
    public static class ComponentInfoCardScene2Builder
    {
        public const string ScenePath = "Assets/Scenes/Scene2.The Lab.unity";
        public const string ContentRoot = "Assets/Arts/UI/ComponentInfoCard/Content";

        private const string CanvasName = "WindowsCanvas";
        private const string SystemName = "ComponentInfoCardHoverSystem";
        private const string CardInstanceName = "ComponentInfoCardHoverCard";
        private const string ComponentLayerName = "Component";
        private const int ExpectedComponentLayer = 7;

        private const string LaserDescription =
            "激光器为实验系统提供稳定、准直的单色光源，是偏振调制与晶体光学测量的入射光源。" +
            "实验开始后，需要先将光束中心对准光屏十字标记，再进行后续元件搭建与测量。";

        private const string PolarizerDescription =
            "偏振片用于选择和分析光的偏振方向。起偏器建立入射线偏振态，检偏器分析出射偏振态；" +
            "旋转两者的透振轴可以观察光强变化，当透振轴互相正交时可验证消光现象。";

        private const string CrystalBoxDescription =
            "晶体盒用于装载所选电光晶体，并提供晶体姿态调节。晶体的双折射和电光效应会改变光的相位差与偏振态，" +
            "是锥光干涉观察和半波电压测量的核心元件。";

        private const string BeamExpanderDescription =
            "扩束镜安装在晶体盒前方，用于将入射光转换为锥形光束，使不同传播方向的光同时通过晶体，" +
            "从而在光屏上形成锥光干涉图样。";

        private const string ScreenDescription =
            "光屏用于接收并显示实验光束。搭建初期可通过十字标记观察光斑位置并完成激光对准；" +
            "放置扩束镜和晶体盒后，可用于观察锥光干涉图样。";

        private const string ReceiverDescription =
            "光电接收器将入射光信号转换为可测量的电信号，用于记录不同外加电压下的光功率。" +
            "完成光路对准后，可据此测量半波电压并分析电光调制规律。";

        private static readonly ContentDefinition[] ContentDefinitions =
        {
            new ContentDefinition("Laser", "激光器", LaserDescription),
            new ContentDefinition("Polarizer", "偏振片", PolarizerDescription),
            new ContentDefinition("CrystalBox", "晶体盒", CrystalBoxDescription),
            new ContentDefinition("BeamExpander", "扩束镜", BeamExpanderDescription),
            new ContentDefinition("Screen", "光屏", ScreenDescription),
            new ContentDefinition("PhotoReceiver", "光电接收器", ReceiverDescription)
        };

        private static readonly TargetDefinition[] TargetDefinitions =
        {
            new TargetDefinition("新发射器", "Laser"),
            new TargetDefinition("起偏器", "Polarizer"),
            new TargetDefinition("检偏器", "Polarizer"),
            new TargetDefinition("晶体盒", "CrystalBox"),
            new TargetDefinition("扩束镜", "BeamExpander"),
            new TargetDefinition("光屏", "Screen"),
            new TargetDefinition("接收器", "PhotoReceiver")
        };

        [MenuItem("ElectroOptics/UI/Component Info Card/Install or Repair Hover Interaction In Scene2")]
        public static void InstallOrRepairFromMenu()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Component Info Card", "请先停止 Play Mode。", "确定");
                return;
            }

            bool confirmed = EditorUtility.DisplayDialog(
                "Install Component Info Card Hover Interaction",
                "将原位更新 Scene2.The Lab：创建悬停控制器、一个卡片实例、六份内容资产，并为七个元件添加 Target。\n\n" +
                "已有内容资产会保留，不会修改原交互脚本。是否继续？",
                "安装 / 修复",
                "取消");
            if (!confirmed || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            InstallInternal(true);
        }

        [MenuItem("ElectroOptics/UI/Component Info Card/Validate Hover Interaction In Scene2")]
        public static void ValidateFromMenu()
        {
            ValidateScene2(true);
        }

        public static void InstallForBatch()
        {
            InstallInternal(true);
        }

        public static void ValidateForBatch()
        {
            if (!ValidateScene2(true))
                throw new BuildFailedException("Component Info Card Scene2 hover validation failed.");
        }

        public static bool ValidateScene2(bool logSuccess)
        {
            bool valid = true;
            Dictionary<string, ComponentInfoCardContent> contents = LoadContentAssets(ref valid);

            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool openedForValidation = !scene.IsValid() || !scene.isLoaded;
            if (openedForValidation)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

            try
            {
                ValidateLoadedScene(scene, contents, ref valid);
            }
            finally
            {
                if (openedForValidation && scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }

            if (valid && logSuccess)
                Debug.Log("[ComponentInfoCard] Scene2 悬停交互、七个目标和六份内容资产验证通过。");

            return valid;
        }

        private static void InstallInternal(bool saveScene)
        {
            if (Application.isPlaying)
                throw new InvalidOperationException("请先停止 Play Mode。 ");

            int componentLayer = LayerMask.NameToLayer(ComponentLayerName);
            if (componentLayer != ExpectedComponentLayer)
            {
                throw new BuildFailedException(
                    $"项目的 {ComponentLayerName} Layer 应为 {ExpectedComponentLayer}，当前为 {componentLayer}。");
            }

            EnsureFolder(ContentRoot);
            Dictionary<string, ComponentInfoCardContent> contents = EnsureContentAssets();

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Canvas canvas = EnsureWindowsCanvas(scene);
            ComponentInfoCardView cardView = EnsureSingleCardInstance(scene, canvas.transform);
            ComponentInfoCardHoverController controller = EnsureSingleController(scene);

            ConfigureController(controller, canvas, cardView, componentLayer);
            ConfigureTargets(scene, contents, componentLayer);

            EditorSceneManager.MarkSceneDirty(scene);
            if (saveScene)
                EditorSceneManager.SaveScene(scene);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (!ValidateScene2(false))
                throw new BuildFailedException("Component Info Card Scene2 悬停交互安装后验证失败。 ");

            Selection.activeGameObject = controller.gameObject;
            Debug.Log("[ComponentInfoCard] 已在 Scene2 安装/修复七类元件的悬停介绍卡交互。");
        }

        private static Dictionary<string, ComponentInfoCardContent> EnsureContentAssets()
        {
            Dictionary<string, ComponentInfoCardContent> result =
                new Dictionary<string, ComponentInfoCardContent>(StringComparer.Ordinal);

            for (int index = 0; index < ContentDefinitions.Length; index++)
            {
                ContentDefinition definition = ContentDefinitions[index];
                string path = GetContentPath(definition.Key);
                ComponentInfoCardContent content = AssetDatabase.LoadAssetAtPath<ComponentInfoCardContent>(path);
                if (content == null && AssetDatabase.LoadMainAssetAtPath(path) != null)
                    throw new InvalidDataException("内容资产类型不正确：" + path);

                if (content == null)
                {
                    content = ScriptableObject.CreateInstance<ComponentInfoCardContent>();
                    SerializedObject serialized = new SerializedObject(content);
                    serialized.FindProperty("componentName").stringValue = definition.Title;
                    serialized.FindProperty("description").stringValue = definition.Description;
                    serialized.FindProperty("previewSprite").objectReferenceValue = null;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    AssetDatabase.CreateAsset(content, path);
                }

                result.Add(definition.Key, content);
            }

            return result;
        }

        private static Dictionary<string, ComponentInfoCardContent> LoadContentAssets(ref bool valid)
        {
            Dictionary<string, ComponentInfoCardContent> result =
                new Dictionary<string, ComponentInfoCardContent>(StringComparer.Ordinal);

            for (int index = 0; index < ContentDefinitions.Length; index++)
            {
                ContentDefinition definition = ContentDefinitions[index];
                string path = GetContentPath(definition.Key);
                ComponentInfoCardContent content = AssetDatabase.LoadAssetAtPath<ComponentInfoCardContent>(path);
                Check(ref valid, content != null, "缺少内容资产：" + path);
                if (content != null)
                {
                    Check(ref valid, content.IsValid, "内容资产无效：" + path);
                    result[definition.Key] = content;
                }
            }

            return result;
        }

        private static Canvas EnsureWindowsCanvas(Scene scene)
        {
            GameObject canvasObject = FindSingleSceneObject(scene, CanvasName);
            if (canvasObject == null)
            {
                canvasObject = new GameObject(CanvasName, typeof(RectTransform));
                SceneManager.MoveGameObjectToScene(canvasObject, scene);
            }

            Canvas canvas = GetOrAdd<Canvas>(canvasObject);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = Mathf.Max(100, canvas.sortingOrder);

            CanvasScaler scaler = GetOrAdd<CanvasScaler>(canvasObject);
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            GetOrAdd<GraphicRaycaster>(canvasObject);
            return canvas;
        }

        private static ComponentInfoCardView EnsureSingleCardInstance(Scene scene, Transform canvasTransform)
        {
            List<ComponentInfoCardView> knownCards = FindComponentsInScene<ComponentInfoCardView>(scene)
                .Where(view => view.gameObject.name == CardInstanceName)
                .ToList();

            ComponentInfoCardView cardView = knownCards.Count > 0 ? knownCards[0] : null;
            for (int index = 1; index < knownCards.Count; index++)
                UnityEngine.Object.DestroyImmediate(knownCards[index].gameObject);

            if (cardView == null)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ComponentInfoCardPrefabBuilder.PrefabPath);
                if (prefab == null)
                    throw new FileNotFoundException("找不到元件介绍卡 Prefab", ComponentInfoCardPrefabBuilder.PrefabPath);

                GameObject instance = PrefabUtility.InstantiatePrefab(prefab, canvasTransform) as GameObject;
                if (instance == null)
                    throw new InvalidOperationException("无法实例化 ComponentInfoCard.prefab。 ");

                instance.name = CardInstanceName;
                cardView = instance.GetComponent<ComponentInfoCardView>();
            }

            cardView.transform.SetParent(canvasTransform, false);
            RectTransform rect = cardView.CardRect;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.localScale = Vector3.one;
            cardView.RootGroup.alpha = 0f;
            cardView.RootGroup.interactable = false;
            cardView.RootGroup.blocksRaycasts = false;
            cardView.gameObject.SetActive(false);
            EditorUtility.SetDirty(cardView.gameObject);
            return cardView;
        }

        private static ComponentInfoCardHoverController EnsureSingleController(Scene scene)
        {
            List<ComponentInfoCardHoverController> controllers =
                FindComponentsInScene<ComponentInfoCardHoverController>(scene);
            ComponentInfoCardHoverController controller = controllers.Count > 0 ? controllers[0] : null;

            for (int index = 1; index < controllers.Count; index++)
                UnityEngine.Object.DestroyImmediate(controllers[index]);

            GameObject systemObject = FindSingleSceneObject(scene, SystemName);
            if (systemObject == null)
            {
                systemObject = new GameObject(SystemName);
                SceneManager.MoveGameObjectToScene(systemObject, scene);
            }

            if (controller == null)
                controller = systemObject.AddComponent<ComponentInfoCardHoverController>();
            else if (controller.gameObject != systemObject)
            {
                UnityEngine.Object.DestroyImmediate(controller);
                controller = GetOrAdd<ComponentInfoCardHoverController>(systemObject);
            }

            return controller;
        }

        private static void ConfigureController(
            ComponentInfoCardHoverController controller,
            Canvas canvas,
            ComponentInfoCardView cardView,
            int componentLayer)
        {
            Camera mainCamera = FindComponentsInScene<Camera>(controller.gameObject.scene)
                .FirstOrDefault(camera => camera.CompareTag("MainCamera"));

            SerializedObject serialized = new SerializedObject(controller);
            serialized.FindProperty("hoverCamera").objectReferenceValue = mainCamera;
            serialized.FindProperty("targetCanvas").objectReferenceValue = canvas;
            serialized.FindProperty("cardView").objectReferenceValue = cardView;
            serialized.FindProperty("hoverLayerMask").intValue = 1 << componentLayer;
            serialized.FindProperty("hoverDelay").floatValue = 0.6f;
            serialized.FindProperty("disableInCloseUp").boolValue = true;
            serialized.FindProperty("fadeDuration").floatValue = 0.18f;
            serialized.FindProperty("screenOffset").vector2Value = new Vector2(24f, 18f);
            serialized.FindProperty("edgePadding").floatValue = 20f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(controller);
        }

        private static void ConfigureTargets(
            Scene scene,
            IReadOnlyDictionary<string, ComponentInfoCardContent> contents,
            int componentLayer)
        {
            for (int index = 0; index < TargetDefinitions.Length; index++)
            {
                TargetDefinition definition = TargetDefinitions[index];
                GameObject targetObject = FindSingleSceneObject(scene, definition.SceneObjectName);
                if (targetObject == null)
                    throw new MissingReferenceException("Scene2 缺少目标对象：" + definition.SceneObjectName);

                ComponentInfoCardTarget target = GetOrAdd<ComponentInfoCardTarget>(targetObject);
                SerializedObject serialized = new SerializedObject(target);
                serialized.FindProperty("content").objectReferenceValue = contents[definition.ContentKey];
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EnsureColliderLayers(targetObject, componentLayer);
                EditorUtility.SetDirty(targetObject);
            }
        }

        private static void EnsureColliderLayers(GameObject targetObject, int componentLayer)
        {
            int ignoreRaycastLayer = LayerMask.NameToLayer("Ignore Raycast");
            Collider[] colliders = targetObject.GetComponentsInChildren<Collider>(true);
            for (int index = 0; index < colliders.Length; index++)
            {
                Collider collider = colliders[index];
                if (collider == null || !collider.enabled || collider.isTrigger)
                    continue;

                if (collider.gameObject.layer == ignoreRaycastLayer)
                    continue;

                collider.gameObject.layer = componentLayer;
                EditorUtility.SetDirty(collider.gameObject);
            }
        }

        private static void ValidateLoadedScene(
            Scene scene,
            IReadOnlyDictionary<string, ComponentInfoCardContent> contents,
            ref bool valid)
        {
            List<ComponentInfoCardHoverController> controllers =
                FindComponentsInScene<ComponentInfoCardHoverController>(scene);
            Check(ref valid, controllers.Count == 1, "Scene2 应且仅应有一个 ComponentInfoCardHoverController");

            List<ComponentInfoCardView> cards = FindComponentsInScene<ComponentInfoCardView>(scene)
                .Where(view => view.gameObject.name == CardInstanceName)
                .ToList();
            Check(ref valid, cards.Count == 1, "Scene2 应且仅应有一个悬停卡片实例");
            if (cards.Count == 1)
            {
                ComponentInfoCardView view = cards[0];
                Check(ref valid, view.IsValid, "Scene2 悬停卡片 View 引用不完整");
                Check(ref valid, view.CardRect.rect.size == new Vector2(650f, 340f), "悬停卡片尺寸不是 650×340");
                Check(ref valid, !view.gameObject.activeSelf, "悬停卡片在场景中应默认隐藏");

                GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(view.gameObject);
                Check(
                    ref valid,
                    source != null && AssetDatabase.GetAssetPath(source) == ComponentInfoCardPrefabBuilder.PrefabPath,
                    "悬停卡片不是 ComponentInfoCard.prefab 的实例");
            }

            GameObject canvasObject = FindSingleSceneObject(scene, CanvasName);
            Check(ref valid, canvasObject != null, "Scene2 缺少 WindowsCanvas");
            if (canvasObject != null)
            {
                Canvas canvas = canvasObject.GetComponent<Canvas>();
                CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
                Check(ref valid, canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay, "WindowsCanvas 不是 Overlay");
                Check(ref valid, scaler != null && scaler.referenceResolution == new Vector2(1920f, 1080f), "Canvas 参考分辨率错误");
                Check(ref valid, scaler != null && Mathf.Approximately(scaler.matchWidthOrHeight, 0.5f), "Canvas Match 不是 0.5");
            }

            int componentLayer = LayerMask.NameToLayer(ComponentLayerName);
            Dictionary<string, ComponentInfoCardTarget> targets =
                new Dictionary<string, ComponentInfoCardTarget>(StringComparer.Ordinal);
            for (int index = 0; index < TargetDefinitions.Length; index++)
            {
                TargetDefinition definition = TargetDefinitions[index];
                GameObject targetObject = FindSingleSceneObject(scene, definition.SceneObjectName);
                Check(ref valid, targetObject != null, "Scene2 缺少目标对象：" + definition.SceneObjectName);
                if (targetObject == null)
                    continue;

                ComponentInfoCardTarget[] targetComponents = targetObject.GetComponents<ComponentInfoCardTarget>();
                Check(ref valid, targetComponents.Length == 1, definition.SceneObjectName + " 应且仅应有一个 Target");
                if (targetComponents.Length != 1)
                    continue;

                ComponentInfoCardTarget target = targetComponents[0];
                targets[definition.SceneObjectName] = target;
                Check(ref valid, target.IsValid, definition.SceneObjectName + " 的内容无效");
                if (contents.TryGetValue(definition.ContentKey, out ComponentInfoCardContent expected))
                    Check(ref valid, target.Content == expected, definition.SceneObjectName + " 的内容资产引用错误");

                bool hasHoverCollider = targetObject.GetComponentsInChildren<Collider>(true)
                    .Any(collider => collider.enabled
                                     && !collider.isTrigger
                                     && collider.gameObject.layer == componentLayer);
                Check(ref valid, hasHoverCollider, definition.SceneObjectName + " 没有 Layer 7 的有效 Collider");
                Check(ref valid, target.TryGetWorldBounds(out _), definition.SceneObjectName + " 无法计算包围盒");
            }

            if (targets.TryGetValue("起偏器", out ComponentInfoCardTarget polarizer)
                && targets.TryGetValue("检偏器", out ComponentInfoCardTarget analyzer))
            {
                Check(ref valid, polarizer.Content == analyzer.Content, "起偏器和检偏器必须共享同一内容资产");
            }

            GameObject[] roots = scene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
            {
                Check(
                    ref valid,
                    MissingScriptCount(roots[index]) == 0,
                    roots[index].name + " 层级存在 Missing Script");
            }
        }

        private static GameObject FindSingleSceneObject(Scene scene, string objectName)
        {
            List<GameObject> matches = new List<GameObject>();
            GameObject[] roots = scene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
            {
                Transform[] transforms = roots[index].GetComponentsInChildren<Transform>(true);
                for (int childIndex = 0; childIndex < transforms.Length; childIndex++)
                {
                    if (string.Equals(transforms[childIndex].name, objectName, StringComparison.Ordinal))
                        matches.Add(transforms[childIndex].gameObject);
                }
            }

            if (matches.Count > 1)
                throw new InvalidOperationException($"Scene2 中存在多个名为 {objectName} 的对象，无法安全接入。 ");

            return matches.Count == 1 ? matches[0] : null;
        }

        private static List<T> FindComponentsInScene<T>(Scene scene) where T : Component
        {
            List<T> result = new List<T>();
            GameObject[] roots = scene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
                result.AddRange(roots[index].GetComponentsInChildren<T>(true));
            return result;
        }

        private static T GetOrAdd<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            return component != null ? component : target.AddComponent<T>();
        }

        private static string GetContentPath(string key)
        {
            return ContentRoot + "/ComponentInfoCard_" + key + ".asset";
        }

        private static void EnsureFolder(string folderPath)
        {
            string normalized = folderPath.Replace('\\', '/');
            string[] parts = normalized.Split('/');
            string current = parts[0];
            for (int index = 1; index < parts.Length; index++)
            {
                string next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[index]);
                current = next;
            }
        }

        private static int MissingScriptCount(GameObject root)
        {
            int count = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(root);
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < transforms.Length; index++)
            {
                if (transforms[index].gameObject != root)
                    count += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transforms[index].gameObject);
            }
            return count;
        }

        private static void Check(ref bool valid, bool condition, string message)
        {
            if (condition)
                return;

            valid = false;
            Debug.LogError("[ComponentInfoCard] " + message);
        }

        private readonly struct ContentDefinition
        {
            public ContentDefinition(string key, string title, string description)
            {
                Key = key;
                Title = title;
                Description = description;
            }

            public string Key { get; }
            public string Title { get; }
            public string Description { get; }
        }

        private readonly struct TargetDefinition
        {
            public TargetDefinition(string sceneObjectName, string contentKey)
            {
                SceneObjectName = sceneObjectName;
                ContentKey = contentKey;
            }

            public string SceneObjectName { get; }
            public string ContentKey { get; }
        }
    }
}
#endif

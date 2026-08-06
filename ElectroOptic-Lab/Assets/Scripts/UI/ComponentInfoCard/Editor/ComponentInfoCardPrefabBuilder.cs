#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using ThreeDISevenZeroR.UnityGifDecoder;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ElectroOptics.UI.ComponentInfoCard.Editor
{
    /// <summary>
    /// 创建、显式重建和验证元件介绍卡 Prefab Lab。
    /// Create 不覆盖已有资产；Rebuild 必须由用户确认。
    /// </summary>
    public static class ComponentInfoCardPrefabBuilder
    {
        public const string PrefabPath = "Assets/Prefabs/UI/ComponentInfoCard.prefab";
        public const string ScenePath = "Assets/Scenes/Scene2_ComponentInfoCard_PrefabLab.unity";
        public const float CardScale = 0.8f;
        public const float CardWidth = 520f;
        public const float CardHeight = 272f;

        private const string AssetRoot = "Assets/Arts/UI/ComponentInfoCard";
        private const string GifRoot = "Assets/StreamingAssets/ComponentInfoCard";
        private const string SampleGifFileName = "Laser.gif";
        private const string FontPath = "Assets/Resources/Fonts/SIMHEI SDF.asset";
        private const string DefaultTitle = "元件名称";
        private const string DefaultDescription = "在此填写元件的功能、工作原理及其在实验光路中的作用。";
        private const string SampleTitle = "激光器";
        private const string SampleDescription =
            "激光器为实验系统提供稳定、准直的单色光源，是偏振调制与晶体光学测量的入射光源。" +
            "实验开始后，需要先将光束中心对准光屏十字标记，再进行后续元件搭建与测量。";

        private const string BasePath = AssetRoot + "/ComponentInfoCard_Base.png";
        private const string CornerBracketPath = AssetRoot + "/ComponentInfoCard_CornerBracket.png";
        private const string CrosshairPath = AssetRoot + "/ComponentInfoCard_Crosshair.png";
        private const string HeaderPath = AssetRoot + "/ComponentInfoCard_Header.png";
        private const string LocatorPath = AssetRoot + "/ComponentInfoCard_Locator.png";
        private const string ScanRingPath = AssetRoot + "/ComponentInfoCard_ScanRing.png";

        private static readonly string[] SpritePaths =
        {
            BasePath,
            CornerBracketPath,
            CrosshairPath,
            HeaderPath,
            LocatorPath,
            ScanRingPath
        };

        private static readonly string[] GifFileNames =
        {
            "Laser.gif",
            "Polarizer.gif",
            "CrystalBox.gif",
            "BeamExpander.gif",
            "Screen.gif",
            "PhotoReceiver.gif",
            "Photodiode.gif"
        };

        [MenuItem("ElectroOptics/UI/Component Info Card/Create Prefab Lab")]
        public static void CreatePrefabLab()
        {
            if (!CanChangeScenes())
                return;

            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null
                || AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            {
                EditorUtility.DisplayDialog(
                    "Component Info Card",
                    "Prefab 或 Prefab Lab 场景已经存在。Create 不会覆盖已有资产；如需重新生成，请使用 Rebuild Prefab Lab。",
                    "确定");
                return;
            }

            BuildInternal(false);
        }

        [MenuItem("ElectroOptics/UI/Component Info Card/Rebuild Prefab Lab")]
        public static void RebuildPrefabLab()
        {
            if (!CanChangeScenes())
                return;

            bool confirmed = EditorUtility.DisplayDialog(
                "Rebuild Component Info Card Prefab Lab",
                "该操作会覆盖 ComponentInfoCard.prefab 和专用 Prefab Lab 场景。源切图不会被删除或覆盖。是否继续？",
                "重建",
                "取消");
            if (!confirmed)
                return;

            BuildInternal(true);
        }

        [MenuItem("ElectroOptics/UI/Component Info Card/Validate Prefab Lab")]
        public static void ValidatePrefabLab()
        {
            ValidateAssetsAndScene(true);
        }

        /// <summary>供 Unity -batchmode -executeMethod 调用。</summary>
        public static void RebuildForBatch()
        {
            BuildInternal(true);
        }

        /// <summary>供 Unity -batchmode -executeMethod 调用。</summary>
        public static void ValidateForBatch()
        {
            if (!ValidateAssetsAndScene(true))
                throw new BuildFailedException("Component Info Card Prefab Lab validation failed.");
        }

        public static bool ValidateAssetsAndScene(bool logSuccess)
        {
            bool valid = true;

            for (int index = 0; index < SpritePaths.Length; index++)
            {
                string path = SpritePaths[index];
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                Check(ref valid, sprite != null, "缺少 Sprite：" + path);

                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                Check(ref valid, importer != null, "缺少 TextureImporter：" + path);
                if (importer == null)
                    continue;

                Check(ref valid, importer.textureType == TextureImporterType.Sprite, path + " 不是 Sprite 类型");
                Check(ref valid, importer.spriteImportMode == SpriteImportMode.Single, path + " 不是 Single Sprite");
                Check(ref valid, !importer.mipmapEnabled, path + " 不应开启 Mipmap");
                Check(ref valid, importer.alphaIsTransparency, path + " 未开启 Alpha Is Transparency");
                Check(ref valid, importer.filterMode == FilterMode.Bilinear, path + " Filter Mode 应为 Bilinear");
                Check(
                    ref valid,
                    importer.textureCompression == TextureImporterCompression.Uncompressed,
                    path + " 应关闭压缩");
                TextureImporterSettings importerSettings = new TextureImporterSettings();
                importer.ReadTextureSettings(importerSettings);
                Check(ref valid, importerSettings.spriteMeshType == SpriteMeshType.FullRect, path + " Mesh Type 应为 Full Rect");
            }

            ValidateGifFiles(ref valid);

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Check(ref valid, prefab != null, "缺少 Prefab：" + PrefabPath);
            if (prefab != null)
            {
                ComponentInfoCardView view = prefab.GetComponent<ComponentInfoCardView>();
                ComponentInfoCardGifPlayer[] players = prefab.GetComponentsInChildren<ComponentInfoCardGifPlayer>(true);
                Check(ref valid, view != null && view.IsValid, "Prefab View 引用不完整");
                Check(ref valid, players.Length == 1, "Prefab 必须只有一个 GIF 播放器");
                if (players.Length == 1)
                    Check(ref valid, players[0].IsValid, "Prefab GIF 播放器引用或缓冲配置无效");
                Check(ref valid, MissingScriptCount(prefab) == 0, "Prefab 存在 Missing Script");

                if (view != null)
                {
                    Check(ref valid, Approximately(view.CardRect.sizeDelta, new Vector2(CardWidth, CardHeight)), "Prefab 尺寸不是 520×272");
                    Check(ref valid, Mathf.Approximately(view.BaseImage.color.a, 0.85f), "底板 Alpha 不是 0.85");
                    Check(ref valid, Mathf.Approximately(view.HeaderImage.rectTransform.sizeDelta.y, ScaleDesignValue(46f)), "标题栏高度未按 80% 缩放");
                    Check(ref valid, Mathf.Approximately(view.GridGraphic.Spacing, ScaleDesignValue(13f)), "程序网格间距未按 80% 缩放");
                    Check(ref valid, Mathf.Approximately(view.GridGraphic.LineThickness, ScaleDesignValue(1f)), "程序网格线宽未按 80% 缩放");
                    Check(ref valid, Mathf.Approximately(view.TitleText.fontSize, ScaleDesignValue(24f)), "标题字号未按 80% 缩放");
                    Check(ref valid, Mathf.Approximately(view.DescriptionText.fontSize, ScaleDesignValue(17f)), "正文字号未按 80% 缩放");
                    Check(ref valid, AssetDatabase.GetAssetPath(view.TitleText.font) == FontPath, "标题未使用 SIMHEI SDF");
                    Check(ref valid, AssetDatabase.GetAssetPath(view.DescriptionText.font) == FontPath, "正文未使用 SIMHEI SDF");
                    Check(ref valid, view.AnimatedPreviewImage != null, "Prefab 缺少 GIF RawImage");
                    Check(ref valid, view.AnimatedPreviewFitter != null, "Prefab 缺少 GIF AspectRatioFitter");
                    Check(
                        ref valid,
                        view.AnimatedPreviewFitter != null
                        && view.AnimatedPreviewFitter.aspectMode == AspectRatioFitter.AspectMode.FitInParent,
                        "GIF 预览未使用 FitInParent");
                    Check(ref valid, !view.AnimatedPreviewImage.raycastTarget, "GIF RawImage 不应拦截 UI 射线");
                }

                Graphic[] graphics = prefab.GetComponentsInChildren<Graphic>(true);
                for (int index = 0; index < graphics.Length; index++)
                    Check(ref valid, !graphics[index].raycastTarget, graphics[index].name + " 不应拦截 UI 射线");
            }

            SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            Check(ref valid, sceneAsset != null, "缺少 Prefab Lab 场景：" + ScenePath);
            Check(
                ref valid,
                EditorBuildSettings.scenes.All(scene => !string.Equals(scene.path, ScenePath, StringComparison.OrdinalIgnoreCase)),
                "Prefab Lab 场景不应加入 Build Settings");

            if (sceneAsset != null)
                ValidateScene(ref valid);

            if (valid && logSuccess)
                Debug.Log("[ComponentInfoCard] Prefab、切图导入设置和 Prefab Lab 场景验证通过。");

            return valid;
        }

        private static void BuildInternal(bool overwrite)
        {
            if (Application.isPlaying)
                throw new InvalidOperationException("请先停止 Play Mode 再创建 Component Info Card Prefab Lab。");

            EnsureFolder("Assets/Prefabs/UI");
            ConfigureTextureImporters();

            bool prefabExists = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null;
            bool sceneExists = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null;
            if (!overwrite && (prefabExists || sceneExists))
                throw new InvalidOperationException("Create 模式不会覆盖已有 Prefab 或场景。");

            GameObject prefab = BuildPrefab();
            BuildScene(prefab);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (!ValidateAssetsAndScene(false))
                throw new BuildFailedException("Component Info Card Prefab Lab 创建后验证失败。");

            Debug.Log("[ComponentInfoCard] 已生成 ComponentInfoCard.prefab 和 Scene2_ComponentInfoCard_PrefabLab 场景。");
        }

        private static GameObject BuildPrefab()
        {
            Sprite baseSprite = LoadSprite(BasePath);
            Sprite bracketSprite = LoadSprite(CornerBracketPath);
            Sprite crosshairSprite = LoadSprite(CrosshairPath);
            Sprite headerSprite = LoadSprite(HeaderPath);
            Sprite locatorSprite = LoadSprite(LocatorPath);
            Sprite scanRingSprite = LoadSprite(ScanRingPath);
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font == null)
                throw new FileNotFoundException("找不到中文字体", FontPath);

            GameObject root = CreateRectObject("ComponentInfoCard", null);
            RectTransform cardRect = root.GetComponent<RectTransform>();
            cardRect.anchorMin = cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.sizeDelta = new Vector2(CardWidth, CardHeight);
            CanvasGroup canvasGroup = root.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            ComponentInfoCardView view = root.AddComponent<ComponentInfoCardView>();
            ComponentInfoCardGifPlayer gifPlayer = root.AddComponent<ComponentInfoCardGifPlayer>();

            Image shadow = CreateImage("Shadow", root.transform, baseSprite, new Color(0f, 0f, 0f, 0.25f));
            SetCenteredRect(shadow.rectTransform, new Vector2(CardWidth, CardHeight), new Vector2(ScaleDesignValue(4f), ScaleDesignValue(-4f)));

            Image baseImage = CreateImage("Base", root.transform, baseSprite, new Color(1f, 1f, 1f, 0.85f));
            SetCenteredRect(baseImage.rectTransform, new Vector2(CardWidth, CardHeight), Vector2.zero);

            Image headerImage = CreateImage("Header", root.transform, headerSprite, Color.white);
            SetTopLeftRect(headerImage.rectTransform, 0f, 0f, CardWidth, ScaleDesignValue(46f));

            Image accentLine = CreateSolidImage("AccentLine", headerImage.transform, HexColor("40DBFFF2"));
            SetTopLeftRect(accentLine.rectTransform, ScaleDesignValue(20f), ScaleDesignValue(22f), ScaleDesignValue(43f), ScaleDesignValue(3f));

            Image locator = CreateImage("Locator", headerImage.transform, locatorSprite, Color.white);
            locator.preserveAspect = true;
            SetTopLeftRect(locator.rectTransform, ScaleDesignValue(77f), ScaleDesignValue(9f), ScaleDesignValue(28f), ScaleDesignValue(28f));

            TextMeshProUGUI title = CreateText(
                "TitleText",
                headerImage.transform,
                DefaultTitle,
                font,
                ScaleDesignValue(24f),
                HexColor("EAF7FFFF"),
                TextAlignmentOptions.MidlineLeft);
            SetStretchRect(title.rectTransform, new Vector2(ScaleDesignValue(120f), 0f), new Vector2(ScaleDesignValue(-20f), 0f));
            title.enableWordWrapping = false;
            title.overflowMode = TextOverflowModes.Ellipsis;

            GameObject showcase = CreateRectObject("ShowcaseArea", root.transform);
            SetTopLeftRect(showcase.GetComponent<RectTransform>(), 0f, ScaleDesignValue(46f), ScaleDesignValue(317f), ScaleDesignValue(294f));

            GameObject gridObject = CreateRectObject("Grid", showcase.transform);
            SetTopLeftRect(gridObject.GetComponent<RectTransform>(), ScaleDesignValue(20f), ScaleDesignValue(12f), ScaleDesignValue(278f), ScaleDesignValue(268f));
            ComponentInfoCardGridGraphic grid = gridObject.AddComponent<ComponentInfoCardGridGraphic>();
            grid.Configure(ScaleDesignValue(13f), ScaleDesignValue(1f), HexColor("40DBFF14"));

            Image preview = CreateImage("ComponentPreview", showcase.transform, null, Color.white);
            preview.preserveAspect = true;
            preview.enabled = false;
            SetTopLeftRect(preview.rectTransform, ScaleDesignValue(43f), ScaleDesignValue(37f), ScaleDesignValue(231f), ScaleDesignValue(220f));

            GameObject animatedSlot = CreateRectObject("AnimatedPreviewSlot", showcase.transform);
            SetTopLeftRect(animatedSlot.GetComponent<RectTransform>(), ScaleDesignValue(43f), ScaleDesignValue(37f), ScaleDesignValue(231f), ScaleDesignValue(220f));
            GameObject animatedObject = CreateRectObject("AnimatedPreview", animatedSlot.transform);
            SetCenteredRect(animatedObject.GetComponent<RectTransform>(), new Vector2(ScaleDesignValue(220f), ScaleDesignValue(220f)), Vector2.zero);
            RawImage animatedPreview = animatedObject.AddComponent<RawImage>();
            animatedPreview.color = Color.white;
            animatedPreview.raycastTarget = false;
            animatedPreview.enabled = false;
            AspectRatioFitter animatedFitter = animatedObject.AddComponent<AspectRatioFitter>();
            animatedFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            animatedFitter.aspectRatio = 1f;

            Image scanRing = CreateImage("ScanRing", showcase.transform, scanRingSprite, Color.white);
            scanRing.preserveAspect = true;
            SetTopLeftRect(scanRing.rectTransform, ScaleDesignValue(55f), ScaleDesignValue(45f), ScaleDesignValue(208f), ScaleDesignValue(204f));

            Image crosshair = CreateImage("Crosshair", showcase.transform, crosshairSprite, Color.white);
            crosshair.preserveAspect = true;
            SetTopLeftRect(crosshair.rectTransform, ScaleDesignValue(109f), ScaleDesignValue(101f), ScaleDesignValue(99f), ScaleDesignValue(91f));

            CreateBracket(showcase.transform, bracketSprite, "CornerBracket_LT", ScaleDesignValue(20f), ScaleDesignValue(24f), 0f);
            CreateBracket(showcase.transform, bracketSprite, "CornerBracket_RT", ScaleDesignValue(279f), ScaleDesignValue(24f), -90f);
            CreateBracket(showcase.transform, bracketSprite, "CornerBracket_RB", ScaleDesignValue(279f), ScaleDesignValue(248f), 180f);
            CreateBracket(showcase.transform, bracketSprite, "CornerBracket_LB", ScaleDesignValue(20f), ScaleDesignValue(248f), 90f);

            TextMeshProUGUI description = CreateText(
                "DescriptionText",
                root.transform,
                DefaultDescription,
                font,
                ScaleDesignValue(17f),
                HexColor("EAF7FFDC"),
                TextAlignmentOptions.TopLeft);
            SetTopLeftRect(description.rectTransform, ScaleDesignValue(344f), ScaleDesignValue(82f), ScaleDesignValue(282f), ScaleDesignValue(234f));
            description.enableWordWrapping = true;
            description.lineSpacing = ScaleDesignValue(1.5f);
            description.overflowMode = TextOverflowModes.Ellipsis;

            SerializedObject serializedView = new SerializedObject(view);
            serializedView.FindProperty("rootGroup").objectReferenceValue = canvasGroup;
            serializedView.FindProperty("cardRect").objectReferenceValue = cardRect;
            serializedView.FindProperty("baseImage").objectReferenceValue = baseImage;
            serializedView.FindProperty("headerImage").objectReferenceValue = headerImage;
            serializedView.FindProperty("titleText").objectReferenceValue = title;
            serializedView.FindProperty("descriptionText").objectReferenceValue = description;
            serializedView.FindProperty("previewImage").objectReferenceValue = preview;
            serializedView.FindProperty("animatedPreviewImage").objectReferenceValue = animatedPreview;
            serializedView.FindProperty("animatedPreviewFitter").objectReferenceValue = animatedFitter;
            serializedView.FindProperty("gridGraphic").objectReferenceValue = grid;
            serializedView.ApplyModifiedPropertiesWithoutUndo();
            gifPlayer.Configure(view, 3);
            view.SetContent(DefaultTitle, DefaultDescription, null);

            SetLayerRecursively(root, LayerMask.NameToLayer("UI"));
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            if (prefab == null)
                throw new InvalidOperationException("保存 ComponentInfoCard Prefab 失败。");
            return prefab;
        }

        private static void BuildScene(GameObject prefab)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = HexColor("06111EFF");
            camera.orthographic = true;

            GameObject canvasObject = new GameObject(
                "PrefabLabCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            Image backdrop = CreateSolidImage("Backdrop", canvasObject.transform, HexColor("06111EFF"));
            Stretch(backdrop.rectTransform);
            backdrop.transform.SetAsFirstSibling();

            GameObject instance = PrefabUtility.InstantiatePrefab(prefab, canvasObject.transform) as GameObject;
            if (instance == null)
                throw new InvalidOperationException("无法在 Prefab Lab 场景中实例化 ComponentInfoCard。");
            RectTransform instanceRect = instance.GetComponent<RectTransform>();
            instanceRect.anchorMin = instanceRect.anchorMax = new Vector2(0.5f, 0.5f);
            instanceRect.pivot = new Vector2(0.5f, 0.5f);
            instanceRect.anchoredPosition = Vector2.zero;
            ComponentInfoCardView view = instance.GetComponent<ComponentInfoCardView>();
            ComponentInfoCardGifPlayer gifPlayer = instance.GetComponent<ComponentInfoCardGifPlayer>();
            view.SetContent(SampleTitle, SampleDescription, null);
            PrefabUtility.RecordPrefabInstancePropertyModifications(view.TitleText);
            PrefabUtility.RecordPrefabInstancePropertyModifications(view.DescriptionText);
            PrefabUtility.RecordPrefabInstancePropertyModifications(view.PreviewImage);

            GameObject driverObject = new GameObject("ComponentInfoCardGifPreviewDriver");
            ComponentInfoCardPrefabLabPreview driver = driverObject.AddComponent<ComponentInfoCardPrefabLabPreview>();
            driver.Configure(view, gifPlayer, SampleGifFileName);
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            SetLayerRecursively(canvasObject, LayerMask.NameToLayer("UI"));

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("保存 Component Info Card Prefab Lab 场景失败。");

            Selection.activeGameObject = instance;
        }

        private static void ConfigureTextureImporters()
        {
            AssetDatabase.Refresh();
            for (int index = 0; index < SpritePaths.Length; index++)
            {
                string path = SpritePaths[index];
                if (!File.Exists(Path.GetFullPath(path)))
                    throw new FileNotFoundException("缺少元件介绍卡切图", path);

                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                    throw new InvalidOperationException("无法获取 TextureImporter：" + path);

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                TextureImporterSettings importerSettings = new TextureImporterSettings();
                importer.ReadTextureSettings(importerSettings);
                importerSettings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(importerSettings);
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.sRGBTexture = true;
                importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = 2048;
                importer.SaveAndReimport();
            }
        }

        private static void ValidateGifFiles(ref bool valid)
        {
            for (int index = 0; index < GifFileNames.Length; index++)
            {
                string fileName = GifFileNames[index];
                string assetPath = GifRoot + "/" + fileName;
                string fullPath = Path.GetFullPath(assetPath);
                bool exists = File.Exists(fullPath);
                Check(ref valid, exists, "缺少 GIF 占位资产：" + assetPath);
                if (!exists)
                    continue;

                FileInfo info = new FileInfo(fullPath);
                if (info.Length > ComponentInfoCardGifPlayer.RecommendedMaxBytes)
                {
                    Debug.LogWarning(
                        $"[ComponentInfoCard] GIF `{fileName}` 为 {info.Length / (1024f * 1024f):F1} MB，超过建议的 5 MB 上限。");
                }

                try
                {
                    byte[] bytes = File.ReadAllBytes(fullPath);
                    int frameCount = 0;
                    using (GifStream gif = new GifStream(bytes))
                    {
                        gif.ReadHeader();
                        Check(
                            ref valid,
                            gif.Header.width == 384 && gif.Header.height == 384,
                            fileName + " 尺寸必须为 384×384");

                        while (gif.HasMoreData && frameCount < 2)
                        {
                            if (gif.CurrentToken == GifStream.Token.Image)
                            {
                                gif.ReadImage();
                                frameCount++;
                            }
                            else
                            {
                                gif.SkipToken();
                            }
                        }
                    }

                    Check(ref valid, frameCount >= 2, fileName + " 必须至少包含两帧");
                }
                catch (Exception exception)
                {
                    Check(ref valid, false, fileName + " 无法解码：" + exception.Message);
                }
            }
        }

        private static void ValidateScene(ref bool valid)
        {
            Scene loaded = SceneManager.GetSceneByPath(ScenePath);
            bool openedForValidation = !loaded.IsValid() || !loaded.isLoaded;
            Scene scene = openedForValidation
                ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive)
                : loaded;

            GameObject[] roots = scene.GetRootGameObjects();
            ComponentInfoCardView[] views = roots
                .SelectMany(root => root.GetComponentsInChildren<ComponentInfoCardView>(true))
                .ToArray();
            Canvas[] canvases = roots.SelectMany(root => root.GetComponentsInChildren<Canvas>(true)).ToArray();
            CanvasScaler[] scalers = roots.SelectMany(root => root.GetComponentsInChildren<CanvasScaler>(true)).ToArray();
            ComponentInfoCardGifPlayer[] players = roots.SelectMany(root => root.GetComponentsInChildren<ComponentInfoCardGifPlayer>(true)).ToArray();
            ComponentInfoCardPrefabLabPreview[] drivers = roots.SelectMany(root => root.GetComponentsInChildren<ComponentInfoCardPrefabLabPreview>(true)).ToArray();

            Check(ref valid, views.Length == 1, "Prefab Lab 场景必须只有一个 ComponentInfoCardView");
            Check(ref valid, players.Length == 1 && players[0].IsValid, "Prefab Lab 必须只有一个有效 GIF 播放器");
            Check(ref valid, drivers.Length == 1 && drivers[0].IsValid, "Prefab Lab 必须只有一个有效 GIF 预览驱动");
            Check(
                ref valid,
                drivers.Length == 1 && drivers[0].GifFileName == SampleGifFileName,
                "Prefab Lab 应自动预览 Laser.gif");
            Check(ref valid, canvases.Length == 1 && canvases[0].renderMode == RenderMode.ScreenSpaceOverlay, "Prefab Lab Canvas 配置错误");
            Check(
                ref valid,
                scalers.Length == 1 && Approximately(scalers[0].referenceResolution, new Vector2(1920f, 1080f)),
                "Prefab Lab Canvas 参考分辨率不是 1920×1080");
            Check(ref valid, scalers.Length == 1 && Mathf.Approximately(scalers[0].matchWidthOrHeight, 0.5f), "Canvas Match 应为 0.5");
            Check(ref valid, roots.Sum(MissingScriptCount) == 0, "Prefab Lab 场景存在 Missing Script");

            if (views.Length == 1)
            {
                Check(ref valid, views[0].TitleText.text == SampleTitle, "Prefab Lab 示例标题不是“激光器”");
                Check(ref valid, views[0].DescriptionText.text == SampleDescription, "Prefab Lab 激光器示例文案不正确");
                Check(ref valid, !views[0].PreviewImage.enabled, "Prefab Lab 静态回退图应默认为空");
                Check(ref valid, !views[0].AnimatedPreviewImage.enabled, "Prefab Lab GIF 应只在 Play Mode 开始播放");
            }

            if (openedForValidation)
                EditorSceneManager.CloseScene(scene, true);
        }

        private static bool CanChangeScenes()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Component Info Card", "请先停止 Play Mode。", "确定");
                return false;
            }

            return EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
        }

        private static Sprite LoadSprite(string path)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
                throw new FileNotFoundException("无法加载 Sprite", path);
            return sprite;
        }

        private static Image CreateImage(string name, Transform parent, Sprite sprite, Color color)
        {
            GameObject gameObject = CreateRectObject(name, parent);
            Image image = gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.type = Image.Type.Simple;
            image.raycastTarget = false;
            return image;
        }

        private static Image CreateSolidImage(string name, Transform parent, Color color)
        {
            return CreateImage(name, parent, null, color);
        }

        private static TextMeshProUGUI CreateText(
            string name,
            Transform parent,
            string value,
            TMP_FontAsset font,
            float fontSize,
            Color color,
            TextAlignmentOptions alignment)
        {
            GameObject gameObject = CreateRectObject(name, parent);
            TextMeshProUGUI text = gameObject.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.font = font;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            return text;
        }

        private static void CreateBracket(
            Transform parent,
            Sprite sprite,
            string name,
            float left,
            float top,
            float rotation)
        {
            Image image = CreateImage(name, parent, sprite, Color.white);
            image.preserveAspect = true;
            SetTopLeftRect(image.rectTransform, left, top, ScaleDesignValue(20f), ScaleDesignValue(19f));
            image.rectTransform.localEulerAngles = new Vector3(0f, 0f, rotation);
        }

        private static GameObject CreateRectObject(string name, Transform parent)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform));
            if (parent != null)
                gameObject.transform.SetParent(parent, false);
            return gameObject;
        }

        private static void SetCenteredRect(RectTransform rect, Vector2 size, Vector2 position)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }

        private static void SetTopLeftRect(RectTransform rect, float left, float top, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(left, -top);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static void SetStretchRect(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            if (layer < 0)
                return;
            root.layer = layer;
            for (int index = 0; index < root.transform.childCount; index++)
                SetLayerRecursively(root.transform.GetChild(index).gameObject, layer);
        }

        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
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
            int count = 0;
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < transforms.Length; index++)
                count += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transforms[index].gameObject);
            return count;
        }

        private static Color HexColor(string value)
        {
            Color color;
            if (!ColorUtility.TryParseHtmlString("#" + value, out color))
                throw new ArgumentException("无效颜色：" + value);
            return color;
        }

        private static bool Approximately(Vector2 left, Vector2 right)
        {
            return Mathf.Abs(left.x - right.x) <= 0.01f && Mathf.Abs(left.y - right.y) <= 0.01f;
        }

        private static float ScaleDesignValue(float value)
        {
            return value * CardScale;
        }

        private static void Check(ref bool valid, bool condition, string message)
        {
            if (condition)
                return;
            valid = false;
            Debug.LogError("[ComponentInfoCard] " + message);
        }
    }
}
#endif

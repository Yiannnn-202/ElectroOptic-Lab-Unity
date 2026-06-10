#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using ElectroOptics;
using ElectroOptics.ConoscopicAnalysis;

public static class ConoscopicJonesVisualizationSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/ConoscopicJonesIntensitySurface_Test.unity";
    private const string LiNbO3ProfilePath = "Assets/Resources/Profiles/LiNbO3_Profile.asset";
    private const string KtpProfilePath = "Assets/Resources/Profiles/KTP_Profile.asset";

    [MenuItem("ElectroOptics/Tests/Create Conoscopic Jones Visualization Scene")]
    public static void CreateScene()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "ConoscopicJonesIntensitySurface_Test";

        CrystalProfile liNbO3 = AssetDatabase.LoadAssetAtPath<CrystalProfile>(LiNbO3ProfilePath);
        CrystalProfile ktp = AssetDatabase.LoadAssetAtPath<CrystalProfile>(KtpProfilePath);

        var surface = new GameObject("Conoscopic Jones Intensity Surface");
        surface.transform.position = Vector3.zero;
        var core = surface.AddComponent<ConoscopicJonesGpuCore>();
        var visualizer = surface.AddComponent<ConoscopicJonesSurfaceVisualizer>();
        visualizer.SetCore(core);
        visualizer.SetProfile(liNbO3 != null ? liNbO3 : ktp);
        visualizer.SetResolution(128);

        var cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        cameraObject.transform.position = new Vector3(0f, 4.6f, -6.2f);
        cameraObject.transform.rotation = Quaternion.Euler(48f, 0f, 0f);
        var camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.025f, 0.04f, 0.045f, 1f);
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 100f;

        var lightObject = new GameObject("Directional Light");
        lightObject.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
        var light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.25f;

        var fillObject = new GameObject("Fill Light");
        fillObject.transform.rotation = Quaternion.Euler(30f, 140f, 0f);
        var fill = fillObject.AddComponent<Light>();
        fill.type = LightType.Directional;
        fill.intensity = 0.35f;

        var labelObject = new GameObject("Jones Runtime Controls");
        labelObject.transform.position = new Vector3(0f, 2.7f, 3.0f);
        labelObject.transform.rotation = Quaternion.Euler(18f, 0f, 0f);
        var text = labelObject.AddComponent<TextMesh>();
        text.text = "Jones Conoscopic Surface Test\nEnter Play Mode and use the control panel";
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.characterSize = 0.13f;
        text.color = Color.white;

        ConfigureVisualizer(visualizer, liNbO3, ktp, surface.transform, text);

        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.Refresh();
        Debug.Log($"[ConoscopicJonesVisualizationSceneBuilder] Created visualization scene: {ScenePath}");
    }

    [MenuItem("ElectroOptics/Tests/Repair Conoscopic Jones Visualization Scene")]
    public static void RepairCurrentScene()
    {
        CrystalProfile liNbO3 = AssetDatabase.LoadAssetAtPath<CrystalProfile>(LiNbO3ProfilePath);
        CrystalProfile ktp = AssetDatabase.LoadAssetAtPath<CrystalProfile>(KtpProfilePath);
        GameObject surface = GameObject.Find("Conoscopic Jones Intensity Surface");
        if (surface == null)
        {
            Debug.LogWarning("[ConoscopicJonesVisualizationSceneBuilder] Surface object not found. Create the Jones visualization scene first.");
            return;
        }

        var core = surface.GetComponent<ConoscopicJonesGpuCore>();
        if (core == null) core = surface.AddComponent<ConoscopicJonesGpuCore>();

        var visualizer = surface.GetComponent<ConoscopicJonesSurfaceVisualizer>();
        if (visualizer == null) visualizer = surface.AddComponent<ConoscopicJonesSurfaceVisualizer>();

        TextMesh statusText = null;
        GameObject controls = GameObject.Find("Jones Runtime Controls");
        if (controls != null)
        {
            statusText = controls.GetComponent<TextMesh>();
        }

        if (statusText == null)
        {
            controls = new GameObject("Jones Runtime Controls");
            controls.transform.position = new Vector3(0f, 2.7f, 3.0f);
            controls.transform.rotation = Quaternion.Euler(18f, 0f, 0f);
            statusText = controls.AddComponent<TextMesh>();
            statusText.anchor = TextAnchor.MiddleCenter;
            statusText.alignment = TextAlignment.Center;
            statusText.characterSize = 0.13f;
            statusText.color = Color.white;
        }

        statusText.text = "Jones Conoscopic Surface Test\nEnter Play Mode and use the control panel";
        visualizer.SetCore(core);
        visualizer.SetProfile(liNbO3 != null ? liNbO3 : ktp);
        visualizer.SetResolution(128);
        ConfigureVisualizer(visualizer, liNbO3, ktp, surface.transform, statusText);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("[ConoscopicJonesVisualizationSceneBuilder] Repaired Jones visualization scene runtime controls.");
    }

    private static void ConfigureVisualizer(
        ConoscopicJonesSurfaceVisualizer visualizer,
        CrystalProfile liNbO3,
        CrystalProfile ktp,
        Transform surfaceRoot,
        TextMesh statusText)
    {
        if (visualizer == null)
        {
            return;
        }

        var serialized = new SerializedObject(visualizer);
        SetObjectReference(serialized, "liNbO3Profile", liNbO3);
        SetObjectReference(serialized, "ktpProfile", ktp);
        SetObjectReference(serialized, "surfaceRoot", surfaceRoot);
        SetObjectReference(serialized, "statusText", statusText);
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetObjectReference(SerializedObject serialized, string propertyName, UnityEngine.Object value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
        {
            property.objectReferenceValue = value;
        }
    }
}
#endif

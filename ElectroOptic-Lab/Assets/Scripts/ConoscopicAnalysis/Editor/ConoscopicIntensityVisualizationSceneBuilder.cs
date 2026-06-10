#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using ElectroOptics;
using ElectroOptics.ConoscopicAnalysis;

public static class ConoscopicIntensityVisualizationSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/ConoscopicIntensitySurface_Test.unity";
    private const string LiNbO3ProfilePath = "Assets/Resources/Profiles/LiNbO3_Profile.asset";
    private const string KtpProfilePath = "Assets/Resources/Profiles/KTP_Profile.asset";

    [MenuItem("ElectroOptics/Tests/Create Conoscopic Intensity Visualization Scene")]
    public static void CreateScene()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "ConoscopicIntensitySurface_Test";

        CrystalProfile liNbO3 = AssetDatabase.LoadAssetAtPath<CrystalProfile>(LiNbO3ProfilePath);
        CrystalProfile ktp = AssetDatabase.LoadAssetAtPath<CrystalProfile>(KtpProfilePath);

        var surface = new GameObject("Conoscopic Intensity Surface");
        surface.transform.position = Vector3.zero;
        var core = surface.AddComponent<ConoscopicIntensityCore>();
        var visualizer = surface.AddComponent<ConoscopicIntensitySurfaceVisualizer>();
        visualizer.SetCore(core);
        visualizer.SetProfile(ktp != null ? ktp : liNbO3);
        visualizer.SetResolution(96);

        var cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        cameraObject.transform.position = new Vector3(0f, 5.2f, -6.5f);
        cameraObject.transform.rotation = Quaternion.Euler(52f, 0f, 0f);
        var camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.03f, 0.035f, 0.05f, 1f);
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 100f;

        var lightObject = new GameObject("Directional Light");
        lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        var light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.1f;

        var labelObject = new GameObject("Runtime Controls");
        labelObject.transform.position = new Vector3(0f, 2.6f, 3.0f);
        labelObject.transform.rotation = Quaternion.Euler(18f, 0f, 0f);
        var text = labelObject.AddComponent<TextMesh>();
        text.text = "Conoscopic Intensity Surface Test\nEnter Play Mode and use the control panel";
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.characterSize = 0.13f;
        text.color = Color.white;

        ConfigureVisualizer(visualizer, liNbO3, ktp, surface.transform, text);

        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.Refresh();
        Debug.Log($"[ConoscopicIntensityVisualizationSceneBuilder] Created visualization scene: {ScenePath}");
    }

    [MenuItem("ElectroOptics/Tests/Repair Conoscopic Intensity Visualization Scene")]
    public static void RepairCurrentScene()
    {
        CrystalProfile liNbO3 = AssetDatabase.LoadAssetAtPath<CrystalProfile>(LiNbO3ProfilePath);
        CrystalProfile ktp = AssetDatabase.LoadAssetAtPath<CrystalProfile>(KtpProfilePath);
        GameObject surface = GameObject.Find("Conoscopic Intensity Surface");
        if (surface == null)
        {
            Debug.LogWarning("[ConoscopicIntensityVisualizationSceneBuilder] Surface object not found. Create the visualization scene first.");
            return;
        }

        var core = surface.GetComponent<ConoscopicIntensityCore>();
        if (core == null) core = surface.AddComponent<ConoscopicIntensityCore>();

        var visualizer = surface.GetComponent<ConoscopicIntensitySurfaceVisualizer>();
        if (visualizer == null) visualizer = surface.AddComponent<ConoscopicIntensitySurfaceVisualizer>();

        TextMesh statusText = null;
        GameObject controls = GameObject.Find("Runtime Controls");
        if (controls != null)
        {
            statusText = controls.GetComponent<TextMesh>();
        }

        if (statusText == null)
        {
            controls = new GameObject("Runtime Controls");
            controls.transform.position = new Vector3(0f, 2.6f, 3.0f);
            controls.transform.rotation = Quaternion.Euler(18f, 0f, 0f);
            statusText = controls.AddComponent<TextMesh>();
            statusText.anchor = TextAnchor.MiddleCenter;
            statusText.alignment = TextAlignment.Center;
            statusText.characterSize = 0.13f;
            statusText.color = Color.white;
        }

        statusText.text = "Conoscopic Intensity Surface Test\nEnter Play Mode and use the control panel";
        visualizer.SetCore(core);
        visualizer.SetProfile(ktp != null ? ktp : liNbO3);
        visualizer.SetResolution(96);
        ConfigureVisualizer(visualizer, liNbO3, ktp, surface.transform, statusText);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("[ConoscopicIntensityVisualizationSceneBuilder] Repaired visualization scene runtime controls.");
    }

    private static void ConfigureVisualizer(
        ConoscopicIntensitySurfaceVisualizer visualizer,
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

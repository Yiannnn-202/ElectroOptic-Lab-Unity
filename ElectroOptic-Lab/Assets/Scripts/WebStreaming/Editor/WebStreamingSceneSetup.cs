#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using ElectroOptics.WebStreaming;

/// <summary>
/// Editor 菜单工具：一键将 WebStreaming 挂载到当前场景。
///
/// 运行方式：
///   Unity 顶部菜单 → ElectroOptics → WebStreaming → Add to Active Scene
///
/// 前置条件：
///   - com.unity.renderstreaming 包已安装并编译通过
///   - 当前打开的场景中有 MainCamera
/// </summary>
public static class WebStreamingSceneSetup
{
    private const string MenuPath = "ElectroOptics/WebStreaming/Add to Active Scene";

    [MenuItem(MenuPath)]
    public static void AddToActiveScene()
    {
        Scene currentScene = SceneManager.GetActiveScene();
        if (!currentScene.IsValid())
        {
            Debug.LogError("[WebStreaming] 没有打开的场景！请先打开目标场景。");
            return;
        }

        // 防重复：检查是否已存在
        var existing = GameObject.Find("WebStreaming");
        if (existing != null)
        {
            Debug.LogWarning($"[WebStreaming] 场景 {currentScene.name} 中已存在 WebStreaming 对象，跳过。");
            Selection.activeGameObject = existing;
            return;
        }

        // 创建 GameObject
        var go = new GameObject("WebStreaming");
        go.transform.position = Vector3.zero;
        go.transform.rotation = Quaternion.identity;

        // 挂载 WebStreamingSetup 组件
        var setup = go.AddComponent<WebStreamingSetup>();

        // 自动查找 MainCamera
        var mainCam = Camera.main;
        if (mainCam != null)
        {
            setup.streamCamera = mainCam;
            Debug.Log($"[WebStreaming] ✅ 已自动关联 Main Camera: {mainCam.name}");
        }
        else
        {
            Debug.LogWarning("[WebStreaming] ⚠️ 未找到 MainCamera！请在 Inspector 中手动指定 Stream Camera。");
        }

        // 默认设置
        setup.signalingUrl = "http://localhost";
        setup.streamSize = new Vector2Int(1920, 1080);
        setup.enableRemoteInput = true;

        // 标记场景脏
        EditorSceneManager.MarkSceneDirty(currentScene);
        Selection.activeGameObject = go;

        Debug.Log($"[WebStreaming] ✅ 已将 WebStreaming 挂载到场景 {currentScene.name}。\n" +
                  "  信令地址: http://localhost\n" +
                  "  推流分辨率: 1920×1080\n" +
                  "  远程输入: 已启用\n\n" +
                  "  如需修改地址，选中 WebStreaming 对象后在 Inspector 中调整 Signaling URL。");
    }

    [MenuItem(MenuPath, validate = true)]
    public static bool ValidateAddToActiveScene()
    {
        var scene = SceneManager.GetActiveScene();
        return scene.IsValid() && !EditorApplication.isPlaying;
    }
}
#endif

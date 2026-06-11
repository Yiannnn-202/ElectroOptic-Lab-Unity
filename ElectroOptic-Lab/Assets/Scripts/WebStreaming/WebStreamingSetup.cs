using UnityEngine;
using UnityEngine.InputSystem;
using Unity.RenderStreaming;

namespace ElectroOptics.WebStreaming
{
    /// <summary>
    /// Render Streaming 场景配置器 — 自动装配 WebRTC 推流和远程输入。
    ///
    /// 挂载方式：
    ///   在主场景 (Scene2.The Lab) 根节点新建空 GameObject 命名为 "WebStreaming"，
    ///   挂载此脚本，Inspector 中设置 Signaling URL 即可。
    ///
    /// 前置条件：
    ///   - manifest.json 中已添加 com.unity.renderstreaming@3.1.0-exp.7
    ///   - com.unity.inputsystem 已安装
    ///   - Player Settings → Input System 设为 "Both" 模式
    /// </summary>
    public class WebStreamingSetup : MonoBehaviour
    {
        [Header("信令服务器")]
        [Tooltip("信令服务器地址。本地: http://localhost, 局域网: http://192.168.x.x")]
        public string signalingUrl = "http://localhost";

        [Header("视频流")]
        [Tooltip("推流相机，留空自动取 Camera.main")]
        public Camera streamCamera;

        [Tooltip("推流分辨率")]
        public Vector2Int streamSize = new Vector2Int(1920, 1080);

        [Header("输入")]
        [Tooltip("启用远程键盘 + 鼠标输入")]
        public bool enableRemoteInput = true;

        private void Start()
        {
            if (streamCamera == null)
                streamCamera = Camera.main;

            if (streamCamera == null)
            {
                Debug.LogError("[WebStreaming] 场景中没有 MainCamera！请在场景中确保有 tag=MainCamera 的相机。");
                return;
            }

            SetupRenderStreaming();
            SetupRemoteInput();

            RemoteInputRelay.IsRemote = true;
            Debug.Log($"[WebStreaming] ✅ 初始化完成 — 相机: {streamCamera.name}, " +
                      $"分辨率: {streamSize.x}x{streamSize.y}, 信令: {signalingUrl}");
        }

        private void SetupRenderStreaming()
        {
            // ── 1. RenderStreaming 主组件 ────────────────────
            var rs = gameObject.AddComponent<RenderStreaming>();
            rs.runOnAwake = true;

            // ── 2. 信令处理器（连接 Web 服务器）───────────────
            var sigGO = new GameObject("Signaling");
            sigGO.transform.SetParent(transform);

            var sig = sigGO.AddComponent<SignalingHandler>();
            sig.url = signalingUrl;

            // 单连接模式（单用户访问）
            sigGO.AddComponent<SingleConnection>();

            // ── 3. 相机推流 ──────────────────────────────────
            var camStreamer = streamCamera.gameObject.AddComponent<CameraStreamer>();
            camStreamer.streamSize = streamSize;

            // 如果相机上已有 AudioListener，也可以推音频
            // var audioListener = streamCamera.GetComponent<AudioListener>();
            // if (audioListener != null)
            //     streamCamera.gameObject.AddComponent<AudioStreamSender>();

            // ── 4. 输入接收（浏览器 → Input System）──────────
            if (enableRemoteInput)
            {
                var inputGO = new GameObject("InputReceiver");
                inputGO.transform.SetParent(transform);
                inputGO.AddComponent<InputReceiver>();

                // 确保 Input System 设备已注册，供 RemoteInputRelay 读取
                InputSystem.AddDevice<Keyboard>();
                InputSystem.AddDevice<Mouse>();
                InputSystem.AddDevice<Touchscreen>();

                Debug.Log("[WebStreaming] 远程输入已启用 — 键盘 + 鼠标 + 触摸。");
            }
        }

        private void SetupRemoteInput()
        {
            if (!enableRemoteInput) return;
            gameObject.AddComponent<RemoteInputUpdater>();
        }
    }
}

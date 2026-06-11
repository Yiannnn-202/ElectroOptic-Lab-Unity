using UnityEngine;

namespace ElectroOptics.WebStreaming
{
    /// <summary>
    /// 每帧驱动 RemoteInputRelay 同步远程输入状态，
    /// 并负责将远程鼠标点击转发为 OnMouseDown 回调。
    ///
    /// 挂载到场景中后自动在 Update 中拉取 Input System 键盘/鼠标状态。
    /// </summary>
    public class RemoteInputUpdater : MonoBehaviour
    {
        private Camera _mainCamera;

        private void Start()
        {
            _mainCamera = Camera.main;
        }

        private void Update()
        {
            if (!RemoteInputRelay.IsRemote) return;

            RemoteInputRelay.BeginFrame();
            RemoteInputRelay.SyncKeyboard();
            RemoteInputRelay.SyncMouse();

            // 将远程鼠标点击转发为 OnMouseDown 回调
            // （Render Streaming 的 InputReceiver 只喂 Input System，
            //   不会触发 Unity 引擎内部的 OnMouseDown 射线检测）
            if (RemoteInputRelay.GetMouseButtonDown(0))
            {
                ForwardMouseClick();
            }
        }

        /// <summary>
        /// 从远程鼠标位置发出射线，对命中的 GameObject 调用 OnMouseDown。
        /// 模拟 Unity 引擎内部的鼠标点击处理流程。
        /// </summary>
        private void ForwardMouseClick()
        {
            if (_mainCamera == null)
            {
                _mainCamera = Camera.main;
                if (_mainCamera == null) return;
            }

            Vector3 mousePos = RemoteInputRelay.MousePosition;
            Ray ray = _mainCamera.ScreenPointToRay(mousePos);

            // Physics.Raycast 返回最近的碰撞体（与引擎 OnMouseDown 行为一致）
            if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, Physics.AllLayers))
            {
                // 向碰撞体所在的 GameObject 及其所有父级发送 OnMouseDown
                // （Unity 的 OnMouseDown 对碰撞体所在对象及其父级都会触发，
                //   且子物体优先。这里只向直接命中对象发送，与引擎行为一致。）
                hit.collider.SendMessage("OnMouseDown", SendMessageOptions.DontRequireReceiver);
            }
        }
    }
}

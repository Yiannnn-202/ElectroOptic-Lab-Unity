#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace ElectroOptics.UI.ComponentInfoCard.Editor
{
    /// <summary>
    /// 采用项目现有 Editor 菜单测试方式验证悬停状态、淡入淡出和包围盒行为。
    /// </summary>
    public static class ComponentInfoCardHoverEditorTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        [MenuItem("ElectroOptics/Tests/Run Component Info Card Hover Tests")]
        public static void RunFromMenu()
        {
            RunForBatch();
        }

        public static void RunForBatch()
        {
            Assert(ComponentInfoCardScene2Builder.ValidateScene2(false), "Scene2 悬停交互结构验证失败");
            TestBoundsIgnoreDynamicLineRenderer();
            TestHoverDelayResetSwitchAndFade();
            Debug.Log("[ComponentInfoCard] 悬停延迟、移出重置、切换重置、淡入淡出和包围盒测试全部通过。");
        }

        private static void TestBoundsIgnoreDynamicLineRenderer()
        {
            GameObject root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            root.name = "BoundsTarget";
            ComponentInfoCardContent content = CreateContent("包围盒", "验证动态激光线不会影响仪器本体包围盒。");
            ComponentInfoCardTarget target = root.AddComponent<ComponentInfoCardTarget>();
            SetSerializedReference(target, "content", content);

            GameObject lineObject = new GameObject("DynamicLaserLine");
            lineObject.transform.SetParent(root.transform, false);
            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.positionCount = 2;
            line.SetPosition(0, Vector3.zero);
            line.SetPosition(1, new Vector3(100f, 100f, 100f));

            try
            {
                Assert(target.TryGetWorldBounds(out Bounds bounds), "Target 应能计算包围盒");
                Assert(bounds.size.x < 10f && bounds.size.y < 10f && bounds.size.z < 10f,
                    "LineRenderer 不应把目标包围盒扩展到远处");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(content);
            }
        }

        private static void TestHoverDelayResetSwitchAndFade()
        {
            GameObject canvasObject = new GameObject("TestCanvas", typeof(RectTransform), typeof(Canvas));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ComponentInfoCardPrefabBuilder.PrefabPath);
            Assert(prefab != null, "缺少 ComponentInfoCard.prefab");
            GameObject cardObject = UnityEngine.Object.Instantiate(prefab, canvasObject.transform, false);
            ComponentInfoCardView view = cardObject.GetComponent<ComponentInfoCardView>();

            GameObject cameraObject = new GameObject("HoverTestCamera", typeof(Camera));
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.orthographic = true;
            camera.orthographicSize = 5f;

            GameObject controllerObject = new GameObject("HoverTestController");
            ComponentInfoCardHoverController controller =
                controllerObject.AddComponent<ComponentInfoCardHoverController>();
            SetPrivateField(controller, "hoverCamera", camera);
            SetPrivateField(controller, "targetCanvas", canvas);
            SetPrivateField(controller, "cardView", view);
            SetPrivateField(controller, "hoverDelay", 0.6f);
            SetPrivateField(controller, "fadeDuration", 0.18f);
            Invoke(controller, "PrepareHiddenState");

            ComponentInfoCardContent firstContent = CreateContent("目标一", "用于验证第一个悬停目标。");
            ComponentInfoCardContent secondContent = CreateContent("目标二", "用于验证切换目标后重新计时。");
            GameObject firstObject = CreateTarget("FirstTarget", firstContent, new Vector3(-1f, 0f, 0f));
            GameObject secondObject = CreateTarget("SecondTarget", secondContent, new Vector3(1f, 0f, 0f));
            ComponentInfoCardTarget firstTarget = firstObject.GetComponent<ComponentInfoCardTarget>();
            ComponentInfoCardTarget secondTarget = secondObject.GetComponent<ComponentInfoCardTarget>();

            try
            {
                Process(controller, firstTarget, 0.59f);
                Assert(controller.VisibleTarget == null, "悬停 0.59 秒时不应显示");

                Process(controller, firstTarget, 0.01f);
                Assert(controller.VisibleTarget == firstTarget, "悬停达到 0.6 秒应锁定第一个目标");
                Assert(view.TitleText.text == "目标一", "显示内容未绑定到第一个目标");

                Invoke(controller, "UpdateFade", 0.18f);
                Assert(Mathf.Approximately(view.RootGroup.alpha, 1f), "0.18 秒后应完成淡入");
                Vector2 lockedPosition = view.CardRect.anchoredPosition;

                firstObject.transform.position += Vector3.up * 2f;
                camera.transform.position += Vector3.right * 2f;
                Invoke(controller, "UpdateFade", 0.05f);
                Assert(view.CardRect.anchoredPosition == lockedPosition, "卡片出现后位置应保持锁定");

                Process(controller, null, 0f);
                Assert(controller.HoveredTarget == null && controller.VisibleTarget == null,
                    "移出目标应清空悬停和显示状态");
                Invoke(controller, "UpdateFade", 0.18f);
                Assert(Mathf.Approximately(view.RootGroup.alpha, 0f) && !view.gameObject.activeSelf,
                    "移出后应完成淡出并停用卡片");

                Process(controller, firstTarget, 0.59f);
                Assert(controller.VisibleTarget == null, "重新进入同一目标必须重新计时");
                Process(controller, firstTarget, 0.01f);
                Assert(controller.VisibleTarget == firstTarget, "重新计时达到阈值后应再次显示");

                Process(controller, secondTarget, 0.3f);
                Assert(controller.VisibleTarget == null, "切换到第二目标后不应立即换内容");
                Process(controller, secondTarget, 0.3f);
                Assert(controller.VisibleTarget == secondTarget, "第二目标持续悬停 0.6 秒后应显示");
                Assert(view.TitleText.text == "目标二", "显示内容未切换到第二目标");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(firstObject);
                UnityEngine.Object.DestroyImmediate(secondObject);
                UnityEngine.Object.DestroyImmediate(firstContent);
                UnityEngine.Object.DestroyImmediate(secondContent);
                UnityEngine.Object.DestroyImmediate(controllerObject);
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(canvasObject);
            }
        }

        private static GameObject CreateTarget(
            string objectName,
            ComponentInfoCardContent content,
            Vector3 position)
        {
            GameObject targetObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            targetObject.name = objectName;
            targetObject.transform.position = position;
            ComponentInfoCardTarget target = targetObject.AddComponent<ComponentInfoCardTarget>();
            SetSerializedReference(target, "content", content);
            return targetObject;
        }

        private static ComponentInfoCardContent CreateContent(string title, string description)
        {
            ComponentInfoCardContent content = ScriptableObject.CreateInstance<ComponentInfoCardContent>();
            SerializedObject serialized = new SerializedObject(content);
            serialized.FindProperty("componentName").stringValue = title;
            serialized.FindProperty("description").stringValue = description;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return content;
        }

        private static void SetSerializedReference(UnityEngine.Object target, string propertyName, UnityEngine.Object value)
        {
            SerializedObject serialized = new SerializedObject(target);
            serialized.FindProperty(propertyName).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, PrivateInstance);
            Assert(field != null, "找不到字段：" + fieldName);
            field.SetValue(target, value);
        }

        private static void Process(
            ComponentInfoCardHoverController controller,
            ComponentInfoCardTarget target,
            float deltaTime)
        {
            Invoke(controller, "ProcessDetectedTarget", target, deltaTime);
        }

        private static object Invoke(object target, string methodName, params object[] parameters)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, PrivateInstance);
            Assert(method != null, "找不到方法：" + methodName);
            return method.Invoke(target, parameters);
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException("[ComponentInfoCard] " + message);
        }
    }
}
#endif

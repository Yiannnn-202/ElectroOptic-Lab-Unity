#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace ElectroOptics.UI.ComponentInfoCard.Editor
{
    /// <summary>
    /// 项目现有测试均为 Editor 菜单测试，本类沿用相同工作流。
    /// </summary>
    public static class ComponentInfoCardEditorTests
    {
        [MenuItem("ElectroOptics/Tests/Run Component Info Card Prefab Tests")]
        public static void RunFromMenu()
        {
            RunInternal();
        }

        public static void RunForBatch()
        {
            RunInternal();
        }

        private static void RunInternal()
        {
            if (!ComponentInfoCardPrefabBuilder.ValidateAssetsAndScene(false))
                throw new BuildFailedException("Component Info Card 结构验证失败。");

            GameObject contents = PrefabUtility.LoadPrefabContents(ComponentInfoCardPrefabBuilder.PrefabPath);
            try
            {
                ComponentInfoCardView view = contents.GetComponent<ComponentInfoCardView>();
                Assert(view != null && view.IsValid, "Prefab View 引用不完整");

                view.SetContent("测试元件", "用于验证正常文本和预览图绑定。", view.BaseImage.sprite);
                Assert(view.TitleText.text == "测试元件", "正常标题写入失败");
                Assert(view.DescriptionText.text == "用于验证正常文本和预览图绑定。", "正常正文写入失败");
                Assert(view.PreviewImage.enabled, "非空预览图应启用 PreviewImage");

                string longDescription = new string('长', 180);
                view.SetContent("长文本", longDescription, null);
                Assert(view.DescriptionText.text == longDescription, "长文本不应被 View 截断或修改");
                Assert(!view.PreviewImage.enabled, "空预览图应隐藏 PreviewImage");

                view.SetContent(null, null, null);
                Assert(view.TitleText.text == string.Empty, "空标题应转换为空字符串");
                Assert(view.DescriptionText.text == string.Empty, "空正文应转换为空字符串");
                Assert(!view.PreviewImage.enabled, "空内容状态下 PreviewImage 应保持隐藏");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }

            Debug.Log("[ComponentInfoCard] View 内容绑定、空值、长文本和 Prefab Lab 结构测试全部通过。");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException("[ComponentInfoCard] " + message);
        }
    }
}
#endif

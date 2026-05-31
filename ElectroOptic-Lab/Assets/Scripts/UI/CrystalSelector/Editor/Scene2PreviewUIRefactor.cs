using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ElectroOptics.UI.CrystalSelector.Editor
{
    /// <summary>
    /// 一键重构 Scene2-preview 的 UI 布局：
    /// 从水平 ScrollView + 3 张卡片 → 四张并排固定卡片（3 晶体 + 1 自定义占位）。
    ///
    /// 使用方法：
    /// 1. 在 Unity Editor 中打开 Scene2-preview.unity
    /// 2. 点击菜单 ElectroOptics → Refactor Scene2-Preview UI
    /// 3. 检查结果，满意后保存场景
    /// </summary>
    public static class Scene2PreviewUIRefactor
    {
        private const string MenuPath = "ElectroOptics/Refactor Scene2-Preview UI";
        private const string ContentNewName = "CardContainer";

        // 新布局参数
        private const float NewCardWidth = 380f;
        private const float NewCardHeight = 530f;
        private const float NewSpacing = 50f;
        private const float NewPaddingLeft = 100f;
        private const float NewPaddingRight = 100f;

        [MenuItem(MenuPath, false, 100)]
        public static void Refactor()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.path.EndsWith("Scene2-preview.unity"))
            {
                if (!EditorUtility.DisplayDialog(
                    "场景不匹配",
                    $"当前场景是 \"{scene.name}\"，不是 Scene2-preview。\n\n请先打开 Scene2-preview.unity，再执行此操作。",
                    "我知道了"))
                {
                }
                return;
            }

            if (!EditorUtility.DisplayDialog(
                "确认重构",
                "将对 Scene2-preview 进行以下操作：\n\n" +
                "1. 移除 ScrollView / Viewport 包裹层\n" +
                "2. 将卡片容器移至 Panel 下并居中对齐\n" +
                "3. 调整卡片尺寸为 380×530，间距 50\n" +
                "4. 添加第四张\"自定义晶体\"占位卡片\n" +
                "5. 清理遗留 Cardclick 绑定\n\n" +
                "建议先备份场景。是否继续？",
                "继续",
                "取消"))
            {
                return;
            }

            Undo.SetCurrentGroupName("Refactor Scene2-Preview UI");
            int undoGroup = Undo.GetCurrentGroup();

            try
            {
                ExecuteRefactor();
                EditorSceneManager.MarkSceneDirty(scene);
                EditorUtility.DisplayDialog("完成", "Scene2-preview UI 重构完成！\n\n请检查场景布局，满意后保存。", "好的");
            }
            catch (System.Exception e)
            {
                // 回滚所有已执行的修改
                Undo.PerformUndo();
                EditorUtility.DisplayDialog("错误", $"重构失败，已自动回滚：\n{e.Message}", "确定");
                Debug.LogError($"Scene2PreviewUIRefactor failed and was rolled back: {e}");
            }
            finally
            {
                Undo.CollapseUndoOperations(undoGroup);
            }
        }

        private static void ExecuteRefactor()
        {
            // ============================================================
            // Step 1: 查找关键 GameObject
            // ============================================================
            Canvas canvas = Object.FindObjectOfType<Canvas>();
            if (canvas == null) throw new System.Exception("找不到 Canvas");

            Transform panel = canvas.transform.Find("Panel");
            if (panel == null) throw new System.Exception("找不到 Panel");

            // 查找 ScrollView 及其子对象
            // 注意: Scroll View 是 Canvas 的直接子对象，不在 Panel 下
            Transform scrollView = canvas.transform.Find("Scroll View");
            if (scrollView == null) throw new System.Exception("找不到 Scroll View（应在 Canvas 下）");

            Transform viewport = scrollView.Find("Viewport");
            if (viewport == null) throw new System.Exception("找不到 Viewport");

            Transform content = viewport.Find("Content");
            if (content == null) throw new System.Exception("找不到 Content");

            // 查找三张晶体卡片
            Transform card1 = content.Find("Card1");
            Transform card1_1 = content.Find("Card1 (1)");
            Transform card1_2 = content.Find("Card1 (2)");

            if (card1 == null) Debug.LogWarning("找不到 Card1（铌酸锂）");
            if (card1_1 == null) Debug.LogWarning("找不到 Card1 (1)（KDP）");
            if (card1_2 == null) Debug.LogWarning("找不到 Card1 (2)（KTP）");

            Transform[] existingCards = { card1, card1_1, card1_2 };

            // 查找滚动条
            Transform scrollbar = scrollView.Find("Scrollbar Horizontal");

            Debug.Log("[Refactor] 找到所有关键 GameObject");

            // ============================================================
            // Step 2: 将 Content 移出 ScrollView，挂到 Panel 下
            // ============================================================
            Undo.SetTransformParent(content, panel, "Re-parent Content to Panel");
            content.name = ContentNewName;

            // ============================================================
            // Step 3: 删除 ScrollView（含 Viewport 和 Scrollbar）
            // ============================================================
            if (scrollbar != null)
            {
                Undo.DestroyObjectImmediate(scrollbar.gameObject);
            }
            Undo.DestroyObjectImmediate(viewport.gameObject);
            Undo.DestroyObjectImmediate(scrollView.gameObject);

            Debug.Log("[Refactor] 已移除 ScrollView / Viewport / Scrollbar");

            // ============================================================
            // Step 4: 调整 CardContainer (原 Content) 的 RectTransform
            // ============================================================
            RectTransform contentRt = content.GetComponent<RectTransform>();
            if (contentRt != null)
            {
                Undo.RecordObject(contentRt, "Adjust CardContainer RectTransform");
                // 居中锚定
                contentRt.anchorMin = new Vector2(0.5f, 0.5f);
                contentRt.anchorMax = new Vector2(0.5f, 0.5f);
                contentRt.anchoredPosition = Vector2.zero;
                contentRt.sizeDelta = Vector2.zero;
                contentRt.pivot = new Vector2(0.5f, 0.5f);
            }

            // ============================================================
            // Step 5: 调整 HorizontalLayoutGroup 参数
            // ============================================================
            HorizontalLayoutGroup hlg = content.GetComponent<HorizontalLayoutGroup>();
            if (hlg != null)
            {
                Undo.RecordObject(hlg, "Adjust HorizontalLayoutGroup");
                hlg.padding.left = (int)NewPaddingLeft;
                hlg.padding.right = (int)NewPaddingRight;
                hlg.padding.top = 20;
                hlg.padding.bottom = 0;
                hlg.spacing = NewSpacing;
                hlg.childAlignment = TextAnchor.MiddleCenter;
                hlg.childForceExpandWidth = false;
                hlg.childForceExpandHeight = false;
                hlg.childControlWidth = false;
                hlg.childControlHeight = false;
                hlg.childScaleWidth = false;
                hlg.childScaleHeight = false;
                hlg.reverseArrangement = false;
            }

            // 移除或保留 ContentSizeFitter（HorizontalFit=PreferredSize 不影响居中布局）
            ContentSizeFitter csf = content.GetComponent<ContentSizeFitter>();
            if (csf != null)
            {
                Undo.RecordObject(csf, "Adjust ContentSizeFitter");
                csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                csf.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
            }

            Debug.Log("[Refactor] CardContainer 布局参数已调整");

            // ============================================================
            // Step 6: 调整现有三张卡片尺寸
            // ============================================================
            foreach (Transform card in existingCards)
            {
                if (card == null) continue;
                AdjustCardSize(card, NewCardWidth, NewCardHeight);
                RemoveCardclickBinding(card);
            }

            Debug.Log("[Refactor] 三张卡片尺寸已调整，遗留绑定已清理");

            // ============================================================
            // Step 7: 创建第四张"自定义晶体"卡片
            // ============================================================
            GameObject customCard = CreateCustomCard(content);
            Debug.Log("[Refactor] 自定义晶体卡片已创建");

            // ============================================================
            // Step 8: 调整卡片内部子对象
            // ============================================================
            foreach (Transform card in existingCards)
            {
                if (card == null) continue;
                AdjustCardChildren(card);
            }
            AdjustCardChildren(customCard.transform);

            Debug.Log("[Refactor] 所有卡片子对象已适配新尺寸");

            // ============================================================
            // 完成
            // ============================================================
            Debug.Log("[Refactor] Scene2-preview UI 重构完成！");
            Selection.activeGameObject = content.gameObject;
        }

        private static void AdjustCardSize(Transform card, float width, float height)
        {
            RectTransform rt = card.GetComponent<RectTransform>();
            if (rt != null)
            {
                Undo.RecordObject(rt, "Resize Card");
                rt.sizeDelta = new Vector2(width, height);
            }
        }

        private static void RemoveCardclickBinding(Transform card)
        {
            Button button = card.GetComponent<Button>();
            if (button == null) return;

            Undo.RecordObject(button, "Remove Cardclick binding");

            // 找到并移除 Cardclick.GoToScene 调用
            var calls = button.onClick.GetPersistentEventCount();
            for (int i = calls - 1; i >= 0; i--)
            {
                var target = button.onClick.GetPersistentTarget(i);
                var methodName = button.onClick.GetPersistentMethodName(i);

                if (target != null && target.GetType().Name == "Cardclick" && methodName == "GoToScene")
                {
                    // Unity 不提供直接删除 PersistentCall 的 API
                    // 我们需要重建 OnClick 列表
                    Debug.Log($"[Refactor] 发现 Cardclick.GoToScene 绑定在 {card.name}，将清理");
                }
            }

            // 重建 OnClick 列表，排除 Cardclick.GoToScene
            var newCalls = new System.Collections.Generic.List<(Object, string)>();
            for (int i = 0; i < calls; i++)
            {
                var target = button.onClick.GetPersistentTarget(i);
                var methodName = button.onClick.GetPersistentMethodName(i);
                if (!(target != null && target.GetType().Name == "Cardclick" && methodName == "GoToScene"))
                {
                    newCalls.Add((target, methodName));
                }
            }

            // 如果发现需要清理的绑定，重建
            int cardclickCalls = calls - newCalls.Count;
            if (cardclickCalls > 0)
            {
                // 保存新的调用列表
                var savedTargets = new Object[newCalls.Count];
                var savedMethods = new string[newCalls.Count];
                for (int i = 0; i < newCalls.Count; i++)
                {
                    savedTargets[i] = newCalls[i].Item1;
                    savedMethods[i] = newCalls[i].Item2;
                }

                // 清空并重建
                // 注意：UnityEvent 没有 Clear() 方法，通过设置 persistentCalls 来实现
                var serializedButton = new SerializedObject(button);
                var onClickProp = serializedButton.FindProperty("m_OnClick.m_PersistentCalls.m_Calls");
                onClickProp.ClearArray();

                // 重新添加保留的调用
                for (int i = 0; i < savedTargets.Length; i++)
                {
                    onClickProp.InsertArrayElementAtIndex(i);
                    var callProp = onClickProp.GetArrayElementAtIndex(i);
                    callProp.FindPropertyRelative("m_Target").objectReferenceValue = savedTargets[i];
                    callProp.FindPropertyRelative("m_TargetAssemblyTypeName").stringValue =
                        savedTargets[i] != null
                            ? $"{savedTargets[i].GetType().FullName}, Assembly-CSharp"
                            : "";
                    callProp.FindPropertyRelative("m_MethodName").stringValue = savedMethods[i];
                    callProp.FindPropertyRelative("m_Mode").intValue = 1;
                    callProp.FindPropertyRelative("m_Arguments.m_ObjectArgumentAssemblyTypeName").stringValue =
                        "UnityEngine.Object, UnityEngine";
                    callProp.FindPropertyRelative("m_CallState").intValue = 2;
                }

                serializedButton.ApplyModifiedProperties();
                Debug.Log($"[Refactor] 已移除 {card.name} 上的 {cardclickCalls} 个 Cardclick 绑定");
            }

            // 同时删除 Cardclick 组件
            var cardclickComponents = card.GetComponents<Component>();
            foreach (var comp in cardclickComponents)
            {
                if (comp != null && comp.GetType().Name == "Cardclick")
                {
                    Undo.DestroyObjectImmediate(comp);
                }
            }
        }

        private static GameObject CreateCustomCard(Transform parent)
        {
            // 复制 Card1 (2) (KTP) 作为模板
            Transform ktpCard = parent.Find("Card1 (2)");
            if (ktpCard == null) throw new System.Exception("找不到模板卡片 Card1 (2)");

            GameObject customCard = Object.Instantiate(ktpCard.gameObject, parent);
            customCard.name = "Card_Custom";

            Undo.RegisterCreatedObjectUndo(customCard, "Create Custom Card");

            RectTransform rt = customCard.GetComponent<RectTransform>();
            if (rt != null)
            {
                Undo.RecordObject(rt, "Set Custom Card Size");
                rt.sizeDelta = new Vector2(NewCardWidth, NewCardHeight);
                rt.localScale = Vector3.one;
            }

            // 修改卡片背景颜色
            Image bgImage = customCard.GetComponent<Image>();
            if (bgImage != null)
            {
                Undo.RecordObject(bgImage, "Set Custom Card Background");
                bgImage.color = new Color(0.85f, 0.85f, 0.88f, 1f); // 浅灰紫色，区别于其他卡片
            }

            // 移除 CrystalCardSelector 组件
            var cardSelector = customCard.GetComponent<CrystalCardSelector>();
            if (cardSelector != null)
            {
                Undo.DestroyObjectImmediate(cardSelector);
            }

            // 移除 Cardclick 组件（如果有）
            var cardclickComponents = customCard.GetComponents<Component>();
            foreach (var comp in cardclickComponents)
            {
                if (comp != null && comp.GetType().Name == "Cardclick")
                {
                    Undo.DestroyObjectImmediate(comp);
                }
            }

            // 添加 CustomCrystalCard 组件
            var customScript = Undo.AddComponent<CustomCrystalCard>(customCard);

            // 修改 Button OnClick：只保留 CustomCrystalCard.OnCardClick
            Button button = customCard.GetComponent<Button>();
            if (button != null)
            {
                Undo.RecordObject(button, "Set Custom Card Button");
                var serializedButton = new SerializedObject(button);
                var onClickProp = serializedButton.FindProperty("m_OnClick.m_PersistentCalls.m_Calls");
                onClickProp.ClearArray();

                onClickProp.InsertArrayElementAtIndex(0);
                var callProp = onClickProp.GetArrayElementAtIndex(0);
                callProp.FindPropertyRelative("m_Target").objectReferenceValue = customScript;
                callProp.FindPropertyRelative("m_TargetAssemblyTypeName").stringValue =
                    "ElectroOptics.UI.CrystalSelector.CustomCrystalCard, Assembly-CSharp";
                callProp.FindPropertyRelative("m_MethodName").stringValue = "OnCardClick";
                callProp.FindPropertyRelative("m_Mode").intValue = 1;
                callProp.FindPropertyRelative("m_Arguments.m_ObjectArgumentAssemblyTypeName").stringValue =
                    "UnityEngine.Object, UnityEngine";
                callProp.FindPropertyRelative("m_CallState").intValue = 2;

                serializedButton.ApplyModifiedProperties();
            }

            // 修改子对象 Text — 改为"自定义晶体"
            var texts = customCard.GetComponentsInChildren<TMPro.TextMeshProUGUI>();
            foreach (var tmp in texts)
            {
                Undo.RecordObject(tmp, "Set Custom Card Text");
                tmp.text = "自定义晶体";
                tmp.color = new Color(0.4f, 0.4f, 0.5f, 1f);
            }

            // 修改子对象 Crystal Image 颜色
            var images = customCard.GetComponentsInChildren<Image>();
            foreach (var img in images)
            {
                if (img.gameObject.name.Contains("Image") || img.gameObject.name.Contains("Crystal"))
                {
                    Undo.RecordObject(img, "Set Custom Card Image Tint");
                    img.color = new Color(0.7f, 0.7f, 0.75f, 1f);
                }
            }

            // 确保是最后一个子对象
            customCard.transform.SetAsLastSibling();

            return customCard;
        }

        private static void AdjustCardChildren(Transform card)
        {
            if (card == null) return;

            // 调整 Crystal Image 子对象的水平边距
            Transform imageChild = card.Find("Crystal Image");
            if (imageChild != null)
            {
                RectTransform rt = imageChild.GetComponent<RectTransform>();
                if (rt != null)
                {
                    Undo.RecordObject(rt, "Adjust Crystal Image Margins");
                    // 原来的 SizeDelta x=-40 (左右各20边距) → 改为 -30 (左右各15)
                    rt.sizeDelta = new Vector2(-30, rt.sizeDelta.y);
                }
            }

            // 调整 Name text 子对象的水平边距
            // Card1（铌酸锂）的边距是 288，远超 Card1(1)/Card1(2) 的 96
            // 卡片宽度从 487 缩到 380 后需要同步缩小边距，防止文字换行
            Transform nameText = card.Find("Name text");
            if (nameText != null)
            {
                RectTransform rt = nameText.GetComponent<RectTransform>();
                if (rt != null)
                {
                    Undo.RecordObject(rt, "Adjust Name Text Margins");
                    // 如果边距 > 120，说明是异常大的边距，统一改为 80（左右各 40）
                    // 文字可用宽度 = 380 - 80 = 300，足以容纳 "铌酸锂晶体"（字号 36，约 180px）
                    if (Mathf.Abs(rt.sizeDelta.x) > 120f)
                    {
                        rt.sizeDelta = new Vector2(-80, rt.sizeDelta.y);
                    }
                }
            }
        }
    }
}

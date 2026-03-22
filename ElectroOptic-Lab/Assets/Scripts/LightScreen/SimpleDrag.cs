using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 拖拽辅助脚本
/// 为 UI 窗口提供拖拽移动功能
/// </summary>
public class SimpleDrag : MonoBehaviour, IDragHandler
{
    public void OnDrag(PointerEventData eventData)
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            Vector2 pos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                (RectTransform)canvas.transform,
                eventData.position,
                canvas.worldCamera,
                out pos);
            transform.position += (Vector3)eventData.delta;
        }
    }
}

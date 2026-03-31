using UnityEngine;

public class FocusableItem : MonoBehaviour
{
    [Tooltip("请在此处拖入一个空物体，作为该原件特写时的相机位置和旋转参考")]
    public Transform closeUpCameraAnchor;

    // 预留接口：如果你后续想在选中时加个高光描边，可以在这里写
    public void OnSelected()
    {
        Debug.Log($"已锁定目标: {gameObject.name}");
        // 例如：GetComponent<Outline>().enabled = true;
    }

    public void OnDeselected()
    {
        // 例如：GetComponent<Outline>().enabled = false;
    }
}
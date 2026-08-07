using UnityEngine;

namespace ElectroOptics.UI.ComponentInfoCard
{
    /// <summary>
    /// 标记一个可显示元件介绍卡的 3D 场景对象，并提供稳定的世界包围盒。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ComponentInfoCardTarget : MonoBehaviour
    {
        [SerializeField] private ComponentInfoCardContent content;

        public ComponentInfoCardContent Content => content;
        public bool IsValid => content != null && content.IsValid;

        public bool TryGetWorldBounds(out Bounds bounds)
        {
            bool hasBounds = false;
            bounds = default;

            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer current = renderers[index];
                if (!IsUsableRenderer(current))
                    continue;

                Encapsulate(ref bounds, ref hasBounds, current.bounds);
            }

            Collider[] colliders = GetComponentsInChildren<Collider>(true);
            for (int index = 0; index < colliders.Length; index++)
            {
                Collider current = colliders[index];
                if (current == null
                    || !current.enabled
                    || current.isTrigger
                    || !current.gameObject.activeInHierarchy)
                {
                    continue;
                }

                Encapsulate(ref bounds, ref hasBounds, current.bounds);
            }

            return hasBounds;
        }

        private static bool IsUsableRenderer(Renderer renderer)
        {
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                return false;

            // 激光束等动态线条会把包围盒延伸到数十米外，不属于仪器本体。
            return !(renderer is LineRenderer)
                   && !(renderer is TrailRenderer)
                   && !(renderer is ParticleSystemRenderer);
        }

        private static void Encapsulate(ref Bounds result, ref bool hasBounds, Bounds value)
        {
            if (!hasBounds)
            {
                result = value;
                hasBounds = true;
                return;
            }

            result.Encapsulate(value);
        }
    }
}

using UnityEngine;

namespace ElectroOptics.Mobile
{
    public static class MobileRuntime
    {
        public static bool IsActive
        {
            get
            {
                if (Application.isMobilePlatform)
                {
                    return true;
                }

#if UNITY_EDITOR
                return UnityEditor.EditorUserBuildSettings.activeBuildTarget == UnityEditor.BuildTarget.Android;
#else
                return false;
#endif
            }
        }
    }
}

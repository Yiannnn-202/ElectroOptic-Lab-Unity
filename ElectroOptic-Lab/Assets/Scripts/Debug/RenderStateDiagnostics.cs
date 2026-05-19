using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.PostProcessing;

/// <summary>
/// 渲染状态诊断工具。挂载到 Scene2.The Lab 的 MainCamera 上，
/// 分别在直接打开和从 preview 跳转时观察 Console 输出，对比差异。
/// </summary>
public class RenderStateDiagnostics : MonoBehaviour
{
    private void Start()
    {
        // 延迟一帧确保所有初始化完成
        Invoke(nameof(DumpRenderState), 0.1f);
    }

    private void DumpRenderState()
    {
        var cam = GetComponent<Camera>();
        var ppLayer = GetComponent<PostProcessLayer>();

        Debug.Log("========== RenderState Diagnostics ==========");

        // 1. 相机
        Debug.Log($"[Camera] HDR={cam.allowHDR}, clearFlags={cam.clearFlags}, "
                  + $"bgColor={cam.backgroundColor}, depth={cam.depth}");

        // 2. PostProcessLayer
        if (ppLayer != null)
        {
            Debug.Log($"[PostProcessLayer] enabled={ppLayer.enabled}, "
                      + $"isActiveAndEnabled={ppLayer.isActiveAndEnabled}, "
                      + $"volumeTrigger={ppLayer.volumeTrigger?.name}, "
                      + $"volumeLayer={ppLayer.volumeLayer.value}, "
                      + $"antialiasingMode={ppLayer.antialiasingMode}, "
                      + $"fog.enabled={ppLayer.fog.enabled}");
        }
        else
        {
            Debug.Log("[PostProcessLayer] NULL!");
        }

        // 3. Global Volume
        var globalVolume = GameObject.Find("Global Volume");
        if (globalVolume != null)
        {
            var vol = globalVolume.GetComponent<PostProcessVolume>();
            Debug.Log($"[GlobalVolume] activeSelf={globalVolume.activeSelf}, "
                      + $"activeInHierarchy={globalVolume.activeInHierarchy}, "
                      + $"isGlobal={vol.isGlobal}, weight={vol.weight}, "
                      + $"blendDistance={vol.blendDistance}, "
                      + $"priority={vol.priority}, "
                      + $"profile={vol.sharedProfile?.name}");
            if (vol.sharedProfile != null)
            {
                foreach (var setting in vol.sharedProfile.settings)
                {
                    Debug.Log($"  [Profile Effect] {setting.GetType().Name}, active={setting.active}");
                    if (setting is Bloom bloom)
                    {
                        Debug.Log($"    [Bloom] enabled={bloom.enabled.value}, "
                                  + $"intensity={bloom.intensity.value}, "
                                  + $"threshold={bloom.threshold.value}, "
                                  + $"color={bloom.color.value}");
                    }
                }
            }
        }
        else
        {
            Debug.Log("[GlobalVolume] NOT FOUND!");
        }

        // 4. 主方向光
        var dirLight = RenderSettings.sun;
        if (dirLight != null)
        {
            Debug.Log($"[DirectionalLight] enabled={dirLight.enabled}, "
                      + $"intensity={dirLight.intensity}, "
                      + $"color={dirLight.color}, "
                      + $"shadows={dirLight.shadows}, "
                      + $"gameObject={dirLight.gameObject.name}");
        }
        else
        {
            Debug.Log("[DirectionalLight] RenderSettings.sun is NULL");
        }

        // 5. RenderSettings
        Debug.Log($"[RenderSettings] ambientMode={RenderSettings.ambientMode}, "
                  + $"ambientIntensity={RenderSettings.ambientIntensity}, "
                  + $"ambientLight={RenderSettings.ambientLight}, "
                  + $"ambientSkyColor={RenderSettings.ambientSkyColor}, "
                  + $"reflectionIntensity={RenderSettings.reflectionIntensity}, "
                  + $"fog={RenderSettings.fog}");

        // 6. QualitySettings
        Debug.Log($"[Quality] activeColorSpace={QualitySettings.activeColorSpace}, "
                  + $"pixelLightCount={QualitySettings.pixelLightCount}, "
                  + $"shadows={QualitySettings.shadows}, "
                  + $"shadowDistance={QualitySettings.shadowDistance}, "
                  + $"vSyncCount={QualitySettings.vSyncCount}");

        // 7. Area Lights 统计
        int totalAreaLights = 0;
        int activeAreaLights = 0;
        foreach (var light in FindObjectsOfType<Light>())
        {
            if (light.type == LightType.Area || light.type == LightType.Disc)
            {
                totalAreaLights++;
                if (light.enabled && light.gameObject.activeInHierarchy) activeAreaLights++;
            }
        }
        Debug.Log($"[AreaLights] total={totalAreaLights}, active={activeAreaLights}");

        // 8. Outline 组件统计
        int totalOutlines = 0;
        int enabledOutlines = 0;
        foreach (var outline in FindObjectsOfType<Outline>())
        {
            totalOutlines++;
            if (outline.enabled) enabledOutlines++;
        }
        Debug.Log($"[Outlines] total={totalOutlines}, enabled={enabledOutlines}");

        Debug.Log("=============================================");
    }
}

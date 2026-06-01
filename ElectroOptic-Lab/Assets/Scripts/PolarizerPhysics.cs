using UnityEngine;

/// <summary>
/// Ideal linear polarizer for the direct red-dot ray chain.
/// </summary>
public class PolarizerPhysics : MonoBehaviour, IOpticalReceiver
{
    private const float VisibleIntensityThreshold = 0.001f;

    private LineRenderer lineRenderer;
    private bool gotLight;

    private void Start()
    {
        lineRenderer = gameObject.AddComponent<LineRenderer>();
        lineRenderer.positionCount = 2;
        lineRenderer.startWidth = 0.02f;
        lineRenderer.endWidth = 0.02f;
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startColor = new Color(1f, 0f, 0f, 0.5f);
        lineRenderer.endColor = new Color(1f, 0f, 0f, 0.5f);
        lineRenderer.enabled = false;
    }

    private void Update()
    {
        if (!gotLight)
        {
            lineRenderer.enabled = false;
        }

        gotLight = false;
    }

    public void ReceiveLight(LightData inLight, Vector3 hitPoint, Vector3 direction)
    {
        gotLight = true;

        float axis = transform.eulerAngles.z;
        float axisRad = axis * Mathf.Deg2Rad;
        float cos2Axis = Mathf.Cos(2f * axisRad);
        float sin2Axis = Mathf.Sin(2f * axisRad);
        float finalIntensity = 0.5f * (inLight.intensity + inLight.stokesQ * cos2Axis + inLight.stokesU * sin2Axis);
        finalIntensity = Mathf.Max(0f, finalIntensity);
        Debug.Log($"[Polarizer:{gameObject.name}] axis={axis:F1}° inI={inLight.intensity:F4}(S1={inLight.stokesQ:F4},S2={inLight.stokesU:F4},S3={inLight.stokesV:F4}) outI={finalIntensity:F4}");

        bool drawLine = finalIntensity > VisibleIntensityThreshold;
        lineRenderer.enabled = drawLine;
        if (drawLine)
        {
            float alpha = Mathf.Clamp01(finalIntensity);
            lineRenderer.startColor = new Color(1f, 0f, 0f, alpha);
            lineRenderer.endColor = new Color(1f, 0f, 0f, alpha);
        }

        Vector3 rayDirection = direction.normalized;
        Vector3 start = hitPoint + rayDirection * 0.05f;
        Vector3 end = start + rayDirection * 50f;

        if (Physics.Raycast(start, rayDirection, out RaycastHit hit, 50f))
        {
            end = hit.point;
            IOpticalReceiver next = hit.collider.GetComponent<IOpticalReceiver>();
            if (next == null) next = hit.collider.GetComponentInParent<IOpticalReceiver>();

            if (next != null)
            {
                LightData outLight = LightData.FromLinear(finalIntensity, axis);
                next.ReceiveLight(outLight, hit.point, rayDirection);
            }
        }

        if (lineRenderer.enabled)
        {
            lineRenderer.SetPosition(0, start);
            lineRenderer.SetPosition(1, end);
        }
    }
}

using UnityEngine;

// Light payload used by the simplified direct-beam ray chain.
[System.Serializable]
public struct LightData
{
    public float intensity;         // S0: total intensity
    public float polarizationAngle; // Legacy linear-polarization angle in degrees
    public float dop;               // Degree of polarization
    public float stokesQ;           // S1: 0/90 degree linear component
    public float stokesU;           // S2: 45/135 degree linear component
    public float stokesV;           // S3: circular component for elliptical polarization

    public LightData(float i, float angle, float d)
    {
        intensity = Mathf.Max(0f, i);
        polarizationAngle = Mathf.Repeat(angle, 180f);
        dop = Mathf.Clamp01(d);

        float angleRad = polarizationAngle * Mathf.Deg2Rad;
        float polarizedIntensity = intensity * dop;
        stokesQ = polarizedIntensity * Mathf.Cos(2f * angleRad);
        stokesU = polarizedIntensity * Mathf.Sin(2f * angleRad);
        stokesV = 0f;
    }

    public static LightData FromLinear(float i, float angle)
    {
        return new LightData(i, angle, i > 0f ? 1f : 0f);
    }

    public static LightData FromStokes(float i, float q, float u, float v)
    {
        LightData light = new LightData();
        light.intensity = Mathf.Max(0f, i);

        if (light.intensity <= 0f)
        {
            light.polarizationAngle = 0f;
            light.dop = 0f;
            light.stokesQ = 0f;
            light.stokesU = 0f;
            light.stokesV = 0f;
            return light;
        }

        float magnitude = Mathf.Sqrt(q * q + u * u + v * v);
        float maxMagnitude = light.intensity;
        if (magnitude > maxMagnitude && magnitude > 0f)
        {
            float scale = maxMagnitude / magnitude;
            q *= scale;
            u *= scale;
            v *= scale;
            magnitude = maxMagnitude;
        }

        light.stokesQ = q;
        light.stokesU = u;
        light.stokesV = v;
        light.dop = Mathf.Clamp01(magnitude / light.intensity);
        light.polarizationAngle = Mathf.Repeat(0.5f * Mathf.Atan2(u, q) * Mathf.Rad2Deg, 180f);
        return light;
    }
}

// Receivers in the simplified direct-beam ray chain.
public interface IOpticalReceiver
{
    void ReceiveLight(LightData lightIn, Vector3 hitPoint, Vector3 direction);
}

using UnityEngine;

[System.Serializable]
public struct PowerReadoutParameters
{
    public float darkPower;
    public float powerScale;
    public float leakage;
    public float visibility;
    public float phaseOffset;
    public float halfWaveVoltage;
    public float beamFocus;

    public static PowerReadoutParameters Default => new PowerReadoutParameters
    {
        darkPower = 0.2f,
        powerScale = 2300f,
        leakage = 0.49f,
        visibility = 0.51f,
        phaseOffset = 0f,
        halfWaveVoltage = 150f,
        beamFocus = 20f
    };
}

[System.Serializable]
public struct PowerNoiseParameters
{
    public bool enabled;
    public float amplitude;
    public float frequency;

    public static PowerNoiseParameters Default => new PowerNoiseParameters
    {
        enabled = true,
        amplitude = 0.2f,
        frequency = 5f
    };
}

public struct PowerReadoutResult
{
    public float stablePower;
    public float displayPower;
    public float transmission;
    public float alignmentEfficiency;
}

public static class PowerReadoutCalculator
{
    public static PowerReadoutResult Calculate(
        float voltage,
        float receiverDevX,
        float receiverDevY,
        PowerReadoutParameters parameters,
        PowerNoiseParameters noise,
        float time)
    {
        float transmission = CalculateTransmission(
            voltage,
            parameters.halfWaveVoltage,
            parameters.leakage,
            parameters.visibility,
            parameters.phaseOffset);
        float alignmentEfficiency = CalculateAlignmentEfficiency(receiverDevX, receiverDevY, parameters.beamFocus);
        float stablePower = CalculateStablePower(parameters.darkPower, parameters.powerScale, transmission, alignmentEfficiency);
        float displayPower = AddDisplayNoise(stablePower, time, noise);

        return new PowerReadoutResult
        {
            stablePower = stablePower,
            displayPower = displayPower,
            transmission = transmission,
            alignmentEfficiency = alignmentEfficiency
        };
    }

    public static float CalculateStablePower(
        float voltage,
        float receiverDevX,
        float receiverDevY,
        PowerReadoutParameters parameters)
    {
        float transmission = CalculateTransmission(
            voltage,
            parameters.halfWaveVoltage,
            parameters.leakage,
            parameters.visibility,
            parameters.phaseOffset);
        float alignmentEfficiency = CalculateAlignmentEfficiency(receiverDevX, receiverDevY, parameters.beamFocus);
        return CalculateStablePower(parameters.darkPower, parameters.powerScale, transmission, alignmentEfficiency);
    }

    public static float CalculateTransmission(
        float voltage,
        float halfWaveVoltage,
        float leakage,
        float visibility,
        float phaseOffset)
    {
        if (!IsFinite(voltage) || !IsFinite(halfWaveVoltage) || !IsFinite(leakage)
            || !IsFinite(visibility) || !IsFinite(phaseOffset) || halfWaveVoltage <= 0f)
        {
            return 0f;
        }

        float phase = phaseOffset + Mathf.PI * voltage / (2f * halfWaveVoltage);
        float sin = Mathf.Sin(phase);
        float transmission = Mathf.Max(0f, leakage) + Mathf.Max(0f, visibility) * sin * sin;
        return Mathf.Clamp01(transmission);
    }

    public static float CalculateAlignmentEfficiency(float receiverDevX, float receiverDevY, float beamFocus)
    {
        if (!IsFinite(receiverDevX) || !IsFinite(receiverDevY))
        {
            return 0f;
        }

        float safeBeamFocus = IsFinite(beamFocus) ? Mathf.Max(0f, beamFocus) : 0f;
        float rSquared = receiverDevX * receiverDevX + receiverDevY * receiverDevY;
        return Mathf.Clamp01(Mathf.Exp(-safeBeamFocus * rSquared));
    }

    public static float AddDisplayNoise(float stablePower, float time, PowerNoiseParameters noise)
    {
        float safeStablePower = IsFinite(stablePower) ? Mathf.Max(0f, stablePower) : 0f;
        if (!noise.enabled || noise.amplitude <= 0f)
        {
            return safeStablePower;
        }

        float frequency = noise.frequency > 0f && IsFinite(noise.frequency) ? noise.frequency : 1f;
        float noiseValue = (Mathf.PerlinNoise(time * frequency, 0f) - 0.5f) * 2f * noise.amplitude;
        return Mathf.Max(0f, safeStablePower + noiseValue);
    }

    private static float CalculateStablePower(
        float darkPower,
        float powerScale,
        float transmission,
        float alignmentEfficiency)
    {
        float safeDarkPower = IsFinite(darkPower) ? Mathf.Max(0f, darkPower) : 0f;
        float safePowerScale = IsFinite(powerScale) ? Mathf.Max(0f, powerScale) : 0f;
        float stablePower = safeDarkPower + alignmentEfficiency * safePowerScale * transmission;
        return Mathf.Max(0f, stablePower);
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}

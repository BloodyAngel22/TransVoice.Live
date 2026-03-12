namespace TransVoice.Live.Core;

public class AudioProcessor
{
    private float _prevRaw = 0;
    private float _prevFiltered = 0;
    private const float FilterAlpha = 0.92f;

    private float _currentGain = 1.0f;
    private float _targetRMS = 0.08f;
    private float _maxGain = 12.0f;
    private float _noiseRMS = 0.001f;

    private const float GainUpStep = 0.2f;
    private const float GainDownStep = 0.8f;
    private const float NoiseGateThresholdMultiplier = 1.3f;

    public float CurrentGain => _currentGain;

    public void SetupAGC(float noiseRMS, float targetRMS, float maxGain)
    {
        _noiseRMS = noiseRMS;
        _targetRMS = targetRMS;
        _maxGain = maxGain;
        _currentGain = 1.0f;
    }

    public void UpdateNoiseRMS(float noiseRMS)
    {
        // Smoothly update noise floor to avoid sudden jumps in gate
        _noiseRMS = _noiseRMS * 0.8f + noiseRMS * 0.2f;
    }

    public (float rawRms, float processedRms) ProcessChunk(
        byte[] byteBuffer,
        float[] currentChunk,
        int chunkSize,
        bool isSpeaking
    )
    {
        double sumSq = 0;

        for (int i = 0; i < chunkSize; i++)
        {
            short sample = BitConverter.ToInt16(byteBuffer, i * 2);
            float rawF = sample / 32768.0f;

            float filtered = rawF - _prevRaw + FilterAlpha * _prevFiltered;
            _prevRaw = rawF;
            _prevFiltered = filtered;

            currentChunk[i] = filtered;
            sumSq += filtered * filtered;
        }

        float rawRms = (float)Math.Sqrt(sumSq / chunkSize);

        // Noise Gate: if signal is below threshold, zero it out to prevent Whisper hallucinations
        bool isSilencedByGate = !isSpeaking && rawRms < (_noiseRMS * NoiseGateThresholdMultiplier);

        if (isSpeaking && rawRms > 0.0001f)
        {
            float targetGain = _targetRMS / rawRms;
            if (targetGain > _currentGain)
                _currentGain = Math.Min(_maxGain, _currentGain + GainUpStep);
            else if (targetGain < _currentGain)
                _currentGain = Math.Max(1.0f, _currentGain - GainDownStep);
        }
        else if (!isSpeaking)
        {
            // Limit gain growth during silence to prevent noise floor amplification
            float idleTargetGain = 3.0f; 
            if (_currentGain > idleTargetGain)
                _currentGain -= 0.5f;
            else if (_currentGain < idleTargetGain)
                _currentGain += 0.1f;
        }

        for (int i = 0; i < chunkSize; i++)
        {
            float amplified = isSilencedByGate ? 0 : currentChunk[i] * _currentGain;

            if (amplified > 0.85f)
                amplified = 0.85f + (amplified - 0.85f) * 0.1f;
            if (amplified < -0.85f)
                amplified = -0.85f + (amplified + 0.85f) * 0.1f;

            currentChunk[i] = amplified;
        }

        return (rawRms, isSilencedByGate ? 0 : rawRms * _currentGain);
    }

    public void Normalize(float[] samples)
    {
        float max = 0f;
        foreach (var s in samples)
        {
            float abs = Math.Abs(s);
            if (abs > max)
                max = abs;
        }
        if (max < 0.01f)
            return;
        float multiplier = 0.85f / max;
        if (multiplier > 3.0f)
            multiplier = 3.0f;
        for (int i = 0; i < samples.Length; i++)
            samples[i] *= multiplier;
    }
}

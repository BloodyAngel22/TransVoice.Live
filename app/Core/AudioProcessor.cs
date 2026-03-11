namespace TransVoice.Live.Core;

public class AudioProcessor
{
    private float _prevRaw = 0;
    private float _prevFiltered = 0;
    private const float FilterAlpha = 0.92f;

    private float _currentGain = 1.0f;
    private float _targetRMS = 0.12f;
    private float _maxGain = 12.0f;
    private float _noiseRMS = 0.001f;

    private const float GainUpStep = 0.2f;
    private const float GainDownStep = 0.8f;

    public float CurrentGain => _currentGain;

    public void SetupAGC(float noiseRMS, float targetRMS, float maxGain)
    {
        _noiseRMS = noiseRMS;
        _targetRMS = targetRMS;
        _maxGain = maxGain;
        _currentGain = 1.0f;
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
            if (_currentGain > 2.0f)
                _currentGain -= 0.5f;
            else if (_currentGain < 2.0f)
                _currentGain += 0.2f;
        }

        for (int i = 0; i < chunkSize; i++)
        {
            float amplified = currentChunk[i] * _currentGain;

            if (amplified > 0.85f)
                amplified = 0.85f + (amplified - 0.85f) * 0.1f;
            if (amplified < -0.85f)
                amplified = -0.85f + (amplified + 0.85f) * 0.1f;

            currentChunk[i] = amplified;
        }

        return (rawRms, rawRms * _currentGain);
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

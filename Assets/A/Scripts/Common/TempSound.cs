using UnityEngine;

namespace MilTrainSimVR.A.Common
{
    // 임시 소리 — 실제 소리 자산이 오기 전까지 코드로 만든다. 분해·결합 피드백과 총이 같이 쓴다.
    public static class TempSound
    {
        const int k_Rate = 44100;

        public static AudioClip Build(string name, float seconds, System.Func<float, float> sample)
        {
            int n = Mathf.CeilToInt(seconds * k_Rate);
            var data = new float[n];
            for (int i = 0; i < n; i++)
                data[i] = sample(i / (float)k_Rate);
            var clip = AudioClip.Create(name, n, 1, k_Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // 짧은 금속음 (덜컥·철컥·탁) — 음 하나와 잡음을 섞어 빠르게 줄어든다.
        // toneMix: 음의 비율 (나머지는 잡음), decay: 클수록 짧게 끊긴다
        public static AudioClip Clack(string name, float seconds, float toneHz, float decay, float toneMix, float volume, int seed)
        {
            var rng = new System.Random(seed);
            return Build(name, seconds, t =>
            {
                float env = Mathf.Exp(-t * decay);
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                return env * (toneMix * Mathf.Sin(2f * Mathf.PI * toneHz * t) + (1f - toneMix) * noise) * volume;
            });
        }
    }
}

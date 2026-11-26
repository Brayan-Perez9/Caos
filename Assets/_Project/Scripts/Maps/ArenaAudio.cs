using UnityEngine;

namespace Caos.Maps
{
    /// <summary>
    /// Sonido generado en runtime: no hay clips externos. Cada efecto se sintetiza
    /// con una mezcla de tono y ruido y se reproduce con AudioSource.PlayClipAtPoint.
    /// </summary>
    public static class ArenaAudio
    {
        public enum Sound
        {
            Shoot,
            Hit,
            Hurt,
            Death,
            Upgrade,
            Boss,
            Victory
        }

        const int SampleRate = 22050;

        static AudioClip _shoot;
        static AudioClip _hit;
        static AudioClip _hurt;
        static AudioClip _death;
        static AudioClip _upgrade;
        static AudioClip _boss;
        static AudioClip _victory;

        public static void Play(Sound sound, Vector3 position, float volume = 0.6f)
        {
            AudioClip clip = Clip(sound);
            if (clip == null) return;

            // 2D para que se oiga igual aunque la cámara esté lejos del golpe.
            var holder = new GameObject("ArenaSound");
            holder.transform.position = position;
            var source = holder.AddComponent<AudioSource>();
            source.clip = clip;
            source.volume = Mathf.Clamp01(volume);
            source.spatialBlend = 0f;
            source.Play();
            Object.Destroy(holder, clip.length + 0.05f);
        }

        static AudioClip Clip(Sound sound)
        {
            switch (sound)
            {
                case Sound.Shoot:
                    if (_shoot == null) _shoot = Synthesize("ArenaShoot", 0.10f, 850f, 180f, 0.55f, 2.2f);
                    return _shoot;
                case Sound.Hit:
                    if (_hit == null) _hit = Synthesize("ArenaHit", 0.09f, 220f, 70f, 0.75f, 2.8f);
                    return _hit;
                case Sound.Hurt:
                    if (_hurt == null) _hurt = Synthesize("ArenaHurt", 0.20f, 150f, 55f, 0.45f, 2.4f);
                    return _hurt;
                case Sound.Death:
                    if (_death == null) _death = Synthesize("ArenaDeath", 0.28f, 320f, 60f, 0.65f, 1.8f);
                    return _death;
                case Sound.Upgrade:
                    if (_upgrade == null) _upgrade = Synthesize("ArenaUpgrade", 0.34f, 440f, 990f, 0.05f, 0.8f);
                    return _upgrade;
                case Sound.Boss:
                    if (_boss == null) _boss = Synthesize("ArenaBoss", 0.55f, 95f, 42f, 0.35f, 1.3f);
                    return _boss;
                case Sound.Victory:
                    if (_victory == null) _victory = Synthesize("ArenaVictory", 0.65f, 520f, 1180f, 0.03f, 0.6f);
                    return _victory;
                default:
                    return null;
            }
        }

        static AudioClip Synthesize(string name, float duration, float startFrequency,
            float endFrequency, float noiseAmount, float decay)
        {
            int count = Mathf.Max(1, Mathf.RoundToInt(duration * SampleRate));
            float[] data = new float[count];
            float phase = 0f;

            for (int i = 0; i < count; i++)
            {
                float t = i / (float)count;
                float frequency = Mathf.Lerp(startFrequency, endFrequency, t);
                phase += 2f * Mathf.PI * frequency / SampleRate;

                float envelope = Mathf.Pow(Mathf.Max(0f, 1f - t), decay);
                float tone = Mathf.Sin(phase);
                float noise = Random.Range(-1f, 1f);
                data[i] = Mathf.Clamp((tone * (1f - noiseAmount) + noise * noiseAmount) * envelope * 0.6f, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create(name, count, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}

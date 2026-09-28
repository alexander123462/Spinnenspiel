using UnityEngine;

namespace Spinnenspiel
{
    /// <summary>
    /// Erzeugt Klänge zur Laufzeit aus Rauschen und Filtern (keine Audiodateien nötig):
    /// Fußtritte je Oberfläche, Windrauschen, Bachplätschern, Vogelzwitschern.
    /// Alle Clips sind deterministisch (fester Seed) und werden einmal erzeugt und gecacht.
    /// </summary>
    public static class ProceduralAudio
    {
        public const int SampleRate = 44100;

        private static readonly AudioClip[][] FootstepCache = new AudioClip[6][];
        private static AudioClip _wind;
        private static AudioClip _stream;
        private static AudioClip[] _birds;

        /// <summary>Kurze Tritt-Geräusche (mehrere Varianten) für eine Oberfläche.</summary>
        public static AudioClip[] Footsteps(SurfaceType surface)
        {
            int index = (int)surface;
            if (FootstepCache[index] != null)
            {
                return FootstepCache[index];
            }

            AudioClip[] clips = new AudioClip[4];
            for (int v = 0; v < clips.Length; v++)
            {
                clips[v] = CreateFootstep(surface, v);
            }

            FootstepCache[index] = clips;
            return clips;
        }

        private static AudioClip CreateFootstep(SurfaceType surface, int variant)
        {
            // Parameter je Oberfläche: Dauer, Tiefpass (0..1, klein = dumpf), Klick-Anteil, Rascheln
            float duration = 0.045f, lowpass = 0.35f, click = 0.4f, rustle = 0f, drip = 0f;
            switch (surface)
            {
                case SurfaceType.Grass: duration = 0.07f; lowpass = 0.55f; click = 0.1f; rustle = 0.8f; break;
                case SurfaceType.Rock: duration = 0.03f; lowpass = 0.85f; click = 0.9f; break;
                case SurfaceType.Wood: duration = 0.05f; lowpass = 0.4f; click = 0.7f; break;
                case SurfaceType.Water: duration = 0.09f; lowpass = 0.3f; click = 0.05f; drip = 1f; break;
                case SurfaceType.Mud: duration = 0.08f; lowpass = 0.18f; click = 0.1f; break;
            }

            int length = Mathf.CeilToInt(duration * SampleRate);
            float[] data = new float[length];
            System.Random random = new System.Random(1000 + (int)surface * 17 + variant);
            float filtered = 0f;
            float dripFrequency = 900f + variant * 180f;
            for (int i = 0; i < length; i++)
            {
                float t = i / (float)SampleRate;
                float envelope = Mathf.Exp(-t / (duration * 0.25f));
                float noise = (float)random.NextDouble() * 2f - 1f;
                filtered += (noise - filtered) * lowpass;
                float sample = filtered * (1f - click) + noise * click * Mathf.Exp(-t / 0.003f);
                if (rustle > 0f)
                {
                    sample += noise * rustle * 0.5f * Mathf.Abs(Mathf.Sin(t * 180f + variant));
                }

                if (drip > 0f)
                {
                    float chirp = dripFrequency * (1f + t * 12f);
                    sample += Mathf.Sin(2f * Mathf.PI * chirp * t) * drip * 0.6f * Mathf.Exp(-t / 0.02f);
                }

                data[i] = sample * envelope * 0.6f;
            }

            return Create("Step_" + surface + "_" + variant, data);
        }

        /// <summary>Nahtlose Schleife aus gefiltertem Rauschen mit langsamer Modulation (Wind).</summary>
        public static AudioClip WindLoop()
        {
            if (_wind != null)
            {
                return _wind;
            }

            const float seconds = 8f;
            int length = Mathf.CeilToInt(seconds * SampleRate);
            float[] data = new float[length];
            System.Random random = new System.Random(77);
            float low = 0f, band = 0f;
            for (int i = 0; i < length; i++)
            {
                float t = i / (float)length;
                float noise = (float)random.NextDouble() * 2f - 1f;
                low += (noise - low) * 0.02f;
                band += (noise - band) * 0.12f;
                float swell = 0.6f + 0.4f * Mathf.Sin(t * Mathf.PI * 2f * 2f) * Mathf.Sin(t * Mathf.PI * 2f * 3f);
                data[i] = (low * 3f + (band - low) * 0.6f) * swell * 0.5f;
            }

            data = Crossfade(data, SampleRate / 2);
            _wind = Create("Ambience_Wind", data);
            return _wind;
        }

        /// <summary>Plätschern: viele kurze, zufällige Tropfen-Chirps über gefiltertem Rauschen.</summary>
        public static AudioClip StreamLoop()
        {
            if (_stream != null)
            {
                return _stream;
            }

            const float seconds = 6f;
            int length = Mathf.CeilToInt(seconds * SampleRate);
            float[] data = new float[length];
            System.Random random = new System.Random(91);
            float band = 0f, low = 0f;
            for (int i = 0; i < length; i++)
            {
                float noise = (float)random.NextDouble() * 2f - 1f;
                low += (noise - low) * 0.05f;
                band += (noise - band) * 0.35f;
                data[i] = (band - low) * 0.25f;
            }

            int drops = (int)(seconds * 70f);
            for (int d = 0; d < drops; d++)
            {
                int start = random.Next(0, length - 3000);
                float frequency = 500f + (float)random.NextDouble() * 1400f;
                float amplitude = 0.05f + (float)random.NextDouble() * 0.12f;
                for (int i = 0; i < 2500; i++)
                {
                    float t = i / (float)SampleRate;
                    data[start + i] += Mathf.Sin(2f * Mathf.PI * frequency * (1f + t * 18f) * t) * amplitude * Mathf.Exp(-t / 0.012f);
                }
            }

            data = Crossfade(data, SampleRate / 2);
            _stream = Create("Ambience_Stream", data);
            return _stream;
        }

        /// <summary>Kurze Vogelrufe (Frequenzbögen mit Vibrato).</summary>
        public static AudioClip[] BirdCalls()
        {
            if (_birds != null)
            {
                return _birds;
            }

            _birds = new AudioClip[4];
            for (int b = 0; b < _birds.Length; b++)
            {
                System.Random random = new System.Random(300 + b);
                int notes = 2 + random.Next(0, 4);
                float noteLength = 0.06f + (float)random.NextDouble() * 0.08f;
                float gap = 0.03f + (float)random.NextDouble() * 0.05f;
                int length = Mathf.CeilToInt((noteLength + gap) * notes * SampleRate) + 1;
                float[] data = new float[length];
                float baseFrequency = 2200f + (float)random.NextDouble() * 1800f;
                float phase = 0f;
                for (int n = 0; n < notes; n++)
                {
                    int start = Mathf.FloorToInt(n * (noteLength + gap) * SampleRate);
                    float sweep = ((float)random.NextDouble() - 0.4f) * 1500f;
                    int count = Mathf.FloorToInt(noteLength * SampleRate);
                    for (int i = 0; i < count && start + i < length; i++)
                    {
                        float t = i / (float)count;
                        float frequency = baseFrequency + sweep * t + Mathf.Sin(t * 60f) * 120f;
                        phase += 2f * Mathf.PI * frequency / SampleRate;
                        float envelope = Mathf.Sin(t * Mathf.PI);
                        data[start + i] = Mathf.Sin(phase) * envelope * 0.25f;
                    }
                }

                _birds[b] = Create("Bird_" + b, data);
            }

            return _birds;
        }

        private static float[] Crossfade(float[] data, int samples)
        {
            // Ende weich in den Anfang überblenden, damit die Schleife nahtlos ist.
            int length = data.Length;
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)samples;
                data[i] = data[i] * t + data[length - samples + i] * (1f - t);
            }

            System.Array.Resize(ref data, length - samples);
            return data;
        }

        private static AudioClip Create(string name, float[] data)
        {
            AudioClip clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}

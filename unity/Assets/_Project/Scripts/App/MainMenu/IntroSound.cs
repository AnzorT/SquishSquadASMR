// The samples of intro.js's sounds (IntroScript.Voice: WebAudio oscillators with exponential gain ramps), with no
// Unity in it: IntroSynth.cs makes AudioClips of them; tools/unity-port/intro-check compares them with what Chrome's
// WebAudio renders for the same voices (sound-check.mjs).
using System;

namespace PlushCrush.App.MainMenu
{
    public static class IntroSound
    {
        public const int Rate = 44100;

        /// <summary>The voices mixed, as WebAudio plays them (no limiter: the volumes are small).</summary>
        public static float[] Render(IntroScript.Voice[] voices, int n)
        {
            var o = new float[n];
            foreach (var v in voices)
            {
                int s0 = (int)Math.Ceiling(v.T0 * Rate), s1 = Math.Min(n, (int)Math.Ceiling((v.T0 + v.Dur + 0.05) * Rate));   // (start(): the first frame at or after it)
                // (Chrome: in the 128-frame render quantum an oscillator starts in, it reads its frequency from the
                // quantum's start, not its own: the param's default, 440 Hz, before the setValueAtTime. The sound
                // starts on a quantum (the context's currentTime), so this is the same in the app.)
                int q0 = s0 - s0 % 128, q1 = q0 + 128;
                double Freq(int frame) => frame < s0 ? 440 : v.F1 == v.F0 ? v.F0 : v.F0 * Math.Pow(v.F1 / v.F0, Math.Min(1, (frame - s0) / (double)Rate / v.Dur));
                double phase = 0;
                for (int i = s0; i < s1; i++)
                {
                    double t = (i - s0) / (double)Rate;
                    double f = Freq(i < q1 ? q0 + (i - s0) : i);
                    o[i] += (float)(Gain(v, t) * Wave(v.Type, phase, f));
                    phase += f / Rate; phase -= Math.Floor(phase);
                }
            }
            return o;
        }

        // the gain: setValueAtTime(0.0001), exponentialRamp to vol at Attack, exponentialRamp to 0.0001 at Dur (held after)
        static double Gain(IntroScript.Voice v, double t)
        {
            const double lo = 0.0001;
            if (t <= 0) return lo;
            if (t < v.Attack) return lo * Math.Pow(v.Vol / lo, t / v.Attack);
            if (t < v.Dur) return v.Vol * Math.Pow(lo / v.Vol, (t - v.Attack) / (v.Dur - v.Attack));
            return lo;
        }

        // WebAudio's triangle is a band-limited periodic wave (its Fourier series up to half the sample rate), peak 1
        static double Wave(IntroScript.Wave type, double phase, double f)
        {
            double a = 2 * Math.PI * phase;
            if (type == IntroScript.Wave.Sine) return Math.Sin(a);
            int top = (int)Math.Floor(Rate / 2.0 / Math.Max(1, f));
            double s = 0;
            for (int k = 1; k <= top; k += 2) s += ((k - 1) / 2 % 2 == 0 ? 1 : -1) * Math.Sin(k * a) / (k * k);
            return s * 8 / (Math.PI * Math.PI);
        }
    }
}

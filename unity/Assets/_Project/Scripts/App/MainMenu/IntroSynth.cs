// intro.js's tiny synth (no sound files) as AudioClips made here: each sound is the WebAudio graph intro.js builds,
// worked out sample by sample.
//   tone(f, t0, dur, vol, type): an oscillator (sine, or WebAudio's band-limited triangle) at f; its gain set to 0.0001
//   at t0, ramped exponentially to vol at t0 + 12 ms and to 0.0001 at t0 + dur; stopped at t0 + dur + 50 ms.
//   sweep(f0, f1, dur, vol, t0): a sine whose frequency ramps exponentially from f0 to f1 over dur; gain up to vol in
//   20 ms and down to 0.0001 at dur.
//   chime(f, t0): tone(f, 1.4 s, .07) + tone(2f, .8 s, .025, triangle) + tone(3.01f, .4 s, .012).
// A sound (IntroScript.Sounds: the voices played at one moment) becomes one clip, cached, played on one AudioSource.
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace PlushCrush.App.MainMenu
{
    public static class IntroSynth
    {
        const int Rate = IntroSound.Rate;
        static readonly Dictionary<string, AudioClip> Cache = new Dictionary<string, AudioClip>();
        static AudioSource _source;

        public static bool Muted;

        public static void Play(IntroScript.Voice[] voices)
        {
            if (Muted || voices == null || voices.Length == 0) return;
            var clip = Clip(voices);
            if (_source == null)
            {
                var go = new GameObject("IntroSynth") { hideFlags = HideFlags.HideAndDontSave };
                UnityEngine.Object.DontDestroyOnLoad(go);
                _source = go.AddComponent<AudioSource>();
                _source.playOnAwake = false; _source.spatialBlend = 0f;
            }
            _source.PlayOneShot(clip, 1f);
        }

        public static AudioClip Clip(IntroScript.Voice[] voices)
        {
            var key = new StringBuilder();
            foreach (var v in voices) key.Append(v.Type).Append(v.F0).Append('>').Append(v.F1).Append('@').Append(v.T0).Append('/').Append(v.Dur).Append('*').Append(v.Vol).Append(';');
            if (Cache.TryGetValue(key.ToString(), out var c) && c != null) return c;
            double end = 0; foreach (var v in voices) end = Math.Max(end, v.T0 + v.Dur + 0.05);
            var data = IntroSound.Render(voices, (int)Math.Ceiling(end * Rate));
            c = AudioClip.Create("intro-" + Cache.Count, data.Length, 1, Rate, false);
            c.SetData(data, 0);
            Cache[key.ToString()] = c;
            return c;
        }
    }
}

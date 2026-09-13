#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace LordOfTheRealms.EditorTools
{
    // Generira placeholder audio fajlove u Assets/Resources/Audio.
    // Svaki je jednostavan sintetizirani ton; zamijeni ih pravim .wav/.mp3
    // fajlovima ISTOG IMENA i igra ih odmah koristi.
    public static class AudioPlaceholderGenerator
    {
        private const string Dir = "Assets/Resources/Audio";
        private const int Rate = 44100;

        [MenuItem("Lord of the Realms/Generate Audio Placeholders")]
        public static void Generate()
        {
            Directory.CreateDirectory(Dir);

            // glazba po rasi (kratke petlje, razlicit ugodaj)
            Music("Music_MainMenu", new float[] { 220f, 277f, 330f }, 6f);
            Music("Music_Orcs",     new float[] { 110f, 147f },       5f); // duboko, ratno
            Music("Music_Elves",    new float[] { 330f, 415f, 494f }, 6f); // svijetlo
            Music("Music_Humans",   new float[] { 262f, 330f, 392f }, 6f); // "fanfare"
            Music("Music_Demons",   new float[] { 92f, 123f },        5f); // mracno

            // sound effecti
            Sfx("Sfx_Click",  880f, 0.06f);
            Sfx("Sfx_Draw",   520f, 0.12f);
            Sfx("Sfx_Attack", 180f, 0.15f);
            Sfx("Sfx_Death",  120f, 0.30f);
            Sfx("Sfx_Hover", 900f, 0.05f); // kratki tihi tick za hover

            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Lord of the Realms",
                "Audio placeholders generated.", "OK");
        }

        // tiha petlja od par sinusa s laganim tremolom
        private static void Music(string name, float[] freqs, float seconds)
        {
            int n = (int)(Rate * seconds);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float s = 0f;
                foreach (var f in freqs)
                    s += Mathf.Sin(2f * Mathf.PI * f * t);
                s /= freqs.Length;
                float trem = 0.7f + 0.3f * Mathf.Sin(2f * Mathf.PI * 0.5f * t);
                // fade na rubovima da petlja ne pucketa
                float edge = Mathf.Min(1f, Mathf.Min(i, n - i) / (Rate * 0.25f));
                data[i] = s * trem * edge * 0.25f;
            }
            WriteWav(name, data);
        }

        // kratki blip s brzim padom
        private static void Sfx(string name, float freq, float seconds)
        {
            int n = (int)(Rate * seconds);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float env = 1f - (i / (float)n);
                data[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * env * env * 0.5f;
            }
            WriteWav(name, data);
        }

        // zapis mono 16-bit WAV-a
        private static void WriteWav(string name, float[] samples)
        {
            // Ako za ovo ime vec postoji prava datoteka (mp3, ogg, rucno ubacen wav),
            // ne diramo je. Dva fajla istog imena u Resources znace da
            // Resources.Load bira nasumicno koji ce ucitati.
            if (RealClipExists(name))
            {
                Debug.Log($"[Audio] {name} vec postoji kao prava datoteka, preskacem placeholder.");
                return;
            }

            string path = $"{Dir}/{name}.wav";
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);
            int byteCount = samples.Length * 2;
            w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            w.Write(36 + byteCount);
            w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            w.Write(16); w.Write((short)1); w.Write((short)1);
            w.Write(Rate); w.Write(Rate * 2); w.Write((short)2); w.Write((short)16);
            w.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            w.Write(byteCount);
            foreach (var s in samples)
                w.Write((short)(Mathf.Clamp(s, -1f, 1f) * short.MaxValue));
            w.Flush(); // bez flusha ToArray moze uhvatiti krnji buffer
            File.WriteAllBytes(path, ms.ToArray());
        }

        // Postoji li za ovo ime prava datoteka? Generator uvijek pise .wav, pa se
        // rucno ubacena glazba prepoznaje po drugoj ekstenziji.
        private static bool RealClipExists(string name)
        {
            if (!Directory.Exists(Dir)) return false;
            foreach (var ext in new[] { ".mp3", ".ogg", ".aiff", ".aif" })
                if (File.Exists($"{Dir}/{name}{ext}")) return true;
            return false;
        }
    }
}
#endif

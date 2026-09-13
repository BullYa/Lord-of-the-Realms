using System.Collections.Generic;
using UnityEngine;

namespace LordOfTheRealms
{
    // Glazba + zvukovi. Klipovi se ucitavaju iz Resources/Audio po imenu, pa se
    // placeholder fajlovi kasnije zamijene pravima (isto ime = radi odmah).
    // Imena: Music_MainMenu, Music_Orcs, Music_Elves, Music_Humans, Music_Demons,
    //        Sfx_Click, Sfx_Hover, Sfx_Attack, Sfx_Death, Sfx_Draw
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        private const string VolumeKey = "master_volume";
        private float _volume = 1f;
        public float Volume => _volume;

        private AudioSource _music;
        private AudioSource _sfx;
        private string _currentMusic = "";
        private readonly Dictionary<string, AudioClip> _cache = new();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            _music = gameObject.AddComponent<AudioSource>();
            _music.loop = true;
            _music.playOnAwake = false;
            _sfx = gameObject.AddComponent<AudioSource>();
            _sfx.playOnAwake = false;

            // Bez AudioListenera Unity ne pusta NISTA. Scene su cisti UI overlay i
            // grade se kodom, pa u njima nema kamere (ni njenog listenera), zato ga
            // nosimo sa sobom; AudioManager je DontDestroyOnLoad pa vrijedi svugdje.
            if (Object.FindFirstObjectByType<AudioListener>() == null)
                gameObject.AddComponent<AudioListener>();

            _volume = PlayerPrefs.GetFloat(VolumeKey, 1f);
            Apply();
        }

        // master volume 0..1, pamti se izmedu sesija
        public void SetVolume(float value)
        {
            _volume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(VolumeKey, _volume);
            Apply();
        }

        private void Apply() => AudioListener.volume = _volume;

        // pusti glazbu (ne restarta ako vec svira ista)
        public void PlayMusic(string clipName)
        {
            if (_currentMusic == clipName && _music.isPlaying) return;
            var clip = Load(clipName);
            if (clip == null) return;
            _currentMusic = clipName;
            _music.clip = clip;
            _music.volume = 0.55f;
            _music.Play();
        }

        // jedan zvucni efekt
        public void PlaySfx(string clipName, float volume = 0.9f)
        {
            var clip = Load(clipName);
            if (clip != null) _sfx.PlayOneShot(clip, volume);
        }

        // klik gumba, zovljivo odasvud
        public static void Click() => Instance?.PlaySfx("Sfx_Click");

        // tihi tick kad mis prijede preko gumba (tise od klika da ne bude naporno)
        public static void Hover() => Instance?.PlaySfx("Sfx_Hover", 0.3f);

        private AudioClip Load(string name)
        {
            if (_cache.TryGetValue(name, out var c)) return c;
            var clip = Resources.Load<AudioClip>("Audio/" + name);
            _cache[name] = clip; // i null se cachira da ne trazimo svaki put
            return clip;
        }

        public static AudioManager EnsureExists()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("AudioManager");
            return go.AddComponent<AudioManager>();
        }
    }
}

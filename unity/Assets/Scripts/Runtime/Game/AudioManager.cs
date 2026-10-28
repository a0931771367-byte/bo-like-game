using System.Collections.Generic;
using BoLike.Sim;
using UnityEngine;

namespace BoLike.Game
{
    /// <summary>
    /// Plays sound cues and music from Resources/Audio/{sfx,music,amb}. Cue names are the ones the
    /// simulation already uses (swing, hit, jump …); each maps to one or more files, picked at random
    /// with a small pitch wobble so repeated hits do not sound identical. Code-only: no mixer asset.
    /// </summary>
    public class AudioManager : MonoBehaviour, ISfx
    {
        class Cue
        {
            public string[] files;
            public float volume = 1f;
            public float pitchVar = 0.06f;
            public float minGapMs = 30f;
            public float lastPlayed = -1000f;
            public AudioClip[] clips;
        }

        const string PrefMuted = "audio.muted";
        const string PrefMusic = "audio.music";
        const string PrefSfx = "audio.sfx";

        static AudioManager instance;
        public static AudioManager Instance
        {
            get
            {
                if (instance == null)
                {
                    var go = new GameObject("Audio");
                    instance = go.AddComponent<AudioManager>();
                    DontDestroyOnLoad(go);
                }
                return instance;
            }
        }

        public bool Muted { get => muted; set { muted = value; PlayerPrefs.SetInt(PrefMuted, value ? 1 : 0); ApplyVolumes(); } }
        public float MusicVolume { get => musicVolume; set { musicVolume = Mathf.Clamp01(value); PlayerPrefs.SetFloat(PrefMusic, musicVolume); ApplyVolumes(); } }
        public float SfxVolume { get => sfxVolume; set { sfxVolume = Mathf.Clamp01(value); PlayerPrefs.SetFloat(PrefSfx, sfxVolume); } }

        bool muted;
        float musicVolume = 0.8f, sfxVolume = 1f;
        readonly List<AudioSource> pool = new();
        AudioSource musicA, musicB, ambience;
        AudioSource musicCur;
        string musicName;
        float musicLoopStart;
        bool nextLoopScheduled;
        float fadeFrom, fadeTo, fadeT, fadeDur;
        AudioSource fading;

        readonly Dictionary<string, Cue> cues = new()
        {
            ["swing"] = new Cue { files = new[] { "sfx_swing_01" }, volume = 0.7f, pitchVar = 0.08f },
            ["hit"] = new Cue { files = new[] { "sfx_hit_01" }, volume = 0.9f, pitchVar = 0.1f },
            ["hitHeavy"] = new Cue { files = new[] { "sfx_hit_heavy" }, pitchVar = 0.05f },
            ["crit"] = new Cue { files = new[] { "sfx_crit" }, pitchVar = 0.03f },
            ["arrow"] = new Cue { files = new[] { "sfx_arrow_01", "sfx_arrow_02" }, volume = 0.8f },
            ["thunder"] = new Cue { files = new[] { "sfx_thunder" }, pitchVar = 0.03f },
            ["fire"] = new Cue { files = new[] { "sfx_fire" } },
            ["ice"] = new Cue { files = new[] { "sfx_ice" } },
            ["cast"] = new Cue { files = new[] { "sfx_cast" }, volume = 0.8f },
            ["skill"] = new Cue { files = new[] { "sfx_skill" } },
            ["dodge"] = new Cue { files = new[] { "sfx_dodge" } },
            ["guard"] = new Cue { files = new[] { "sfx_guard" }, volume = 0.8f },
            ["hurt"] = new Cue { files = new[] { "sfx_hurt" }, minGapMs = 120f },
            ["jump"] = new Cue { files = new[] { "sfx_jump" }, volume = 0.6f },
            ["land"] = new Cue { files = new[] { "sfx_step_dirt_01", "sfx_step_dirt_02", "sfx_step_dirt_03", "sfx_step_dirt_04" }, volume = 0.7f, pitchVar = 0.1f },
            ["step"] = new Cue { files = new[] { "sfx_step_grass_01", "sfx_step_grass_02", "sfx_step_grass_03", "sfx_step_grass_04" }, volume = 0.45f, pitchVar = 0.12f, minGapMs = 150f },
            ["potion"] = new Cue { files = new[] { "sfx_potion" } },
            ["heal"] = new Cue { files = new[] { "sfx_heal" }, volume = 0.8f },
            ["buff"] = new Cue { files = new[] { "sfx_buff" }, volume = 0.7f },
            ["gold"] = new Cue { files = new[] { "sfx_gold" }, volume = 0.5f, minGapMs = 100f },
            ["levelup"] = new Cue { files = new[] { "sfx_levelup" } },
            ["enemyDie"] = new Cue { files = new[] { "sfx_enemy_die" }, pitchVar = 0.12f },
            ["bossDie"] = new Cue { files = new[] { "sfx_boss_die_jelly" }, pitchVar = 0f },
            ["bossAppear"] = new Cue { files = new[] { "sfx_stinger_boss" }, pitchVar = 0f },
            ["bossGrowl"] = new Cue { files = new[] { "sfx_boss_growl" } },
            ["bossStomp"] = new Cue { files = new[] { "sfx_boss_pudding_stomp_01", "sfx_boss_pudding_stomp_02" } },
            ["jellyBounce"] = new Cue { files = new[] { "sfx_jelly_bounce" }, volume = 0.6f, pitchVar = 0.15f },
            ["clear"] = new Cue { files = new[] { "sfx_stinger_clear" }, pitchVar = 0f },
            ["lose"] = new Cue { files = new[] { "sfx_stinger_lose" }, pitchVar = 0f },
            ["select"] = new Cue { files = new[] { "sfx_ui_select" }, volume = 0.7f, pitchVar = 0.02f },
            ["text"] = new Cue { files = new[] { "sfx_ui_text" }, volume = 0.5f, minGapMs = 40f },
            ["wolfGrowl"] = new Cue { files = new[] { "sfx_wolf_growl" } },
            ["bugChirp"] = new Cue { files = new[] { "sfx_bug_chirp" }, volume = 0.6f },
            ["flowerBite"] = new Cue { files = new[] { "sfx_flower_bite" } },
        };

        /// <summary>Music tracks by logical name; loopStart in seconds (0 = whole track loops) is filled in later by the audio team.</summary>
        readonly Dictionary<string, (string file, float loopStart)> music = new()
        {
            ["title"] = ("bgm_title", 0f),
            ["select"] = ("bgm_select", 0f),
            ["field"] = ("bgm_ch1_field", 0f),
            ["intro"] = ("bgm_ch1_intro", 0f),
            ["boss"] = ("bgm_boss", 0f),
            ["story"] = ("bgm_story", 0f),
            ["forest"] = ("bgm_ch2_forest", 0f),
            ["calm"] = ("bgm_ch2_calm", 0f),
        };

        void Awake()
        {
            instance = this;
            muted = PlayerPrefs.GetInt(PrefMuted, 0) == 1;
            musicVolume = PlayerPrefs.GetFloat(PrefMusic, 0.8f);
            sfxVolume = PlayerPrefs.GetFloat(PrefSfx, 1f);
            musicA = NewSource("MusicA"); musicB = NewSource("MusicB"); ambience = NewSource("Ambience");
            ambience.loop = true;
            for (int i = 0; i < 12; i++) pool.Add(NewSource("Sfx" + i));
            ApplyVolumes();
        }

        AudioSource NewSource(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 0f;
            return src;
        }

        // ---------- sfx ----------

        public void Play(string name, float volume = 1f)
        {
            if (muted || !cues.TryGetValue(name, out var cue)) return;
            float now = Time.unscaledTime * 1000f;
            if (now - cue.lastPlayed < cue.minGapMs) return;
            cue.lastPlayed = now;
            cue.clips ??= LoadAll("Audio/sfx/", cue.files);
            var clip = cue.clips[Random.Range(0, cue.clips.Length)];
            if (clip == null) return;
            var src = FreeSource();
            src.pitch = 1f + Random.Range(-cue.pitchVar, cue.pitchVar);
            src.PlayOneShot(clip, cue.volume * volume * sfxVolume);
        }

        AudioSource FreeSource()
        {
            foreach (var s in pool) if (!s.isPlaying) return s;
            var extra = NewSource("Sfx" + pool.Count);
            pool.Add(extra);
            return extra;
        }

        static AudioClip[] LoadAll(string dir, string[] files)
        {
            var clips = new AudioClip[files.Length];
            for (int i = 0; i < files.Length; i++)
            {
                clips[i] = Resources.Load<AudioClip>(dir + files[i]);
                if (clips[i] == null) Debug.LogWarning($"[Audio] missing clip Resources/{dir}{files[i]}");
            }
            return clips;
        }

        // ---------- music ----------

        public void PlayMusic(string name, float fadeSeconds = 0.6f)
        {
            if (name == musicName && musicCur != null && musicCur.isPlaying) return;
            if (!music.TryGetValue(name, out var m)) { StopMusic(fadeSeconds); return; }
            var clip = Resources.Load<AudioClip>("Audio/music/" + m.file);
            if (clip == null) { Debug.LogWarning($"[Audio] missing music {m.file}"); StopMusic(fadeSeconds); return; }
            var next = musicCur == musicA ? musicB : musicA;
            if (musicCur != null && musicCur.isPlaying) StartFade(musicCur, fadeSeconds);
            next.Stop();
            next.clip = clip;
            next.loop = m.loopStart <= 0f;
            next.time = 0f;
            next.volume = MusicLevel();
            next.Play();
            musicCur = next;
            musicName = name;
            musicLoopStart = m.loopStart;
            nextLoopScheduled = false;
        }

        public void StopMusic(float fadeSeconds = 0.6f)
        {
            if (musicCur != null && musicCur.isPlaying) StartFade(musicCur, fadeSeconds);
            musicCur = null;
            musicName = null;
        }

        public void PlayAmbience(string file, float volume = 0.5f)
        {
            var clip = string.IsNullOrEmpty(file) ? null : Resources.Load<AudioClip>("Audio/amb/" + file);
            if (clip == null) { ambience.Stop(); return; }
            if (ambience.clip == clip && ambience.isPlaying) return;
            ambience.clip = clip;
            ambience.volume = volume * MusicLevel();
            ambience.Play();
        }

        public void StopAmbience() => ambience.Stop();

        void StartFade(AudioSource src, float seconds)
        {
            if (seconds <= 0f) { src.Stop(); return; }
            if (fading != null && fading != src) fading.Stop();
            fading = src; fadeFrom = src.volume; fadeTo = 0f; fadeT = 0f; fadeDur = seconds;
        }

        float MusicLevel() => muted ? 0f : musicVolume;

        void ApplyVolumes()
        {
            if (musicCur != null && musicCur != fading) musicCur.volume = MusicLevel();
            if (ambience != null && ambience.clip != null) ambience.volume = 0.5f * MusicLevel();
        }

        void Update()
        {
            if (fading != null)
            {
                fadeT += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(fadeT / fadeDur);
                fading.volume = Mathf.Lerp(fadeFrom, fadeTo, k);
                if (k >= 1f) { fading.Stop(); fading = null; }
            }
            // Custom loop point: hand over to the other source at loopStart just before the track ends.
            if (musicCur != null && musicLoopStart > 0f && !nextLoopScheduled && musicCur.clip != null)
            {
                float remaining = musicCur.clip.length - musicCur.time;
                if (remaining < 1f)
                {
                    var next = musicCur == musicA ? musicB : musicA;
                    next.clip = musicCur.clip;
                    next.loop = false;
                    next.volume = MusicLevel();
                    next.time = musicLoopStart;
                    next.PlayScheduled(AudioSettings.dspTime + remaining);
                    nextLoopScheduled = true;
                    var finishing = musicCur;
                    musicCur = next;
                    // The old source simply runs out; clear the flag once the new one is audible.
                    StartCoroutine(ClearLoopFlag(remaining + 0.05f, finishing));
                }
            }
        }

        System.Collections.IEnumerator ClearLoopFlag(float after, AudioSource finishing)
        {
            yield return new WaitForSecondsRealtime(after);
            if (finishing != musicCur) finishing.Stop();
            nextLoopScheduled = false;
        }
    }
}

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
            /// <summary>Cue to play instead when none of this cue's files exist in Resources (e.g. audio not yet delivered).</summary>
            public string fallback;
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
        float musicLoopEnd;
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
            // 11/1 罐子：每職業受傷音不同（音效部 sfx-final-v1 第七節）。Player 以 "hurt_<classId>" 呼叫，
            // 檔案尚未進專案時自動退回通用 sfx_hurt（見 Play 的 fallback）。sfx_hurt 保留給陷阱等非敵人傷害。
            ["hurt_swordsman"] = new Cue { files = new[] { "sfx_hurt_swordsman" }, pitchVar = 0.04f, minGapMs = 120f, fallback = "hurt" },
            ["hurt_mage"] = new Cue { files = new[] { "sfx_hurt_mage" }, pitchVar = 0.04f, minGapMs = 120f, fallback = "hurt" },
            ["hurt_merchant"] = new Cue { files = new[] { "sfx_hurt_artificer" }, pitchVar = 0.04f, minGapMs = 120f, fallback = "hurt" },
            ["hurt_acolyte"] = new Cue { files = new[] { "sfx_hurt_priest" }, pitchVar = 0.04f, minGapMs = 120f, fallback = "hurt" },
            ["hurt_thief"] = new Cue { files = new[] { "sfx_hurt_assassin" }, pitchVar = 0.04f, minGapMs = 120f, fallback = "hurt" },
            ["hurt_archer"] = new Cue { files = new[] { "sfx_hurt_ranger" }, pitchVar = 0.04f, minGapMs = 120f, fallback = "hurt" },
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

        /// <summary>
        /// Music tracks by logical name with loop points in seconds (audio/loop-points-final-v1.md). loopStart 0 = the whole
        /// track loops; otherwise the first play runs from 0 and at loopEnd (0 = track end) hands over to loopStart.
        /// Phase-1 tracks stay whole-track loops for the beta by the audio team's decision; phase-2 tracks use their final values.
        /// </summary>
        readonly Dictionary<string, (string file, float loopStart, float loopEnd)> music = new()
        {
            ["title"] = ("bgm_title", 0f, 0f),
            ["select"] = ("bgm_select", 0f, 0f),
            ["field"] = ("bgm_ch1_field", 0f, 0f),
            ["intro"] = ("bgm_ch1_intro", 0f, 0f),
            ["boss"] = ("bgm_boss", 0f, 0f),
            ["story"] = ("bgm_story", 0f, 0f),
            ["forest"] = ("bgm_ch2_forest", 0f, 0f),
            ["calm"] = ("bgm_ch2_calm", 0f, 0f),
            // Phase 2 (beta 2): second exploration tracks, the desert, and one track per chapter boss.
            ["field_b"] = ("bgm_ch1_field_b", 27.20f, 194.90f),
            ["forest_b"] = ("bgm_ch2_forest_b", 14.10f, 112.80f),
            ["desert"] = ("bgm_ch3_desert", 26.90f, 185.30f),
            ["boss_pudding"] = ("bgm_boss_pudding", 18.40f, 89.60f),
            ["boss_treant"] = ("bgm_boss_treant", 17.80f, 176.10f),
            ["boss_guardian"] = ("bgm_boss_guardian", 13.90f, 76.00f),
        };

        /// <summary>Phase-2 music key → phase-1 key to play until the new file is in the project.</summary>
        static readonly Dictionary<string, string> musicFallback = new()
        {
            ["field_b"] = "field", ["forest_b"] = "forest", ["desert"] = "field",
            ["boss_pudding"] = "boss", ["boss_treant"] = "boss", ["boss_guardian"] = "boss",
        };

        void Awake()
        {
            instance = this;
            if (GetComponent<AudioListener>() == null) gameObject.AddComponent<AudioListener>(); // the only listener; 2D audio needs no position
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
            cue.clips ??= LoadAll("Audio/sfx/", cue.files, quiet: cue.fallback != null);
            var clip = cue.clips[Random.Range(0, cue.clips.Length)];
            if (clip == null)
            {
                if (cue.fallback != null && cue.fallback != name) { cue.lastPlayed = -1000f; Play(cue.fallback, volume); }
                return;
            }
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

        static AudioClip[] LoadAll(string dir, string[] files, bool quiet = false)
        {
            var clips = new AudioClip[files.Length];
            for (int i = 0; i < files.Length; i++)
            {
                clips[i] = Resources.Load<AudioClip>(dir + files[i]);
                if (clips[i] == null && !quiet) Debug.LogWarning($"[Audio] missing clip Resources/{dir}{files[i]}");
            }
            return clips;
        }

        // ---------- music ----------

        public void PlayMusic(string name, float fadeSeconds = 0.6f)
        {
            if (name == musicName && musicCur != null && musicCur.isPlaying) return;
            if (!music.TryGetValue(name, out var m)) { StopMusic(fadeSeconds); return; }
            var clip = Resources.Load<AudioClip>("Audio/music/" + m.file);
            if (clip == null && musicFallback.TryGetValue(name, out var fb) && music.TryGetValue(fb, out var fm))
            {
                clip = Resources.Load<AudioClip>("Audio/music/" + fm.file); // phase-2 track not delivered yet: play the phase-1 one
                if (clip != null) m = fm;
            }
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
            musicLoopEnd = m.loopEnd > 0f && m.loopEnd < clip.length ? m.loopEnd : clip.length;
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
                float remaining = musicLoopEnd - musicCur.time;
                if (remaining < 1f)
                {
                    var next = musicCur == musicA ? musicB : musicA;
                    next.clip = musicCur.clip;
                    next.loop = false;
                    next.volume = MusicLevel();
                    next.time = musicLoopStart;
                    next.PlayScheduled(AudioSettings.dspTime + System.Math.Max(0f, remaining));
                    nextLoopScheduled = true;
                    var finishing = musicCur;
                    musicCur = next;
                    // The old source is stopped right after the hand-over (it would otherwise play past loopEnd); the
                    // 40 ms overlap hides the seam, as the audio team asked.
                    StartCoroutine(ClearLoopFlag(System.Math.Max(0f, remaining) + 0.04f, finishing));
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

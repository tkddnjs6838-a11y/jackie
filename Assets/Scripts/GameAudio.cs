using UnityEngine;

/// <summary>
/// One place that owns every sound in the game. Scripts ask for a sound by name
/// (GameAudio.Play(GameAudio.Sfx.Swing)) and never touch an AudioSource themselves, so
/// clips can be swapped or regenerated without editing gameplay code.
///
/// Clips can be assigned in the inspector, but any empty slot also falls back to
/// Resources/Audio/&lt;name&gt; — dropping "Swing.wav" in that folder is enough to make the
/// sound play, and Resources survives WebGL build stripping the way Shader.Find did not.
/// </summary>
public class GameAudio : MonoBehaviour
{
    public enum Sfx
    {
        Swing,          // every swing, hit or miss
        Hit,            // the blade connecting
        EnemyDie,
        PlayerHurt,
        PlayerDie,
        Dash,
        Pickup,
        UpgradeOpen,
        UpgradePick,
        BossSpawn,
        BossDie,
        Victory
    }

    [System.Serializable]
    public class Entry
    {
        public Sfx id;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 1f;
        // a little pitch scatter stops the same clip fired repeatedly from sounding robotic
        public float pitchJitter = 0.06f;
        // 35 monsters can die inside the same second; without a floor on the gap between
        // repeats their death sounds stack into one wall of noise and clip the mix
        public float minRepeatInterval = 0.06f;

        [HideInInspector] public float lastPlayed = -999f;
    }

    public static GameAudio Instance;

    [Header("Mix")]
    [Range(0f, 1f)] public float sfxVolume = 0.8f;
    [Range(0f, 1f)] public float musicVolume = 0.35f;
    // one source per simultaneous sound; past this the oldest is reused
    public int voiceCount = 12;

    [Header("Clips")]
    public Entry[] entries;

    [Header("Music")]
    public AudioClip musicCombat;
    public AudioClip musicBoss;
    public float musicFadeSeconds = 1.5f;

    private AudioSource[] voices;
    private int nextVoice;
    private AudioSource musicA;
    private AudioSource musicB;
    private AudioSource activeMusic;
    private Coroutine fadeRoutine;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;

        BuildMissingEntries();
        ResolveClips();

        voices = new AudioSource[Mathf.Max(1, voiceCount)];
        for (int i = 0; i < voices.Length; i++)
        {
            var go = new GameObject("Voice" + i);
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 0f;      // 2D: the camera is far from the action, and a
                                        // positional mix made hits on screen edges inaudible
            voices[i] = src;
        }

        musicA = NewMusicSource("MusicA");
        musicB = NewMusicSource("MusicB");
        activeMusic = musicA;
    }

    AudioSource NewMusicSource(string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var src = go.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.loop = true;
        src.spatialBlend = 0f;
        src.volume = 0f;
        return src;
    }

    /// <summary>Makes sure every Sfx value has a slot, so nothing is silently missing.</summary>
    void BuildMissingEntries()
    {
        var names = System.Enum.GetValues(typeof(Sfx));
        var list = new System.Collections.Generic.List<Entry>();
        if (entries != null) list.AddRange(entries);

        foreach (var value in names)
        {
            var id = (Sfx)value;
            bool found = false;
            foreach (var e in list)
                if (e != null && e.id == id) { found = true; break; }
            if (!found) list.Add(new Entry { id = id });
        }
        entries = list.ToArray();
    }

    void ResolveClips()
    {
        foreach (var e in entries)
        {
            if (e == null || e.clip != null) continue;
            e.clip = Resources.Load<AudioClip>("Audio/" + e.id.ToString());
        }

        // music follows the same drop-in rule as the effects, so a generated track only has
        // to be named MusicCombat or MusicBoss to be picked up
        if (musicCombat == null) musicCombat = Resources.Load<AudioClip>("Audio/MusicCombat");
        if (musicBoss == null) musicBoss = Resources.Load<AudioClip>("Audio/MusicBoss");
    }

    void OnEnable()
    {
        Enemy.OnEnemyDied += HandleEnemyDied;
        Enemy.OnBossSpawned += HandleBossSpawned;
        Enemy.OnBossDefeated += HandleBossDefeated;
        PlayerHealth.OnPlayerDied += HandlePlayerDied;
    }

    void OnDisable()
    {
        Enemy.OnEnemyDied -= HandleEnemyDied;
        Enemy.OnBossSpawned -= HandleBossSpawned;
        Enemy.OnBossDefeated -= HandleBossDefeated;
        PlayerHealth.OnPlayerDied -= HandlePlayerDied;
    }

    void HandleEnemyDied(Enemy enemy)
    {
        Play(enemy != null && enemy.isBoss ? Sfx.BossDie : Sfx.EnemyDie);
    }

    void HandleBossSpawned(Enemy boss)
    {
        Play(Sfx.BossSpawn);
        PlayMusic(musicBoss);
    }

    void HandleBossDefeated()
    {
        PlayMusic(musicCombat);
    }

    void HandlePlayerDied()
    {
        Play(Sfx.PlayerDie);
        StopMusic();
    }

    /// <summary>Fire a sound by name. Safe to call before any clip exists — it just does nothing.</summary>
    public static void Play(Sfx id)
    {
        if (Instance != null) Instance.PlayLocal(id);
    }

    void PlayLocal(Sfx id)
    {
        Entry entry = null;
        foreach (var e in entries)
            if (e != null && e.id == id) { entry = e; break; }

        if (entry == null || entry.clip == null) return;

        // unscaled: the upgrade panel and the hit-stop both run at timeScale 0 or 0.2,
        // and sound has to keep its own timing through those
        float now = Time.unscaledTime;
        if (now - entry.lastPlayed < entry.minRepeatInterval) return;
        entry.lastPlayed = now;

        var src = voices[nextVoice];
        nextVoice = (nextVoice + 1) % voices.Length;

        src.pitch = 1f + Random.Range(-entry.pitchJitter, entry.pitchJitter);
        src.PlayOneShot(entry.clip, entry.volume * sfxVolume);
    }

    public void PlayMusic(AudioClip clip)
    {
        if (clip == null) { StopMusic(); return; }
        if (activeMusic != null && activeMusic.clip == clip && activeMusic.isPlaying) return;

        var from = activeMusic;
        var to = (activeMusic == musicA) ? musicB : musicA;

        to.clip = clip;
        to.volume = 0f;
        to.Play();
        activeMusic = to;

        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(Crossfade(from, to));
    }

    public void StopMusic()
    {
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(Crossfade(activeMusic, null));
    }

    System.Collections.IEnumerator Crossfade(AudioSource from, AudioSource to)
    {
        float t = 0f;
        float fromStart = from != null ? from.volume : 0f;
        float duration = Mathf.Max(0.01f, musicFadeSeconds);

        while (t < duration)
        {
            // unscaledDeltaTime: a paused game must not freeze the music fade
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duration);
            if (from != null) from.volume = Mathf.Lerp(fromStart, 0f, k);
            if (to != null) to.volume = Mathf.Lerp(0f, musicVolume, k);
            yield return null;
        }

        if (from != null) { from.volume = 0f; from.Stop(); }
        if (to != null) to.volume = musicVolume;
        fadeRoutine = null;
    }

    /// <summary>Called when a run starts, so retrying restarts the combat track.</summary>
    public void StartRunMusic()
    {
        PlayMusic(musicCombat);
    }
}

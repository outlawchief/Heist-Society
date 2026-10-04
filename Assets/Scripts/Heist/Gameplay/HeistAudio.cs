using System.Collections.Generic;
using UnityEngine;

public class HeistAudio : MonoBehaviour
{
    public static HeistAudio Instance { get; private set; }

    AudioSource music;
    AudioSource[] sfxPool;
    int sfxCursor;
    AudioClip[] tracks = System.Array.Empty<AudioClip>();
    AudioClip[] operativePunches = System.Array.Empty<AudioClip>();
    AudioClip[] guardPunches = System.Array.Empty<AudioClip>();
    float trackEndTime = float.PositiveInfinity;
    bool playlistActive;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (FindFirstObjectByType<HeistAudio>() != null) return;
        var go = new GameObject("HeistAudio");
        DontDestroyOnLoad(go);
        go.AddComponent<HeistAudio>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        music = gameObject.AddComponent<AudioSource>();
        music.loop = false;
        music.playOnAwake = false;
        music.spatialBlend = 0f;
        music.volume = 0.45f;
        music.priority = 0;

        sfxPool = new AudioSource[4];
        for (int i = 0; i < sfxPool.Length; i++)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.priority = 64;
            sfxPool[i] = source;
        }

        tracks = LoadClips("Audio/Locked_Entry", "Audio/Perimeter_Breach");
        operativePunches = Resources.LoadAll<AudioClip>("Audio/SFX/Operative");
        guardPunches = Resources.LoadAll<AudioClip>("Audio/SFX/Guard");
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public static void StartLevel()
    {
        if (Instance == null) return;
        Instance.playlistActive = true;
        Instance.PlayRandomTrack();
    }

    public static void PlayOperativePunch()
    {
        if (Instance == null) return;
        Instance.PlaySfx(Instance.operativePunches, 0.9f, 0.96f, 1.08f);
    }

    public static void PlayGuardPunch()
    {
        if (Instance == null) return;
        Instance.PlaySfx(Instance.guardPunches, 0.95f, 0.86f, 0.98f);
    }

    void Update()
    {
        if (!playlistActive || tracks.Length == 0) return;
        if (Time.unscaledTime < trackEndTime) return;
        PlayRandomTrack();
    }

    void PlayRandomTrack()
    {
        if (tracks.Length == 0) return;
        var clip = tracks[Random.Range(0, tracks.Length)];
        if (clip == null)
        {
            trackEndTime = Time.unscaledTime + 1f;
            return;
        }

        music.Stop();
        music.clip = clip;
        music.pitch = 1f;
        music.Play();
        float length = clip.length > 0.05f ? clip.length : 1f;
        trackEndTime = Time.unscaledTime + length;
    }

    void PlaySfx(AudioClip[] bank, float volume, float pitchMin, float pitchMax)
    {
        if (bank == null || bank.Length == 0 || sfxPool == null || sfxPool.Length == 0) return;
        var clip = bank[Random.Range(0, bank.Length)];
        if (clip == null) return;
        var source = sfxPool[sfxCursor];
        sfxCursor = (sfxCursor + 1) % sfxPool.Length;
        source.pitch = Random.Range(pitchMin, pitchMax);
        source.PlayOneShot(clip, volume);
    }

    static AudioClip[] LoadClips(params string[] paths)
    {
        var found = new List<AudioClip>();
        foreach (var path in paths)
        {
            var clip = Resources.Load<AudioClip>(path);
            if (clip != null) found.Add(clip);
        }
        return found.ToArray();
    }
}

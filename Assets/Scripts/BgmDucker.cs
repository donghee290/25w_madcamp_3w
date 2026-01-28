using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class BgmDucker : MonoBehaviour
{
    public static BgmDucker I { get; private set; }

    [Header("BGM Source (required)")]
    [SerializeField] private AudioSource bgmSource;

    [Header("Only play in this scene")]
    [SerializeField] private string onlySceneName = "Main";

    [Header("Default volumes")]
    [Range(0f, 1f)][SerializeField] private float normalVolume = 0.35f;

    [Header("Ducking")]
    [Tooltip("효과음이 나올 때 내려갈 볼륨(겹치면 작아지는 정도)")]
    [Range(0f, 1f)][SerializeField] private float duckVolume = 0.18f;

    [Tooltip("볼륨 내려가는 속도(초)")]
    [SerializeField] private float fadeDownSec = 0.05f;

    [Tooltip("볼륨 복구 속도(초)")]
    [SerializeField] private float fadeUpSec = 0.25f;

    Coroutine co;
    float duckUntilTime = 0f;

    void Awake()
    {
        if (I != null && I != this)
        {
            Destroy(gameObject);
            return;
        }
        I = this;
        DontDestroyOnLoad(gameObject);

        SceneManager.sceneLoaded += OnSceneLoaded;

        if (bgmSource == null) bgmSource = GetComponent<AudioSource>();
        if (bgmSource != null)
        {
            bgmSource.loop = true;
            bgmSource.playOnAwake = true;
            bgmSource.spatialBlend = 0f; // 2D
            bgmSource.volume = normalVolume;
        }
    }

    void OnDestroy()
    {
        if (I == this) I = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void Start()
    {
        ApplySceneRule(SceneManager.GetActiveScene().name);
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplySceneRule(scene.name);
    }

    void ApplySceneRule(string sceneName)
    {
        if (bgmSource == null) return;

        bool shouldPlay = sceneName == onlySceneName;
        if (!shouldPlay)
        {
            bgmSource.Stop();
            return;
        }

        // Main이면 재생(이미 재생중이면 유지)
        if (!bgmSource.isPlaying) bgmSource.Play();
        bgmSource.volume = normalVolume;
    }

    // 다른 사운드가 나올 때 호출: 겹치는 동안만 BGM을 낮춘다.
    public void Duck(float seconds)
    {
        if (bgmSource == null) return;
        if (SceneManager.GetActiveScene().name != onlySceneName) return;

        duckUntilTime = Mathf.Max(duckUntilTime, Time.time + Mathf.Max(0f, seconds));

        if (co == null) co = StartCoroutine(DuckRoutine());
    }

    IEnumerator DuckRoutine()
    {
        // 다운
        yield return FadeTo(duckVolume, fadeDownSec);

        // 유지(효과음이 연속으로 오면 duckUntilTime이 연장됨)
        while (Time.time < duckUntilTime) yield return null;

        // 업
        yield return FadeTo(normalVolume, fadeUpSec);

        co = null;
    }

    IEnumerator FadeTo(float target, float sec)
    {
        if (sec <= 0f)
        {
            bgmSource.volume = target;
            yield break;
        }

        float start = bgmSource.volume;
        float t = 0f;
        while (t < sec)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / sec);
            bgmSource.volume = Mathf.Lerp(start, target, u);
            yield return null;
        }
        bgmSource.volume = target;
    }
}
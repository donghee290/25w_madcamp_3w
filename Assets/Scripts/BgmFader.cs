using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class BgmFader : MonoBehaviour
{
    public static BgmFader I { get; private set; }

    [Header("BGM Source")]
    [SerializeField] private AudioSource bgmSource;

    [Header("Only play in this scene")]
    [SerializeField] private string onlySceneName = "StartScene";

    [Header("Volume")]
    [Range(0f, 1f)]
    [SerializeField] private float normalVolume = 0.35f;

    [Header("Fade Out")]
    [SerializeField] private float fadeOutSec = 0.6f;

    Coroutine fadeCo;

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

        if (bgmSource == null)
            bgmSource = GetComponent<AudioSource>();

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

        if (sceneName != onlySceneName)
        {
            bgmSource.Stop();
            return;
        }

        if (!bgmSource.isPlaying)
            bgmSource.Play();

        bgmSource.volume = normalVolume;
    }

    // ✅ 패널 애니메이션 시작 시 한 번만 호출
    public void FadeOut()
    {
        if (bgmSource == null) return;
        if (!bgmSource.isPlaying) return;

        if (fadeCo != null) StopCoroutine(fadeCo);
        fadeCo = StartCoroutine(FadeOutRoutine());
    }

    IEnumerator FadeOutRoutine()
    {
        float start = bgmSource.volume;
        float t = 0f;

        while (t < fadeOutSec)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / fadeOutSec);
            bgmSource.volume = Mathf.Lerp(start, 0f, u);
            yield return null;
        }

        bgmSource.volume = 0f;
        bgmSource.Stop();
        fadeCo = null;
    }
}
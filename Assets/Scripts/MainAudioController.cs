using UnityEngine;
using UnityEngine.SceneManagement;

public class MainAudioController : MonoBehaviour
{
    [Header("Only active in this scene")]
    [SerializeField] private string onlySceneName = "Main";

    [Header("Audio Sources (required)")]
    [SerializeField] private AudioSource bgmSource;
    [SerializeField] private AudioSource sfxSource;

    [Header("Clips")]
    [SerializeField] private AudioClip mainBgmClip;
    [SerializeField] private AudioClip failClip;

    [Header("Volumes")]
    [Range(0f, 1f)][SerializeField] private float bgmVolume = 0.35f;
    [Range(0f, 1f)][SerializeField] private float failVolume = 1.0f;

    bool failPlayed = false;

    void Awake()
    {
        // 씬 전용 컨트롤러: 중복 생성 방지(혹시 프리팹 중복 배치했을 때)
        var others = FindObjectsByType<MainAudioController>(FindObjectsSortMode.None);
        if (others != null && others.Length > 1)
        {
            Destroy(gameObject);
            return;
        }

        if (bgmSource == null) bgmSource = GetComponent<AudioSource>();
        if (bgmSource != null)
        {
            bgmSource.loop = true;
            bgmSource.playOnAwake = false;
            bgmSource.spatialBlend = 0f;
            bgmSource.volume = bgmVolume;
        }

        if (sfxSource != null)
        {
            sfxSource.loop = false;
            sfxSource.playOnAwake = false;
            sfxSource.spatialBlend = 0f;
        }
    }

    void Start()
    {
        if (SceneManager.GetActiveScene().name != onlySceneName) return;
        PlayBgm();
    }

    void OnEnable()
    {
        // 리스타트/재진입 시 failPlayed 초기화
        failPlayed = false;
    }

    public void PlayBgm()
    {
        if (bgmSource == null) return;
        if (SceneManager.GetActiveScene().name != onlySceneName) return;
        if (mainBgmClip == null) return;

        bgmSource.clip = mainBgmClip;
        bgmSource.volume = bgmVolume;
        if (!bgmSource.isPlaying) bgmSource.Play();
    }

    public void StopBgm()
    {
        if (bgmSource == null) return;
        if (bgmSource.isPlaying) bgmSource.Stop();
    }

    // GameManager에서 GameOver 시 딱 1번 호출
    public void OnFailOnce()
    {
        if (failPlayed) return;
        failPlayed = true;

        StopBgm();

        if (sfxSource == null || failClip == null) return;
        sfxSource.PlayOneShot(failClip, failVolume);
    }
}
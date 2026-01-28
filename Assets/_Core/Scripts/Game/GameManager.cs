using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager I { get; private set; }

    [Header("Runtime")]
    [SerializeField] private GameState state = GameState.Playing;
    [SerializeField] private GameOverReason gameOverReason = GameOverReason.HitObstacle;

    [Header("Distance (meters)")]
    [SerializeField] private float distanceMeters = 0f;

    [Header("Refs")]
    public PlayerMotor playerMotor;
    public PoseInput poseInput;

    [Header("Audio (Main Scene only)")]
    [SerializeField] private MainAudioController mainAudio;

    [Header("UI (optional)")]
    [Tooltip("게임오버 즉시 사라져야 하는 TopBar 루트")]
    [SerializeField] private GameObject topBarRoot;

    [Tooltip("게임오버 후 2초 뒤에 뜨는 ReportPopup 루트")]
    [SerializeField] private GameObject ReportCardPopup;

    [Tooltip("ReportPopup 지연 시간(초)")]
    [SerializeField] private float reportPopupDelay = 5f;

    [Header("Comic Outro (optional)")]
    [Tooltip("Canvas/ComicOutroRoot에 붙은 ComicSlideOutro")]
    [SerializeField] private ComicSlideOutro comicOutro;

    [Header("Find throttling")]
    [Tooltip("PlayerMotor를 못 찾았을 때 재탐색 쿨다운(초)")]
    [SerializeField] private float findCooldown = 1.0f;

    public GameState State => state;
    public GameOverReason Reason => gameOverReason;
    public float DistanceMeters => distanceMeters;

    public System.Action<GameOverReason> OnGameOverEvent;

    private Coroutine reportPopupCo;
    private float nextFindTime = 0f;
    private bool warnedNoPlayer = false;

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
    }

    void OnDestroy()
    {
        if (I == this) SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void Start()
    {
        EnsureRefs(false);
        EnsureUIRefs();
        EnsureAudioRefs();
        StartRunImmediate();
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 씬 전환 시 레퍼런스 초기화
        playerMotor = null;
        poseInput = null;
        mainAudio = null;

        topBarRoot = null;
        ReportCardPopup = null;
        comicOutro = null;

        warnedNoPlayer = false;
        nextFindTime = 0f;

        EnsureRefs(false);
        EnsureUIRefs();
        EnsureAudioRefs();

        StartRunImmediate();

        // UI 기본 상태
        if (topBarRoot != null) topBarRoot.SetActive(true);
        if (ReportCardPopup != null) ReportCardPopup.SetActive(false);
    }

    void EnsureRefs(bool forceLog = false)
    {
        if (playerMotor == null)
            playerMotor = FindFirstObjectByType<PlayerMotor>();

        if (poseInput == null)
            poseInput = FindFirstObjectByType<PoseInput>();

        if (playerMotor == null)
        {
            if (!warnedNoPlayer || forceLog)
            {
                warnedNoPlayer = true;
                Debug.LogWarning("[GameManager] PlayerMotor not found in scene.");
            }
        }
        else
        {
            warnedNoPlayer = false;
            if (forceLog) Debug.Log($"[GameManager] PlayerMotor bound: {playerMotor.name}");
        }
    }

    void EnsureUIRefs()
    {
        if (topBarRoot == null)
        {
            var go = GameObject.Find("TopBarRoot");
            if (go != null) topBarRoot = go;
        }

        if (ReportCardPopup == null)
        {
            var go = GameObject.Find("ReportCardPopup");
            if (go != null) ReportCardPopup = go;
        }

        if (comicOutro == null)
        {
            var go = GameObject.Find("ComicOutroRoot");
            if (go != null) comicOutro = go.GetComponent<ComicSlideOutro>();
            if (comicOutro == null)
                comicOutro = FindFirstObjectByType<ComicSlideOutro>();
        }
    }

    void EnsureAudioRefs()
    {
        if (mainAudio == null)
            mainAudio = FindFirstObjectByType<MainAudioController>();
    }

    public void StartRunImmediate()
    {
        CancelReportPopupCo();

        state = GameState.Playing;
        gameOverReason = GameOverReason.HitObstacle;
        distanceMeters = 0f;

        EnsureRefs(false);
        EnsureUIRefs();
        EnsureAudioRefs();

        if (topBarRoot != null) topBarRoot.SetActive(true);
        if (ReportCardPopup != null) ReportCardPopup.SetActive(false);

        if (playerMotor != null)
        {
            playerMotor.enabled = true;
            playerMotor.ForceStopToIdle();
            playerMotor.StartOnGroundForCountdown();
        }
    }

    void Update()
    {
        if (state != GameState.Playing) return;

        if (playerMotor == null)
        {
            if (Time.time >= nextFindTime)
            {
                nextFindTime = Time.time + findCooldown;
                EnsureRefs(false);
            }
            return;
        }

        distanceMeters += playerMotor.CurrentForwardSpeed * Time.deltaTime;
    }

    public void GameOver(GameOverReason reason)
    {
        if (state == GameState.GameOver) return;

        state = GameState.GameOver;
        gameOverReason = reason;

        EnsureUIRefs();
        EnsureAudioRefs();

        // ✅ BGM 종료 + fail 효과음 1회
        if (mainAudio != null)
        {
            // 필요하면 이유 추가 가능
            // ex) reason == GameOverReason.CaughtByChaser
            if (reason == GameOverReason.HitObstacle)
                mainAudio.OnFailOnce();
        }

        if (topBarRoot != null) topBarRoot.SetActive(false);

        if (comicOutro != null) comicOutro.Play();

        if (playerMotor != null)
        {
            playerMotor.ForceStopToIdle();
            playerMotor.enabled = false;
        }

        if (reportPopupCo != null) StopCoroutine(reportPopupCo);
        reportPopupCo = StartCoroutine(ShowReportPopupAfterDelay());

        OnGameOverEvent?.Invoke(reason);
    }

    private IEnumerator ShowReportPopupAfterDelay()
    {
        yield return new WaitForSeconds(reportPopupDelay);
        EnsureUIRefs();
        if (ReportCardPopup != null) ReportCardPopup.SetActive(true);
    }

    void CancelReportPopupCo()
    {
        if (reportPopupCo != null)
        {
            StopCoroutine(reportPopupCo);
            reportPopupCo = null;
        }
    }

    public void RestartSceneSimple()
    {
        CancelReportPopupCo();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void ResetRun()
    {
        CancelReportPopupCo();
        StartRunImmediate();

        state = GameState.Playing;
        gameOverReason = GameOverReason.HitObstacle;
        distanceMeters = 0f;

        if (playerMotor != null)
            playerMotor.enabled = true;

        EnsureUIRefs();
        if (topBarRoot != null) topBarRoot.SetActive(true);
        if (ReportCardPopup != null) ReportCardPopup.SetActive(false);
    }

    public void RestartMainScene()
    {
        CancelReportPopupCo();
        SceneManager.LoadScene("Main");
    }

    public void GoToStartScene()
    {
        CancelReportPopupCo();
        playerMotor = null;
        SceneManager.LoadScene("StartScene");
    }
}
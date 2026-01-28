using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ReportCardUI : MonoBehaviour
{
    [Header("UI Refs")]
    public GameObject popupRoot;   // ReportCardPopup (루트 오브젝트)
    public Button retryButton;     // retry 버튼

    [Header("Timing")]
    public float showDelay = 2f;

    Coroutine showCo;

    void Awake()
    {
        if (popupRoot != null) popupRoot.SetActive(false);

        if (retryButton != null)
        {
            retryButton.onClick.RemoveListener(OnRetryClicked);
            retryButton.onClick.AddListener(OnRetryClicked);
        }
    }

    void OnEnable()
    {
        // GameManager 이벤트 구독
        if (GameManager.I != null)
            GameManager.I.OnGameOverEvent += HandleGameOver;

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        if (GameManager.I != null)
            GameManager.I.OnGameOverEvent -= HandleGameOver;

        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 씬 재시작 시 팝업 숨김
        if (showCo != null) { StopCoroutine(showCo); showCo = null; }
        if (popupRoot != null) popupRoot.SetActive(false);
    }

    void HandleGameOver(GameOverReason reason)
    {
        if (showCo != null) StopCoroutine(showCo);
        showCo = StartCoroutine(ShowAfterDelay());
    }

    IEnumerator ShowAfterDelay()
    {
        yield return new WaitForSeconds(showDelay);
        if (popupRoot != null) popupRoot.SetActive(true);
        showCo = null;
    }

    void OnRetryClicked()
    {
        // 팝업 닫고 MainScene 재시작
        if (popupRoot != null) popupRoot.SetActive(false);

        if (GameManager.I != null)
        {
            GameManager.I.RestartMainScene();
        }
        else
        {
            SceneManager.LoadScene("MainScene");
        }
    }
}
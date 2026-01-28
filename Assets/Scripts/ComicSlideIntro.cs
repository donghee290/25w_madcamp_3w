using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ComicSlideIntro : MonoBehaviour
{
    [Header("Start UI (숨길 것들)")]
    public GameObject startUIRoot;

    [Header("White Background (처음엔 OFF)")]
    public GameObject whiteBG;

    [Header("Tap Button")]
    public Button fullscreenButton;

    [Header("Panels")]
    public RectTransform panel1;
    public RectTransform panel2;
    public RectTransform panel3;

    [Header("Final positions (anchoredPosition)")]
    public Vector2 finalPos1;
    public Vector2 finalPos2;
    public Vector2 finalPos3;

    [Header("Slide")]
    public float slideDuration = 0.55f;
    public float gapDelay = 0.20f;
    public AnimationCurve slideEase = null;

    [Header("Cartoon Push (Overshoot)")]
    public float overshootPx = 90f;
    public float settleDuration = 0.10f;

    [Header("Squash & Stretch")]
    public float stretchAmount = 0.12f;

    [Tooltip("화면 밖으로 숨길 여유 마진(px)")]
    public float offscreenMargin = 150f;

    [Header("Pop (Scale 1.05 -> 1.0)")]
    public float popScale = 1.05f;
    public float popDuration = 0.12f;

    [Header("Shake")]
    public RectTransform shakeTarget;        // PanelsRoot 또는 Canvas
    public float shakeDuration = 0.12f;
    public float shakeStrength = 12f;

    [Header("SFX")]
    [Tooltip("효과음 재생용(AudioSource). 2D로 설정 권장, PlayOnAwake OFF")]
    public AudioSource audioSource;

    [Tooltip("컷 등장 스위시")]
    public AudioClip swish;

    [Tooltip("1컷 내레이션/효과")]
    public AudioClip intro_1;

    [Tooltip("2컷 내레이션/효과")]
    public AudioClip intro_2;

    [Tooltip("패널 슬라이드(페이지 플립)")]
    public AudioClip pageFlip;

    [Tooltip("펀치(2컷)")]
    public AudioClip punch;

    [Header("Audio timing")]
    [Tooltip("컷 '등장과 동시에' swish+intro를 같이 시작하고, 다음 컷은 '둘 다 끝난 뒤'에 진행")]
    public bool playSwishAndIntroTogether = true;

    [Tooltip("컷 음원이 너무 길면 템포가 늘어지니, 최대 대기 시간을 제한(0이면 제한 없음)")]
    public float maxWaitCutAudioSec = 0f;

    [Header("Scene Transition")]
    public float sceneTransitionDelay = 2f;

    [Header("Next Scene")]
    public string mainSceneName = "Main";

    bool started = false;
    float offscreenX;
    Vector2 shakeOrigin;

    void Awake()
    {
        if (slideEase == null) slideEase = AnimationCurve.EaseInOut(0, 0, 1, 1);

        if (whiteBG != null) whiteBG.SetActive(false);

        // ✅ 시작 시 패널은 꺼둠
        if (panel1 != null) panel1.gameObject.SetActive(false);
        if (panel2 != null) panel2.gameObject.SetActive(false);
        if (panel3 != null) panel3.gameObject.SetActive(false);

        if (shakeTarget != null) shakeOrigin = shakeTarget.anchoredPosition;

        RectTransform root = GetRootRect(panel1) ?? GetRootRect(panel2) ?? GetRootRect(panel3);
        float rootWidth = root != null ? root.rect.width : 1080f;
        offscreenX = (rootWidth * 0.55f) + offscreenMargin;
    }

    RectTransform GetRootRect(RectTransform rt)
    {
        if (rt == null) return null;
        Transform t = rt.parent;
        while (t != null)
        {
            if (t is RectTransform r) return r;
            t = t.parent;
        }
        return null;
    }

    void PrepPanel(RectTransform panel, Vector2 finalPos, float sideSign)
    {
        if (panel == null) return;
        panel.anchoredPosition = new Vector2(offscreenX * sideSign, finalPos.y);
        panel.localScale = Vector3.one;
    }

    public void Play()
    {
        if (started) return;
        started = true;

        if (startUIRoot != null) startUIRoot.SetActive(false);
        if (whiteBG != null) whiteBG.SetActive(true);

        if (fullscreenButton != null) fullscreenButton.interactable = false;

        StartCoroutine(Sequence());
    }

    IEnumerator Sequence()
    {
        // 1) panel1
        if (panel1 != null)
        {
            panel1.gameObject.SetActive(true);
            PrepPanel(panel1, finalPos1, +1f);
        }

        // ✅ 컷과 동시에 재생 + 다음 컷은 끝난 뒤
        yield return SlideInWithFX_AndCutAudio(panel1, finalPos1, isPunch: false, cutIndex: 1);
        yield return new WaitForSeconds(gapDelay);

        // 2) panel2
        if (panel2 != null)
        {
            panel2.gameObject.SetActive(true);
            PrepPanel(panel2, finalPos2, -1f);
        }

        yield return SlideInWithFX_AndCutAudio(panel2, finalPos2, isPunch: true, cutIndex: 2);
        yield return new WaitForSeconds(gapDelay);

        // 3) panel3
        if (panel3 != null)
        {
            panel3.gameObject.SetActive(true);
            PrepPanel(panel3, finalPos3, +1f);
        }

        yield return SlideInWithFX_AndCutAudio(panel3, finalPos3, isPunch: false, cutIndex: 3);

        yield return new WaitForSeconds(sceneTransitionDelay);
        SceneManager.LoadScene(mainSceneName);
    }

    IEnumerator SlideInWithFX_AndCutAudio(RectTransform panel, Vector2 finalPos, bool isPunch, int cutIndex)
    {
        if (panel == null) yield break;

        // ✅ 컷 등장과 "동시에" 오디오 시작
        float waitAudio = StartCutAudioReturnWaitSeconds(cutIndex);

        // ✅ 슬라이드/연출은 그대로 진행(오디오는 같이 재생됨)
        yield return SlideInWithFX(panel, finalPos, isPunch);

        // ✅ 다음 컷으로 넘어가기 전에(겹치지 않게) 남은 오디오가 끝날 때까지 대기
        // - 슬라이드 시간이 오디오보다 길면 waitAudio - slideFX시간이 음수일 수 있는데,
        //   여기서는 "슬라이드가 끝난 시점" 기준으로 '남은 오디오'만 기다리려고 별도 계산을 하지 않고,
        //   그냥 오디오 전체 길이만큼 기다리면 슬라이드가 끝난 뒤 추가 대기가 생길 수 있음.
        //   그래서 "슬라이드 시작과 동시에 오디오 시작"을 고려해 남은 시간을 계산한다.
        //   -> SlideInWithFX 내부 총 재생 시간을 추정해서 빼줌.
        float fxTime = GetFxDurationEstimate(isPunch);
        float remaining = waitAudio - fxTime;
        if (remaining > 0f) yield return new WaitForSeconds(remaining);
    }

    float StartCutAudioReturnWaitSeconds(int cutIndex)
    {
        if (audioSource == null) return 0f;

        AudioClip intro = null;
        if (cutIndex == 1) intro = intro_1;
        else if (cutIndex == 2) intro = intro_2;
        else intro = null; // 3컷은 intro 없음

        // 둘 다 동시에 시작
        if (swish != null) audioSource.PlayOneShot(swish);
        if (intro != null) audioSource.PlayOneShot(intro);

        // "다 끝난 뒤" 넘어가야 하므로 더 긴 쪽 길이를 기다림
        float lenA = swish != null ? swish.length : 0f;
        float lenB = intro != null ? intro.length : 0f;
        float wait = Mathf.Max(lenA, lenB);

        if (maxWaitCutAudioSec > 0f) wait = Mathf.Min(wait, maxWaitCutAudioSec);
        return wait;
    }

    // SlideInWithFX가 대략 얼마나 걸리는지(오디오 남은 시간 계산용) 추정
    float GetFxDurationEstimate(bool isPunch)
    {
        // SlideTo: slideDuration + settleDuration
        // Shake: shakeDuration
        // Pop: popDuration
        float t = slideDuration + settleDuration + shakeDuration + popDuration;
        return t;
    }

    IEnumerator SlideInWithFX(RectTransform panel, Vector2 finalPos, bool isPunch)
    {
        if (panel == null) yield break;

        // 페이지 플립은 컷 등장과 동시에 "겹쳐도 되는" 효과음 (원하면 빼세요)
        PlaySfx(pageFlip);

        yield return SlideTo(panel, finalPos);

        if (isPunch) PlaySfx(punch);
        yield return StartCoroutine(Shake());

        yield return StartCoroutine(Pop(panel));
    }

    IEnumerator SlideTo(RectTransform rt, Vector2 target)
    {
        Vector2 start = rt.anchoredPosition;

        float dir = Mathf.Sign(target.x - start.x);
        if (dir == 0) dir = 1f;

        Vector2 overshootTarget = new Vector2(target.x + (overshootPx * dir), target.y);

        float t = 0f;
        while (t < slideDuration)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / slideDuration);

            float k = 1f - Mathf.Pow(1f - u, 2.4f);

            rt.anchoredPosition = Vector2.Lerp(start, overshootTarget, k);

            float s = Mathf.Sin(u * Mathf.PI);
            float stretch = 1f + (stretchAmount * s);
            float squash = 1f - (stretchAmount * 0.6f * s);
            rt.localScale = new Vector3(stretch, squash, 1f);

            yield return null;
        }

        t = 0f;
        Vector2 from = rt.anchoredPosition;
        Vector3 scaleFrom = rt.localScale;

        while (t < settleDuration)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / settleDuration);
            float k = 1f - Mathf.Pow(1f - u, 3f);

            rt.anchoredPosition = Vector2.Lerp(from, target, k);
            rt.localScale = Vector3.Lerp(scaleFrom, Vector3.one, k);

            yield return null;
        }

        rt.anchoredPosition = target;
        rt.localScale = Vector3.one;
    }

    IEnumerator Pop(RectTransform rt)
    {
        rt.localScale = Vector3.one * popScale;

        float t = 0f;
        Vector3 start = rt.localScale;
        Vector3 end = Vector3.one;

        while (t < popDuration)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / popDuration);
            rt.localScale = Vector3.Lerp(start, end, u);
            yield return null;
        }
        rt.localScale = end;
    }

    IEnumerator Shake()
    {
        if (shakeTarget == null) yield break;

        float t = 0f;
        Vector2 origin = shakeOrigin;

        while (t < shakeDuration)
        {
            t += Time.deltaTime;
            float x = Random.Range(-shakeStrength, shakeStrength);
            float y = Random.Range(-shakeStrength, shakeStrength);
            shakeTarget.anchoredPosition = origin + new Vector2(x, y);
            yield return null;
        }

        shakeTarget.anchoredPosition = origin;
    }

    void PlaySfx(AudioClip clip)
    {
        if (audioSource == null || clip == null) return;
        audioSource.PlayOneShot(clip);
    }
}
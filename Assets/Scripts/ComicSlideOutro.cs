using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class ComicSlideOutro : MonoBehaviour
{
    [Header("GameOver 시 숨길 것(선택)")]
    public GameObject topBarRoot; // 여기서도 숨길 수 있게(없으면 무시)

    [Header("Panels")]
    public RectTransform panel1;
    public RectTransform panel2;

    [Header("Final positions (anchoredPosition)")]
    public Vector2 finalPos1;
    public Vector2 finalPos2;

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
    public RectTransform shakeTarget;
    public float shakeDuration = 0.12f;
    public float shakeStrength = 12f;

    [Header("SFX")]
    public AudioSource audioSource;
    public AudioClip pageFlip;
    public AudioClip punch;

    public System.Action OnFinished;

    bool started = false;
    float offscreenX;
    Vector2 shakeOrigin;

    void Awake()
    {
        if (slideEase == null) slideEase = AnimationCurve.EaseInOut(0, 0, 1, 1);

        // 시작 시 패널 끄기
        if (panel1 != null) panel1.gameObject.SetActive(false);
        if (panel2 != null) panel2.gameObject.SetActive(false);

        if (shakeTarget != null) shakeOrigin = shakeTarget.anchoredPosition;

        RectTransform root = GetRootRect(panel1) ?? GetRootRect(panel2);
        float rootWidth = root != null ? root.rect.width : 1080f;
        offscreenX = (rootWidth * 0.55f) + offscreenMargin;

        gameObject.SetActive(false); // 기본은 꺼두는 걸 추천(필요시 제거)
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

        gameObject.SetActive(true);

        StartCoroutine(Sequence());
    }

    public void ResetState()
    {
        StopAllCoroutines();
        started = false;

        if (panel1 != null) panel1.gameObject.SetActive(false);
        if (panel2 != null) panel2.gameObject.SetActive(false);

        if (shakeTarget != null) shakeTarget.anchoredPosition = shakeOrigin;

        gameObject.SetActive(false);
    }

    IEnumerator Sequence()
    {
        // panel1 (오른쪽 -> 중앙)
        if (panel1 != null)
        {
            panel1.gameObject.SetActive(true);
            PrepPanel(panel1, finalPos1, +1f);
        }
        yield return SlideInWithFX(panel1, finalPos1, isPunch: false);
        yield return new WaitForSeconds(gapDelay);

        // panel2 (왼쪽 -> 중앙)
        if (panel2 != null)
        {
            panel2.gameObject.SetActive(true);
            PrepPanel(panel2, finalPos2, -1f);
        }
        yield return SlideInWithFX(panel2, finalPos2, isPunch: true);
        yield return new WaitForSeconds(gapDelay);

        // 끝났으면 콜백
        OnFinished?.Invoke();
    }

    IEnumerator SlideInWithFX(RectTransform panel, Vector2 finalPos, bool isPunch)
    {
        if (panel == null) yield break;

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
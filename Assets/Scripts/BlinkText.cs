using UnityEngine;
using TMPro;
using System.Collections;

public class BlinkText : MonoBehaviour
{
    public float fadeDuration = 1.0f;   // 사라졌다/나타나는 시간
    public float minAlpha = 0.2f;        // 완전 투명은 싫으면 0.2 정도
    public float maxAlpha = 1.0f;

    private TextMeshProUGUI text;

    void Awake()
    {
        text = GetComponent<TextMeshProUGUI>();
        StartCoroutine(Blink());
    }

    IEnumerator Blink()
    {
        while (true)
        {
            // Fade Out
            yield return Fade(maxAlpha, minAlpha);
            // Fade In
            yield return Fade(minAlpha, maxAlpha);
        }
    }

    IEnumerator Fade(float from, float to)
    {
        float t = 0f;

        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            float alpha = Mathf.Lerp(from, to, t / fadeDuration);

            Color c = text.color;
            c.a = alpha;
            text.color = c;

            yield return null;
        }
    }
}

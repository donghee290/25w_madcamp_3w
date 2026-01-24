using UnityEngine;
using TMPro;

public class ChaserDistanceUI : MonoBehaviour
{
    [Header("Refs")]
    public RunChaserSystem chaser;
    public TextMeshProUGUI distanceText;

    [Header("Color")]
    public Color safeColor = new Color(0.2f, 0.6f, 1f);   // 파랑
    public Color warningColor = Color.yellow;            // 노랑
    public Color dangerColor = Color.red;                 // 빨강

    [Header("Shake")]
    public float shakeStartDistance = 1.5f;   // 이 거리 이하부터 흔들림
    public float shakeAmount = 6f;             // 흔들림 세기

    Vector3 basePos;

    void Start()
    {
        // 원래 위치 저장 (흔들림 복구용)
        basePos = distanceText.rectTransform.anchoredPosition;
    }

    void Update()
    {
        if (chaser == null || distanceText == null) return;

        float d = chaser.chaserDistance;
        float max = chaser.maxDistance;

        // 1️⃣ 텍스트 갱신
        distanceText.text = $"학주 거리 : {d:0.0}m";

        // 2️⃣ 색상 연출
        // t = 0 (안 위험) → t = 1 (아주 위험)
        float t = Mathf.InverseLerp(max, 0f, d);

        if (t < 0.5f)
        {
            // 파랑 → 노랑
            distanceText.color = Color.Lerp(safeColor, warningColor, t * 2f);
        }
        else
        {
            // 노랑 → 빨강
            distanceText.color = Color.Lerp(warningColor, dangerColor, (t - 0.5f) * 2f);
        }

        // 3️⃣ 흔들림 연출
        if (d <= shakeStartDistance)
        {
            Vector2 shake = Random.insideUnitCircle * shakeAmount;
            distanceText.rectTransform.anchoredPosition = basePos + (Vector3)shake;
        }
        else
        {
            distanceText.rectTransform.anchoredPosition = basePos;
        }
    }
}

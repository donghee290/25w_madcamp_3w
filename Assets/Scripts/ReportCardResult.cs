using TMPro;
using UnityEngine;

public class ReportCardResult : MonoBehaviour
{
    [Header("TMP (Result Popup)")]
    [SerializeField] private TMP_Text distanceTMP;   // ReportCardPopup 안 거리 값 TMP
    [SerializeField] private TMP_Text coinTMP;       // ReportCardPopup 안 코인 값 TMP
    [SerializeField] private TMP_Text caloriesTMP;   // ReportCardPopup 안 칼로리 값 TMP

    [Header("Formatting")]
    [Tooltip("게임 내 DistanceMeters가 어떤 단위인지에 따라 조절하세요. (현재 HUD는 *0.1 해서 m 표시중)")]
    public float hudMeterScale = 0.1f;

    [Tooltip("달리기 칼로리 추정: kcal = kg * km * 계수(대략 1.0 전후).")]
    public float assumedWeightKg = 65f;

    [Tooltip("러닝 kcal 추정 계수(대충 1.0 근처). 1.036은 자주 쓰는 근사치.")]
    public float kcalPerKgPerKm = 1.036f;

    public void RefreshFinal()
    {
        if (GameManager.I == null) return;

        // 1) 최종 거리
        // HUD랑 동일하게 보이게 하려면 *0.1을 그대로 적용
        float metersShown = GameManager.I.DistanceMeters * hudMeterScale;
        int m = Mathf.FloorToInt(metersShown);

        // 2) 최종 코인
        int coins = CoinCounter.Count;

        // 3) 칼로리(대충): km로 변환 후 kg*km*계수
        float km = metersShown / 1000f;
        int kcal = Mathf.Max(0, Mathf.RoundToInt(assumedWeightKg * km * kcalPerKgPerKm));

        if (distanceTMP) distanceTMP.text = $"{m} m";
        if (coinTMP) coinTMP.text = $"{coins}";
        if (caloriesTMP) caloriesTMP.text = $"{kcal} kcal";
    }

    // 팝업이 켜질 때 자동으로 갱신
    void OnEnable()
    {
        RefreshFinal();
    }
}
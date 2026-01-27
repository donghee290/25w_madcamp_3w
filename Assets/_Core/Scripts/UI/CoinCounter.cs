using TMPro;
using UnityEngine;

public class CoinCounter : MonoBehaviour
{
    public static int Count { get; private set; }

    [Header("UI")]
    public TMP_Text coinText;

    private static CoinCounter instance;

    void Awake()
    {
        // 씬에 여러 개 있으면 하나만 남김
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        Count = 0;
        Refresh();
        Debug.Log("[CoinCounter] Awake OK");
    }

    public static void Add(int amount)
    {
        // instance가 비어있으면 씬에서 찾아서라도 연결
        if (instance == null)
            instance = FindObjectOfType<CoinCounter>();

        Count += amount;
        if (Count < 0) Count = 0;

        if (instance != null)
        {
            instance.Refresh();
            Debug.Log($"[CoinCounter] Add -> {Count}");
        }
        else
        {
            Debug.LogWarning("[CoinCounter] instance not found in scene!");
        }
    }

    void Refresh()
    {
        if (coinText != null)
            coinText.text = $"COIN : {Count}";
        else
            Debug.LogWarning("[CoinCounter] coinText is null!");
    }
}

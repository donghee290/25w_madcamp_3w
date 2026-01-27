using UnityEngine;

public class BananaPickup : MonoBehaviour
{
    public ChaserSystem chaser;

    [Header("Drop Visual (optional)")]
    public GameObject bananaDropPrefab;     // chaser 앞에 떨어지는 연출용
    public float dropForwardOffset = 1.0f;  // chaser 앞 거리
    public float dropY = 0.1f;

    void Awake()
    {
        if (chaser == null) chaser = FindFirstObjectByType<ChaserSystem>();
    }

    void OnTriggerEnter(Collider other)
    {
        // 플레이어만 먹게
        var pm = other.GetComponentInParent<PlayerMotor>();
        if (pm == null) return;

        // 1) 즉시 스턴 + 넘어짐 애니(ChaserSystem에서 처리)
        if (chaser != null)
            chaser.ApplyBananaStun(chaser.bananaStunSeconds);

        // 2) chaser 앞에 바나나 드랍(연출)
        if (bananaDropPrefab != null && chaser != null && chaser.chaserRoot != null)
        {
            Transform c = chaser.chaserRoot;

            Vector3 dropPos = c.position + c.forward * dropForwardOffset;
            dropPos.y = dropY;

            Instantiate(bananaDropPrefab, dropPos, Quaternion.identity);
        }

        Destroy(gameObject);
    }
}
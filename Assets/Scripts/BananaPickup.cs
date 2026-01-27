using UnityEngine;

public class BananaPickup : MonoBehaviour
{
    [Header("Refs (optional)")]
    public ChaserSystem chaser;

    [Header("Drop Visual (optional)")]
    public GameObject bananaDropPrefab;     // chaser 앞에 떨어지는 연출용
    public float dropForwardOffset = 1.0f;  // chaser 앞 거리
    public float dropY = 0.1f;

    [Header("Debug")]
    public bool debug = true;

    void Awake()
    {
        if (chaser == null) chaser = FindFirstObjectByType<ChaserSystem>();

        if (debug)
        {
            var col = GetComponent<Collider>();
            var rb = GetComponent<Rigidbody>();
        }
    }

    void OnTriggerEnter(Collider other)
    {

        // 플레이어 판정(자식 콜라이더 대응)
        var pm = other.GetComponentInParent<PlayerMotor>();
        if (pm == null)
        {
            return;
        }

        // chaser 확보
        if (chaser == null) chaser = FindFirstObjectByType<ChaserSystem>();

        // 1) 스턴 적용(핵심)
        if (chaser != null)
        {
            chaser.ApplyBananaStun(chaser.bananaStunSeconds);
        }

        // 2) chaser 앞에 바나나 드랍(연출)
        if (bananaDropPrefab != null && chaser != null && chaser.chaserRoot != null)
        {
            Transform c = chaser.chaserRoot;

            Vector3 dropPos = c.position + c.forward * dropForwardOffset;
            dropPos.y = dropY;

            var go = Instantiate(bananaDropPrefab, dropPos, Quaternion.identity);

        }

        if (debug) Debug.Log("[BananaPickup] Destroy pickup");
        Destroy(gameObject);
    }
}
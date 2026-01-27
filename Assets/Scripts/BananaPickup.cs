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
            Debug.Log($"[BananaPickup] Awake on '{name}' active={gameObject.activeInHierarchy}");
            Debug.Log($"[BananaPickup] Found chaser={(chaser ? chaser.name : "NULL")}");
            var col = GetComponent<Collider>();
            Debug.Log($"[BananaPickup] Collider={(col ? col.GetType().Name : "NULL")} isTrigger={(col ? col.isTrigger : false)} enabled={(col ? col.enabled : false)}");
            var rb = GetComponent<Rigidbody>();
            Debug.Log($"[BananaPickup] Rigidbody={(rb ? "YES" : "NO")} (kinematic={(rb ? rb.isKinematic : false)})");
        }
    }

    void OnEnable()
    {
        if (debug) Debug.Log($"[BananaPickup] OnEnable '{name}'");
    }

    void OnTriggerEnter(Collider other)
    {
        if (debug) Debug.Log($"[BananaPickup] OnTriggerEnter by '{other.name}' (tag={other.tag})");

        // 플레이어 판정(자식 콜라이더 대응)
        var pm = other.GetComponentInParent<PlayerMotor>();
        if (pm == null)
        {
            if (debug) Debug.Log("[BananaPickup] PlayerMotor NOT found in parent chain -> return");
            return;
        }

        if (debug) Debug.Log($"[BananaPickup] PICKED by '{other.name}' (pm={pm.name})");

        // chaser 확보
        if (chaser == null) chaser = FindFirstObjectByType<ChaserSystem>();
        if (debug) Debug.Log($"[BananaPickup] chaser={(chaser ? chaser.name : "NULL")}");

        // 1) 스턴 적용(핵심)
        if (chaser != null)
        {
            // 호출 전 상태
            if (debug)
            {
                Debug.Log($"[BananaPickup] BEFORE stun: now={Time.time:0.00} IsBananaStunned={chaser.IsBananaStunned} stunSec={chaser.bananaStunSeconds}");
            }

            chaser.ApplyBananaStun(chaser.bananaStunSeconds);

            // 호출 후 상태(이게 true로 안 바뀌면 ChaserSystem 쪽 문제/다른 인스턴스)
            if (debug)
            {
                Debug.Log($"[BananaPickup] AFTER stun: now={Time.time:0.00} IsBananaStunned={chaser.IsBananaStunned}");
                Debug.Log($"[BananaPickup] chaser instanceID={chaser.GetInstanceID()}");
            }
        }
        else
        {
            if (debug) Debug.LogWarning("[BananaPickup] chaser is NULL -> stun skipped");
        }

        // 2) chaser 앞에 바나나 드랍(연출)
        if (bananaDropPrefab != null && chaser != null && chaser.chaserRoot != null)
        {
            Transform c = chaser.chaserRoot;

            Vector3 dropPos = c.position + c.forward * dropForwardOffset;
            dropPos.y = dropY;

            var go = Instantiate(bananaDropPrefab, dropPos, Quaternion.identity);

            if (debug)
            {
                Debug.Log($"[BananaPickup] Spawned BananaDrop '{go.name}' at {dropPos} (forwardOffset={dropForwardOffset}, y={dropY})");
            }
        }
        else
        {
            if (debug)
            {
                Debug.Log($"[BananaPickup] Drop skipped: prefab={(bananaDropPrefab ? "OK" : "NULL")}, chaser={(chaser ? "OK" : "NULL")}, chaserRoot={(chaser != null && chaser.chaserRoot != null ? "OK" : "NULL")}");
            }
        }

        if (debug) Debug.Log("[BananaPickup] Destroy pickup");
        Destroy(gameObject);
    }
}
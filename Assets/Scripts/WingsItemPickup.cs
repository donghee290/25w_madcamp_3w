using UnityEngine;

public class WingsItemPickup : MonoBehaviour
{
    public float durationSec = 10f;

    [Header("Auto")]
    public GameObject visualRoot; // 비우면 부모의 Visual 자동 탐색

    bool used;

    void Awake()
    {
        if (visualRoot == null)
        {
            var p = transform.parent;
            if (p != null)
            {
                var v = p.Find("Visual");
                if (v != null) visualRoot = v.gameObject;
            }
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (used) return;

        Debug.Log($"[WingsPickup] hit={other.name} root={other.transform.root.name}");

        // 플레이어 루트에서 WingsBuffController 찾기
        var buff = other.GetComponentInParent<WingsBuffController>();
        if (buff == null) return;

        used = true;

        // 즉시 적용 (남은 시간 리셋 방식)
        buff.ApplyWings(durationSec);

        // 아이템 즉시 사라짐
        if (visualRoot != null) visualRoot.SetActive(false);

        var col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        // 프리팹 루트(Item_Wings) 제거
        Destroy(transform.parent != null ? transform.parent.gameObject : gameObject, 0.05f);
    }
}
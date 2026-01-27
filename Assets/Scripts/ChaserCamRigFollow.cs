using UnityEngine;

public class ChaserCamRigFollow : MonoBehaviour
{
    [Header("Refs")]
    public Transform target;          // ChaserRoot
    public Transform lookTarget;      // PlayerRoot
    public ChaserSystem chaserSystem; // ✅ 바나나 상태 확인용
    public GameObject camObj;         // ChaserCam

    [Header("Follow")]
    public Vector3 localOffset = new Vector3(0f, 1.8f, 2.0f);
    public float lookHeight = 1.5f;
    public float followSharpness = 12f;

    [Header("Danger")]
    [Range(0f, 1f)]
    public float dangerThreshold = 0.5f; // 🔥 기존에 쓰던 그 값

    void Awake()
    {
        if (chaserSystem == null)
            chaserSystem = FindFirstObjectByType<ChaserSystem>();

        if (camObj == null)
        {
            var cam = GetComponentInChildren<Camera>(true);
            if (cam != null) camObj = cam.gameObject;
        }
    }

    void LateUpdate()
    {
        if (!target) return;

        // =========================
        // 1. 원래 하던 Follow (절대 수정 ❌)
        // =========================
        Vector3 desiredPos = target.position
                             + target.right * localOffset.x
                             + Vector3.up * localOffset.y
                             + target.forward * localOffset.z;

        float t = 1f - Mathf.Exp(-followSharpness * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, desiredPos, t);

        Transform lt = lookTarget ? lookTarget : target;
        Vector3 lookAt = lt.position + Vector3.up * lookHeight;
        Quaternion desiredRot =
            Quaternion.LookRotation((lookAt - transform.position).normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRot, t);

        // =========================
        // 2. 카메라 ON 조건
        // =========================

        bool nearDanger = false;
        bool bananaEvent = false;

        if (chaserSystem != null)
        {
            // 🔥 이 값은 "원래 잘 되던 0~1 위험도"
            float danger01 = 1f - Mathf.Clamp01(
                chaserSystem.chaserDistance / chaserSystem.maxDistance
            );

            nearDanger = danger01 >= dangerThreshold;

            // 🍌 바나나 스턴 중이면 무조건 ON
            bananaEvent = chaserSystem.IsBananaStunned;
        }

        bool camOn = nearDanger || bananaEvent;

        if (camObj != null && camObj.activeSelf != camOn)
            camObj.SetActive(camOn);
    }
}
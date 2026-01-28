using UnityEngine;

public class ChaserCamRigFollow : MonoBehaviour
{
    [Header("Refs")]
    public Transform target;          // ChaserRoot
    public ChaserSystem chaserSystem; // 바나나/거리 상태
    public GameObject camObj;         // ChaserCam

    [Header("Follow")]
    public Vector3 localOffset = new Vector3(0f, 1.8f, 2.0f);
    public float followSharpness = 12f;

    [Header("Look (Chaser-based)")]
    [Tooltip("Chaser를 바라볼 때 위로 올리는 높이")]
    public float lookHeightFromTarget = 1.5f;

    [Header("Danger")]
    [Range(0f, 1f)]
    public float dangerThreshold = 0.5f;

    void Awake()
    {
        BindRefsIfNeeded();
    }

    void BindRefsIfNeeded()
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

        // 씬 재로드/비활성화 등으로 참조 끊기는 케이스 방어
        if (chaserSystem == null) BindRefsIfNeeded();

        // =========================
        // 1) Follow (position)
        // =========================
        Vector3 desiredPos =
            target.position
            + target.right * localOffset.x
            + Vector3.up * localOffset.y
            + target.forward * localOffset.z;

        float t = 1f - Mathf.Exp(-followSharpness * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, desiredPos, t);

        // =========================
        // 2) Rotation: lookTarget 제거 (Chaser만 봄)
        // =========================
        Vector3 lookAt = target.position + Vector3.up * lookHeightFromTarget;

        Vector3 dir = lookAt - transform.position;
        if (dir.sqrMagnitude > 0.0001f)
        {
            Quaternion desiredRot = Quaternion.LookRotation(dir.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRot, t);
        }

        // =========================
        // 3) Camera ON 조건
        // =========================
        bool nearDanger = false;
        bool bananaEvent = false;

        if (chaserSystem != null)
        {
            float danger01 = 1f - Mathf.Clamp01(chaserSystem.chaserDistance / chaserSystem.maxDistance);
            nearDanger = danger01 >= dangerThreshold;

            // 바나나 스턴 중이면 무조건 ON
            bananaEvent = chaserSystem.IsBananaStunned;
        }

        bool camOn = nearDanger || bananaEvent;

        if (camObj != null && camObj.activeSelf != camOn)
            camObj.SetActive(camOn);
    }
}
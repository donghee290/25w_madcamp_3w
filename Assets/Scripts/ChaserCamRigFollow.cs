using UnityEngine;

public class ChaserCamRigFollow : MonoBehaviour
{
    [Header("Refs")]
    public Transform target;              // ChaserRoot
    public ChaserSystem chaserSystem;     // banana 상태 확인용
    public GameObject camObj;             // ChaserCam (카메라 GO) - Camera 컴포넌트가 달린 오브젝트

    [Header("Follow")]
    public Vector3 localOffset = new Vector3(0f, 2.6f, 3.5f);
    public float followSharpness = 10f;

    [Header("Danger (0~1)")]
    [Range(0f, 1f)]
    public float dangerThreshold = 0.5f;

    [Header("Hold (flicker 방지)")]
    public float minOnTime = 0.35f;
    float onUntilTime = -1f;

    [Header("Rotation Fixed (LookRotation 없음)")]
    public Vector3 fixedEuler = new Vector3(10f, 180f, 0f);

    Camera cam;

    void Awake()
    {
        if (chaserSystem == null)
            chaserSystem = FindFirstObjectByType<ChaserSystem>();

        // camObj가 비었으면 자식/자기에서 Camera를 찾아 camObj로 지정
        if (camObj == null)
        {
            var found = GetComponentInChildren<Camera>(true);
            if (found != null) camObj = found.gameObject;
        }

        CacheCamera();
        // 시작은 꺼둠 (MainCam 건드리지 않음)
        if (cam != null) cam.enabled = false;
    }

    void CacheCamera()
    {
        cam = null;

        if (camObj != null)
        {
            cam = camObj.GetComponent<Camera>();
            if (cam == null) cam = camObj.GetComponentInChildren<Camera>(true);
        }

        // 혹시 camObj를 못 잡았으면 마지막 fallback
        if (cam == null)
        {
            var found = GetComponentInChildren<Camera>(true);
            if (found != null)
            {
                cam = found;
                camObj = found.gameObject;
            }
        }
    }

    void LateUpdate()
    {
        if (!target) return;

        // 0) cam 레퍼런스가 중간에 날아갔으면 복구
        if (cam == null)
            CacheCamera();

        // 1) 위치 follow (원래 로직 유지)
        Vector3 desiredPos = target.position
                             + target.right * localOffset.x
                             + Vector3.up * localOffset.y
                             + target.forward * localOffset.z;

        float t = 1f - Mathf.Exp(-followSharpness * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, desiredPos, t);

        // 2) 회전 완전 고정 (LookRotation 없음)
        transform.rotation = Quaternion.Euler(fixedEuler);

        // 3) 카메라 ON 조건 (danger01 + banana)
        bool nearDanger = false;
        bool bananaEvent = false;

        if (chaserSystem != null)
        {
            // 가까울수록 1이 되는 위험도(0~1)
            float danger01 = 1f - Mathf.Clamp01(chaserSystem.chaserDistance / chaserSystem.maxDistance);
            nearDanger = danger01 >= dangerThreshold;

            bananaEvent = chaserSystem.IsBananaStunned;
        }

        bool shouldOn = nearDanger || bananaEvent;

        // 4) 홀드(깜빡임 방지)
        if (shouldOn) onUntilTime = Time.time + minOnTime;
        bool finalOn = Time.time <= onUntilTime;

        // 5) MainCam은 절대 건드리지 않고, 이 카메라만 enabled 토글
        if (cam != null && cam.enabled != finalOn)
            cam.enabled = finalOn;
    }
}
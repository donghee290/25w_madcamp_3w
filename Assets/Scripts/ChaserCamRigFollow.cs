using UnityEngine;

public class ChaserCamRigFollow : MonoBehaviour
{
    [Header("Refs")]
    public Transform target;       // ChaserRoot(Granny)
    public Transform lookTarget;   // PlayerRoot
    public ChaserSystem chaserSystem;

    [Header("Follow")]
    public Vector3 localOffset = new Vector3(0f, 1.8f, 2.0f);
    public float lookHeight = 1.5f;
    public float followSharpness = 12f;

    [Header("Enable Conditions")]
    public float dangerDistanceThreshold = 0.5f;

    [Tooltip("조건이 꺼져도 이 시간만큼은 카메라를 유지(깜빡임 방지)")]
    public float minOnTime = 0.35f;

    float onUntilTime;

    void Awake()
    {
        if (chaserSystem == null)
            chaserSystem = FindFirstObjectByType<ChaserSystem>();
    }

    void LateUpdate()
    {
        // 1) 카메라 ON 조건 계산
        bool nearDanger = false;
        bool bananaEvent = false;

        if (chaserSystem != null)
        {
            nearDanger = chaserSystem.chaserDistance <= dangerDistanceThreshold;
            bananaEvent = chaserSystem.IsBananaStunned; // 바나나 스턴 동안
        }

        bool shouldOn = nearDanger || bananaEvent;

        // 깜빡임 방지
        if (shouldOn) onUntilTime = Time.time + minOnTime;
        bool finalOn = Time.time <= onUntilTime;

        // 2) 카메라(리그) 활성/비활성
        if (gameObject.activeSelf != finalOn)
            gameObject.SetActive(finalOn);

        // 비활성 전환 프레임엔 아래 로직 실행하면 문제 생길 수 있어 early return
        if (!finalOn) return;

        // 3) 기존 follow 로직
        if (!target) return;

        Vector3 desiredPos = target.position
                             + target.right * localOffset.x
                             + Vector3.up * localOffset.y
                             + target.forward * localOffset.z;

        float t = 1f - Mathf.Exp(-followSharpness * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, desiredPos, t);

        Transform lt = lookTarget ? lookTarget : target;
        Vector3 lookAt = lt.position + Vector3.up * lookHeight;
        Quaternion desiredRot = Quaternion.LookRotation((lookAt - transform.position).normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRot, t);
    }
}
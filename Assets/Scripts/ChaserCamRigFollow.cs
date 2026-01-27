using UnityEngine;

public class ChaserCamRigFollow : MonoBehaviour
{
    [Header("Refs")]
    public Transform target;              // ChaserRoot(Granny)
    public Transform lookTarget;          // PlayerRoot
    public ChaserSystem chaserSystem;

    [Tooltip("실제로 켜고/끄고 싶은 카메라 오브젝트(예: ChaserCam)")]
    public GameObject camObj;

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

        // camObj 미지정이면 자기/자식에서 Camera 찾아서 그 GO를 잡음
        if (camObj == null)
        {
            var cam = GetComponentInChildren<Camera>(true);
            if (cam != null) camObj = cam.gameObject;
        }
    }

    void LateUpdate()
    {
        if (!target) return;

        // 1) 켜야 하는지 판단
        bool nearDanger = false;
        bool bananaEvent = false;

        if (chaserSystem != null)
        {
            nearDanger = chaserSystem.chaserDistance <= dangerDistanceThreshold;
            bananaEvent = chaserSystem.IsBananaStunned; // 바나나 스턴 동안도
        }

        bool shouldOn = nearDanger || bananaEvent;

        // 깜빡임 방지
        if (shouldOn) onUntilTime = Time.time + minOnTime;
        bool finalOn = Time.time <= onUntilTime;

        // 2) 카메라만 토글
        if (camObj != null && camObj.activeSelf != finalOn)
            camObj.SetActive(finalOn);

        // 3) 리그는 항상 따라가게(카메라 꺼져도 위치는 갱신해둠)
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
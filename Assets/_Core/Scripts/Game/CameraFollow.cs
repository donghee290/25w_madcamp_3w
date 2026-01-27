using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;

    [Tooltip("월드 오프셋(기본 카메라 위치). Rig에 붙여쓰는 경우도 OK")]
    public Vector3 offset = new Vector3(0, 1.3f, -10.5f);

    public float followLerp = 10f;

    public PlayerMotor motor;
    public float flyCamAddY = 0.6f;

    [Header("Lock Y")]
    public bool lockY = true;
    public float fixedY = 1.3f;

    [Header("Follow Axis")]
    public bool followZOnly = true;        // ★ 러너면 보통 Z만 따라가게
    public bool lookAtTarget = true;       // ★ 회전도 원하면 켜기
    public float lookAtUp = 1.0f;

    // === 내부 캐시: "원래 상태" 유지용 ===
    Vector3 basePos;
    Quaternion baseRot;
    float targetZ0;

    void Awake()
    {
        basePos = transform.position;      // ★ 시작 시점 위치 고정(0,0,0이든 뭐든)
        baseRot = transform.rotation;      // ★ 시작 시점 회전 고정
        if (target) targetZ0 = target.position.z;
    }

    void LateUpdate()
    {
        if (!target) return;

        // Rig(루트)은 "처음 위치"를 기준으로만 움직이게(인스펙터 값이 지혼자 바뀌는 현상 방지)
        Vector3 desired = basePos + offset;

        if (followZOnly)
        {
            float dz = target.position.z - targetZ0;
            desired.z = (basePos + offset).z + dz;  // ★ Z만 따라감
            desired.x = (basePos + offset).x;       // ★ X는 고정(레인 카메라 흔들림 방지)
        }
        else
        {
            // 전체 따라가기(원하면 사용)
            desired = target.position + offset;
        }

        // Y 처리
        if (motor != null && motor.IsFlying)
        {
            desired.y = (basePos + offset).y + flyCamAddY; // ★ 비행 때만 살짝 위
        }
        else
        {
            if (lockY) desired.y = fixedY;
        }

        transform.position = Vector3.Lerp(transform.position, desired, Time.deltaTime * followLerp);

        // 루트 회전 고정 + LookAt은 선택(루트 회전이 꼬이는 문제 방지)
        transform.rotation = baseRot;

        if (lookAtTarget)
        {
            // LookAt을 루트에 적용하면 baseRot을 덮어쓰니, "원하면" 아래 2줄 중 택1
            // 1) 루트 회전도 같이 움직이게 하고 싶으면 위의 transform.rotation = baseRot; 줄을 삭제하세요.
            // 2) 루트 회전은 고정하면서 시선만 바꾸고 싶으면 "Main Camera(자식)"에 LookAt을 걸어야 합니다.
            //
            // 지금은 안전하게: 루트는 고정(권장). 자식 카메라로 LookAt 하고 싶으면 아래 주석 참고.

            // (권장) Main Camera가 자식이면, 여기에 할당해서 그 Transform에 LookAt:
            // public Transform cam; 추가하고 cam.LookAt(target.position + Vector3.up * lookAtUp);

            // 루트에 LookAt을 걸고 싶으면(루트 회전 고정 줄 삭제하고) 아래 한 줄만 사용:
            // transform.LookAt(target.position + Vector3.up * lookAtUp);
        }
    }
}
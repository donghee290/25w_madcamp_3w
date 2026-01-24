using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public Vector3 offset = new Vector3(0, 6f, -10f);
    public float followLerp = 10f;

    [Header("Framing")]
    public float lookAtHeight = 0.6f;   // ✅ 여기! 낮출수록 캐릭터가 화면 위로 올라감
    public bool lockRotation = false;
    public Vector3 fixedEuler = new Vector3(25f, 0f, 0f); // lockRotation=true일 때 사용

    void LateUpdate()
    {
        if (target == null) return;

        // 위치 따라가기
        Vector3 desired = target.position + offset;
        transform.position = Vector3.Lerp(transform.position, desired, Time.deltaTime * followLerp);

        // 회전
        if (lockRotation)
        {
            transform.rotation = Quaternion.Euler(fixedEuler);
        }
        else
        {
            // ✅ 전신이 잘리면 lookAtHeight를 더 낮춰봐: 0.3 ~ 0.8 추천
            transform.LookAt(target.position + Vector3.up * lookAtHeight);
        }
    }
}

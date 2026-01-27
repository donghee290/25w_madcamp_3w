using UnityEngine;

public class BananaDropFX : MonoBehaviour
{
    [Header("Lifetime")]
    public float lifeTime = 2.0f;

    [Header("Drop Motion")]
    public float startHeight = 1.2f;   // 생성 위치 기준 위로 얼마나 띄워서 시작할지
    public float dropDuration = 0.18f; // 떨어지는 시간
    public float bounceHeight = 0.15f; // 바닥 닿고 1번 튀는 높이
    public float bounceDuration = 0.12f;

    [Header("Spin")]
    public float spinSpeed = 240f;

    Vector3 basePos;
    float t;
    int phase; // 0=drop, 1=bounce, 2=settle

    void Start()
    {
        basePos = transform.position;
        transform.position = basePos + Vector3.up * startHeight;
        Destroy(gameObject, lifeTime);
    }

    void Update()
    {
        // 회전은 그냥 계속
        transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);

        // 간단 드랍 + 1회 바운스
        t += Time.deltaTime;

        if (phase == 0)
        {
            float a = Mathf.Clamp01(t / dropDuration);
            float eased = 1f - (1f - a) * (1f - a); // easeOutQuad
            transform.position = Vector3.Lerp(basePos + Vector3.up * startHeight, basePos, eased);

            if (a >= 1f)
            {
                phase = 1;
                t = 0f;
            }
        }
        else if (phase == 1)
        {
            float a = Mathf.Clamp01(t / bounceDuration);
            // 위로 갔다가 내려오는 포물선 느낌
            float y = 4f * bounceHeight * a * (1f - a);
            transform.position = basePos + Vector3.up * y;

            if (a >= 1f)
            {
                phase = 2;
                t = 0f;
                transform.position = basePos;
            }
        }
    }
}
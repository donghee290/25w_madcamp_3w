using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerCollision : MonoBehaviour
{
    [Header("Death")]
    public string obstacleTag = "Obstacle";
    public bool debugLogs = true;

    [Header("Ceiling ignore while flying (IMPORTANT)")]
    [Tooltip("Fly 중 천장으로 판정되는 충돌은 IgnoreCollision으로 끊어버립니다.")]
    public bool ignoreCeilingWhileFlying = true;

    [Tooltip("천장 레이어 마스크 (Ceiling_Collider는 반드시 Ceiling 레이어여야 함)")]
    public LayerMask ceilingLayerMask;

    [Tooltip("hit.normal.y가 이 값보다 작으면(대개 -1 근처) 천장으로 보고 무시합니다.")]
    public float ceilingNormalThreshold = -0.5f;

    [Tooltip("콜라이더 이름에 이 문자열이 포함되면 천장으로 보고 무시합니다(보조).")]
    public string ceilingNameContains = "Ceiling";

    private bool dead = false;
    private PlayerMotor motor;
    private CharacterController cc;

    private bool wasFlying = false;

    // Fly 중 Ignore 해둔 collider 목록
    private readonly HashSet<Collider> ignoredWhileFlying = new HashSet<Collider>();

    // CharacterController는 Collider 취급 가능하지만, 혹시 모를 경우 대비해서 캐시
    private Collider ccCollider;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        ccCollider = GetComponent<Collider>(); // CharacterController가 붙으면 보통 Collider로 취급됨

        motor = GetComponent<PlayerMotor>();
        if (motor == null) motor = GetComponentInParent<PlayerMotor>();

        if (debugLogs)
            Debug.Log($"[PlayerCollision] on={name} motor={(motor ? motor.name : "NULL")}");
    }

    void Update()
    {
        bool flying = (motor != null && motor.IsFlying);

        // ✅ 비행이 끝났으면, 즉시 원복
        if (wasFlying && !flying)
        {
            RestoreIgnoredCollisions();
        }

        wasFlying = flying;
    }

    void OnDisable()
    {
        RestoreIgnoredCollisions();
    }

    void OnDestroy()
    {
        RestoreIgnoredCollisions();
    }

    void RestoreIgnoredCollisions()
    {
        if (!ignoreCeilingWhileFlying) return;

        // CharacterController(Collider) 없으면 복구 불가
        if (cc == null) return;
        if (ignoredWhileFlying.Count == 0) return;

        // cc 자체를 Collider로 쓸 수 있으면 그걸 사용, 아니면 cc를 Collider로 캐스팅 시도
        Collider myCol = ccCollider != null ? ccCollider : cc as Collider;

        foreach (var col in ignoredWhileFlying)
        {
            if (col == null) continue;

            // IgnoreCollision 해제
            if (myCol != null)
                Physics.IgnoreCollision(myCol, col, false);
            else
                Physics.IgnoreCollision(cc, col, false); // Unity에서 허용되는 경우가 많음
        }

        ignoredWhileFlying.Clear();

        if (debugLogs)
            Debug.Log("[FlyIgnore] RestoreIgnoredCollisions: cleared");
    }

    bool IsCeilingCollider(Collider col)
    {
        if (col == null) return false;

        // 1) 레이어 마스크로 1차 판정 (가장 확실)
        if (ceilingLayerMask.value != 0)
        {
            int layerBit = 1 << col.gameObject.layer;
            if ((ceilingLayerMask.value & layerBit) != 0)
                return true;
        }

        // 2) 이름 포함으로 보조 판정
        if (!string.IsNullOrEmpty(ceilingNameContains) && col.name.Contains(ceilingNameContains))
            return true;

        return false;
    }

    bool IsCeilingHit(ControllerColliderHit hit)
    {
        if (hit == null || hit.collider == null) return false;

        // 레이어/이름 기반 우선 판정
        if (IsCeilingCollider(hit.collider))
            return true;

        // 3) 법선으로 추가 판정: 아래에서 위를 치면 normal.y가 -1 쪽
        if (hit.normal.y <= ceilingNormalThreshold)
            return true;

        return false;
    }

    void IgnoreCeilingCollision(Collider col)
    {
        if (!ignoreCeilingWhileFlying) return;
        if (cc == null || col == null) return;

        if (ignoredWhileFlying.Contains(col)) return;

        Collider myCol = ccCollider != null ? ccCollider : cc as Collider;

        if (myCol != null)
            Physics.IgnoreCollision(myCol, col, true);
        else
            Physics.IgnoreCollision(cc, col, true);

        ignoredWhileFlying.Add(col);

        if (debugLogs)
            Debug.Log($"[FlyIgnore] IgnoreCollision ON -> name={col.name} layer={LayerMask.LayerToName(col.gameObject.layer)}");
    }

    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (dead) return;
        if (GameManager.I == null) return;
        if (GameManager.I.State != GameState.Playing) return;
        if (hit == null || hit.collider == null) return;

        bool flying = (motor != null && motor.IsFlying);

        if (debugLogs)
        {
            var go = hit.collider.gameObject;
            Debug.Log($"[Hit] flying={flying} name={go.name} layerName={LayerMask.LayerToName(go.layer)} layerIndex={go.layer} tag={hit.collider.tag} normal={hit.normal}");
        }

        // ✅ Fly 중이면 천장 충돌은 끊고, 나머지는 죽지 않게 그냥 return
        if (flying)
        {
            if (IsCeilingHit(hit))
            {
                IgnoreCeilingCollision(hit.collider);
            }
            return;
        }

        // Fly 아닐 때만 장애물 태그로 사망 처리
        if (hit.collider.CompareTag(obstacleTag))
        {
            // ✅ 착지 직후 무적 시간
            if (Time.time < PlayerMotor.SafeUntilTime)
            {
                if (debugLogs) Debug.Log($"[Safe] ignore obstacle hit until {PlayerMotor.SafeUntilTime:F2}");
                return;
            }
            dead = true;
            GameManager.I.GameOver(GameOverReason.HitObstacle);
        }
    }
}

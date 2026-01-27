using UnityEngine;

public class ChaserSystem : MonoBehaviour
{
    [Header("Refs")]
    public PlayerMotor playerMotor;         // 필수 (PlayerRoot에 있는 PlayerMotor)
    public Transform playerRoot;            // 보통 playerMotor.transform (캐릭터 루트)
    public MonoBehaviour inputSource;       // KeyboardInput or PoseInput (IPlayerInput 구현체)
    private IPlayerInput input;

    [Header("Chaser Actor")]
    public Transform chaserRoot;            // 학주 캐릭터 루트(이 스크립트를 학주에 붙이면 비워도 됨)
    public Animator chaserAnimator;         // Run 애니메이션 적용된 Animator
    public string animParamIsRunning = "IsRunning";   // 없으면 비워도 됨
    public string animParamMoveLevel = "MoveLevel";   // BlendTree용(있으면 쓰고 없으면 비움)

    [Header("World Follow (positioning)")]
    [Tooltip("거리값(chaserDistance)을 실제 위치로 반영할지")]
    public bool applyWorldFollow = true;

    [Tooltip("학주가 플레이어 뒤에 붙는 방향. 보통 플레이어의 -forward")]
    public bool usePlayerForward = true;

    [Tooltip("레인/좌우는 따라가되, Z(앞뒤)는 거리값으로 유지")]
    public bool followPlayerX = true;

    [Tooltip("Y(높이)도 플레이어 기준으로 맞출지. 보통 false 추천(바닥 높이 고정)")]
    public bool followPlayerY = false;

    [Tooltip("학주의 고정 Y (followPlayerY=false일 때 사용)")]
    public float fixedChaserY = 0f;

    [Tooltip("스무딩(값이 클수록 빠르게 따라감)")]
    public float followSharpness = 12f;

    [Tooltip("플레이어 위치에 더해지는 추가 오프셋(예: 뒤로 좀 더, 약간 옆으로)")]
    public Vector3 extraOffset = Vector3.zero;

    [Header("Distance (meters)")]
    public float maxDistance = 15f;
    public float initialDistance = 13f;      // 0 아님!
    public float chaserDistance = 7f;       // 상태 표시용 + 실제 위치 반영에도 사용

    [Header("Distance change per second")]
    public float gainPerSec_Run = 0.2f;     // RUN이면 회복(+)
    public float losePerSec_Walk = 0.8f;    // WALK이면 감소(-)
    public float losePerSec_Stop = 1.6f;    // STOP이면 크게 감소(-)

    [Header("MoveLevel thresholds")]
    [Tooltip("이 값 이상이면 RUN")]
    public float runThreshold = 0.7f;

    [Tooltip("이 값 미만이면 STOP")]
    public float stopThreshold = 0.3f;

    [Header("Caught freeze")]
    public bool freezeOnCatch = true;
    private bool frozen = false;

    public void FreezeChaser(bool value)
    {
        frozen = value;
        if (chaserAnimator != null)
            chaserAnimator.speed = value ? 0f : 1f;
    }

    // ====== Banana Stun ======
    [Header("Banana Stun")]
    public float bananaStunSeconds = 2.0f;     // (카메라 이벤트/표시 유지 시간) 필요 없으면 1로 맞춰도 됨
    public float bananaFreezeSeconds = 1.0f;   // (추격 로직 멈추는 시간) <- 요청: 1초
    public string trigFall = "Fall";           // 넘어짐 트리거
    public string trigRun = "";                // Run 트리거가 있으면 넣고, 없으면 비워두세요

    private float bananaStunUntil = -1f;       // 이벤트(카메라) 유지
    private float bananaFreezeUntil = -1f;     // 실제 추격 멈춤(1초)
    public bool IsBananaStunned => Time.time < bananaStunUntil;

    Coroutine bananaRoutine;

    public void ApplyBananaStun(float seconds)
    {
        if (!gameObject.activeInHierarchy) return;

        // 1) 이벤트(카메라)용 유지 시간 갱신 (연속으로 먹어도 끊기지 않게)
        bananaStunUntil = Mathf.Max(bananaStunUntil, Time.time + seconds);

        // 2) 실제 추격 멈추는 시간은 "1초"로 고정 (원하면 bananaFreezeSeconds 조절)
        bananaFreezeUntil = Mathf.Max(bananaFreezeUntil, Time.time + bananaFreezeSeconds);

        // 3) Fall 트리거
        if (chaserAnimator != null && !string.IsNullOrEmpty(trigFall))
            chaserAnimator.SetTrigger(trigFall);

        // 4) 1초 뒤 Run 복귀
        if (bananaRoutine != null) StopCoroutine(bananaRoutine);
        bananaRoutine = StartCoroutine(CoBananaRecover());
    }

    System.Collections.IEnumerator CoBananaRecover()
    {
        // 추격 멈춤 시간(1초) 기다림
        float wait = Mathf.Max(0f, bananaFreezeUntil - Time.time);
        if (wait > 0f) yield return new WaitForSeconds(wait);

        // Run으로 복귀시키기
        if (chaserAnimator != null)
        {
            // 트리거 기반이면 trigRun 사용
            if (!string.IsNullOrEmpty(trigRun))
            {
                chaserAnimator.SetTrigger(trigRun);
            }
            else
            {
                // 트리거 없으면 파라미터로라도 "달리는 상태"를 강제
                if (!string.IsNullOrEmpty(animParamIsRunning))
                    chaserAnimator.SetBool(animParamIsRunning, true);
            }
        }

        bananaRoutine = null;
    }

    [Header("Runtime")]
    public ChaserState chaserState = ChaserState.Far;

    [Header("Debug")]
    public bool debugLogs = false;

    void Awake()
    {
        // 거리 초기화
        chaserDistance = Mathf.Clamp(initialDistance, 0f, maxDistance);

        // player 자동 로드
        if (playerMotor == null)
            playerMotor = FindFirstObjectByType<PlayerMotor>();

        if (playerMotor != null && playerRoot == null)
            playerRoot = playerMotor.transform;

        // chaserRoot 자동 로드
        if (chaserRoot == null)
            chaserRoot = transform;

        // animator 자동 로드
        if (chaserAnimator == null && chaserRoot != null)
            chaserAnimator = chaserRoot.GetComponentInChildren<Animator>();

        // input 로드: 우선 inputSource, 없으면 playerMotor 오브젝트에서 탐색
        if (inputSource != null && inputSource is IPlayerInput ii)
            input = ii;

        if (input == null && playerMotor != null)
        {
            var monos = playerMotor.GetComponents<MonoBehaviour>();
            foreach (var m in monos)
            {
                if (m is IPlayerInput inp) { input = inp; break; }
            }
        }

        if (playerMotor == null)
            Debug.LogError("[ChaserSystem] PlayerMotor not found. Assign it in Inspector.");

        if (input == null)
            Debug.LogError("[ChaserSystem] IPlayerInput not found. Assign inputSource or attach KeyboardInput/PoseInput to PlayerMotor object.");
    }

    void Update()
    {
        if (frozen) return;
        if (Time.time < bananaFreezeUntil) return;   // 추격만 1초 멈춤
        
        if (GameManager.I == null) return;
        if (GameManager.I.State != GameState.Playing) return;
        if (playerMotor == null || playerRoot == null || input == null) return;

        // 1) 추격 거리(숫자) 업데이트
        UpdateChaserDistance();

        // 2) 학주 위치 반영
        if (applyWorldFollow && chaserRoot != null)
            ApplyWorldFollow();

        // 3) 학주 애니메이션 반영
        UpdateChaserAnimation();
    }

    void UpdateChaserDistance()
    {
        if (playerMotor != null && playerMotor.IsFlying)
        {
            // 따라오기 불가: 거리 회복 or 고정
            chaserDistance = Mathf.Min(maxDistance, chaserDistance + 2.0f * Time.deltaTime);
            chaserState = ChaserState.Far;
            return;
        }

        // 착지 직후 안전시간엔 추격 완화(거리 회복 or 고정)
        if (Time.time < PlayerMotor.SafeUntilTime)
        {
            chaserDistance = Mathf.Min(maxDistance, chaserDistance + 2.0f * Time.deltaTime);
            chaserState = ChaserState.Far;
            return;
        }

        float move = Mathf.Clamp01(input.MoveLevel);

        bool isRun = move >= runThreshold;
        bool isStop = move < stopThreshold;
        bool isWalk = !isRun && !isStop;

        float deltaPerSec = 0f;
        if (isRun) deltaPerSec = +gainPerSec_Run;
        else if (isWalk) deltaPerSec = -losePerSec_Walk;
        else deltaPerSec = -losePerSec_Stop;

        chaserDistance += deltaPerSec * Time.deltaTime;
        chaserDistance = Mathf.Clamp(chaserDistance, 0f, maxDistance);

        if (chaserDistance <= maxDistance * 0.3f) chaserState = ChaserState.Close;
        else chaserState = ChaserState.Far;

        if (chaserDistance <= 0f)
        {
            chaserState = ChaserState.Caught;
            GameManager.I.GameOver(GameOverReason.CaughtByChaser);
            return;
        }

        if (debugLogs && Time.frameCount % 30 == 0)
        {
            Debug.Log($"[Chaser] move={move:0.00} run={isRun} walk={isWalk} stop={isStop} dist={chaserDistance:0.00}/{maxDistance}");
        }
    }

    void ApplyWorldFollow()
    {
        // 목표 위치 = 플레이어 위치 - (거리 * 방향) + extraOffset
        Vector3 basePos = playerRoot.position;

        Vector3 backDir;
        if (usePlayerForward)
            backDir = -playerRoot.forward;
        else
            backDir = Vector3.back; // 월드 -Z 방향

        Vector3 targetPos = basePos + backDir.normalized * chaserDistance + extraOffset;

        // 레인/좌우 따라가기
        Vector3 cur = chaserRoot.position;

        if (followPlayerX) cur.x = targetPos.x;
        // 앞뒤는 거리값으로 유지
        cur.z = targetPos.z;

        // Y 처리
        if (followPlayerY) cur.y = targetPos.y;
        else cur.y = fixedChaserY;

        // 스무딩
        float t = 1f - Mathf.Exp(-followSharpness * Time.deltaTime);
        chaserRoot.position = Vector3.Lerp(chaserRoot.position, cur, t);

        // 시선/회전: 플레이어를 향하게(선택)
        // 3레인 러닝이면 보통 플레이어와 동일 forward로 두는 게 안정적이라, 아래는 옵션으로 남깁니다.
        // chaserRoot.rotation = Quaternion.Lerp(chaserRoot.rotation, playerRoot.rotation, t);
    }

    void UpdateChaserAnimation()
    {
        if (chaserAnimator == null || input == null) return;

        float move = Mathf.Clamp01(input.MoveLevel);

        bool isRun = move >= runThreshold;

        // 1) 단순 IsRunning 파라미터가 있으면 그걸로 구동
        if (!string.IsNullOrEmpty(animParamIsRunning))
        {
            // 파라미터가 실제로 있는지 체크(없으면 SetBool이 경고를 뿜을 수 있어서)
            // 매 프레임 해시 탐색은 부담이니, 필요하면 캐싱해도 됩니다.
            chaserAnimator.SetBool(animParamIsRunning, isRun);
        }

        // 2) BlendTree로 속도값이 있으면 MoveLevel 같이 넣어주기
        if (!string.IsNullOrEmpty(animParamMoveLevel))
        {
            chaserAnimator.SetFloat(animParamMoveLevel, move, 0.1f, Time.deltaTime);
        }
    }

    public void ResetDistance()
    {
        chaserDistance = Mathf.Clamp(initialDistance, 0f, maxDistance);
        chaserState = ChaserState.Far;
    }
}
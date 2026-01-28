using UnityEngine;

public class KeyboardInput : MonoBehaviour, IPlayerInput
{
    public int Lane { get; private set; }
    public bool JumpTriggered { get; private set; }
    public bool RollHeld { get; private set; }
    public float MoveLevel { get; private set; }
    public bool FlyForward { get; private set; }

    [Header("Action Keys")]
    public KeyCode jumpKey = KeyCode.Space;
    public KeyCode rollKey = KeyCode.DownArrow;
    public KeyCode rollAltKey = KeyCode.LeftControl;

    [Header("Run/WalK/Stop Keys (Ground)")]
    public KeyCode runKey = KeyCode.UpArrow;
    public KeyCode walkKey = KeyCode.W;
    public KeyCode stopKey = KeyCode.S;

    [Header("Fly Forward Key (Fly)")]
    public KeyCode flyKey = KeyCode.F;

    [Header("No input default")]
    [Range(0f, 1f)]
    public float neutralMoveLevel = 0f;

    // ✅ GetKeyDown 대체용(엣지 감지)
    private bool prevJumpHeld = false;

    void Update()
    {
        // ✅ 점프: GetKeyDown 대신 GetKey + 이전 프레임 비교
        bool jumpHeld = Input.GetKey(jumpKey);
        JumpTriggered = jumpHeld && !prevJumpHeld;
        prevJumpHeld = jumpHeld;

        // 레인 이동은 기존대로(여기도 GetKeyDown이 씹히면 같은 방식으로 바꿀 수 있음)
        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
            Lane = Mathf.Max(-1, Lane - 1);

        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
            Lane = Mathf.Min(1, Lane + 1);

        // 롤: Hold 방식(이미 정상)
        RollHeld = Input.GetKey(rollKey) || Input.GetKey(rollAltKey);

        // Fly
        FlyForward = Input.GetKey(flyKey);

        // MoveLevel
        bool stopHeld = Input.GetKey(stopKey);
        bool runHeld = Input.GetKey(runKey);
        bool walkHeld = Input.GetKey(walkKey);

        if (stopHeld) MoveLevel = 0f;
        else if (runHeld) MoveLevel = 1f;
        else if (walkHeld) MoveLevel = 0.5f;
        else MoveLevel = neutralMoveLevel;

        // (선택) 디버그 로그 유지
        if (Time.frameCount % 30 == 0)
        {
            Debug.Log($"[KB] anyKey={Input.anyKey} anyDown={Input.anyKeyDown} " +
                      $"JDown={Input.GetKeyDown(KeyCode.J)} SpaceDown={Input.GetKeyDown(KeyCode.Space)} " +
                      $"jumpHeld={jumpHeld} JumpTrig={JumpTriggered} Roll={RollHeld}");
        }
    }
}

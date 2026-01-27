using UnityEngine;

public class KeyboardInput : MonoBehaviour, IPlayerInput
{
    public int Lane { get; private set; }
    public bool JumpTriggered { get; private set; }
    public bool RollHeld { get; private set; }
    public float MoveLevel { get; private set; } // 0=STOP, 0.5=WALK, 1=RUN (지상용)

    public bool FlyForward { get; private set; } // ✅ 인터페이스와 동일(소문자)

    [Header("Run/WalK/Stop Keys (Ground)")]
    public KeyCode runKey = KeyCode.UpArrow;   // RUN (지상)
    public KeyCode walkKey = KeyCode.W;        // WALK (지상)
    public KeyCode stopKey = KeyCode.S;        // STOP (지상)

    [Header("Fly Forward Key (Fly)")]
    public KeyCode flyKey = KeyCode.F;         // Fly 중 전진

    [Header("No input default")]
    [Range(0f, 1f)]
    public float neutralMoveLevel = 0f;        // 무입력 기본값(0=STOP)

    void Update()
    {
        JumpTriggered = Input.GetKeyDown(KeyCode.Space);

        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
            Lane = Mathf.Max(-1, Lane - 1);

        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
            Lane = Mathf.Min(1, Lane + 1);

        RollHeld = Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.LeftControl);

        // ✅ Fly 전진(F)
        FlyForward = Input.GetKey(flyKey);

        // 지상 이동 입력(기존 유지)
        bool stopHeld = Input.GetKey(stopKey);
        bool runHeld = Input.GetKey(runKey);
        bool walkHeld = Input.GetKey(walkKey);

        // 우선순위: STOP > RUN > WALK
        if (stopHeld) MoveLevel = 0f;
        else if (runHeld) MoveLevel = 1f;
        else if (walkHeld) MoveLevel = 0.5f;
        else MoveLevel = neutralMoveLevel;
    }
}
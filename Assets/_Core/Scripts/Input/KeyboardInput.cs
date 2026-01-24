using UnityEngine;

public class KeyboardInput : MonoBehaviour, IPlayerInput
{
    public int Lane { get; private set; }
    public bool JumpTriggered { get; private set; }
    public bool RollHeld { get; private set; }

    public float MoveLevel { get; private set; }
    public float RunIntensity { get; private set; }

    void Update()
    {
        // ─────────────────
        // Jump
        // ─────────────────
        JumpTriggered =
            Input.GetKeyDown(KeyCode.Space) ||
            Input.GetKeyDown(KeyCode.UpArrow);

        // ─────────────────
        // Lane
        // ─────────────────
        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
            Lane = Mathf.Max(-1, Lane - 1);

        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
            Lane = Mathf.Min(1, Lane + 1);

        // ─────────────────
        // Roll (S / ↓ / Ctrl)
        // ─────────────────
        RollHeld =
            Input.GetKey(KeyCode.S) ||
            Input.GetKey(KeyCode.DownArrow) ||
            Input.GetKey(KeyCode.LeftControl);

        // ─────────────────
        // Run / Walk / Stop
        // ─────────────────
        if (Input.GetKey(KeyCode.X))
        {
            // STOP
            RunIntensity = 0f;
        }
        else if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
        {
            // WALK
            RunIntensity = 0.5f;
        }
        else
        {
            // RUN (기본)
            RunIntensity = 1f;
        }

        MoveLevel = RunIntensity;
    }
}

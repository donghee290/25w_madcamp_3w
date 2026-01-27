using System.Collections;
using UnityEngine;

public class WingsBuffController : MonoBehaviour
{
    [Header("Motor")]
    public PlayerMotor motor;   // 비우면 자동 탐색

    [Header("Buff")]
    public float speedMultiplier = 1.5f;
    public float durationSec = 10f;

    [Header("Fly Tuning")]
    public float flyLiftY = 2.4f;
    public float flyImmediateStepY = 0.4f;
    public float flyGraceSec = 0.6f;

    Coroutine buffCo;

    float runSpeed0;
    float walkSpeed0;
    bool applied;

    void Awake()
    {
        if (motor == null)
            motor = GetComponent<PlayerMotor>();

        if (motor == null)
            Debug.LogError("[WingsBuffController] PlayerMotor not found.");
    }

    /// <summary>
    /// Wings 아이템 효과 적용 (중복 시 시간 리셋)
    /// </summary>
    public void ApplyWings(float overrideDuration = -1f)
    {
        float d = overrideDuration > 0f ? overrideDuration : durationSec;

        if (buffCo != null)
            StopCoroutine(buffCo);

        buffCo = StartCoroutine(BuffRoutine(d));
    }

    IEnumerator BuffRoutine(float d)
    {
        ApplySpeed();

        float t = 0f;
        while (t < d)
        {
            t += Time.deltaTime;
            yield return null;
        }

        RevertSpeed();
        buffCo = null;
    }

    void ApplySpeed()
    {
        if (applied) return;
        if (motor == null) return;

        Debug.Log($"[WingsBuff] motor={motor.name} y={motor.transform.position.y}");

        runSpeed0 = motor.runSpeed;
        walkSpeed0 = motor.walkSpeed;

        motor.runSpeed = runSpeed0 * speedMultiplier;
        motor.walkSpeed = walkSpeed0 * speedMultiplier;

        // Fly ON
        motor.StartFlyingImmediate(flyLiftY, flyImmediateStepY, flyGraceSec);
        Debug.Log($"[WingsBuff] applied liftY={motor.flyLiftY}");
        Debug.Log($"[WingsBuff] after StartFlyingImmediate y={motor.transform.position.y}");

        applied = true;
    }

    void RevertSpeed()
    {
        if (!applied) return;
        if (motor == null) return;

        motor.runSpeed = runSpeed0;
        motor.walkSpeed = walkSpeed0;

        // Fly OFF
        motor.SetFlying(false);

        applied = false;
    }
}
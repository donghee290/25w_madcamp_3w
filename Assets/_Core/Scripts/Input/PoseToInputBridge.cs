using System.Collections;
using UnityEngine;
using Mediapipe.Unity.Sample.PoseLandmarkDetection; // PoseLandmarkerRunner
using Mediapipe.Tasks.Vision.PoseLandmarker;        // PoseLandmarkerResult

public class PoseToInputBridge : MonoBehaviour
{
    public PoseLandmarkerRunner runner;  // Solution에 붙은 Runner
    public PoseInput poseInput;          // PlayerRoot에 붙은 PoseInput
    public bool debugLog = true;

    private readonly Vector3[] _buf = new Vector3[33];

    void Update()
    {
        if (runner == null || poseInput == null) return;
        if (!runner.HasLatestResult) return;

        var result = runner.LatestResult;
        if (result.poseLandmarks == null || result.poseLandmarks.Count == 0) return;

        // NormalizedLandmarks는 struct라 null 체크 불가 → 내부 landmarks만 체크
        var lmContainer = result.poseLandmarks[0];
        var lmList = lmContainer.landmarks;
        if (lmList == null) return;

        int n = lmList.Count;
        if (n < 33) return;

        try
        {
            for (int i = 0; i < 33; i++)
            {
                var lm = lmList[i];
                _buf[i] = new Vector3(lm.x, lm.y, lm.z);
            }
        }
        catch (System.ArgumentOutOfRangeException)
        {
            // 프레임 중간 갱신/레이스 방어: 이번 프레임 스킵
            return;
        }

        poseInput.SetLandmarks(_buf);

        if (debugLog && Time.frameCount % 60 == 0)
            Debug.Log($"[Bridge] landmarks OK. x0={_buf[0].x:0.00} y0={_buf[0].y:0.00}");
    }

}

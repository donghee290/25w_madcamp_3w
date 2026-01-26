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

        // poseLandmarks가 없으면 리턴
        if (result.poseLandmarks == null || result.poseLandmarks.Count == 0) return;

        // 첫 번째 사람만 사용
        var lmList = result.poseLandmarks[0].landmarks;
        if (lmList == null || lmList.Count < 33) return;

        for (int i = 0; i < 33; i++)
        {
            // x,y,z는 0~1 normalized (y는 위가 0)
            _buf[i] = new Vector3(lmList[i].x, lmList[i].y, lmList[i].z);
        }

        poseInput.SetLandmarks(_buf);

        if (debugLog && Time.frameCount % 60 == 0)
            Debug.Log($"[Bridge] landmarks OK. x0={_buf[0].x:0.00} y0={_buf[0].y:0.00}");
    }
}

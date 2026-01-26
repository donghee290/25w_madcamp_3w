using UnityEngine;

public class ClickTraceRenderer : MonoBehaviour
{
    Camera cam;

    void Awake()
    {
        cam = GetComponent<Camera>();
        if (!cam) cam = Camera.main;
    }

    void Update()
    {
        if (!Input.GetMouseButtonDown(0) || cam == null) return;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out var hit, 5000f))
        {
            var go = hit.collider.gameObject;
            var r = go.GetComponent<Renderer>();
            var cr = go.GetComponent<CanvasRenderer>();
            var canvas = go.GetComponentInParent<Canvas>();

            Debug.Log($"[HIT] {GetPath(go.transform)}  collider={hit.collider.GetType().Name}  point={hit.point}", go);

            if (r)
            {
                var mats = r.sharedMaterials;
                Debug.Log($"Renderer={r.GetType().Name} matsLen={(mats!=null?mats.Length:0)}", go);
                if (mats != null)
                {
                    for (int i = 0; i < mats.Length; i++)
                    {
                        var m = mats[i];
                        if (!m) { Debug.Log($"  [{i}] NULL_MAT", go); continue; }
                        var s = m.shader;
                        Debug.Log($"  [{i}] mat={m.name} shader={(s ? s.name : "NULL_SHADER")} supported={(s ? s.isSupported : false)}", go);
                    }
                }
            }
            else
            {
                Debug.Log("Renderer = (none)", go);
            }

            Debug.Log($"CanvasRenderer={(cr? "YES":"NO")}  InCanvas={(canvas? "YES":"NO")}", go);
        }
        else
        {
            Debug.Log("[HIT] nothing (no collider). 핑크가 보이는 면에 Collider가 없을 수 있습니다.");
        }
    }

    static string GetPath(Transform t)
    {
        string p = t.name;
        while (t.parent != null) { t = t.parent; p = t.name + "/" + p; }
        return p;
    }
}
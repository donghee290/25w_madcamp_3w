using UnityEditor;
using UnityEngine;

public static class FindMaterialSubmeshMismatch
{
    [MenuItem("Tools/Find Material/Submesh Mismatch (Scene)")]
    public static void FindInScene()
    {
        int count = 0;

        // MeshRenderer
        foreach (var r in Object.FindObjectsOfType<MeshRenderer>(true))
        {
            var mf = r.GetComponent<MeshFilter>();
            if (!mf || !mf.sharedMesh) continue;

            int sub = mf.sharedMesh.subMeshCount;
            int mats = r.sharedMaterials != null ? r.sharedMaterials.Length : 0;

            if (mats > sub)
            {
                Debug.LogWarning(
                    $"[Mismatch] {GetPath(r.transform)} | Mesh={mf.sharedMesh.name} subMeshes={sub} materials={mats}",
                    r.gameObject
                );
                count++;
            }
        }

        // SkinnedMeshRenderer도 혹시 몰라 같이 검사
        foreach (var r in Object.FindObjectsOfType<SkinnedMeshRenderer>(true))
        {
            if (!r.sharedMesh) continue;

            int sub = r.sharedMesh.subMeshCount;
            int mats = r.sharedMaterials != null ? r.sharedMaterials.Length : 0;

            if (mats > sub)
            {
                Debug.LogWarning(
                    $"[Mismatch] {GetPath(r.transform)} | SkinnedMesh={r.sharedMesh.name} subMeshes={sub} materials={mats}",
                    r.gameObject
                );
                count++;
            }
        }

        Debug.Log($"Done. Found {count} mismatches.");
    }

    private static string GetPath(Transform t)
    {
        string path = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }
        return path;
    }
}
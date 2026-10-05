using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Anatomy model ke systems (Skeleton, Muscles, Organs, Nervous) ko on/off karta hai
/// aur har system ko static-batch karta hai taaki draw calls kam rahein.
/// Famous parts (AnatomyPart) alag GameObjects hain - grab/label ke liye.
/// </summary>
public class AnatomyLayers : MonoBehaviour
{
    [System.Serializable]
    public class Preset
    {
        public string label;
        public string[] layers;      // system root names (child names)
        public string[] hideParts;   // is preset mein chhupane wale AnatomyPart groups (jaise "Skull")
    }

    [Tooltip("Model ki poori height (m) jab sab systems on hon - placement scale ke liye")]
    public float fullBodyHeight = 1.70f;

    public Preset[] presets =
    {
        new Preset { label = "Skeleton + Muscles", layers = new[] { "Skeleton", "Muscles" } },
        new Preset { label = "Skeleton",           layers = new[] { "Skeleton" } },
        new Preset { label = "Muscles",            layers = new[] { "Muscles" } },
        new Preset { label = "Organs",             layers = new[] { "Skeleton", "Organs" }, hideParts = new[] { "Ribcage" } },
        new Preset { label = "Nervous System",     layers = new[] { "Skeleton", "Nervous" }, hideParts = new[] { "Skull" } },
    };

    public int CurrentPreset { get; private set; }
    public string CurrentLabel => presets.Length > 0 ? presets[CurrentPreset].label : "";

    readonly Dictionary<string, GameObject> layerRoots = new Dictionary<string, GameObject>();
    readonly Dictionary<string, List<GameObject>> partsByName = new Dictionary<string, List<GameObject>>();
    // Future grab ke liye: static batching se pehle ka original mesh
    readonly Dictionary<MeshFilter, Mesh> originalMeshes = new Dictionary<MeshFilter, Mesh>();

    void Awake()
    {
        foreach (Transform child in transform)
        {
            layerRoots[child.name] = child.gameObject;
            child.gameObject.SetActive(true);
            foreach (var mf in child.GetComponentsInChildren<MeshFilter>(true))
                if (mf.sharedMesh != null) originalMeshes[mf] = mf.sharedMesh;

            // 1) Famous parts ko grab ke liye tayyar karo
            foreach (var part in child.GetComponentsInChildren<AnatomyPart>(true))
            {
                if (!partsByName.TryGetValue(part.name, out var list))
                    partsByName[part.name] = list = new List<GameObject>();
                list.Add(part.gameObject);
                part.CaptureHome();
                PrepareGrabbable(part);
            }

            // 2) Baaki body (jo kisi famous part mein nahi) ko static-batch karo.
            //    Famous parts batching se BAHAR rehte hain, taaki woh hil sakein.
            var statics = new List<GameObject>();
            foreach (var r in child.GetComponentsInChildren<MeshRenderer>(true))
                if (r.GetComponentInParent<AnatomyPart>(true) == null) statics.Add(r.gameObject);
            if (statics.Count > 0) StaticBatchingUtility.Combine(statics.ToArray(), child.gameObject);
        }
        ApplyPreset(0);
    }

    // ---------- Grab preparation ----------

    void PrepareGrabbable(AnatomyPart part)
    {
        var filters = part.GetComponentsInChildren<MeshFilter>(true);

        // a) Collider: har mesh tukde pe ek halka BoxCollider (mesh ke size ka)
        foreach (var mf in filters)
        {
            if (mf.sharedMesh == null) continue;
            var bc = mf.gameObject.AddComponent<BoxCollider>();
            var b = mf.sharedMesh.bounds;
            bc.center = b.center;
            bc.size = Vector3.Max(b.size, Vector3.one * 0.004f);
        }

        // b) Bahut tukde ho to har material ka EK mesh bana do (draw calls kam)
        if (filters.Length > 1) CombinePart(part, filters);
        else foreach (var mf in filters)
        {
            var r = mf.GetComponent<Renderer>();
            if (r != null) part.VisibleRenderers.Add(r);
        }

        // c) Kinematic rigidbody: hilne wale colliders ke liye zaroori (physics sasta rehta hai)
        var rb = part.gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
    }

    struct Piece { public Mesh mesh; public int sub; public Matrix4x4 m; }

    void CombinePart(AnatomyPart part, MeshFilter[] filters)
    {
        var byMat = new Dictionary<Material, List<Piece>>();
        Matrix4x4 toLocal = part.transform.worldToLocalMatrix;

        foreach (var mf in filters)
        {
            var mr = mf.GetComponent<MeshRenderer>();
            if (mr == null || mf.sharedMesh == null) continue;
            var mats = mr.sharedMaterials;
            for (int i = 0; i < mf.sharedMesh.subMeshCount && i < mats.Length; i++)
            {
                if (mats[i] == null || mf.sharedMesh.GetTopology(i) != MeshTopology.Triangles) continue;
                if (!byMat.TryGetValue(mats[i], out var list)) byMat[mats[i]] = list = new List<Piece>();
                list.Add(new Piece { mesh = mf.sharedMesh, sub = i, m = toLocal * mf.transform.localToWorldMatrix });
            }
            mr.enabled = false; // asli tukda chhupa do (collider bacha rehta hai)
        }

        foreach (var kv in byMat)
        {
            var go = new GameObject("_Combined_" + kv.Key.name);
            go.transform.SetParent(part.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = BuildMesh(kv.Value, part.name);
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = kv.Key;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            part.VisibleRenderers.Add(r);
        }
    }

    // Manual combine: mirror (-1 scale) wale tukdon ke triangles ulat dete hain,
    // warna jode hue mesh mein woh andar se bahar (invisible) dikhte.
    static Mesh BuildMesh(List<Piece> pieces, string name)
    {
        var verts = new List<Vector3>();
        var norms = new List<Vector3>();
        var tris = new List<int>();
        bool normalsOk = true;

        foreach (var p in pieces)
        {
            int baseIdx = verts.Count;
            var v = p.mesh.vertices;
            var n = p.mesh.normals;
            var nm = p.m.inverse.transpose;
            for (int i = 0; i < v.Length; i++) verts.Add(p.m.MultiplyPoint3x4(v[i]));
            if (n != null && n.Length == v.Length)
                for (int i = 0; i < n.Length; i++) norms.Add(nm.MultiplyVector(n[i]).normalized);
            else normalsOk = false;

            var t = p.mesh.GetTriangles(p.sub);
            bool flip = p.m.determinant < 0f;
            for (int i = 0; i + 2 < t.Length; i += 3)
            {
                tris.Add(baseIdx + t[i]);
                if (flip) { tris.Add(baseIdx + t[i + 2]); tris.Add(baseIdx + t[i + 1]); }
                else { tris.Add(baseIdx + t[i + 1]); tris.Add(baseIdx + t[i + 2]); }
            }
        }

        var mesh = new Mesh { name = name + "_combined", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        if (normalsOk && norms.Count == verts.Count) mesh.SetNormals(norms);
        else mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    public void NextPreset() => ApplyPreset((CurrentPreset + 1) % presets.Length);

    public void ApplyPreset(int index)
    {
        if (presets.Length == 0) return;
        CurrentPreset = Mathf.Clamp(index, 0, presets.Length - 1);
        var p = presets[CurrentPreset];
        var on = new HashSet<string>(p.layers);
        foreach (var kv in layerRoots) kv.Value.SetActive(on.Contains(kv.Key));

        // Pehle sab parts wapas dikhao, phir is preset wale chhupao
        foreach (var kv in partsByName)
            foreach (var go in kv.Value) go.SetActive(true);
        if (p.hideParts != null)
            foreach (var name in p.hideParts)
                if (partsByName.TryGetValue(name, out var list))
                    foreach (var go in list) go.SetActive(false);
    }

    public Mesh GetOriginalMesh(MeshFilter mf) =>
        originalMeshes.TryGetValue(mf, out var m) ? m : mf.sharedMesh;
}

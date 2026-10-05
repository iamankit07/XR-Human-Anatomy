using System.Collections.Generic;
using Oculus.Interaction.Input;
using UnityEngine;

/// <summary>
/// Famous anatomy parts (AnatomyPart) ko pakadna, ghumana aur do haath se bada-chhota karna.
///  - Haath: anguutha + index pinch (dono haath)
///  - Controller: grip ya trigger
///  - Haath/controller paas aaye to part halka chamakta hai (highlight)
///  - Sirf dikhne wale (active) parts hi pakde ja sakte hain
/// AnatomyPlacer wale GameObject pe lagao.
/// </summary>
public class AnatomyGrabber : MonoBehaviour
{
    [Header("Hands (khaali ho to auto-find)")]
    public Hand rightHand;          // Interaction SDK: Hand Interactions/RightHand
    public Hand leftHand;           // Interaction SDK: Hand Interactions/LeftHand
    public OVRHand ovrRight;        // backup pinch source
    public OVRHand ovrLeft;

    [Header("Controllers (khaali ho to auto-find)")]
    public Transform rightController;  // RightControllerAnchor
    public Transform leftController;   // LeftControllerAnchor

    [Header("Grab tuning")]
    [Tooltip("Pinch point se itni doori (m) tak ka part pakda ja sakta hai")]
    public float grabRadius = 0.06f;
    [Tooltip("Itni doori (m) pe part chamakne lagta hai")]
    public float hoverRadius = 0.08f;
    [Range(0f, 1f)] public float pinchOn = 0.5f;
    [Range(0f, 1f)] public float pinchOff = 0.3f;
    [Tooltip("Do haath se scale: part ki asli size ke kitne guna tak")]
    public float minScale = 0.5f, maxScale = 3f;

    [Header("Highlight")]
    public Color highlightColor = new Color(1f, 0.95f, 0.55f);
    [Range(0f, 1f)] public float highlightAmount = 0.35f;

    [Header("Snap (wapas apni jagah)")]
    public bool enableSnap = true;
    [Tooltip("Ghar (asli jagah) se itni doori (m) ke andar chhodo to part khud wapas fit ho jaata hai")]
    public float snapDistance = 0.10f;
    [Tooltip("Wapas jaane ka animation kitne second ka")]
    public float snapDuration = 0.25f;

    [Header("Ghost (asli jagah ka halka outline)")]
    public bool showGhost = false;   // user ne hataya (v12). true karo to wapas aa jayega
    [Tooltip("true = ghost sirf pakadte waqt dikhe")]
    public bool ghostOnlyWhileHeld = false;
    public Material ghostMaterial;
    public Color ghostColor = new Color(0.85f, 0.95f, 1f, 0.6f);
    public Color ghostReadyColor = new Color(0.3f, 1f, 0.45f, 0.9f);

    // Ek haath/controller ki haalat
    class Grip
    {
        public bool isRight;
        public bool valid;          // haath/controller track ho raha hai
        public bool held;           // abhi pinch/grip dabaa hua hai
        public bool started;        // isi frame dabaya
        public Vector3 point;       // pakadne ka point (anguutha-ungli ke beech)
        public Quaternion rot;      // haath ki rotation
        public AnatomyPart part;    // kya pakda hua hai
        public Vector3 offPos;      // part ka offset (haath ke frame mein)
        public Quaternion offRot;
        public AnatomyPart near;    // haath ke paas wala part (hover)
        public float nearTime;      // kab dikha tha
    }

    readonly Grip R = new Grip { isRight = true };
    readonly Grip L = new Grip { isRight = false };

    // Do-haath scale ki haalat
    AnatomyPart twoPart;
    float twoStartDist;
    Vector3 twoStartScale;
    Vector3 twoOffset;

    AnatomyPart hovered;
    readonly Collider[] hits = new Collider[48];
    MaterialPropertyBlock mpb;
    static readonly int ColorId = Shader.PropertyToID("_Color");

    void Awake()
    {
        mpb = new MaterialPropertyBlock();
        if (rightHand == null || leftHand == null)
            foreach (var h in FindObjectsOfType<Hand>())
            {
                if (h.Handedness == Handedness.Right && rightHand == null) rightHand = h;
                if (h.Handedness == Handedness.Left && leftHand == null) leftHand = h;
            }
        var rig = FindObjectOfType<OVRCameraRig>();
        if (rig != null)
        {
            if (rightController == null) rightController = rig.rightControllerAnchor;
            if (leftController == null) leftController = rig.leftControllerAnchor;
        }
    }

    void LateUpdate()
    {
        var placer = AnatomyPlacer.Instance;
        bool enabledNow = placer != null && placer.IsPlaced && !placer.IsPlacing;

        bool handsMode = (OVRInput.GetActiveController() & OVRInput.Controller.Hands) != 0;
        ReadInput(R, handsMode ? rightHand : null, ovrRight, rightController, OVRInput.Controller.RTouch, handsMode);
        ReadInput(L, handsMode ? leftHand : null, ovrLeft, leftController, OVRInput.Controller.LTouch, handsMode);

        if (!enabledNow) { ReleaseAll(); SetHover(null); HideAllGhosts(); return; }

        // Layer badli aur pakda hua part chhup gaya -> chhod do
        if (R.part != null && !R.part.gameObject.activeInHierarchy) ReleaseAll();
        if (L.part != null && !L.part.gameObject.activeInHierarchy) ReleaseAll();

        // Har haath ke paas kaunsa part hai, yaad rakho (grab lock-on ke liye)
        UpdateNear(R);
        UpdateNear(L);

        // Naya grab
        if (R.started) TryGrab(R, L);
        if (L.started) TryGrab(L, R);

        // Chhodna
        if (R.part != null && (!R.held || !R.valid)) Release(R, L);
        if (L.part != null && (!L.held || !L.valid)) Release(L, R);

        // Part ko haath ke saath chalao
        if (twoPart != null) UpdateTwoHand();
        else
        {
            if (R.part != null) Follow(R);
            if (L.part != null) Follow(L);
        }

        // Snap animation + ghost outline
        UpdateSnapAnims();
        UpdateGhosts();

        // Highlight: pakda hua part, warna right (phir left) haath ke paas wala part
        AnatomyPart h = R.part ?? L.part;
        if (h == null && R.valid) h = FindNearest(R.point, hoverRadius);
        if (h == null && L.valid) h = FindNearest(L.point, hoverRadius);
        SetHover(h);
    }

    // ---------- Input ----------

    void ReadInput(Grip g, Hand hand, OVRHand ovr, Transform controller, OVRInput.Controller ctrl, bool handsMode)
    {
        bool wasHeld = g.held;
        g.started = false;

        if (handsMode)
        {
            g.valid = false;
            Pose thumb = default, index = default, wrist = default;
            if (hand != null && hand.IsConnected && hand.IsTrackedDataValid &&
                hand.GetJointPose(HandJointId.HandThumbTip, out thumb) &&
                hand.GetJointPose(HandJointId.HandIndexTip, out index) &&
                hand.GetJointPose(HandJointId.HandWristRoot, out wrist))
                g.valid = true;
            if (!g.valid) { g.held = false; return; }

            g.point = (thumb.position + index.position) * 0.5f;   // anguutha-ungli ke beech
            g.rot = wrist.rotation;

            float s = hand.GetFingerPinchStrength(HandFinger.Index);
            if (ovr != null && ovr.IsTracked) s = Mathf.Max(s, ovr.GetFingerPinchStrength(OVRHand.HandFinger.Index));
            bool pinch = hand.GetIndexFingerIsPinching() || s >= pinchOn;
            if (!g.held && pinch) g.held = true;
            else if (g.held && !pinch && s <= pinchOff) g.held = false;
        }
        else
        {
            g.valid = controller != null && OVRInput.IsControllerConnected(ctrl);
            if (!g.valid) { g.held = false; return; }
            g.point = controller.position + controller.forward * 0.03f;
            g.rot = controller.rotation;
            float grip = Mathf.Max(OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, ctrl),
                                   OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, ctrl));
            if (!g.held && grip > 0.6f) g.held = true;
            else if (g.held && grip < 0.3f) g.held = false;
        }

        g.started = g.held && !wasHeld;
    }

    // ---------- Grab / release ----------

    void TryGrab(Grip g, Grip other)
    {
        if (g.part != null) return;

        // Doosra haath pehle se kuch pakde hai aur ye haath usi part ke paas hai -> do-haath scale
        if (other.part != null)
        {
            var near = FindNearest(g.point, grabRadius * 2f);
            if (near == other.part || PointNearPart(g.point, other.part, 0.12f))
            {
                StartTwoHand(other.part);
                return;
            }
        }

        // Pinch karte waqt ungliyaan band hoti hain aur point khisakta hai:
        // seedha paas wala part na mile to woh part lo jo pinch se theek pehle (0.4s) chamak raha tha
        var part = FindNearest(g.point, grabRadius);
        if (part == null && g.near != null && Time.time - g.nearTime < 0.4f && g.near.gameObject.activeInHierarchy)
            part = g.near;
        if (part == null || part.IsGrabbed) return;

        g.part = part;
        part.IsGrabbed = true;
        MarkDisplaced(part);
        SetOffsets(g);
    }

    void SetOffsets(Grip g)
    {
        Quaternion inv = Quaternion.Inverse(g.rot);
        g.offPos = inv * (g.part.transform.position - g.point);
        g.offRot = inv * g.part.transform.rotation;
    }

    void Follow(Grip g)
    {
        g.part.transform.SetPositionAndRotation(g.point + g.rot * g.offPos, g.rot * g.offRot);
    }

    void Release(Grip g, Grip other)
    {
        if (twoPart != null && g.part == twoPart)
        {
            // Do-haath se ek-haath pe wapas: bacha hua haath part pakde rahe
            var tp = twoPart;
            twoPart = null;
            g.part = null;
            if (other.part != null && other.held && other.valid) SetOffsets(other);
            else { ReleaseAll(); TrySnap(tp); }
            return;
        }
        var released = g.part;
        if (released != null) released.IsGrabbed = false;
        g.part = null;
        TrySnap(released);   // ghar ke paas chhoda to wapas fit
    }

    void ReleaseAll()
    {
        if (R.part != null) R.part.IsGrabbed = false;
        if (L.part != null) L.part.IsGrabbed = false;
        R.part = null; L.part = null; twoPart = null;
    }

    // ---------- Two-hand scale ----------

    void StartTwoHand(AnatomyPart part)
    {
        twoPart = part;
        R.part = part; L.part = part;
        part.IsGrabbed = true;
        MarkDisplaced(part);
        Vector3 mid = (R.point + L.point) * 0.5f;
        twoStartDist = Mathf.Max(0.01f, Vector3.Distance(R.point, L.point));
        twoStartScale = part.transform.localScale;
        twoOffset = part.transform.position - mid;
    }

    void UpdateTwoHand()
    {
        Vector3 mid = (R.point + L.point) * 0.5f;
        float ratio = Vector3.Distance(R.point, L.point) / twoStartDist;

        // Scale ko asli size ke minScale..maxScale guna ke beech rakho
        Vector3 home = twoPart.HomeLocalScale;
        float homeMag = Mathf.Max(0.0001f, home.x);
        float target = Mathf.Clamp(twoStartScale.x * ratio, homeMag * minScale, homeMag * maxScale);
        float applied = target / Mathf.Max(0.0001f, twoStartScale.x);

        twoPart.transform.localScale = twoStartScale * applied;
        twoPart.transform.position = mid + twoOffset * applied;
    }

    // ---------- Finding parts ----------

    void UpdateNear(Grip g)
    {
        // Sirf tab yaad rakho jab haath khula ho (pinch se pehle ka irada)
        if (!g.valid || g.part != null || g.held) return;
        var n = FindNearest(g.point, hoverRadius);
        if (n != null) { g.near = n; g.nearTime = Time.time; }
    }

    AnatomyPart FindNearest(Vector3 p, float radius)
    {
        int n = Physics.OverlapSphereNonAlloc(p, radius, hits, ~0, QueryTriggerInteraction.Collide);
        AnatomyPart best = null;
        float bestScore = float.MaxValue;
        for (int i = 0; i < n; i++)
        {
            var col = hits[i];
            var part = col.GetComponentInParent<AnatomyPart>();
            if (part == null || !part.gameObject.activeInHierarchy) continue;
            // Score: collider ki surface tak doori + centre tak doori ka chhota hissa (overlap mein tie-break)
            float d = Vector3.Distance(col.ClosestPoint(p), p);
            float score = d + 0.05f * Vector3.Distance(col.bounds.center, p);
            if (score < bestScore) { bestScore = score; best = part; }
        }
        return best;
    }

    bool PointNearPart(Vector3 p, AnatomyPart part, float maxDist)
    {
        foreach (var col in part.GetComponentsInChildren<Collider>())
            if (Vector3.Distance(col.ClosestPoint(p), p) <= maxDist) return true;
        return false;
    }

    // ---------- Highlight ----------

    void SetHover(AnatomyPart part)
    {
        if (part == hovered) return;
        if (hovered != null) ApplyHighlight(hovered, false);
        hovered = part;
        if (hovered != null) ApplyHighlight(hovered, true);
    }

    void ApplyHighlight(AnatomyPart part, bool on)
    {
        foreach (var r in part.VisibleRenderers)
        {
            if (r == null) continue;
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                if (!on || mats[i] == null) { r.SetPropertyBlock(null, i); continue; }
                mpb.Clear();
                mpb.SetColor(ColorId, Color.Lerp(mats[i].color, highlightColor, highlightAmount));
                r.SetPropertyBlock(mpb, i);
            }
        }
    }

    // =====================================================================
    // ---------- Snap (wapas apni jagah) + Ghost ----------
    // =====================================================================

    class SnapAnim
    {
        public AnatomyPart part;
        public Vector3 fromPos, fromScale;
        public Quaternion fromRot;
        public float t;
    }

    readonly List<SnapAnim> snaps = new List<SnapAnim>();
    readonly HashSet<AnatomyPart> displaced = new HashSet<AnatomyPart>();
    readonly Dictionary<AnatomyPart, Ghost> ghosts = new Dictionary<AnatomyPart, Ghost>();
    readonly List<AnatomyPart> tmpParts = new List<AnatomyPart>();

    class Ghost
    {
        public GameObject root;
        public readonly List<MeshRenderer> renderers = new List<MeshRenderer>();
        public int state = -1;   // -1 abhi rang nahi laga, 0 normal (safed), 1 ready (hara)
        public int shownFrame = -1;
    }

    void MarkDisplaced(AnatomyPart part)
    {
        CancelSnap(part);
        displaced.Add(part);
    }

    /// Part ki "ghar" (home) wali world position
    Vector3 HomeWorldPos(AnatomyPart part)
    {
        var p = part.transform.parent;
        return p != null ? p.TransformPoint(part.HomeLocalPosition) : part.HomeLocalPosition;
    }

    bool InSnapRange(AnatomyPart part)
    {
        return Vector3.Distance(part.transform.position, HomeWorldPos(part)) <= snapDistance;
    }

    /// Part chhoda gaya: ghar ke paas hai to wapas khinch lo, warna hawa mein rehne do
    void TrySnap(AnatomyPart part)
    {
        if (part == null || !enableSnap || part.IsGrabbed) return;
        if (!InSnapRange(part)) return;
        CancelSnap(part);
        var t = part.transform;
        snaps.Add(new SnapAnim
        {
            part = part,
            fromPos = t.localPosition,
            fromRot = t.localRotation,
            fromScale = t.localScale,
            t = 0f
        });
    }

    void CancelSnap(AnatomyPart part)
    {
        for (int i = snaps.Count - 1; i >= 0; i--)
            if (snaps[i].part == part) snaps.RemoveAt(i);
    }

    bool IsSnapping(AnatomyPart part)
    {
        for (int i = 0; i < snaps.Count; i++) if (snaps[i].part == part) return true;
        return false;
    }

    void UpdateSnapAnims()
    {
        float dt = Time.deltaTime / Mathf.Max(0.01f, snapDuration);
        for (int i = snaps.Count - 1; i >= 0; i--)
        {
            var s = snaps[i];
            if (s.part == null) { snaps.RemoveAt(i); continue; }
            s.t = Mathf.Min(1f, s.t + dt);
            float k = s.t * s.t * (3f - 2f * s.t);   // smoothstep: shuru aur ant mein dheere
            var tr = s.part.transform;
            tr.localPosition = Vector3.LerpUnclamped(s.fromPos, s.part.HomeLocalPosition, k);
            tr.localRotation = Quaternion.Slerp(s.fromRot, s.part.HomeLocalRotation, k);
            tr.localScale = Vector3.LerpUnclamped(s.fromScale, s.part.HomeLocalScale, k);
            if (s.t >= 1f)
            {
                s.part.ResetToHome();          // bilkul exact jagah pe lock
                displaced.Remove(s.part);
                snaps.RemoveAt(i);
            }
        }
    }

    bool IsAwayFromHome(AnatomyPart part)
    {
        var t = part.transform;
        if ((t.localPosition - part.HomeLocalPosition).sqrMagnitude > 0.0001f * Sq(ScaleOf(part))) return true;
        if (Quaternion.Angle(t.localRotation, part.HomeLocalRotation) > 3f) return true;
        if ((t.localScale - part.HomeLocalScale).sqrMagnitude > Sq(0.02f * part.HomeLocalScale.x)) return true;
        return false;
    }

    static float Sq(float v) { return v * v; }

    // localPosition ko parent ke scale se world metre mein badalne ke liye (1 cm check)
    static float ScaleOf(AnatomyPart part)
    {
        var p = part.transform.parent;
        float s = p != null ? Mathf.Abs(p.lossyScale.x) : 1f;
        return s > 0.00001f ? 1f / s : 1f;
    }

    void UpdateGhosts()
    {
        if (!showGhost) return;

        // Jo part ghar pe wapas aa gaye unhe list se hatao
        tmpParts.Clear();
        foreach (var p in displaced)
            if (p == null || (!p.IsGrabbed && !IsSnapping(p) && !IsAwayFromHome(p))) tmpParts.Add(p);
        foreach (var p in tmpParts) displaced.Remove(p);

        // Ghost dikhao/chhupao
        int frame = Time.frameCount;
        foreach (var part in displaced)
        {
            if (part == null || !part.gameObject.activeInHierarchy || IsSnapping(part)) continue;
            if (ghostOnlyWhileHeld && !part.IsGrabbed) continue;
            if (!IsAwayFromHome(part)) continue;

            var g = GetGhost(part);
            if (g == null) continue;
            g.shownFrame = frame;
            if (!g.root.activeSelf) g.root.SetActive(true);
            int state = part.IsGrabbed && enableSnap && InSnapRange(part) ? 1 : 0;
            if (state != g.state) SetGhostColor(g, state);
        }
        foreach (var kv in ghosts)
            if (kv.Value.root != null && kv.Value.shownFrame != frame && kv.Value.root.activeSelf)
                kv.Value.root.SetActive(false);
    }

    Ghost GetGhost(AnatomyPart part)
    {
        Ghost g;
        if (ghosts.TryGetValue(part, out g) && g.root != null) return g;

        var mat = ghostMaterial;
        if (mat == null)
        {
            var sh = Shader.Find("Legacy Shaders/Transparent/Diffuse");
            if (sh == null) return null;
            mat = ghostMaterial = new Material(sh);
        }

        g = new Ghost();
        g.root = new GameObject("_Ghost_" + part.name);
        var rt = g.root.transform;
        rt.SetParent(part.transform.parent, false);
        rt.localPosition = part.HomeLocalPosition;
        rt.localRotation = part.HomeLocalRotation;
        rt.localScale = part.HomeLocalScale;

        foreach (var r in part.VisibleRenderers)
        {
            if (r == null) continue;
            var mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;

            // Part se renderer tak ka raasta copy karo (mirror / -1 scale bhi sahi rahe)
            Transform holder = CopyChain(rt, part.transform, r.transform);
            var gmf = holder.gameObject.AddComponent<MeshFilter>();
            gmf.sharedMesh = mf.sharedMesh;
            var gmr = holder.gameObject.AddComponent<MeshRenderer>();
            var mats = new Material[Mathf.Max(1, mf.sharedMesh.subMeshCount)];
            for (int i = 0; i < mats.Length; i++) mats[i] = mat;
            gmr.sharedMaterials = mats;
            gmr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            gmr.receiveShadows = false;
            gmr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            gmr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            g.renderers.Add(gmr);
        }

        g.root.SetActive(false);
        ghosts[part] = g;
        return g;
    }

    static Transform CopyChain(Transform ghostRoot, Transform partRoot, Transform target)
    {
        if (target == partRoot) return MakeChild(ghostRoot, target, true);
        var chain = new List<Transform>();
        for (var t = target; t != null && t != partRoot; t = t.parent) chain.Add(t);
        Transform cur = ghostRoot;
        for (int i = chain.Count - 1; i >= 0; i--) cur = MakeChild(cur, chain[i], false);
        return cur;
    }

    static Transform MakeChild(Transform parent, Transform src, bool identity)
    {
        var go = new GameObject(src.name);
        var t = go.transform;
        t.SetParent(parent, false);
        if (!identity)
        {
            t.localPosition = src.localPosition;
            t.localRotation = src.localRotation;
            t.localScale = src.localScale;
        }
        return t;
    }

    void SetGhostColor(Ghost g, int state)
    {
        g.state = state;
        mpb.Clear();
        mpb.SetColor(ColorId, state == 1 ? ghostReadyColor : ghostColor);
        foreach (var r in g.renderers) if (r != null) r.SetPropertyBlock(mpb);
    }

    void HideAllGhosts()
    {
        foreach (var kv in ghosts) if (kv.Value.root != null) kv.Value.root.SetActive(false);
    }

    /// Reset button ke liye: sab chhod do, snap/ghost/highlight hatao
    public void ClearAll()
    {
        ReleaseAll();
        SetHover(null);
        snaps.Clear();
        displaced.Clear();
        HideAllGhosts();
    }
}

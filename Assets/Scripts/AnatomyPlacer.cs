using Meta.XR.MRUtilityKit;
using Oculus.Interaction.Input;
using UnityEngine;

public class AnatomyPlacer : MonoBehaviour
{
    public static AnatomyPlacer Instance;

    [Header("References")]
    public GameObject anatomyPrefab;
    public GameObject reticle;
    public Transform rayOrigin;      // RightHandAnchor (controller ray)
    public Transform trackingSpace;  // Camera Rig/TrackingSpace (auto-find)

    [Header("Hand tracking (Interaction SDK - poke wale haath)")]
    public Hand isdkRightHand;       // Hand Interactions/RightHand (auto-find)
    public Hand isdkLeftHand;        // Hand Interactions/LeftHand (auto-find)
    [Tooltip("false = sirf RIGHT haath se pinch-place (left haath menu ke liye free)")]
    public bool allowLeftHandPinch = false;

    [Header("Fallback (purana OVRHand) - dono sources milke check hote hain")]
    public OVRHand rightHand;
    public OVRHand leftHand;

    [Header("Ray settings")]
    public float maxDistance = 5f;
    public MRUKAnchor.SceneLabels surfaces =
        MRUKAnchor.SceneLabels.FLOOR | MRUKAnchor.SceneLabels.TABLE;

    [Header("Placement tuning")]
    public Vector3 rotationOffset = Vector3.zero;
    public float targetHeight = 1.7f;
    [Tooltip("User se kam se kam itni horizontal doori (m) - body ke andar na ghuso")]
    public float minDistanceFromUser = 1.0f;
    [Tooltip("Floor pe ray na lage to body user ke saamne itni door rakho (m)")]
    public float fallbackDistance = 1.5f;
    [Tooltip("App khulte hi placement mode on - pehla pinch hi body dikha de")]
    public bool placeOnStart = true;

    [Header("Pinch tuning")]
    [Range(0f, 1f)] public float pinchOnStrength = 0.7f;
    [Range(0f, 1f)] public float pinchOffStrength = 0.4f;
    [Tooltip("Pinch karte waqt haath hilta hai; itne sec purana valid hit use kar sakte hain")]
    public float hitGraceTime = 0.5f;

    [Header("Debug (headset mein live panel)")]
    public bool debugLogs;
    public bool showDebugHud = true;
    public TMPro.TMP_Text debugText;

    [Header("Dwell-to-place (pinch ke bina): reticle ruka rahe to apne aap place")]
    public bool dwellToPlace = true;
    public float dwellTime = 1.5f;
    [Tooltip("Reticle itna (m) hile to timer dobara shuru")]
    public float dwellRadius = 0.08f;

    bool placing;
    bool rightPinchHeld, leftPinchHeld;
    float placeAllowedTime;
    float nextLogTime;
    GameObject spawned;
    AnatomyLayers layers;
    float baseHeight = 1.7f;
    Vector3 spawnPos, spawnScale;
    Quaternion spawnRot;

    bool hasLastHit;
    Vector3 lastHitPoint;
    float lastHitTime;
    string lastEvent = "-";
    float peakPinch, peakPinchTime;
    bool dwellActive;
    Vector3 dwellAnchor;
    float dwellStart;
    bool reticleScaleCaptured;
    Vector3 reticleBaseScale;
    Renderer reticleRenderer;

    public AnatomyLayers Layers => layers;
    public bool IsPlaced => spawned != null && spawned.activeSelf;
    public bool IsPlacing => placing;

    void Awake()
    {
        Instance = this;
        if (reticle) reticle.SetActive(false);
        AutoWire();
    }

    void Start()
    {
        // Model sirf EK baar load hota hai (startup pe) - place karte waqt freeze nahi
        if (anatomyPrefab == null) { Debug.LogError("Anatomy Prefab assign nahi hai!"); return; }
        spawned = Instantiate(anatomyPrefab, new Vector3(0f, -100f, 0f), Quaternion.identity);
        layers = spawned.GetComponent<AnatomyLayers>();
        if (layers != null && layers.fullBodyHeight > 0.01f) baseHeight = layers.fullBodyHeight;
        spawned.SetActive(false);

        if (placeOnStart) StartPlacement();
        if (debugText) debugText.gameObject.SetActive(showDebugHud);
    }

    void AutoWire()
    {
        if (trackingSpace == null)
        {
            var rig = FindObjectOfType<OVRCameraRig>();
            if (rig != null) trackingSpace = rig.trackingSpace;
        }
        if (isdkRightHand == null || isdkLeftHand == null)
        {
            foreach (var h in FindObjectsOfType<Hand>())
            {
                if (h.Handedness == Handedness.Right && isdkRightHand == null) isdkRightHand = h;
                if (h.Handedness == Handedness.Left && isdkLeftHand == null) isdkLeftHand = h;
            }
        }
    }

    public void StartPlacement()
    {
        if (spawned != null) spawned.SetActive(false);
        placing = true;
        hasLastHit = false;
        // Menu dabate waqt ka pinch placement trigger na kare: pehle release zaroori
        rightPinchHeld = true;
        leftPinchHeld = true;
        placeAllowedTime = Time.time + 0.4f;
        lastEvent = "placing ON";
    }

    public void DeleteAnatomy()
    {
        if (spawned != null) spawned.SetActive(false);
        placing = false;
        if (reticle) reticle.SetActive(false);
        lastEvent = "deleted";
    }

    public void ResetAnatomy()
    {
        if (!IsPlaced) return;
        spawned.transform.SetPositionAndRotation(spawnPos, spawnRot);
        spawned.transform.localScale = spawnScale;
        // Grab se bikhre saare famous parts bhi apni jagah wapas
        var grabber = GetComponent<AnatomyGrabber>();
        if (grabber != null) grabber.ClearAll();
        foreach (var part in spawned.GetComponentsInChildren<AnatomyPart>(true)) part.ResetToHome();
    }

    public void NextLayer()
    {
        if (layers != null) layers.NextPreset();
    }

    // ---------- Hand helpers: ISDK aur OVRHand DONO check (jo bhi kaam kare) ----------

    static bool IsdkOk(Hand h) => h != null && h.IsConnected && h.IsTrackedDataValid;

    bool IsTracked(Hand isdk, OVRHand ovr) => IsdkOk(isdk) || (ovr != null && ovr.IsTracked);

    float PinchStrength(Hand isdk, OVRHand ovr)
    {
        float a = IsdkOk(isdk) ? isdk.GetFingerPinchStrength(HandFinger.Index) : 0f;
        float b = (ovr != null && ovr.IsTracked) ? ovr.GetFingerPinchStrength(OVRHand.HandFinger.Index) : 0f;
        return Mathf.Max(a, b);
    }

    bool IsPinching(Hand isdk, OVRHand ovr)
    {
        bool a = IsdkOk(isdk) && isdk.GetIndexFingerIsPinching();
        bool b = ovr != null && ovr.IsTracked && ovr.GetFingerIsPinching(OVRHand.HandFinger.Index);
        return a || b;
    }

    bool UpdatePinch(Hand isdk, OVRHand ovr, bool tracked, ref bool held)
    {
        if (!tracked) { held = false; return false; }
        float s = PinchStrength(isdk, ovr);
        bool isPinch = IsPinching(isdk, ovr) || s >= pinchOnStrength;

        if (!held && isPinch) { held = true; return true; }
        if (held && !isPinch && s <= pinchOffStrength) held = false;
        return false;
    }

    void Update()
    {
        bool rightTracked = IsTracked(isdkRightHand, rightHand);
        bool leftTracked = allowLeftHandPinch && IsTracked(isdkLeftHand, leftHand);

        // Pinch hamesha track karo (taaki HUD sahi dikhe), placement sirf placing mode mein
        bool rightPinchStart = UpdatePinch(isdkRightHand, rightHand, rightTracked, ref rightPinchHeld);
        bool leftPinchStart = UpdatePinch(isdkLeftHand, leftHand, leftTracked, ref leftPinchHeld);
        bool pinching = (rightTracked && rightPinchHeld) || (leftTracked && leftPinchHeld);
        bool handMode = rightTracked || leftTracked;

        // HUD ke liye: pichhle 2 sec ka sabse zyada pinch
        float s = PinchStrength(isdkRightHand, rightHand);
        if (s > peakPinch || Time.time - peakPinchTime > 2f) { peakPinch = s; peakPinchTime = Time.time; }

        var room = MRUK.Instance != null ? MRUK.Instance.GetCurrentRoom() : null;

        if (placing && room != null && !(handMode && pinching && hasLastHit))
        {
            // Pinch hold ke dauraan ray freeze: pinch karte hi pointer hil jata hai
            TryRaycast(room, rightTracked, leftTracked);
        }

        bool hitUsable = hasLastHit && Time.time - lastHitTime <= hitGraceTime;

        // Dwell-to-place: reticle ek jagah ruka rahe to apne aap place (pinch ki zaroorat nahi)
        float dwellProgress = 0f;
        if (placing && handMode && dwellToPlace && hitUsable && Time.time >= placeAllowedTime)
        {
            if (!dwellActive || Vector3.Distance(lastHitPoint, dwellAnchor) > dwellRadius)
            {
                dwellActive = true;
                dwellAnchor = lastHitPoint;
                dwellStart = Time.time;
            }
            dwellProgress = Mathf.Clamp01((Time.time - dwellStart) / dwellTime);
        }
        else dwellActive = false;

        UpdateReticle(placing && (hitUsable || (pinching && hasLastHit)), dwellProgress);

        bool triggerDown = OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger,
                                            OVRInput.Controller.RTouch);
        bool pinchStarted = rightPinchStart || leftPinchStart;
        if (pinchStarted) lastEvent = "pinch START " + Time.time.ToString("F1");

        if (placing && Time.time >= placeAllowedTime)
        {
            if (triggerDown || pinchStarted)
            {
                if (!hitUsable && room != null) hitUsable = TryRaycast(room, rightTracked, leftTracked);
                if (hitUsable) { Place(lastHitPoint); lastEvent = "PLACED (pinch, floor hit)"; }
                else { Place(FallbackPoint(room)); lastEvent = "PLACED (pinch, fallback)"; }
                dwellActive = false;
            }
            else if (dwellActive && dwellProgress >= 1f)
            {
                Place(dwellAnchor);
                lastEvent = "PLACED (dwell)";
                dwellActive = false;
            }
        }

        UpdateHud(rightTracked, pinching, hitUsable, room != null, dwellProgress);
    }

    // Reticle: dwell ke saath bada hota hai aur safed se hara ho jaata hai
    void UpdateReticle(bool visible, float progress)
    {
        if (!reticle) return;
        if (!reticleScaleCaptured)
        {
            reticleBaseScale = reticle.transform.localScale;
            reticleRenderer = reticle.GetComponent<Renderer>();
            reticleScaleCaptured = true;
        }
        reticle.SetActive(visible);
        if (!visible) return;
        reticle.transform.localScale = reticleBaseScale * (1f + 0.6f * progress);
        if (reticleRenderer != null)
            reticleRenderer.material.color = Color.Lerp(Color.white, new Color(0.2f, 1f, 0.4f), progress);
    }

    bool TryRaycast(MRUKRoom room, bool rightTracked, bool leftTracked)
    {
        Ray ray = GetRay(rightTracked, leftTracked);
        if (!room.Raycast(ray, maxDistance, new LabelFilter(surfaces), out RaycastHit rh, out _))
            return false;
        hasLastHit = true;
        lastHitPoint = rh.point;
        lastHitTime = Time.time;
        if (reticle)
            reticle.transform.SetPositionAndRotation(rh.point, Quaternion.FromToRotation(Vector3.up, rh.normal));
        return true;
    }

    // Ray floor pe na lage to: user ke saamne, floor ki height pe
    Vector3 FallbackPoint(MRUKRoom room)
    {
        Transform cam = Camera.main.transform;
        Vector3 fwd = cam.forward; fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
        fwd.Normalize();
        // Floor-level tracking: TrackingSpace ki height = asli floor
        float floorY = trackingSpace != null ? trackingSpace.position.y : cam.position.y - 1.6f;
        var floor = room != null ? room.GetFloorAnchor() : null;
        if (floor != null) floorY = floor.transform.position.y;
        Vector3 p = cam.position + fwd * fallbackDistance;
        p.y = floorY;
        return p;
    }

    Ray GetRay(bool rightTracked, bool leftTracked)
    {
        // Hand mode: right haath ka pointer (left sirf tab jab allowed ho aur right na dikhe)
        Hand isdk = rightTracked ? isdkRightHand : (leftTracked ? isdkLeftHand : null);
        OVRHand ovr = rightTracked ? rightHand : (leftTracked ? leftHand : null);

        if (IsdkOk(isdk) && isdk.GetPointerPose(out Pose p))
            return new Ray(p.position, p.rotation * Vector3.forward);

        if (ovr != null && ovr.IsTracked && ovr.IsPointerPoseValid && ovr.PointerPose != null)
        {
            Transform pp = ovr.PointerPose;
            if (pp.parent == null && trackingSpace != null)
                return new Ray(trackingSpace.TransformPoint(pp.localPosition),
                               trackingSpace.TransformDirection(pp.localRotation * Vector3.forward));
            return new Ray(pp.position, pp.forward);
        }

        if (!rightTracked && !leftTracked)
            return new Ray(rayOrigin.position, rayOrigin.forward); // controller

        // Fallback: kandhe se haath ki taraf
        Transform head = Camera.main.transform;
        bool isLeft = !rightTracked;
        Vector3 shoulder = head.position + head.right * (isLeft ? -0.15f : 0.15f) + Vector3.down * 0.2f;
        Vector3 handPos = IsdkOk(isdk) && isdk.GetRootPose(out Pose root) ? root.position
                        : (ovr != null ? ovr.transform.position : rayOrigin.position);
        return new Ray(handPos, (handPos - shoulder).normalized);
    }

    void Place(Vector3 pos)
    {
        if (spawned == null) return;

        Transform cam = Camera.main.transform;

        // User ke bahut paas hit ho to body ko door dhakel do
        Vector3 flatUser = new Vector3(cam.position.x, pos.y, cam.position.z);
        Vector3 away = pos - flatUser;
        if (away.sqrMagnitude < 0.0001f) { away = cam.forward; away.y = 0f; }
        if (away.magnitude < minDistanceFromUser)
            pos = flatUser + away.normalized * minDistanceFromUser;

        Vector3 toUser = cam.position - pos;
        toUser.y = 0;
        Quaternion face = toUser.sqrMagnitude > 0.001f
            ? Quaternion.LookRotation(toUser) : Quaternion.identity;

        // Model ka pivot pairon (feet) pe hai -> seedha hit point pe rakho
        float scale = targetHeight > 0f ? targetHeight / baseHeight : 1f;
        spawned.transform.SetPositionAndRotation(pos, face * Quaternion.Euler(rotationOffset));
        spawned.transform.localScale = Vector3.one * scale;
        spawned.SetActive(true);

        spawnPos = spawned.transform.position;
        spawnRot = spawned.transform.rotation;
        spawnScale = spawned.transform.localScale;

        placing = false;
        if (reticle) reticle.SetActive(false);
        Log("PLACED at " + spawnPos + " scale " + spawnScale);
    }

    void UpdateHud(bool rightTracked, bool pinching, bool hitUsable, bool hasRoom, float dwell)
    {
        if (!showDebugHud || debugText == null) return;
        float now = PinchStrength(isdkRightHand, rightHand);
        debugText.text =
            "DEBUG\n" +
            "placing: " + placing + "   placed: " + IsPlaced + "\n" +
            "R tracked: " + rightTracked + "  (isdk " + IsdkOk(isdkRightHand) + ")\n" +
            "pinch now: " + now.ToString("F2") + "  peak(2s): " + peakPinch.ToString("F2") + "\n" +
            "pinch held: " + pinching + "   dwell: " + Mathf.RoundToInt(dwell * 100f) + "%\n" +
            "room: " + hasRoom + "   floor hit: " + hitUsable + "\n" +
            "last: " + lastEvent;
    }

    void Log(string msg)
    {
        if (!debugLogs || Time.time < nextLogTime) return;
        nextLogTime = Time.time + 0.5f;
        Debug.Log("PLACE | " + msg);
    }
}

using UnityEngine;

public class MenuFeature : MonoBehaviour
{
    public static MenuFeature Instance { get; private set; }

    [Header("Menu")]
    [SerializeField] private GameObject menu;
    [SerializeField] private OVRInput.Button buttonForMenuActivation = OVRInput.Button.Two;

    [Header("Menu position")]
    [SerializeField] private bool floatInFront = false;  // true = controller se alag karke saamne tikata hai
    [SerializeField] private bool startVisible = true;   // hand-tracking users ke paas B button nahi hota, isliye default dikhe
    [SerializeField] private float distance = 0.5f;
    [SerializeField] private float heightOffset = -0.1f;
    [SerializeField] private Vector3 rotationOffset = Vector3.zero; // menu ulta/tirchha dikhe to yahan Y=180 wagairah

    [Header("Shortcuts")]
    [SerializeField] private bool enableShortcuts = true; // Right A = place, Right B = delete, Left X = reset

    void Awake()
    {
        Instance = this;
        if (floatInFront) menu.transform.SetParent(null, true);
        menu.SetActive(startVisible);
    }

    void Update()
    {
        // IMPORTANT: Meta SDK haath ke pinch ko bhi button maanta hai
        // (right pinch = A, left pinch = X). Isliye shortcuts SIRF asli controller pe chalne chahiye,
        // warna right pinch se body gayab (A = Place) aur left pinch se Reset ho jaata tha.
        bool handsActive = (OVRInput.GetActiveController() & OVRInput.Controller.Hands) != 0;
        if (handsActive) return;

        if (OVRInput.GetDown(buttonForMenuActivation)) ToggleMenu();

        if (!enableShortcuts) return;
        if (OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.RTouch)) PlaceAnatomy();
        // Note: Right B menu toggle hai, isliye delete ka shortcut hata diya (dono ek saath chal rahe the)
        if (OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.LTouch)) ResetAnatomy();
        if (OVRInput.GetDown(OVRInput.Button.Two, OVRInput.Controller.LTouch)) NextLayer(); // Left Y = next layer
    }

    void ToggleMenu()
    {
        bool show = !menu.activeSelf;
        if (show && floatInFront) PlaceMenuInFront();
        menu.SetActive(show);
    }

    void PlaceMenuInFront()
    {
        Transform cam = Camera.main.transform;
        Vector3 fwd = cam.forward; fwd.y = 0f; fwd.Normalize();
        menu.transform.position = cam.position + fwd * distance + Vector3.up * heightOffset;
        menu.transform.rotation = Quaternion.LookRotation(fwd) * Quaternion.Euler(rotationOffset);
    }

    public void PlaceAnatomy()
    {
        if (AnatomyPlacer.Instance != null) AnatomyPlacer.Instance.StartPlacement();
        if (floatInFront) menu.SetActive(false); // haath/controller pe laga menu dikhta rahe
    }

    public void ResetAnatomy()
    {
        if (AnatomyPlacer.Instance != null) AnatomyPlacer.Instance.ResetAnatomy();
    }

    public void NextLayer()
    {
        if (AnatomyPlacer.Instance != null) AnatomyPlacer.Instance.NextLayer();
        UpdateLayerLabel();
    }

    [Header("Layer button label (optional)")]
    [SerializeField] private TMPro.TMP_Text layerLabel;

    void UpdateLayerLabel()
    {
        if (layerLabel == null || AnatomyPlacer.Instance == null || AnatomyPlacer.Instance.Layers == null) return;
        layerLabel.text = "LAYER: " + AnatomyPlacer.Instance.Layers.CurrentLabel;
    }

    void Start() { Invoke(nameof(UpdateLayerLabel), 0.5f); }

    public void DeleteAnatomy()
    {
        if (AnatomyPlacer.Instance != null) AnatomyPlacer.Instance.DeleteAnatomy();
    }
}
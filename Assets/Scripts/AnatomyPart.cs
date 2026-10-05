using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ek "famous" anatomy structure (jaise Liver, Femur (Left), Biceps brachii (Right)).
/// Iske children us structure ke saare mesh tukde hain.
/// Grab / snap / label feature isi component ko use karta hai.
/// </summary>
public class AnatomyPart : MonoBehaviour
{
    public string displayName;
    public string system;          // Skeleton / Muscles / Organs / Nervous
    public string side;            // "Left", "Right" ya ""
    [TextArea(2, 6)] public string summary;

    public Vector3 HomeLocalPosition { get; private set; }
    public Quaternion HomeLocalRotation { get; private set; }
    public Vector3 HomeLocalScale { get; private set; }
    public bool IsGrabbed { get; set; }

    // Highlight ke liye: part ke dikhne wale renderers (runtime pe bharte hain)
    public readonly List<Renderer> VisibleRenderers = new List<Renderer>();

    bool homeCaptured;

    void Awake() { CaptureHome(); }

    /// Asli jagah yaad rakho (sirf pehli baar)
    public void CaptureHome()
    {
        if (homeCaptured) return;
        HomeLocalPosition = transform.localPosition;
        HomeLocalRotation = transform.localRotation;
        HomeLocalScale = transform.localScale;
        homeCaptured = true;
    }

    public void ResetToHome()
    {
        transform.localPosition = HomeLocalPosition;
        transform.localRotation = HomeLocalRotation;
        transform.localScale = HomeLocalScale;
    }
}

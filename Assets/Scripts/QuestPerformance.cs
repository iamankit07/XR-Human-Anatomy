using UnityEngine;

/// <summary>Quest ke liye runtime performance settings (foveation, refresh rate).</summary>
public class QuestPerformance : MonoBehaviour
{
    public float displayFrequency = 72f;
    public OVRManager.FoveatedRenderingLevel foveation = OVRManager.FoveatedRenderingLevel.High;

    void Start()
    {
        OVRManager.foveatedRenderingLevel = foveation;
        OVRManager.useDynamicFoveatedRendering = true;
        if (OVRManager.display != null) OVRManager.display.displayFrequency = displayFrequency; // Editor mein headset na ho to null
        QualitySettings.vSyncCount = 0;
    }
}

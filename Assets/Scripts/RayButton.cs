using UnityEngine;

[RequireComponent(typeof(Collider))]
public class RayButton : MonoBehaviour
{
    public enum ButtonAction { PlaceAnatomy, Reset, Delete, NextLayer }

    public ButtonAction action;
    [SerializeField] private float hoverScale = 1.1f;

    Vector3 baseScale;

    void Awake() { baseScale = transform.localScale; }
    void OnDisable() { transform.localScale = baseScale; }

    public void SetHover(bool hover)
    {
        transform.localScale = hover ? baseScale * hoverScale : baseScale;
    }

    public void Click()
    {
        var menu = MenuFeature.Instance;
        if (menu == null) return;

        switch (action)
        {
            case ButtonAction.PlaceAnatomy: menu.PlaceAnatomy(); break;
            case ButtonAction.Reset: menu.ResetAnatomy(); break;
            case ButtonAction.Delete: menu.DeleteAnatomy(); break;
            case ButtonAction.NextLayer: menu.NextLayer(); break;
        }
    }
}
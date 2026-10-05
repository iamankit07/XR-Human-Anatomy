using UnityEngine;

public class MenuRayPointer : MonoBehaviour
{
    [SerializeField] private Transform rayOrigin;   // RightHandAnchor
    [SerializeField] private GameObject menu;
    [SerializeField] private float maxDistance = 3f;

    LineRenderer line;
    RayButton hovered;

    void Awake()
    {
        line = gameObject.AddComponent<LineRenderer>();
        line.positionCount = 2;
        line.startWidth = 0.004f;
        line.endWidth = 0.002f;
        line.material = new Material(Shader.Find("Sprites/Default"));
        line.startColor = line.endColor = Color.cyan;
        line.enabled = false;
    }

    void Update()
    {
        if (!menu.activeInHierarchy) { ClearHover(); line.enabled = false; return; }

        if (OVRInput.GetActiveController() == OVRInput.Controller.Hands)
        { ClearHover(); line.enabled = false; return; }

        Vector3 start = rayOrigin.position;
        Vector3 dir = rayOrigin.forward;
        Vector3 end = start + dir * maxDistance;
        RayButton target = null;

        if (Physics.Raycast(start, dir, out RaycastHit hit, maxDistance,
                            ~0, QueryTriggerInteraction.Collide))
        {
            end = hit.point;
            target = hit.collider.GetComponentInParent<RayButton>();
        }

        line.enabled = true;
        line.SetPosition(0, start);
        line.SetPosition(1, end);

        if (target != hovered)
        {
            ClearHover();
            hovered = target;
            if (hovered != null) hovered.SetHover(true);
        }

        if (hovered != null &&
            OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch))
        {
            hovered.Click();
        }
    }

    void ClearHover()
    {
        if (hovered != null) hovered.SetHover(false);
        hovered = null;
    }
}
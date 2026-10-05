using Oculus.Interaction.Input;
using UnityEngine;

/// <summary>
/// Menu ko controller mode mein controller pe (original jagah) rakhta hai,
/// aur hand-tracking mode mein left hatheli ke bagal mein, user ki taraf mudा hua.
/// Right haath ki ungli poke karne paas aaye to menu ruk jaata hai (button miss na ho).
/// Menu GameObject pe lagao (jo LeftControllerAnchor ka child hai).
/// </summary>
public class MenuHandFollow : MonoBehaviour
{
    [Header("References (khaali ho to auto-find)")]
    [SerializeField] private Hand leftHand;     // Interaction SDK: Hand Interactions/LeftHand
    [SerializeField] private Hand rightHand;    // Interaction SDK: Hand Interactions/RightHand
    [SerializeField] private Transform head;    // CenterEyeAnchor

    [Header("Hand mode placement (head ke hisaab se, metres)")]
    [Tooltip("x = body ki taraf (right), y = upar, z = aage")]
    [SerializeField] private Vector3 offset = new Vector3(0.15f, 0.02f, 0.0f);
    [SerializeField] private float followSpeed = 14f;     //menu haath ke peeche kitni tezi se aaye. Zyada → turant chipkega (jitter bhi dikhega); kam → dheere aur smooth.
    [Tooltip("Right index ungli itni paas ho to menu freeze (poke stable rahe)")]
    [SerializeField] private float freezeDistance = 0.15f;   //right ungli menu se 15 cm ke andar aaye to menu ruk jaye.

    Vector3 orignalLocalPosition;
    Quaternion orignalLocalRotation;
    bool wasHandMode;

    void Awake()
    {
        orignalLocalPosition = transform.localPosition;
        orignalLocalRotation = transform.localRotation;
        if (head == null && Camera.main != null) head = Camera.main.transform;
        if (leftHand == null || rightHand == null)
        {
            foreach (var h in FindObjectsOfType<Hand>())
            {
                if (h.Handedness == Handedness.Left && leftHand == null) leftHand = h;
                if (h.Handedness == Handedness.Right && rightHand == null) rightHand = h;
            }
        }
    }

    static bool HandsActive()
    {
        return (OVRInput.GetActiveController() & OVRInput.Controller.Hands) != 0;
    }

    /*LateUpdate hi kyun? Unity pehle Update mein haath aur headset ki nayi position update karta hai.
    LateUpdate uske baad chalta hai, isliye menu hamesha taaza haath ki position pe lagta hai, ek frame peeche nahi.
    */
    void LateUpdate()
    {
        bool handMode = HandsActive() && leftHand != null && head != null;

        if (!handMode)
        {
            // Controller mode: original jagah (controller pe perfect aligned)
            if (wasHandMode)
            {
                transform.localPosition = orignalLocalPosition;
                transform.localRotation = orignalLocalRotation;
            }
            wasHandMode = false;
            return;
        }

        // Hatheli ka centre = wrist aur beech ki ungli ke knuckle ke beech
        if (!leftHand.GetJointPose(HandJointId.HandWristRoot, out Pose wrist) ||
            !leftHand.GetJointPose(HandJointId.HandMiddle1, out Pose knuckle))
            return; // haath track nahi ho raha: menu wahin ruka rahe
        Vector3 palmCenter = Vector3.Lerp(wrist.position, knuckle.position, 0.5f);

        // Head ke hisaab se flat directions (haath ghumne se menu na hile)
        Vector3 fwd = head.forward; fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
        fwd.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, fwd);

        Vector3 targetPos = palmCenter + right * offset.x + Vector3.up * offset.y + fwd * offset.z;
        Vector3 look = targetPos - head.position;
        Quaternion targetRot = look.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(look.normalized, Vector3.up)
            : transform.rotation;

        if (!wasHandMode)
        {
            // Mode badla: seedha jagah pe rakh do (slide nahi)
            transform.SetPositionAndRotation(targetPos, targetRot);
            wasHandMode = true;
            return;
        }

        // Right ungli poke karne aa rahi ho to menu freeze
        if (rightHand != null && rightHand.GetJointPose(HandJointId.HandIndexTip, out Pose tip) &&
            Vector3.Distance(tip.position, transform.position) < freezeDistance)
            return;

        float t = 1f - Mathf.Exp(-followSpeed * Time.deltaTime);
        transform.SetPositionAndRotation(
            Vector3.Lerp(transform.position, targetPos, t),
            Quaternion.Slerp(transform.rotation, targetRot, t));
    }
}

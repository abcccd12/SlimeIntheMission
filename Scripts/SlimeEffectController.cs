using UnityEngine;
using MoreMountains.Feedbacks;
using MoreMountains.Tools;

public class SlimeEffectController : MonoBehaviour
{
    [Header("MMF Effects")]
    [SerializeField] private MMF_Player charging;
    [SerializeField] private MMF_Player bounce;
    [SerializeField] private MMF_Player stick;
    [SerializeField] private MMF_Player landing;

    public void PlayCharging()
    {
        if (charging != null)
            charging.PlayFeedbacks();
    }

    public void StopCharging()
    {
        if (charging != null)
            charging.StopFeedbacks();
    }

    // bounce forward=normal하니까 z축돌아가서 이상함. 일단그대로둠

    public void PlayBounce(Vector3 normal)
    {
        if (bounce == null) return;
        bounce.transform.forward = normal; // TODO 이거바꾸기
        bounce.PlayFeedbacks();
    }

    public void PlayStick(Vector3 normal)
    {
        if (stick == null) return;
        stick.transform.forward = normal;
        stick.PlayFeedbacks();
    }

    public void PlayLanding()
    {
        if (landing != null)
            landing.PlayFeedbacks();
    }
}

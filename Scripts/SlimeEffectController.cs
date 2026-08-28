using UnityEngine;
using MoreMountains.Feedbacks; // MMF_Player 는 이 네임스페이스에 있다
using MoreMountains.Tools;

/// <summary>
/// 슬라임 상태별 Feel(MMF_Player) 이펙트를 재생하는 컨트롤러.
///
/// 참조는 인스펙터에서 직접 드래그로 넣는다. (SerializeField = 런타임 검색 0, 가장 효율적)
/// - charging : 차징(직선발사 준비) 이펙트. 방향 무관 → 그냥 재생/정지.
/// - bounce/stick : 방향 의존(파티클). 재생 전에 벽 normal 각도로 '회전'시켜야 함.
/// - landing : 착지/정지 이펙트.
/// </summary>
public class SlimeEffectController : MonoBehaviour
{
    [Header("MMF Effects")]
    [SerializeField] private MMF_Player charging;
    [SerializeField] private MMF_Player bounce;
    [SerializeField] private MMF_Player stick;
    [SerializeField] private MMF_Player landing;

    // ================= Charging (지금 테스트 대상) =================

    /// <summary>차징 이펙트 시작. (shader/particle/sound 한 번에 재생)</summary>
    public void PlayCharging()
    {
        if (charging != null)
            charging.PlayFeedbacks();
    }

    /// <summary>차징 이펙트 정지. (놓거나 차징이 풀렸을 때)</summary>
    public void StopCharging()
    {
        if (charging != null)
            charging.StopFeedbacks();
    }

    // ================= 나머지 (나중에 연결) =================
    //  ⚠️ 방향 의존 이펙트는 transform.forward = normal 이 아니라
    //     Quaternion.FromToRotation(기준축, normal) 으로 'Z축 회전'만 줘야 2.5D 평면이 유지된다.
    //     지금은 charge만 테스트하므로 아래는 초안 그대로 둔다.

    public void PlayBounce(Vector3 normal)
    {
        if (bounce == null) return;
        bounce.transform.forward = normal; // TODO: 2.5D용 회전으로 교체 예정
        bounce.PlayFeedbacks();
    }

    public void PlayStick(Vector3 normal)
    {
        if (stick == null) return;
        stick.transform.forward = normal; // TODO: 2.5D용 회전으로 교체 예정
        stick.PlayFeedbacks();
    }

    public void PlayLanding()
    {
        if (landing != null)
            landing.PlayFeedbacks();
    }
}

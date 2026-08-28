using UnityEngine;

/// <summary>
/// Frog가 발사하는 총알(pistol). 슬라임에 맞으면 넉백 + 데미지 + 사이즈 감소를 한 번에 건다.
///
/// 핵심: SlimeLaunchController.Knockback() 하나가
///   넉백(AddForce) + 데미지(slimeStats.TakeDamage) + 사이즈 감소(size.SetGoo)를 전부 처리한다.
///   그래서 여기서는 슬라임을 찾아 Knockback만 호출하면 된다.
///
/// 총알은 Frog의 오브젝트 풀로 재사용되므로, 맞으면 SetActive(false)로 돌려보낸다.
/// </summary>
public class pistol : MonoBehaviour
{
    [Tooltip("이 태그를 가진 대상에만 반응.")]
    [SerializeField] private string targetTag = "Slime";

    [Tooltip("맞은 뒤 총알을 비활성화(풀 반환)할지.")]
    [SerializeField] private bool deactivateOnHit = true;

    // DOTween으로 이동하는 총알은 트리거 방식이 안정적이다.
    // (총알 콜라이더 IsTrigger 체크 + Rigidbody(Is Kinematic) 필요)
    private void OnTriggerEnter(Collider other)
    {
        TryHit(other);
    }

    // 물리 충돌로 쓰고 싶다면 이쪽도 동작한다. (총알에 non-trigger 콜라이더 + Rigidbody)
    private void OnCollisionEnter(Collision collision)
    {
        TryHit(collision.collider);
    }

    private void TryHit(Collider hit)
    {
        if (!hit.CompareTag(targetTag)) return;
        Debug.Log("hti");
        // 콜라이더가 자식에 있어도 부모의 컨트롤러를 찾는다.
        SlimeLaunchController slime = hit.GetComponentInParent<SlimeLaunchController>();
        if (slime == null) return;

        // 이 한 줄이 넉백 + 데미지 + 사이즈 감소를 모두 처리한다.
        // (인자는 '공격자 위치' = 총알 위치. 슬라임이 이 위치 반대로 밀려난다)
        slime.Knockback(transform.position);

        if (deactivateOnHit)
            gameObject.SetActive(false); // 풀로 반환 (Frog가 재사용)
    }
}

using UnityEngine;

public class pistol : MonoBehaviour
{
    [SerializeField] private string targetTag = "Slime";

    [SerializeField] private bool deactivateOnHit = true; // 안끄니까 총알이 계속남아있음

    // collision은 닷트윈이랑 안맞음. 트리거로바꿈
    private void OnTriggerEnter(Collider other)
    {
        TryHit(other);
    }

    private void OnCollisionEnter(Collision collision)
    {
        TryHit(collision.collider);
    }

    private void TryHit(Collider hit)
    {
        if (!hit.CompareTag(targetTag)) return;
        Debug.Log("hti");
        SlimeLaunchController slime = hit.GetComponentInParent<SlimeLaunchController>();
        if (slime == null) return; // 예외처리 안했더니오류생김

        slime.Knockback(transform.position);

        if (deactivateOnHit)
            gameObject.SetActive(false);
    }
}

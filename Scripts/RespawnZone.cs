using UnityEngine;

public class RespawnZone : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        // GetComponent하니까 자식콜라이더일때 못찾음
        SlimeLaunchController slime = other.GetComponentInParent<SlimeLaunchController>();
        if (slime == null)
            return; // 예외처리 안했더니오류생김

        slime.Respawn();
    }
}

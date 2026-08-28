using UnityEngine;

/// <summary>
/// 맵 아래쪽(낙사 구역)에 큰 Trigger Collider로 배치하는 스크립트.
///
/// 동작:
/// - Collider의 Is Trigger를 켜 둔다.
/// - 슬라임이 이 구역에 들어오면(=맵 밖으로 떨어지면) 리스폰시킨다.
/// - 실제 리스폰 위치/처리는 SlimeLaunchController.Respawn()이 담당한다.
///   (이 스크립트는 "떨어졌다"는 사실만 감지하고 넘겨준다 → 역할 분리)
/// </summary>
public class RespawnZone : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        // 들어온 것이 슬라임인지 확인.
        SlimeLaunchController slime = other.GetComponentInParent<SlimeLaunchController>();
        if (slime == null)
            return;

        // 리스폰 처리는 슬라임 본인에게 맡긴다.
        slime.Respawn();
    }
}

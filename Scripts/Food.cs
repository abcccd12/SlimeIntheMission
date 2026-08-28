using UnityEngine;

/// <summary>
/// 먹이 프리팹에 붙인다. 태그 Slime + 이 스크립트.
/// TiersToGrow가 한 번 먹었을 때 올라가는 크기 단계 수.
/// </summary>
public class Food : MonoBehaviour
{
    [Tooltip("한 번 먹으면 올라갈 단계 수. 1이면 Tiny→Small, Normal→Big.")]
    [SerializeField] private int tiersToGrow = 1;

    public int TiersToGrow => Mathf.Max(0, tiersToGrow);
}

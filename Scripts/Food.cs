using UnityEngine;

public class Food : MonoBehaviour
{
    [SerializeField] private int tiersToGrow = 1; // 음수넣으니까 줄어들어서 max함

    public int TiersToGrow => Mathf.Max(0, tiersToGrow);
}

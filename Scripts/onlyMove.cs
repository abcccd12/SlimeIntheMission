using Opsive.BehaviorDesigner.Runtime.Tasks.Actions.NavMeshTasks;
using UnityEngine;
using DG.Tweening;
public class onlyMove : MonoBehaviour
{
    [SerializeField] private float movespeed = 100f;
    [SerializeField] private float arriveddistance = 0.05f;
    [SerializeField] private Transform left;
    [SerializeField] private Transform right;
    
    [Header("달그락 효과 설정")]
    [SerializeField] private float rattleDuration = 0.2f;
    [SerializeField] private Vector3 rattleStrength = new Vector3(5f, 5f, 5f);
    [SerializeField] private int rattleVibrato = 10;

    private Transform currentTarget;

    private void Start()
    {
        currentTarget = left;
        if(left!=null)  left.SetParent(null);
        if(right!=null)  right.SetParent(null);
        
        // 달그락 여기넣으려다 말음
       
    }

    void Update()
    {
        Patrol();
    }
    
    private void Patrol()
    {
        bool arrived = MoveTo(currentTarget);
        if (arrived)
        {
            currentTarget = currentTarget == left ? right : left;
        }
    }
    
    private bool MoveTo(Transform target)
    {
        Vector3 targetPosition = new Vector3(target.position.x, target.position.y, target.position.z);
        transform.position = Vector3.MoveTowards(transform.position, targetPosition, movespeed * Time.deltaTime);
        float distance = Vector3.Distance(transform.position, targetPosition);

        return distance <= arriveddistance;
    }

    private void OnDestroy()
    {
        transform.DOKill();
    }
}

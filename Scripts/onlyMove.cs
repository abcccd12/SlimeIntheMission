using Opsive.BehaviorDesigner.Runtime.Tasks.Actions.NavMeshTasks;
using UnityEngine;
using DG.Tweening; // DOTween 추가
public class onlyMove : MonoBehaviour
{
    [SerializeField] private float movespeed = 100f;
    [SerializeField] private float arriveddistance = 0.05f;
    [SerializeField] private Transform left;
    [SerializeField] private Transform right;
    
    [Header("달그락 효과 설정")]
    [SerializeField] private float rattleDuration = 0.2f; // 한 사이클당 흔들리는 시간 (짧을수록 촐랑거림)
    [SerializeField] private Vector3 rattleStrength = new Vector3(5f, 5f, 5f); // X, Y, Z 흔들림 각도 (숫자가 클수록 크게 흔들림)
    [SerializeField] private int rattleVibrato = 10; // 흔들림의 빈도 (덜덜덜 횟수)

    private Transform currentTarget;

    private void Start()
    {
        currentTarget = left;
        if(left!=null)  left.SetParent(null);
        if(right!=null)  right.SetParent(null);
        
        // DOTween: X,Y,Z 축으로 무한히 달그락거리는 효과 실행
       
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
        // 기존 이동 로직 그대로 유지
        Vector3 targetPosition = new Vector3(target.position.x, target.position.y, target.position.z);
        transform.position = Vector3.MoveTowards(transform.position, targetPosition, movespeed * Time.deltaTime);
        float distance = Vector3.Distance(transform.position, targetPosition);

        return distance <= arriveddistance;
    }

    private void OnDestroy()
    {
        // 오브젝트가 파괴될 때 달그락 효과도 안전하게 종료
        transform.DOKill();
    }
}
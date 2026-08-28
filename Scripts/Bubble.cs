using System;
using UnityEngine;
using DG.Tweening;
using Opsive.BehaviorDesigner.Runtime.Tasks.Actions.GameObjectTasks;

[RequireComponent(typeof(Collider))] // 콜라이더는 Is Trigger 체크!
public class Bubble : MonoBehaviour
{
    [Header("대상")]
    [SerializeField] private string slimeTag = "Slime";

    [Header("흡수(Absorb)")]
    [Tooltip("비눗방울 중앙으로 빨려드는 시간(초).")]
    [SerializeField] private float absorbDuration = 0.25f;

    [Header("발사(Pop)")]
    [Tooltip("발사 방향(X/Y). 위로 튀기려면 (0,1).")]
    [SerializeField] private Vector2 popDirection = Vector2.up;
    [Tooltip("발사 속도.")]
    [SerializeField] private float popForce = 15f;

    [Header("꿀렁 효과(Hold)")]
    [SerializeField] private float punchScale = 0.2f;
    [SerializeField] private float punchDuration = 0.5f;
    [SerializeField] private float movespeed = 10f;
    [SerializeField] private float arriveddistance = 0.05f;
    [SerializeField] private Transform left;
    [SerializeField] private Transform right;   
    

    private Transform currentTarget;
    
    private SlimeLaunchController _slime;
   
    private bool _holding;   // 슬라임을 품고 있는 중인지
    private Tween _wobble;
  
    private void Start()
    {
        currentTarget = left;
        if(left!=null)  left.SetParent(null);
        if(right!=null)  right.SetParent(null);
    }

    private void Update()
    {
        Patrol();
    }
    private void Patrol()
    {

        if (left == null || right == null) return;
        
        
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

        float distance = Vector3.Distance(transform.position, targetPosition);  // ★ 전체 거리
        return distance <= arriveddistance;
    }
    // Bubble.cs 에 추가
    private void OnDisable()
    {
        _wobble?.Kill();
        // 혹시 아직 슬라임이 자식으로 붙어있으면 안전하게 떼어냄 (같이 파괴/비활성 방지)
        if (_slime != null && _slime.transform.parent == transform)
            _slime.transform.SetParent(null);
        _slime = null;
        _holding = false;   // ★ 이게 있어야 재활성 후 다시 탑승 가능
    }


    // ── 1) 흡수 ──────────────────────────────────────────
    private void OnTriggerEnter(Collider other)
    {
        if (_holding) return;                 // 이미 하나 품고 있으면 무시
        if (!other.CompareTag(slimeTag)) return;

        SlimeLaunchController slime = other.GetComponentInChildren<SlimeLaunchController>();
        if (slime == null)
        {
            Debug.Log("noslime");
            return;
        }
        Debug.Log("haveslime");
        _holding = true;
        _slime = slime;

        // 슬라임을 얼림 + Stuck 상태로 전환 (속도0, isKinematic=true)
        _slime.EnterBubble(gameObject);

        // 중앙으로 부드럽게 이동 (z는 슬라임 원래 값 유지 = 평면 게임)
        // Vector3 center = transform.position;
        // center.z = _slime.transform.position.z;
        //
        // _slime.transform.DOMove(center, absorbDuration)
        //     .SetEase(Ease.OutQuad)
        //     .OnComplete(OnReachedCenter);
        _slime.transform.SetParent(transform);

        // [핵심 2] 자식 좌표계(Local) 기준 중앙 위치를 계산합니다.
        // Z값만 기존 슬라임의 로컬 Z를 유지하고, X와 Y는 비눗방울의 중심인 0으로 설정합니다.
        Vector3 targetLocalPos = new Vector3(0f, 0f, _slime.transform.localPosition.z);

        // [핵심 3] DOMove 대신 DOLocalMove를 사용하여 비눗방울 중심(0,0)으로 이동합니다.
        // 비눗방울이 이동하더라도 로컬 (0,0) 위치가 같이 움직이므로 완벽하게 추적합니다!
        _slime.transform.DOLocalMove(targetLocalPos, absorbDuration)
            .SetEase(Ease.OutQuad)
            .OnComplete(OnReachedCenter);
    }

    

    // ── 2) 대기 ──────────────────────────────────────────
    private void OnReachedCenter()
    {
        // 비눗방울의 자식으로 붙임 (같이 움직이게)
        _slime.transform.SetParent(transform);
        _slime.transform.localPosition = Vector3.zero; 
        // 꿀렁꿀렁 반복
        _wobble = transform
            .DOPunchScale(Vector3.one * punchScale, punchDuration, 5, 0.5f)
            .SetLoops(-1)
            .SetLink(gameObject);
    }

    // ── 3) 발사 ──────────────────────────────────────────
    // 입력/타이머 등 원하는 곳에서 이 함수를 호출하면 튀어나간다.
    public void Pop()
    {
        if (!_holding) return;

        _wobble?.Kill();

        // 부모 해제
        _slime.transform.SetParent(null);

        // 지정 방향으로 발사 (isKinematic 해제 + velocity + Flying 상태)
        Vector3 velocity = ((Vector3)popDirection).normalized * popForce;
        velocity.z = 0f;
        _slime.PopFromBubble(velocity);

        _holding = false;

    }
}

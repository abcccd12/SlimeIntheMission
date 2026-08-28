using System;
using UnityEngine;
using DG.Tweening;
using Opsive.BehaviorDesigner.Runtime.Tasks.Actions.GameObjectTasks;

[RequireComponent(typeof(Collider))] // is trigger 체크!!
public class Bubble : MonoBehaviour
{
    [Header("대상")]
    [SerializeField] private string slimeTag = "Slime";

    [Header("흡수(Absorb)")]
    [SerializeField] private float absorbDuration = 0.25f;

    [Header("발사(Pop)")]
    [SerializeField] private Vector2 popDirection = Vector2.up;
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
   
    private bool _holding;
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

        float distance = Vector3.Distance(transform.position, targetPosition);
        return distance <= arriveddistance;
    }

    private void OnDisable()
    {
        _wobble?.Kill();
        if (_slime != null && _slime.transform.parent == transform)
            _slime.transform.SetParent(null);
        _slime = null;
        _holding = false; // 이거안끄면 다시못탐 왜
    }


    private void OnTriggerEnter(Collider other)
    {
        if (_holding) return;
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

        _slime.EnterBubble(gameObject);

        // Vector3 center = transform.position;
        // center.z = _slime.transform.position.z;
        //
        // _slime.transform.DOMove(center, absorbDuration)
        //     .SetEase(Ease.OutQuad)
        //     .OnComplete(OnReachedCenter);
        _slime.transform.SetParent(transform);

        // DOMove하니까 방울움직일때 슬라임안따라감. 로컬로바꿈
        Vector3 targetLocalPos = new Vector3(0f, 0f, _slime.transform.localPosition.z);

        _slime.transform.DOLocalMove(targetLocalPos, absorbDuration)
            .SetEase(Ease.OutQuad)
            .OnComplete(OnReachedCenter);
    }

    

    private void OnReachedCenter()
    {
        _slime.transform.SetParent(transform);
        _slime.transform.localPosition = Vector3.zero; 
        _wobble = transform
            .DOPunchScale(Vector3.one * punchScale, punchDuration, 5, 0.5f)
            .SetLoops(-1)
            .SetLink(gameObject);
    }

    public void Pop()
    {
        if (!_holding) return;

        _wobble?.Kill();

        _slime.transform.SetParent(null);

        Vector3 velocity = ((Vector3)popDirection).normalized * popForce;
        velocity.z = 0f;
        _slime.PopFromBubble(velocity);

        _holding = false;

    }
}

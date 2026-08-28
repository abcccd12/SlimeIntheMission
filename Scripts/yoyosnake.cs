using System;
using UnityEngine;
using DG.Tweening;

public class yoyosnake : MonoBehaviour
{
    [Header("Rotation (Spin)")]
    [SerializeField] private Transform pivotTransform;
    [Tooltip("한 바퀴(360도) 도는 데 걸리는 시간")]
    [SerializeField] private float spinDuration = 2f;
    [Tooltip("회전의 역동성 (휙! 돌고 천천히)")]
    [SerializeField] private Ease spinEase = Ease.InOutSine;

    [Header("Radius (Stretch)")]
    [Tooltip("스윙하는 타겟(뱀 자신)")]
    [SerializeField] private Transform target;
    [Tooltip("최대로 늘어날 추가 거리")]
    [SerializeField] private float stretchDistance = 3f;
    [Tooltip("늘어났다 줄어드는 느낌")]
    [SerializeField] private Ease stretchEase = Ease.OutQuad;

    private Vector3 baseLocalPos;
    private Tween spinTween;
    private Tween radiusTween;

    private void Awake()
    {
        if (target == null) target = transform;
        if (pivotTransform == null) pivotTransform = transform; 
        
        // 중요: 늘어나고 줄어드는 기준점이 될 원래의 로컬 위치 저장
        baseLocalPos = target.localPosition;
    }

    private void OnEnable()  => PlayYoyo();
    private void OnDisable() => StopYoyo();
    
    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Slime")) return;
        SlimeLaunchController slime = other.GetComponentInParent<SlimeLaunchController>();
        if (slime == null) return;

        // 이 한 줄이 넉백 + 데미지 + 사이즈 감소를 모두 처리한다.
        // (인자는 '공격자 위치' = 총알 위치. 슬라임이 이 위치 반대로 밀려난다)
        slime.Knockback(transform.position);
    }
    
    private void PlayYoyo()
    {
        spinTween?.Kill();
        radiusTween?.Kill();

        // 1. 회전 애니메이션 (속도 완급조절)
        // Ease.InOutSine을 쓰면 회전할 때 가속도가 붙었다가 끝에서 살짝 느려지는 쫀득한 맛이 생깁니다.
        spinTween = pivotTransform.DORotate(new Vector3(0, 0, -360), spinDuration, RotateMode.FastBeyond360)
            .SetEase(spinEase)               // 역동적인 회전 속도!
            .SetLoops(-1, LoopType.Restart)
            .SetRelative(true)
            .SetLink(gameObject);

        // 2. 거리(반경) 확장 애니메이션 (요요처럼 뻗어나감)
        // 로컬 좌표계를 기준으로 Y축(아래) 방향으로 쭉 늘어났다가 돌아옵니다.
        // 스윙 한 바퀴(spinDuration) 도는 동안 늘어났다(1/2) 줄어들기(1/2) 위해 시간을 반으로 나눕니다.
        radiusTween = target.DOLocalMoveY(baseLocalPos.y - stretchDistance, spinDuration / 2f)
            .SetEase(stretchEase)
            .SetLoops(-1, LoopType.Yoyo)     // 갔다가 돌아와야 하므로 Yoyo!
            .SetLink(gameObject);
    }

    private void StopYoyo()
    {
        spinTween?.Kill();
        radiusTween?.Kill();
        
        // 꺼질 때 원래 위치로 복구
        if (target != null) target.localPosition = baseLocalPos;
    }
}
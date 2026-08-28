using System;
using UnityEngine;
using DG.Tweening;
public class Snake : MonoBehaviour
{
    [Header("Squash & Stretch (Idle)")]
    [Tooltip("한 번 눌렸다 펴지는 데 걸리는 시간(초). 작을수록 빠르게 팔딱거린다.")]
    [SerializeField] private float duration = 0.6f;
    [Tooltip("스케일을 적용할 대상. 비우면 자기 자신을 사용.")]
    [SerializeField] private Transform target;
    [Tooltip("눌림 세기(0~1). 0.1이면 세로 10% 줄고 가로 10% 늘어난다.")]
    [SerializeField] private float squashAmount = 0.12f;

    [Tooltip("보간 곡선. InOutSine이면 부드러운 호흡 느낌.")]
    [SerializeField] private Ease ease = Ease.InOutSine;

    [SerializeField] private float spinduration = 2f;
    [SerializeField] private Transform pivotTransform;
    private Tween spinTween;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private Vector3 baseScale;   // 원래 크기 (기준값)
    private Tween squashTween;
    // [수정] baseScale 저장을 Start()에서 Awake()로 옮김.
    //  이유: OnEnable(→PlayIdle)이 Start보다 먼저 실행돼서, Start에서 저장하면
    //  PlayIdle 시점엔 baseScale이 아직 (0,0,0)이라 스케일이 0까지 줄어들었다.
    private void Awake()
    {
        if (target == null) target = transform; // 안전: 미할당 시 자기 자신
        baseScale = target.localScale;
    }

    private void OnEnable()  => PlayIdle();
    private void OnDisable() => StopIdle();
    
    private void PlayIdle()
    {
        squashTween?.Kill();

        // 부피 유지 느낌: 세로(y)가 줄면 가로(x/z)는 늘어난다.
        Vector3 squashed = new Vector3(
            baseScale.x * (1f + squashAmount),
            baseScale.y * (1f - squashAmount),
            baseScale.z * (1f + squashAmount)
        );


        squashTween = target.DOScale(squashed, duration)
            .SetEase(ease)
            .SetLoops(-1, LoopType.Yoyo)
            .SetLink(gameObject);
        
        spinTween = pivotTransform.DORotate(new Vector3(0, 0, -360), spinduration, RotateMode.FastBeyond360)
            .SetEase(Ease.Linear)            // 중요: 가감속 없이 일정한 속도로 돕니다!
            .SetLoops(-1, LoopType.Restart)  // 중요: Yoyo가 아니라 0도->360도 반복(Restart)
            .SetRelative(true)               // 현재 각도 기준으로 계속 더해서 돌도록 설정
            .SetLink(gameObject);
    }
    private void StopIdle()
    {
        squashTween?.Kill();
        if (target != null) target.localScale = baseScale;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Slime")) return;
        SlimeLaunchController slime = other.GetComponentInParent<SlimeLaunchController>();
        if (slime == null) return;

        // 이 한 줄이 넉백 + 데미지 + 사이즈 감소를 모두 처리한다.
        // (인자는 '공격자 위치' = 총알 위치. 슬라임이 이 위치 반대로 밀려난다)
        slime.Knockback(transform.position);
    }
}

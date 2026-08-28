using System;
using UnityEngine;
using DG.Tweening;
public class Snake : MonoBehaviour
{
    [Header("Squash & Stretch (Idle)")]
    [SerializeField] private float duration = 0.6f;
    [SerializeField] private Transform target;
    [SerializeField] private float squashAmount = 0.12f;

    [SerializeField] private Ease ease = Ease.InOutSine;

    [SerializeField] private float spinduration = 2f;
    [SerializeField] private Transform pivotTransform;
    private Tween spinTween;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private Vector3 baseScale;
    private Tween squashTween;
    // start에넣으니까 onenable이 먼저돌아서 스케일0됨. 한참찾음
    private void Awake()
    {
        if (target == null) target = transform;
        baseScale = target.localScale;
    }

    private void OnEnable()  => PlayIdle();
    private void OnDisable() => StopIdle();
    
    private void PlayIdle()
    {
        squashTween?.Kill();

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
            .SetEase(Ease.Linear)            // yoyo하니까 왔다갔다함 짜증
            .SetLoops(-1, LoopType.Restart)
            .SetRelative(true)
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

        slime.Knockback(transform.position);
    }
}

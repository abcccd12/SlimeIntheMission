using System;
using UnityEngine;
using DG.Tweening;

public class yoyosnake : MonoBehaviour
{
    [Header("Rotation (Spin)")]
    [SerializeField] private Transform pivotTransform;
    [SerializeField] private float spinDuration = 2f; // 한바퀴
    [SerializeField] private Ease spinEase = Ease.InOutSine;

    [Header("Radius (Stretch)")]
    [SerializeField] private Transform target;
    [SerializeField] private float stretchDistance = 3f;
    [SerializeField] private Ease stretchEase = Ease.OutQuad;

    private Vector3 baseLocalPos;
    private Tween spinTween;
    private Tween radiusTween;

    private void Awake()
    {
        if (target == null) target = transform;
        if (pivotTransform == null) pivotTransform = transform; 
        
        baseLocalPos = target.localPosition; // 저장안하니까 계속늘어남 뭔데
    }

    private void OnEnable()  => PlayYoyo();
    private void OnDisable() => StopYoyo();
    
    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Slime")) return;
        SlimeLaunchController slime = other.GetComponentInParent<SlimeLaunchController>();
        if (slime == null) return;

        slime.Knockback(transform.position);
    }
    
    private void PlayYoyo()
    {
        spinTween?.Kill();
        radiusTween?.Kill();

        spinTween = pivotTransform.DORotate(new Vector3(0, 0, -360), spinDuration, RotateMode.FastBeyond360)
            .SetEase(spinEase)
            .SetLoops(-1, LoopType.Restart)
            .SetRelative(true)
            .SetLink(gameObject);

        // 한바퀴동안 늘었다줄어야해서 /2 아니면타이밍안맞음
        radiusTween = target.DOLocalMoveY(baseLocalPos.y - stretchDistance, spinDuration / 2f)
            .SetEase(stretchEase)
            .SetLoops(-1, LoopType.Yoyo)
            .SetLink(gameObject);
    }

    private void StopYoyo()
    {
        spinTween?.Kill();
        radiusTween?.Kill();
        
        if (target != null) target.localPosition = baseLocalPos;
    }
}

using UnityEngine;
using DG.Tweening;
using MoreMountains.Feedbacks; // DOTween Pro

/// <summary>
/// 뼈/애니메이션 없이 localScale만 움직여 만드는 idle "말랑" 호흡(squash & stretch) 연출
/// + OnTriggerEnter 시 일정 간격으로 총알을 발사하는 기능.
///
/// 총알은 '오브젝트 풀'로 재사용한다.
/// - 시작할 때 bullet 복사본을 poolSize개 미리 만들어 꺼둔다.
/// - 쏠 때 꺼진 총알 하나를 켜서 DOTween으로 startpoint→muzzle 방향으로 날린다.
/// - 다 날아가면 다시 꺼서(=reload) 다음 발사에 재사용한다. (Instantiate/Destroy 반복 없음)
/// </summary>
public class Frog : MonoBehaviour
{
    [Header("Squash & Stretch (Idle)")]
    [Tooltip("한 번 눌렸다 펴지는 데 걸리는 시간(초). 작을수록 빠르게 팔딱거린다.")]
    [SerializeField] private float duration = 0.6f;

    [Tooltip("눌림 세기(0~1). 0.1이면 세로 10% 줄고 가로 10% 늘어난다.")]
    [SerializeField] private float squashAmount = 0.12f;

    [Tooltip("보간 곡선. InOutSine이면 부드러운 호흡 느낌.")]
    [SerializeField] private Ease ease = Ease.InOutSine;

    [Tooltip("스케일을 적용할 대상. 비우면 자기 자신을 사용.")]
    [SerializeField] private Transform target;

    [Header("Shooting")]
    [Tooltip("발사할 총알(원본/템플릿). 이걸 복제해서 풀을 만든다.")]
    [SerializeField] private GameObject bullet;

    [Tooltip("총알이 출발하는 지점.")]
    [SerializeField] private GameObject startpoint;

    [Tooltip("총알이 향하는 목표 지점. startpoint→muzzle 방향(X/Y 평면)으로 직진한다.")]
    [SerializeField] private Transform[] muzzle;

    [Tooltip("발사 간격(초). 작을수록 빠르게 연사.")]
    [SerializeField] private float fireInterval = 0.5f;

    [Tooltip("총알 속도(월드 단위/초).")]
    [SerializeField] private float bulletSpeed = 12f;

    [Tooltip("총알이 이 거리만큼 날아가면 회수(비활성)된다.")]
    [SerializeField] private float bulletRange = 15f;

    [Tooltip("미리 만들어둘 총알 개수. 화면에 동시에 떠있을 최대 총알 수. (연사 간격 대비 넉넉하게)")]
    [SerializeField] private int poolSize = 8;

    [Tooltip("트리거에서 벗어나면 발사를 멈출지. 끄면 한 번 시작하면 계속 쏜다.")]
    [SerializeField] private bool stopOnTriggerExit = true;

    [Tooltip("이 태그를 가진 대상이 들어올 때만 발사. 비우면 아무거나.")]
    [SerializeField] private string triggerTag = "Slime";

    [SerializeField] private MMF_Player frogsoud;
    private Vector3 baseScale;   // 원래 크기 (기준값)
    private Tween squashTween;

    private GameObject[] _pool;
    private int _poolCursor;
  
    private Vector3 _bulletScale = Vector3.one;   // 원본이 씬에서 보이던 실제 크기(lossyScale) 기억
    private bool _targetInZone;
    private float _fireTimer;

    private bool _isShooting = false;
    private bool onetime = false;
    
    private void Awake()
    {
        if (target == null) target = transform;
        baseScale = target.localScale;
        
        BuildBulletPool();
        foreach (Transform t in muzzle)
        {
            if (t != null) t.SetParent(null, true);
            
        }
    }

    private void OnEnable()  => PlayIdle();
    private void OnDisable() => StopIdle();

    // ================= Squash & Stretch =================

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
    }

    private void StopIdle()
    {
        squashTween?.Kill();
        if (target != null) target.localScale = baseScale;
    }

    // ================= Shooting =================

    /// <summary>시작 시 총알 풀을 미리 만들어둔다. (이후엔 Instantiate 없이 재사용)</summary>
    private void BuildBulletPool()
    {
        if (bullet == null) return;

        // 원본이 부모 스케일 때문에 씬에서 보이던 '실제 크기'를 기억.
        // (월드로 복제하면 부모 배율이 빠져 작아지는 문제 보정용)
        _bulletScale = bullet.transform.lossyScale;

        bullet.SetActive(false); // 원본은 템플릿으로만 쓰고 화면엔 안 보이게

        _pool = new GameObject[poolSize];
        for (int i = 0; i < poolSize; i++)
        {
            GameObject b = Instantiate(bullet);    // 부모 없이(월드) 생성 → 개구리 따라다니지 않음
            b.transform.SetParent(null, true);
            b.transform.localScale = _bulletScale; // 원래 보이던 크기로 복원
            b.SetActive(false);
            _pool[i] = b;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        
        if (!IsTarget(other)) return;
        _targetInZone = true;
        if(onetime) return;
        frogsoud.PlayFeedbacks();
       
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsTarget(other)) return;
        _targetInZone = false;
        frogsoud.StopFeedbacks();
        onetime = true; // 한 번만 실행하도록 설정
    }
    private void Update()
    {
        if (!_targetInZone) return;

        _fireTimer += Time.deltaTime;
        if (_fireTimer >= fireInterval)
        {
            _fireTimer -= fireInterval;   // 정확한 간격 유지 (=0보다 이게 더 정확)
            Fire();
        }
    }

    private bool IsTarget(Collider other)
        => string.IsNullOrEmpty(triggerTag) || other.CompareTag(triggerTag);

    private void Fire()
    {
        foreach (Transform t in muzzle)
        {
            if(t==null) continue;
            Firebullet(t);
        }
    }
    
    private void Firebullet(Transform target)
    {
        GameObject b = GetPooledBullet();
        if (b == null) return; // 풀이 전부 사용 중이면 이번 발사는 건너뜀 (poolSize 늘리면 해결)

        // 출발점: startpoint (없으면 muzzle, 그것도 없으면 개구리 자신)
        Transform origin = startpoint != null ? startpoint.transform
                         : (target != null ? target : transform);
        Vector3 startPos = origin.position;

        // 방향: startpoint → muzzle, 단 X/Y 평면만 (Z 고정)
        Vector3 dir = (target != null) ? (target.position - startPos) : origin.right;
        dir.z = 0f;
        if (dir.sqrMagnitude < 0.0001f) dir = Vector3.right; // 두 점이 겹치면 안전하게 오른쪽
        dir.Normalize();

        // 목적지: 방향으로 bulletRange만큼 직진 (Z는 출발점 Z 유지)
        Vector3 dest = startPos + dir * bulletRange;
        dest.z = startPos.z;

        b.transform.DOKill(); // 혹시 남은 트윈 정리
        b.transform.position = startPos;
        b.transform.localScale = _bulletScale; // 매 발사마다 크기 보정(안전)
        b.transform.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(90f, 0 , 0);
        
        
        b.SetActive(true);

        float travelTime = bulletRange / Mathf.Max(0.01f, bulletSpeed);

        b.transform.DOMove(dest, travelTime)
            .SetEase(Ease.Linear)
            .SetLink(b)
            .OnComplete(() => b.SetActive(false)); // 다 날아가면 꺼서 재사용(reload)
    }

    /// <summary>꺼져있는(사용 가능한) 총알을 순환하며 하나 찾아 준다. 없으면 null.</summary>
    private GameObject GetPooledBullet()
    {
        for (int i = 0; i < _pool.Length; i++)
        {
            _poolCursor = (_poolCursor + 1) % _pool.Length;
            if (!_pool[_poolCursor].activeSelf)
                return _pool[_poolCursor];
        }
        return null;
    }
}

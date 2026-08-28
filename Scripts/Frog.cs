using UnityEngine;
using DG.Tweening;
using MoreMountains.Feedbacks; // DOTween Pro

public class Frog : MonoBehaviour
{
    [Header("Squash & Stretch (Idle)")]
    [SerializeField] private float duration = 0.6f;

    [SerializeField] private float squashAmount = 0.12f;

    [SerializeField] private Ease ease = Ease.InOutSine;

    [SerializeField] private Transform target;

    [Header("Shooting")]
    [SerializeField] private GameObject bullet;

    [SerializeField] private GameObject startpoint;

    [SerializeField] private Transform[] muzzle;

    [SerializeField] private float fireInterval = 0.5f;

    [SerializeField] private float bulletSpeed = 12f;

    [SerializeField] private float bulletRange = 15f;

    [SerializeField] private int poolSize = 8; // 8개로하니까 연사때 모자람 나중에늘리기

    [SerializeField] private bool stopOnTriggerExit = true;

    [SerializeField] private string triggerTag = "Slime";

    [SerializeField] private MMF_Player frogsoud;
    private Vector3 baseScale;
    private Tween squashTween;

    private GameObject[] _pool;
    private int _poolCursor;
  
    private Vector3 _bulletScale = Vector3.one; // 부모빼니까 총알작아짐 lossyscale로기억
    private bool _targetInZone;
    private float _fireTimer;

    private bool _isShooting = false;
    private bool onetime = false;
    
    private void Awake()
    {
        if (target == null) target = transform; // 인스펙터비워두면null임
        baseScale = target.localScale;
        
        BuildBulletPool();
        foreach (Transform t in muzzle)
        {
            if (t != null) t.SetParent(null, true); // 부모에있으면 개구리따라가서 총알방향이상함
            
        }
    }

    private void OnEnable()  => PlayIdle();
    private void OnDisable() => StopIdle();

    private void PlayIdle()
    {
        squashTween?.Kill();

        // y만줄이니까 납작해보여서 xz늘림
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

    private void BuildBulletPool()
    {
        if (bullet == null) return;

        _bulletScale = bullet.transform.lossyScale;

        bullet.SetActive(false);

        _pool = new GameObject[poolSize];
        for (int i = 0; i < poolSize; i++)
        {
            GameObject b = Instantiate(bullet);
            b.transform.SetParent(null, true);
            b.transform.localScale = _bulletScale;
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
        onetime = true; // 이거안하니까 들어갈때마다소리남
    }
    private void Update()
    {
        if (!_targetInZone) return;

        _fireTimer += Time.deltaTime;
        if (_fireTimer >= fireInterval)
        {
            _fireTimer -= fireInterval; // =0하니까 간격밀림
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
        if (b == null) return; // 예외처리 안했더니오류생김 풀모자랄때

        Transform origin = startpoint != null ? startpoint.transform
                         : (target != null ? target : transform);
        Vector3 startPos = origin.position;

        Vector3 dir = (target != null) ? (target.position - startPos) : origin.right;
        dir.z = 0f;
        if (dir.sqrMagnitude < 0.0001f) dir = Vector3.right;
        dir.Normalize();

        Vector3 dest = startPos + dir * bulletRange;
        dest.z = startPos.z;

        b.transform.DOKill();
        b.transform.position = startPos;
        b.transform.localScale = _bulletScale;
        b.transform.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(90f, 0 , 0);
        
        
        b.SetActive(true);

        float travelTime = bulletRange / Mathf.Max(0.01f, bulletSpeed);

        b.transform.DOMove(dest, travelTime)
            .SetEase(Ease.Linear)
            .SetLink(b)
            .OnComplete(() => b.SetActive(false));
    }

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

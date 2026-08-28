using System.Collections; // 코루틴쓰려면이거필요 자꾸까먹음
using UnityEngine;
using MoreMountains.Feedbacks;
using Animancer;
using RayFire;
using DG.Tweening;
[RequireComponent(typeof(Rigidbody))]
public class SlimeLaunchController : MonoBehaviour
{

    public enum SlimeState
    {
        Stuck,
        Flying,   // 이때조준하면안됨
        Sliding,
        Dead
    }

    public enum AimState
    {
        None,
        Aiming  // 미끄러지면서조준해야해서 상태나눔. 한개로하니까 미끄럼멈춤
    }
    

    [Header("발사 설정")]
    [Tooltip("당길 수 있는 최대 거리(월드 단위). 이보다 멀리 당겨도 힘은 더 세지지 않는다.")]
    [SerializeField] private float maxDragDistance = 3f;

    [Tooltip("드래그 거리를 발사 속도로 바꾸는 배율. 클수록 같은 거리라도 더 빠르게 날아간다.")]
    [SerializeField] private float launchPower = 5f;

    [Tooltip("발사/튕김 직후 콜라이더를 꺼 두는 시간(초). 같은 벽에 즉시 다시 붙는 걸 막는다.")]
    [SerializeField] private float launchColliderOffTime = 0.12f;

    [Header("[추가] 벽 붙기 / 튕기기 설정")]
    [Tooltip("이 값 이상으로 당겨서 발사하면(=최대 당김) 벽에 붙지 않고 튕긴다. 0~1 사이 값.")]
    [SerializeField] private float maxPullThreshold = 0.95f;

    [Tooltip("벽에서 튕길 때 속도 배율. 1이면 속도 유지, >1이면 더 빠르게, <1이면 느리게 튕긴다.")]
    [SerializeField] private float bounceSpeedMultiplier = 1f;

    [Header("[추가] MaxTrigger(직선 발사) 설정")]
    [Tooltip("maxPullThreshold 이상으로 '유지'해야 하는 시간(초). 이 시간 동안 계속 당기고 있으면 직선 발사가 준비된다.")]
    [SerializeField] private float maxTriggerHoldTime = 1f;

    [Tooltip("직선 발사 속도 배율. 1이면 일반 발사와 같은 크기로, >1이면 더 빠르게 직선으로 날아간다.")]
    [SerializeField] private float straightShotSpeedMultiplier = 1f;

    [Tooltip("직선 미리보기 선의 길이(월드 단위). 준비 완료 시 이 길이만큼 일직선으로 보여준다.")]
    [SerializeField] private float straightLinePreviewLength = 8f;

    [Tooltip("직선 미리보기 선의 점 개수. 직선이라 2개면 충분하다.")]
    [SerializeField] private int straightLinePointCount = 2;

    [Header("[추가] 미끄럼벽(Slippery) 주행 설정")]
    [Tooltip("미끄럼벽 표면을 따라 흘러내리는 속도. 클수록 빨리 미끄러진다.")]
    [SerializeField] private float slideSpeed = 2f;

    [Tooltip("미끄러지는 동안 벽에 붙어있게 벽 안쪽으로 살짝 눌러주는 정도. 0이면 순수하게 표면 방향으로만 흐른다.")]
    [SerializeField] private float slideStickForce = 0.5f;

    [Header("궤적선(TrajectoryLine) 설정")]
    [Tooltip("궤적을 몇 개의 점으로 그릴지. 많을수록 곡선이 부드럽지만 계산량이 늘어난다.")]
    [SerializeField] private int trajectoryPointCount = 30;

    [Tooltip("궤적 점 사이의 시간 간격(초). 클수록 더 먼 미래까지(긴 궤적) 예측해서 그린다.")]
    [SerializeField] private float trajectoryTimeStep = 0.05f;

    [Tooltip("궤적선을 그릴 LineRenderer. TrajectoryLine 오브젝트에 붙은 것을 여기에 연결한다.")]
    [SerializeField] private LineRenderer trajectoryLineRenderer;

    [Header("참조")]
    [Tooltip("발사할 Rigidbody. 비워두면 이 오브젝트에서 자동으로 가져온다.")]
    [SerializeField] private Rigidbody rb;

    [Tooltip("마우스 좌표를 월드 좌표로 바꿀 때 쓰는 카메라. 비워두면 Camera.main을 사용한다.")]
    [SerializeField] private Camera cam;

    [Tooltip("슬라임 체력/자원 관리. 비워두면 이 오브젝트에서 자동으로 가져온다.")]
    [SerializeField] private SlimeStats slimeStats;

    [Tooltip("죽었을 때 리스폰까지 기다리는 시간(초). 0이면 즉시 리스폰. 튕기는 모습을 잠깐 보여주려면 0.5 정도.")]
    [SerializeField] private float respawnDelay = 0.5f;

    [Tooltip("슬라임 겉모습(squash/stretch) 담당. 비워두면 자식에서 자동으로 가져온다.")]
    [SerializeField] private SlimeVisualController visuals;

    [SerializeField] private float straightShotGravityDelay = 0.5f;

    private Coroutine _straightShotGravityCoroutine;

    [SerializeField] private SlimeSizeController sizeController;

    [SerializeField] private SlimeEffectController effects;
    private bool _chargingFxPlaying; // charge 이펙트가 지금 재생 중인지 (중복 재생 방지)
    [SerializeField] private Transform bounceFxPivot;
    private ParticleSystem _bounceParticle;
    [SerializeField] private GameObject rightBounceFxPrefab;
    [SerializeField] private GameObject leftBounceFxPrefab;
    [SerializeField] private GameObject verticleBounceFxprefab;
    
    [SerializeField] private MMF_Player hitfeedback;
    [SerializeField] private float knockbackForce = 10f;
    [SerializeField] private float knockbackUpForce = 3f;

    private GameObject _currentBubble;

    private Plane _dragPlane;

    [Header("상태 (디버그용, 실행 중 확인)")]
    [SerializeField] private SlimeState moveState = SlimeState.Stuck;
    [SerializeField] private AimState aimState = AimState.None; // [추가]
    [SerializeField] private MMF_Player charging_fx;   
    [SerializeField] private MMF_Player normaljump_fx;    
    [SerializeField] private MMF_Player strightlaunch_fx;    
    [SerializeField] private MMF_Player stick_fx;
    [SerializeField] private MMF_Player bounce_fx;
    [SerializeField] private AnimancerComponent animancer;
    [SerializeField] private AnimationClip launchAnimation;

    [SerializeField] private ParticleSystem popparticle;

    [SerializeField] private AudioSource popaudio;
    [SerializeField] private AudioSource enter_buble;

    private Vector3 anchor;
    private bool lastLaunchWasMaxPull;

    private float _maxPullHoldTimer;

    private bool _maxTriggerReady;

    public bool _currentLaunchIsStraightShot;

    private int _remainingMaxBounces;

    private Vector3 _respawnPosition;

    private bool _isRespawning;

    private bool _isStageCleared;

    private Coroutine _respawnCoroutine;

    private Vector3 _velocityBeforeCollision;

    private float _fixedZ;

    private Vector3 _slideVelocity;

    private Vector3 _stuckWallNormal = Vector3.up;
    private Collider _slimeCollider;
    private Collider _ignoredWallCollider;
    private float _ignoreWallUntil;
    private Coroutine _colliderOffRoutine;

    private Vector3 _dragStartWorld;

    private Vector3[] _trajectoryPoints;
    private Vector3 windforce;
    private bool _controlLocked;

    public bool IsStuck => moveState == SlimeState.Stuck;   // 착지 판정용

    public void SetControlLocked(bool locked)
    {
        _controlLocked = locked;
    }

    public void SetWind(Vector3 w)
    {
        windforce = w;
    }
    
    public void StopSliding()
    {
    if (moveState != SlimeState.Sliding) return;
    moveState = SlimeState.Flying; // 또는 Stuck
    rb.useGravity = true;
    _slideVelocity = Vector3.zero;
    }

    private void Awake()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        if (cam == null)
            cam = Camera.main;

        _trajectoryPoints = new Vector3[trajectoryPointCount];

        _fixedZ = transform.position.z; // z고정안하니까 슬라임뒤로날아감

        if (slimeStats == null)
            slimeStats = GetComponent<SlimeStats>();

        if (visuals == null)
            visuals = GetComponentInChildren<SlimeVisualController>();

        if (sizeController == null)
            sizeController = GetComponent<SlimeSizeController>();

        _slimeCollider = GetComponent<Collider>();

        if (effects == null)
            effects = GetComponentInChildren<SlimeEffectController>();

        _respawnPosition = transform.position;

        moveState = SlimeState.Stuck;
        rb.useGravity = false;           // 붙어있는데 중력켜니까 미끄러짐
        rb.linearVelocity = Vector3.zero;
        rb.isKinematic = true;           // kinematic안하니까 붙어있는데덜덜거림

        if (trajectoryLineRenderer == null)
        {
            Debug.LogWarning($"[{name}] trajectoryLineRenderer가 연결되지 않았습니다. 궤적선은 그리지 않습니다.");
        }
        else
        {
            trajectoryLineRenderer.useWorldSpace = true; // false하니까 선위치다틀림
            trajectoryLineRenderer.positionCount = trajectoryPointCount;
            trajectoryLineRenderer.enabled = false; // 처음엔 숨김
        }
    }

    
    private void Update()
    {
        
        if (_isStageCleared || _isRespawning || _controlLocked)   // ★ _controlLocked 추가
            return;

        if (IsPointerDown())
            OnPointerDown(GetPointerScreenPosition());
        else if (aimState == AimState.Aiming && IsPointerHeld())
            OnPointerDrag(GetPointerScreenPosition());
        else if (aimState == AimState.Aiming && IsPointerUp())
            OnPointerUp(GetPointerScreenPosition());
    }

    private bool IsPointerDown() => Input.GetMouseButtonDown(0);

    private bool IsPointerHeld() => Input.GetMouseButton(0);

    private bool IsPointerUp() => Input.GetMouseButtonUp(0);

    private Vector3 GetPointerScreenPosition() => Input.mousePosition;

    private void OnPointerDown(Vector3 screenPos)
    {
        if (moveState != SlimeState.Stuck && moveState != SlimeState.Sliding)
            return; // 날아가는데 조준됨 짜증

        // if (!IsPointerOnThisSlime(screenPos))
        //     return; // 슬라임클릭해야하는데 아무데나눌러도됨. 일단꺼둠 모바일에서애매함

        _dragPlane = new Plane(Vector3.forward, transform.position); // ScreenToWorldPoint하니까 z튀어서 이걸로바꿈

        aimState = AimState.Aiming;

        if (moveState == SlimeState.Stuck)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.useGravity = false;
        } // sliding일때 여기넣으니까 미끄럼멈춤 절대건드리지마

        _dragStartWorld = PointerToWorld(screenPos); // 당김의 기준점
        anchor = _dragStartWorld - transform.position;
        if (visuals != null) visuals.BeginPull();

        _maxPullHoldTimer = 0f;
        _maxTriggerReady = false;

        if (trajectoryLineRenderer != null)
            trajectoryLineRenderer.enabled = true; // 드래그 중에만 궤적선 표시
    }

    private void OnPointerDrag(Vector3 screenPos)
    {
        _dragStartWorld = transform.position + anchor;
        
        Vector3 currentWorld = PointerToWorld(screenPos);

        Vector3 launchVelocity = CalculateLaunchVelocity(currentWorld);

        Vector3 dragVector = _dragStartWorld - currentWorld;
        float clampedDistance = Mathf.Min(dragVector.magnitude, maxDragDistance);
        float chargeRatio = clampedDistance / maxDragDistance;

        if (visuals != null) visuals.UpdatePull(launchVelocity, chargeRatio);

        if (chargeRatio >= maxPullThreshold)
        {
            _maxPullHoldTimer += Time.deltaTime;
            if (_maxPullHoldTimer >= maxTriggerHoldTime)
                _maxTriggerReady = true;
        }
        else
        {
            _maxPullHoldTimer = 0f;
            _maxTriggerReady = false;
        }

        if (_maxTriggerReady && !_chargingFxPlaying)
        {
            _chargingFxPlaying = true;
            if(charging_fx != null) charging_fx.PlayFeedbacks(); // 매프레임하니까 소리겹침
        }
        else if (!_maxTriggerReady && _chargingFxPlaying)
        {
            _chargingFxPlaying = false;
            if (charging_fx != null) charging_fx.StopFeedbacks();
        }

        if (_maxTriggerReady)
            DrawStraightLinePreview(transform.position, launchVelocity);
        else
            DrawTrajectory(transform.position, launchVelocity);
    }

    private void OnPointerUp(Vector3 screenPos)
    {
        
        _dragStartWorld = transform.position + anchor;
        Vector3 currentWorld = PointerToWorld(screenPos);

        Vector3 dragVector = _dragStartWorld - currentWorld;
        float clampedDistance = Mathf.Min(dragVector.magnitude, maxDragDistance);
        float chargeRatio = clampedDistance / maxDragDistance;

        _currentLaunchIsStraightShot = _maxTriggerReady; // 세게당긴다고직선되는거아님. 유지안하니까 자꾸일반발사됨

        _remainingMaxBounces = _currentLaunchIsStraightShot ? 1 : 0;

        lastLaunchWasMaxPull = _currentLaunchIsStraightShot;

        Vector3 launchVelocity = CalculateLaunchVelocity(currentWorld);

        Launch(launchVelocity, _currentLaunchIsStraightShot);

        _maxPullHoldTimer = 0f;
        _maxTriggerReady = false;

        if (_chargingFxPlaying)
        {
            _chargingFxPlaying = false;
            if (charging_fx != null) charging_fx.StopFeedbacks();
        }
    }
    
    public void Knockback(Vector3 attackerposition)
    {
        if (_isRespawning || _isStageCleared ) //||moveState == SlimeState.Flying
            return;
        aimState = AimState.None;
        
        if (_chargingFxPlaying)
        {
            _chargingFxPlaying = false;

            if (charging_fx != null)
                charging_fx.StopFeedbacks();
        }
        if (visuals != null)
            visuals.ResetVisuals();

      

        rb.isKinematic = false;
        rb.useGravity = true;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        moveState = SlimeState.Flying;

        _currentLaunchIsStraightShot = false;
        _remainingMaxBounces = 0;
        lastLaunchWasMaxPull = false;
        float direction =
            transform.position.x >= attackerposition.x ? 1f : -1f;
        Vector3 force = new Vector3(
            direction * knockbackForce,
            knockbackUpForce,
            0f
        );
        rb.AddForce(force, ForceMode.Impulse);

        if (hitfeedback != null)
            hitfeedback.PlayFeedbacks();
       
        slimeStats.TakeDamage(1);
       
    }
    
    public void PlaceAtAndStop(Vector3 position)
    {
        cam = Camera.main;
        StopAllCoroutines();

        _fixedZ = position.z;                 // 이 스테이지의 평면 Z 기준 갱신

        rb.isKinematic = false;               // 속도 확실히 0으로 만들려고 잠깐 품
        rb.linearVelocity  = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.useGravity = false;
        rb.isKinematic = true;                // 완전 고정 (떨림 방지)

        transform.position = position;
        rb.position = position;
        rb.rotation = Quaternion.identity;

        moveState = SlimeState.Stuck;
        aimState  = AimState.None;

        _currentLaunchIsStraightShot = false;
        _remainingMaxBounces = 0;
        lastLaunchWasMaxPull = false;
        _maxPullHoldTimer = 0f;
        _maxTriggerReady = false;
        Debug.Log("stop");
        if (visuals != null) visuals.ResetVisuals();
    }
    
    private void Launch(Vector3 velocity)
    {
        Launch(velocity, false);
    }

    private void Launch(Vector3 velocity, bool straightShot)
    {
        
        if (_currentBubble != null)
        {
            transform.DOKill();
            transform.SetParent(null);
            transform.position = _currentBubble.transform.position; 
            
           popparticle.transform.SetParent(null);
           popaudio.transform.SetParent(null);
           popparticle.gameObject.SetActive(true);
           popaudio.gameObject.SetActive(true);
            popparticle.Play();
            popaudio.Play();

            
            StartCoroutine(Respawnbubble(_currentBubble, 1f));
            _currentBubble = null;
        }
        Vector3 launchDir = velocity;
        launchDir.z = 0f;
        if (launchDir.sqrMagnitude > 0.0001f)
            launchDir.Normalize();
        else
            launchDir = _stuckWallNormal;

        Vector3 unstickDir = _stuckWallNormal;
        if (unstickDir.sqrMagnitude < 0.0001f)
            unstickDir = launchDir;

        Vector3 pre = rb.position + unstickDir * 0.04f;
        pre.z = _fixedZ;
        rb.position = pre;
        transform.position = pre;

        PulseColliderOff(launchColliderOffTime); // 안끄니까 같은벽에바로다시붙음

        rb.isKinematic = false;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        velocity.z = 0f;                       // Z 속도 고정 (평면 밖으로 날아가지 않게)

        float launchForceMul = (sizeController != null) ? sizeController.LaunchForceMultiplier : 1f;
        velocity *= launchForceMul;

        _currentLaunchIsStraightShot = straightShot;

        if (straightShot)
        {
            rb.useGravity = false;
            rb.linearVelocity = velocity * straightShotSpeedMultiplier; // 방향·크기 유지 + 배율
            if(_straightShotGravityCoroutine != null) StopCoroutine(_straightShotGravityCoroutine);
            _straightShotGravityCoroutine = StartCoroutine(EnableGravityAfterStraightShotDelay());
            
            if(strightlaunch_fx != null) strightlaunch_fx.PlayFeedbacks();
            animancer.Play(launchAnimation, 0.1f, FadeMode.FromStart); // 0,1f= fadeduration , 바뀌는데 걸리는 시간. fademode.fromstart면 시작한 부분에 시작. 

        }
        else
        {
            rb.useGravity = true;              // 일반 발사: 중력 적용(포물선)
            rb.linearVelocity = velocity;      // Unity 6: velocity → linearVelocity (구버전이면 rb.velocity)
            if(normaljump_fx != null) normaljump_fx.PlayFeedbacks();
        }

        rb.angularVelocity = Vector3.zero;     // 불필요한 회전 제거
        rb.rotation = Quaternion.identity; 

        moveState = SlimeState.Flying;         // 몸: 날아가는 중
        aimState = AimState.None;              // [추가] 손을 놓았으니 조준 종료

        if (visuals != null)
        {
            if (straightShot)
            {
                float squashMul = 1f;
                float recoverMul = 1f;

                if (sizeController != null)
                {
                    squashMul = sizeController.StraightShotSquashMultiplier;
                    recoverMul = sizeController.StraightShotRecoverMultiplier;
                }

                visuals.OnStraightShotLaunchCompressed(
                    rb.linearVelocity.normalized,
                    squashMul,
                    recoverMul
                );
            }
            else
            {
                visuals.OnLaunch(
                    rb.linearVelocity.normalized,
                    rb.linearVelocity.magnitude
                );
            }
        }
        if (trajectoryLineRenderer != null)
            trajectoryLineRenderer.enabled = false; // 발사 후 궤적선 숨김
    }

    private IEnumerator Respawnbubble(GameObject bubble, float delaytime)
    {
        bubble.SetActive(false);
        yield return new WaitForSeconds(delaytime);
        bubble.SetActive(true);
    }

    private IEnumerator EnableGravityAfterStraightShotDelay()
    {
        float gravityDelayMul = (sizeController != null) ? sizeController.StraightShotGravityDelayMultiplier : 1f;
        yield return new WaitForSeconds(straightShotGravityDelay * gravityDelayMul);

        _straightShotGravityCoroutine = null;

        if (moveState != SlimeState.Flying)
            yield break;

        if (!_currentLaunchIsStraightShot)
            yield break;

        rb.useGravity = true;

        _currentLaunchIsStraightShot = false;
    }

    private void FixedUpdate()
    {
        if (moveState != SlimeState.Flying && moveState != SlimeState.Sliding)
            return;

        if (moveState == SlimeState.Flying)
        {
            _velocityBeforeCollision = rb.linearVelocity; // collision시점에속도이미바껴있음 이거써야됨

            float dt = Time.fixedDeltaTime;
            float windMul = (sizeController != null) ? sizeController.WindInfluence : 1f;
            rb.linearVelocity += windforce * windMul * dt;

            if (rb.useGravity)
            {
                float gMul = (sizeController != null) ? sizeController.GravityMultiplier : 1f;
                rb.linearVelocity += Physics.gravity * (gMul - 1f) * dt;
            }

            rb.linearVelocity += StageGravity.SumAcceleration(transform.position) * dt;
        }
        else // Sliding
        {
            rb.linearVelocity = _slideVelocity; // 마찰때문에멈춤 매프레임다시넣음
        }

        Vector3 v = rb.linearVelocity;
        if (v.z != 0f) { v.z = 0f; rb.linearVelocity = v; }

        Vector3 p = transform.position;
        if (p.z != _fixedZ) { p.z = _fixedZ; transform.position = p; }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (moveState != SlimeState.Flying)
            return;

        if (Time.time < _ignoreWallUntil && collision.collider == _ignoredWallCollider)
            return; // 콜라이더켜지자마자같은벽다시붙음
    
        Wallsurface wall = collision.gameObject.GetComponentInParent<Wallsurface>();
        if (wall == null)
            return; // 레이어로하다가 안맞아서 컴포넌트로바꿈

        Vector3 contactPoint = collision.contacts[0].point;
        Vector3 normal = collision.contacts[0].normal;
        normal.z = 0f;
        if (normal.sqrMagnitude < 1e-6f)
            normal = Vector3.up;
        else
            normal.Normalize();

        if (_currentLaunchIsStraightShot && wall.Type == Wallsurface.WallType.Rayfire)
        {
            
            RayfireRigid rayfire = wall.GetComponentInParent<RayfireRigid>();
            if(rayfire != null)
            {
                
                rayfire.Demolish();
                wall.PlayDestroy();
                StartCoroutine(Explodewait(contactPoint));
            }
            
        }
        switch (wall.Type)
        {
            case Wallsurface.WallType.Normal:
                HandleNormalWall(contactPoint, normal, wall, collision.collider);
                break;
            case Wallsurface.WallType.Slippery:
                HandleSlipperyWall(contactPoint, normal, wall, collision.collider);
                break;
            case Wallsurface.WallType.Spike:
                HandleSpikeWall(contactPoint, normal, wall);
                break;
            case Wallsurface.WallType.Rayfire:
                HandleNormalWall(contactPoint, normal, wall, collision.collider);
                break;
            case Wallsurface.WallType.Stick:
                StickToWall(contactPoint, normal, collision.collider);
                Debug.Log("sticktowall");
                break;
        }

        
    }

    private IEnumerator Explodewait(Vector3 contactPoint)
    {
        yield return new WaitForFixedUpdate();
        Collider[] colliders = Physics.OverlapSphere(contactPoint, 3f);
        foreach (Collider col in colliders)
        {
            Rigidbody fragRb = col.GetComponent<Rigidbody>();
            
            if (fragRb != null && fragRb != this.rb)
            {
                fragRb.AddExplosionForce(20f, contactPoint, 3f, 0.5f, ForceMode.Impulse);
            }
        }
          
    }
  
    

    private void HandleNormalWall(Vector3 contactPoint, Vector3 normal, Wallsurface wall, Collider wallCollider)
    {
        if (_remainingMaxBounces > 0)
        {
            BounceAndConsumeMaxBounce(contactPoint, normal, wallCollider);
            return;
        }

        StickToWall(contactPoint, normal, wallCollider);
    }

    

    private void HandleSlipperyWall(Vector3 contactPoint, Vector3 normal, Wallsurface wall, Collider wallCollider)
    {
        if (_remainingMaxBounces > 0)
        {
            BounceAndConsumeMaxBounce(contactPoint, normal, wallCollider);

            return;
        }

        StartSlidingOnWall(normal, wall);
    }

    private void StartSlidingOnWall(Vector3 normal, Wallsurface wall)
    {

        moveState = SlimeState.Sliding;

        rb.useGravity = false;              // 중력은 우리가 slideVelocity로 대신한다
        rb.angularVelocity = Vector3.zero;  // 불필요한 회전 제거

        Vector3 slideDirection = Vector3.ProjectOnPlane(Physics.gravity, normal).normalized;

        if (slideDirection.sqrMagnitude < 0.0001f)
            slideDirection = Vector3.down;

        Vector3 slideVelocity = slideDirection * slideSpeed - normal * slideStickForce;
        slideVelocity.z = 0f;               // Z 속도는 항상 0 (평면 유지)

        _slideVelocity = slideVelocity;     // FixedUpdate에서 계속 유지할 속도로 저장
        rb.linearVelocity = _slideVelocity; // 첫 속도 즉시 적용

        if (visuals != null) visuals.OnSlide(slideDirection, slideSpeed);

        if (trajectoryLineRenderer != null)
            trajectoryLineRenderer.enabled = false; // 궤적선 숨김
    }

    private void HandleSpikeWall(Vector3 contactPoint, Vector3 normal, Wallsurface wall)
    {
        // if (slimeStats != null)
        //     slimeStats.TakeDamage(wall.Slimedamage);
        // if (_remainingMaxBounces > 0)
        //     _remainingMaxBounces = Mathf.Max(0, _remainingMaxBounces - 1);
        // _currentLaunchIsStraightShot = false;
        // lastLaunchWasMaxPull = false;
        // BounceOffWall(contactPoint,normal,  bounceSpeedMultiplier * wall.Bouncyness);
        // if (slimeStats != null && slimeStats.IsDead)
        //     StartRespawn();
        Knockback(transform.position); // 가시 튕김넣으니까 이상해서 넉백으로통일
    }

    private void StartRespawn()
    {
        if (_isRespawning)
            return; // 이미 리스폰 진행 중이면 중복 실행 방지

        _isRespawning = true;
        moveState = SlimeState.Dead; // 죽음 상태 (입력은 _isRespawning으로도 막힘)

        if (_respawnCoroutine != null)
            StopCoroutine(_respawnCoroutine);
        _respawnCoroutine = StartCoroutine(RespawnAfterDelay(respawnDelay));
    }

    private IEnumerator RespawnAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        _respawnCoroutine = null;
        Respawn();
    }

    public void Respawn()
    {
        if (_respawnCoroutine != null)
        {
            StopCoroutine(_respawnCoroutine);
            _respawnCoroutine = null;
        }

        Vector3 pos = _respawnPosition;
        pos.z = _fixedZ;
        transform.position = pos;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.useGravity = false;
        rb.isKinematic = true;      // [추가] 리스폰 후 Stuck도 완전 고정(떨림 방지)

        moveState = SlimeState.Stuck;
        aimState = AimState.None;   // [추가] 리스폰 시 조준 상태도 해제

        if (trajectoryLineRenderer != null)
            trajectoryLineRenderer.enabled = false;

        _maxPullHoldTimer = 0f;
        _maxTriggerReady = false;
        _currentLaunchIsStraightShot = false;
        _remainingMaxBounces = 0;
        lastLaunchWasMaxPull = false;

        if (slimeStats != null)
            slimeStats.ResetStats();

        if (visuals != null) visuals.ResetVisuals();

        _isRespawning = false;
    }

    public void OnStageClear()
    {
        _isStageCleared = true; // Update 맨 앞에서 이 값을 보고 입력을 전부 막는다
        aimState = AimState.None; // [추가] 혹시 조준 중이었다면 해제

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.useGravity = false;

        if (trajectoryLineRenderer != null)
            trajectoryLineRenderer.enabled = false;

        if (visuals != null) visuals.ResetVisuals();

     
    }

    public void SetFlying()
    {
        moveState = SlimeState.Flying;   // ★ 착지 시 OnCollisionEnter가 Stick 처리하게
        aimState  = AimState.None;
        _currentLaunchIsStraightShot = false;
        _remainingMaxBounces = 0;
    }
    private void StickToWall(Vector3 contactPoint, Vector3 normal)
    {
        StickToWall(contactPoint, normal, null);
    }

    private void StickToWall(Vector3 contactPoint, Vector3 normal, Collider wallCollider)
    {
        normal.z = 0f;
        if (normal.sqrMagnitude < 1e-6f)
            normal = Vector3.up;
        else
            normal.Normalize();

        _stuckWallNormal = normal;
        _ignoredWallCollider = wallCollider;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.useGravity = false;

        EnableColliderNow();
        SnapFlushToWall(contactPoint, normal);

        rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
        rb.isKinematic = true;

        moveState = SlimeState.Stuck;
        if (stick_fx != null) stick_fx.PlayFeedbacks();

        if (visuals != null) visuals.OnStick(normal);

        if (trajectoryLineRenderer != null)
            trajectoryLineRenderer.enabled = false;
    }

  private void PlayBounceFx(Vector3 normal)
{
    if (bounceFxPivot == null) return;

    normal.z = 0f;
    if (normal.sqrMagnitude < 1e-6f) normal = Vector3.up;

    Vector3 mappedNormal = new Vector3(normal.x, 0f, normal.y); // Y를 Z로 이동
    if (mappedNormal.sqrMagnitude < 1e-6f) mappedNormal = Vector3.forward;

    bounceFxPivot.forward = mappedNormal;

    if (_bounceParticle == null)
        _bounceParticle = bounceFxPivot.GetComponentInChildren<ParticleSystem>();

    if (_bounceParticle != null)
    {
        _bounceParticle.Clear();
        _bounceParticle.Play();
    }
}
    public void EnterBubble(GameObject bubble)
    {
        enter_buble.Play();
        
        _currentBubble = bubble;
        if (_straightShotGravityCoroutine != null)   // 이미 있는 핸들
               StopCoroutine(_straightShotGravityCoroutine);

        rb.linearVelocity  = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.useGravity = false;
        rb.isKinematic = true;             // 완전 고정 (DOTween이 transform으로 옮김)

        moveState = SlimeState.Stuck;      // 벽에 붙은 것과 같은 상태
        aimState  = AimState.None;

        _currentLaunchIsStraightShot = false;
        _remainingMaxBounces = 0;
        lastLaunchWasMaxPull = false;

        if (visuals != null) visuals.ResetVisuals();
    }

    public void PopFromBubble(Vector3 velocity)
    {
        _fixedZ = transform.position.z;    // 현재 z를 평면 기준으로 갱신 (튐 방지)

        rb.isKinematic = false;
        rb.useGravity = true;

        moveState = SlimeState.Flying;     // 날아가는 중
        aimState  = AimState.None;

        rb.linearVelocity  = velocity;
        rb.angularVelocity = Vector3.zero;

        if (visuals != null)
            visuals.OnLaunch(velocity.normalized, velocity.magnitude); // 발사 연출(선택)
    }

    private void BounceOffWall(Vector3 contact_point, Vector3 normal)
    {
        BounceOffWall(contact_point, normal, bounceSpeedMultiplier);
    }

    private void BounceOffWall(Vector3 contact_point, Vector3 normal, float multiplier)
    {
        BounceOffWall(contact_point, normal, multiplier, null);
    }

    private void BounceOffWall(Vector3 contact_point, Vector3 normal, float multiplier, Collider wallCollider)
    {
        ApplyBouncePhysics(normal, multiplier, wallCollider);

        if (bounce_fx != null)
        {
            Debug.Log("bounceplat");
            bounce_fx.PlayFeedbacks();
        }
        
        GameObject fxPrefab = null;

        if (normal.x < -0.5f)
        {
            fxPrefab = rightBounceFxPrefab;
        }
        else if (normal.x > 0.5f)
        {
            fxPrefab = leftBounceFxPrefab;
        }
        else if (Mathf.Abs(normal.y) > 0.5f) fxPrefab = verticleBounceFxprefab;

        if (fxPrefab == null)
            return;

        Vector3 spawnPosition = contact_point;
        spawnPosition.z = _fixedZ;

        GameObject fx = Instantiate(
            fxPrefab
        );
        fx.transform.position = spawnPosition;
        ParticleSystem particle = fx.GetComponent<ParticleSystem>();
        if (particle != null)
            particle.Play();

        Destroy(fx, 1f);

    }

    private void BounceAndConsumeMaxBounce(Vector3 contact_point, Vector3 normal)
    {
        BounceAndConsumeMaxBounce(contact_point, normal, null);
    }

    private void BounceAndConsumeMaxBounce(Vector3 contact_point, Vector3 normal, Collider wallCollider)
    {
        BounceOffWall(contact_point, normal, bounceSpeedMultiplier, wallCollider);

        _remainingMaxBounces = Mathf.Max(0, _remainingMaxBounces - 1);
        _currentLaunchIsStraightShot = false;
        lastLaunchWasMaxPull = false;
    }

    private void ApplyBouncePhysics(Vector3 normal, float multiplier, Collider wallCollider)
    {
        normal.z = 0f;
        if (normal.sqrMagnitude < 1e-6f)
            normal = Vector3.up;
        else
            normal.Normalize();

        Vector3 incoming = _velocityBeforeCollision;
        incoming.z = 0f;
        if (incoming.sqrMagnitude < 0.01f)
            incoming = -normal * 8f;

        Vector3 reflected = Vector3.Reflect(incoming, normal);
        float away = Vector3.Dot(reflected, normal);
        if (away < 2f)
            reflected += normal * (2f - away);

        reflected *= Mathf.Max(0.01f, multiplier);
        reflected.z = 0f;

        Vector3 pos = rb.position + normal * 0.04f;
        pos.z = _fixedZ;
        rb.position = pos;
        transform.position = pos;

        PulseColliderOff(launchColliderOffTime);

        rb.isKinematic = false;
        rb.useGravity = true;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.linearVelocity = reflected;
        rb.angularVelocity = Vector3.zero;

        moveState = SlimeState.Flying;
        _stuckWallNormal = normal;
        _ignoredWallCollider = wallCollider;
        _ignoreWallUntil = Time.time + launchColliderOffTime;
    }

    private void PulseColliderOff(float seconds)
    {
        if (_colliderOffRoutine != null)
            StopCoroutine(_colliderOffRoutine);
        _colliderOffRoutine = StartCoroutine(ColliderOffRoutine(seconds));
    }

    private IEnumerator ColliderOffRoutine(float seconds)
    {
        if (_slimeCollider != null)
            _slimeCollider.enabled = false;

        yield return new WaitForSeconds(seconds);

        EnableColliderNow();
        _colliderOffRoutine = null;
    }

    private void EnableColliderNow()
    {
        if (_colliderOffRoutine != null)
        {
            StopCoroutine(_colliderOffRoutine);
            _colliderOffRoutine = null;
        }

        if (_slimeCollider != null)
            _slimeCollider.enabled = true;
    }

    private void SnapFlushToWall(Vector3 contactPoint, Vector3 normal)
    {
        Vector3 colCenter = transform.position;
        float radius = 0.15f;

        CapsuleCollider cap = _slimeCollider as CapsuleCollider;
        if (cap != null)
        {
            colCenter = transform.TransformPoint(cap.center);
            Vector3 nLocal = transform.InverseTransformDirection(normal);
            nLocal.z = 0f;
            if (nLocal.sqrMagnitude < 1e-6f)
                nLocal = Vector3.up;
            else
                nLocal.Normalize();

            float rad = cap.radius * Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.z));
            float half = cap.height * 0.5f * Mathf.Abs(transform.lossyScale.y);
            radius = Mathf.Abs(nLocal.y) * half + Mathf.Sqrt(nLocal.x * nLocal.x + nLocal.z * nLocal.z) * rad;
        }
        else if (_slimeCollider != null)
        {
            colCenter = _slimeCollider.bounds.center;
            Vector3 e = _slimeCollider.bounds.extents;
            radius = Mathf.Abs(normal.x) * e.x + Mathf.Abs(normal.y) * e.y;
        }

        Vector3 desiredCenter = contactPoint + normal * radius;
        Vector3 pos = transform.position + (desiredCenter - colCenter);
        pos.z = _fixedZ;
        rb.position = pos;
        transform.position = pos;
        Physics.SyncTransforms();
    }

    private Vector3 CalculateLaunchVelocity(Vector3 currentWorld)
    {
        Vector3 dragVector = _dragStartWorld - currentWorld;

        float clampedDistance = Mathf.Min(dragVector.magnitude, maxDragDistance);

        if (dragVector.sqrMagnitude < 0.0001f)
            return Vector3.zero;

        Vector3 velocity = dragVector.normalized * clampedDistance * launchPower;

        velocity.z = 0f;
        return velocity;
    }

    private void DrawTrajectory(Vector3 startPos, Vector3 velocity)
    {
        if (trajectoryLineRenderer == null) return;

        if (trajectoryLineRenderer.positionCount != trajectoryPointCount)
            trajectoryLineRenderer.positionCount = trajectoryPointCount;

        Vector3 pos = startPos;
        Vector3 vel = velocity;
        float dt = trajectoryTimeStep;
        float gMul = (sizeController != null) ? sizeController.GravityMultiplier : 1f;

        for (int i = 0; i < trajectoryPointCount; i++)
        {
            _trajectoryPoints[i] = pos;   // 현재 위치 기록

            Vector3 acc = Physics.gravity * gMul + StageGravity.SumAcceleration(pos);
            acc.z = 0f;

            vel += acc * dt;              // 속도 갱신
            pos += vel * dt;             // 위치 갱신
            pos.z = startPos.z;          // 평면 유지
        }

        trajectoryLineRenderer.SetPositions(_trajectoryPoints);

    }

    private void DrawStraightLinePreview(Vector3 startPos, Vector3 velocity)
    {
        if (trajectoryLineRenderer == null)
            return;

        if (trajectoryLineRenderer.positionCount != straightLinePointCount)
            trajectoryLineRenderer.positionCount = straightLinePointCount;

        Vector3 dir = velocity;
        dir.z = 0f;

        if (dir.sqrMagnitude < 0.0001f)
        {
            for (int i = 0; i < straightLinePointCount; i++)
                trajectoryLineRenderer.SetPosition(i, startPos);
            return;
        }

        dir.Normalize();

        Vector3 endPos = startPos + dir * straightLinePreviewLength;

        for (int i = 0; i < straightLinePointCount; i++)
        {
            float t = (straightLinePointCount <= 1) ? 0f : (float)i / (straightLinePointCount - 1);
            Vector3 p = Vector3.Lerp(startPos, endPos, t);
            p.z = startPos.z;   // Z 고정
            trajectoryLineRenderer.SetPosition(i, p);
        }
    }

    private Vector3 PointerToWorld(Vector3 screenPos)
    {
        Ray ray = cam.ScreenPointToRay(screenPos);

        if (_dragPlane.Raycast(ray, out float enter))
        {
            return ray.GetPoint(enter);
        }

        return transform.position;
    }

    private bool IsPointerOnThisSlime(Vector3 screenPos)
    {
        Ray ray = cam.ScreenPointToRay(screenPos);
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            return hit.transform == transform || hit.transform.IsChildOf(transform);
        }
        return false;
    }
}

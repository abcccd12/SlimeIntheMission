using System.Collections; // [추가] 0.5초 뒤 리스폰 코루틴에 필요
using UnityEngine;
using MoreMountains.Feedbacks;
using Animancer;
using RayFire;
using DG.Tweening;
/// <summary>
/// 슬라임 드래그 발사 + 예상 궤적선(TrajectoryLine) 시스템.
///
/// 게임 규칙 요약:
/// - 슬라임은 3D 모델이지만, 실제 움직임은 X/Y 평면에서만 일어난다. (Z는 고정)
/// - 슬라임을 마우스로 누르고 뒤로 당긴 뒤 놓으면, 당긴 반대 방향으로 발사된다.
/// - 당긴 거리가 길수록 더 강하게 날아간다. (단, maxDragDistance로 상한을 둔다)
/// - 드래그 중에는 중력을 고려한 예상 궤적선을 LineRenderer로 보여준다.
///
/// 설계 의도:
/// - 입력(마우스/터치) 부분과 발사 계산 부분을 분리해서, 나중에 터치로 바꾸기 쉽게 했다.
/// - 발사는 AddForce가 아니라 velocity(=linearVelocity)를 직접 설정한다.
///   이유: 캐주얼 모바일 게임은 "이만큼 당기면 이만큼 날아간다"가 예측 가능해야 조작감이 좋다.
///   AddForce는 mass/시간에 영향을 받아 예측이 어렵고, 궤적선 계산과도 어긋나기 쉽다.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class SlimeLaunchController : MonoBehaviour
{
    // [수정] 상태를 '이동 상태'와 '조준 상태' 두 개로 분리했다.
    //  이유: 미끄러지는(Sliding) 동시에 조준(Aiming)해야 하므로, 하나의 enum에
    //  둘 중 하나만 담으면 안 된다. (Sliding이면서 Aiming = 몸은 미끄러지고 손은 조준)

    // 이동 상태: 슬라임 '몸'이 지금 어떻게 움직이는가.
    public enum SlimeState
    {
        Stuck,    // 벽에 붙어 정지
        Flying,   // 발사되어 날아가는 중 (이때는 조준 불가)
        Sliding,  // 미끄럼벽을 따라 미끄러지는 중
        Dead
    }

    // [추가] 조준 상태: 플레이어가 지금 '조준(드래그) 중'인가.
    //  이동 상태와 독립적이라, Sliding + Aiming 같은 조합이 가능해진다.
    public enum AimState
    {
        None,   // 조준 안 함
        Aiming  // 마우스로 당겨 조준 중
    }
    


    [Header("발사 설정")]
    [Tooltip("당길 수 있는 최대 거리(월드 단위). 이보다 멀리 당겨도 힘은 더 세지지 않는다.")]
    [SerializeField] private float maxDragDistance = 3f;

    [Tooltip("드래그 거리를 발사 속도로 바꾸는 배율. 클수록 같은 거리라도 더 빠르게 날아간다.")]
    [SerializeField] private float launchPower = 5f;

    [Tooltip("발사/튕김 직후 콜라이더를 꺼 두는 시간(초). 같은 벽에 즉시 다시 붙는 걸 막는다.")]
    [SerializeField] private float launchColliderOffTime = 0.12f;

    // [추가] ---------------- 벽 붙기 / 튕기기 설정 ----------------
    [Header("[추가] 벽 붙기 / 튕기기 설정")]
    [Tooltip("이 값 이상으로 당겨서 발사하면(=최대 당김) 벽에 붙지 않고 튕긴다. 0~1 사이 값.")]
    [SerializeField] private float maxPullThreshold = 0.95f;

    [Tooltip("벽에서 튕길 때 속도 배율. 1이면 속도 유지, >1이면 더 빠르게, <1이면 느리게 튕긴다.")]
    [SerializeField] private float bounceSpeedMultiplier = 1f;

    // [추가] ---------------- MaxTrigger(직선 발사) 설정 ----------------
    [Header("[추가] MaxTrigger(직선 발사) 설정")]
    [Tooltip("maxPullThreshold 이상으로 '유지'해야 하는 시간(초). 이 시간 동안 계속 당기고 있으면 직선 발사가 준비된다.")]
    [SerializeField] private float maxTriggerHoldTime = 1f;

    [Tooltip("직선 발사 속도 배율. 1이면 일반 발사와 같은 크기로, >1이면 더 빠르게 직선으로 날아간다.")]
    [SerializeField] private float straightShotSpeedMultiplier = 1f;

    [Tooltip("직선 미리보기 선의 길이(월드 단위). 준비 완료 시 이 길이만큼 일직선으로 보여준다.")]
    [SerializeField] private float straightLinePreviewLength = 8f;

    [Tooltip("직선 미리보기 선의 점 개수. 직선이라 2개면 충분하다.")]
    [SerializeField] private int straightLinePointCount = 2;
    // [추가] ---------------------------------------------------------

    // [수정] 벽 판정은 이제 Layer/Tag가 아니라 'Wallsurface 컴포넌트가 붙어있는가'로 한다.
    //        (그래서 기존 wallLayer / alsoUseWallTag / wallTag 필드와 IsWall() 함수는 제거했다.)

    [Header("[추가] 미끄럼벽(Slippery) 주행 설정")]
    [Tooltip("미끄럼벽 표면을 따라 흘러내리는 속도. 클수록 빨리 미끄러진다.")]
    [SerializeField] private float slideSpeed = 2f;

    [Tooltip("미끄러지는 동안 벽에 붙어있게 벽 안쪽으로 살짝 눌러주는 정도. 0이면 순수하게 표면 방향으로만 흐른다.")]
    [SerializeField] private float slideStickForce = 0.5f;
    // [추가] -----------------------------------------------------

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

    // [추가] ---------------- 게임 루프(Stats / Respawn / Clear) ----------------
    [Tooltip("슬라임 체력/자원 관리. 비워두면 이 오브젝트에서 자동으로 가져온다.")]
    [SerializeField] private SlimeStats slimeStats;

    [Tooltip("죽었을 때 리스폰까지 기다리는 시간(초). 0이면 즉시 리스폰. 튕기는 모습을 잠깐 보여주려면 0.5 정도.")]
    [SerializeField] private float respawnDelay = 0.5f;

    // [추가] 말랑 비주얼 연출 담당. 비워두면 자식에서 자동으로 찾는다. (없어도 게임은 정상 동작)
    [Tooltip("슬라임 겉모습(squash/stretch) 담당. 비워두면 자식에서 자동으로 가져온다.")]
    [SerializeField] private SlimeVisualController visuals;

    [SerializeField] private float straightShotGravityDelay = 0.5f;

    private Coroutine _straightShotGravityCoroutine;

    // [추가] 슬라임 크기 제공자(선택). 발사 힘/중력지연/눌림 배율을 여기서 읽는다.
    //  비워두면 Awake에서 자동으로 찾는다. (없으면 배율 1로 처리)
    [SerializeField] private SlimeSizeController sizeController;

    // [추가] Feel 이펙트 컨트롤러(선택). 지금은 charge만 연결해 테스트.
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
    // [추가] 직선발사 '중간 눌림' 타이밍. 발사 후 이 시간(초) 뒤에 한 번 눌린다. (유저가 조정)

    // [추가] ----------------------------------------------------------------------

    // --- 내부 상태 (Inspector에 노출할 필요 없음) ---

    // 슬라임이 움직이는 평면(X/Y). Z가 고정이므로, 이 평면 위에서만 마우스 위치를 해석한다.
    // 이 평면 덕분에 마우스 좌표가 엉뚱한 Z 깊이로 튀는 문제를 막는다. (아래 PointerToWorld 참고)
    private Plane _dragPlane;

    // [수정] 상태를 이동/조준 두 개로 분리해서 관리한다. (Inspector에 노출해 디버깅)
    //  moveState: 몸의 이동 상태(Stuck/Flying/Sliding/Dead)
    //  aimState : 조준 상태(None/Aiming) — moveState와 '동시에' 성립 가능
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
    //  [수정] 이제 이 값은 '단순 세게 당김'이 아니라 'MaxTrigger 직선 발사'일 때만 true로 쓴다.
    //  실제 튕김 제어는 아래 _remainingMaxBounces가 담당하고, 이 값은 기존 호환/보조용으로만 남긴다.
    private bool lastLaunchWasMaxPull;

    // [추가] ---------------- MaxTrigger(직선 발사) 내부 상태 ----------------
    // maxPullThreshold 이상으로 '당긴 채 유지한' 시간을 누적한다. (드래그 중에만 증가)
    private float _maxPullHoldTimer;

    // 위 타이머가 maxTriggerHoldTime을 넘겨서 '직선 발사 준비'가 끝났는지.
    //  true가 되면 LineRenderer가 포물선 → 일직선으로 바뀐다.
    private bool _maxTriggerReady;

    // 이번 발사가 MaxTrigger 직선 발사인지. (중력 off 직선 비행 여부 판단에 사용)
    public bool _currentLaunchIsStraightShot;

    // MaxTrigger 발사 후 '남은 튕김 횟수'. 발사 시 1, 한 번 튕기면 0.
    //  0이 되면 그 다음 벽부터는 일반 발사처럼(붙기/Sliding) 처리된다.
    private int _remainingMaxBounces;
    // [추가] -----------------------------------------------------------------

    // [추가] ---------------- Respawn / Clear 내부 상태 ----------------
    // 리스폰(부활)할 위치. 지금은 시작 위치. 나중에 체크포인트에서 갱신하면 체크포인트 부활이 된다.
    private Vector3 _respawnPosition;

    // 리스폰 진행 중인지. (0.5초 delay 동안 중복 리스폰/입력을 막는다)
    private bool _isRespawning;

    // 스테이지를 클리어했는지. (true면 입력을 전부 막는다)
    private bool _isStageCleared;

    // 진행 중인 리스폰 코루틴 핸들. (중복 실행 방지 / 정리용)
    private Coroutine _respawnCoroutine;
    // [추가] -----------------------------------------------------------

    // [추가] 충돌 '직전'의 속도. OnCollisionEnter 시점엔 물리 처리로 속도가 이미 바뀔 수 있어서,
    //  FixedUpdate에서 매 스텝 저장해둔 이 값을 반사(튕김) 계산에 사용한다.
    private Vector3 _velocityBeforeCollision;

    // [추가] 이 슬라임이 항상 유지해야 할 고정 Z값. (X/Y 평면 게임이므로 Z는 절대 변하면 안 된다)
    private float _fixedZ;

    // [추가] 미끄럼(Sliding) 중 유지할 속도. StartSlidingOnWall에서 계산해두고,
    //  FixedUpdate에서 매 스텝 다시 적용해 마찰로 멈추지 않고 계속 미끄러지게 한다.
    private Vector3 _slideVelocity;

    private Vector3 _stuckWallNormal = Vector3.up;
    private Collider _slimeCollider;
    private Collider _ignoredWallCollider;
    private float _ignoreWallUntil;
    private Coroutine _colliderOffRoutine;

    // 드래그를 시작한 지점(월드 좌표). "얼마나 당겼는가"를 이 지점 기준으로 잰다.
    private Vector3 _dragStartWorld;

    // 궤적 계산 결과를 담아둘 배열. 매 프레임 새로 new 하지 않으려고 미리 만들어 재사용한다.
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
        // Rigidbody: Inspector에서 안 넣었으면 자동으로 가져온다.
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        // Camera: 왜 필요한가?
        // 마우스는 "화면 좌표(픽셀)"를 준다. 하지만 슬라임은 "월드 좌표"에 있다.
        // 이 둘을 이어주려면 카메라가 필요하다. 카메라는 "이 픽셀에서 쏜 광선(Ray)이
        // 월드의 어디를 지나가는가"를 계산해준다.
        if (cam == null)
            cam = Camera.main;

        // 궤적 점 배열을 점 개수만큼 미리 확보. (Line.cs의 _segments 와 같은 역할)
        _trajectoryPoints = new Vector3[trajectoryPointCount];

        // [추가] 고정 Z값 기록. 이후 모든 Z 관련 처리(발사/튕김/붙기)는 이 값을 기준으로 한다.
        _fixedZ = transform.position.z;

        // [추가] SlimeStats: Inspector에서 안 넣었으면 자동으로 가져온다.
        if (slimeStats == null)
            slimeStats = GetComponent<SlimeStats>();

        // [추가] 비주얼 컨트롤러: 자식(SlimeVisualRoot 등)에서 자동으로 찾는다.
        if (visuals == null)
            visuals = GetComponentInChildren<SlimeVisualController>();

        // [추가] 크기 컨트롤러: 안 넣었으면 자동으로 찾는다. (직선발사 압축 세기 조절용)
        if (sizeController == null)
            sizeController = GetComponent<SlimeSizeController>();

        _slimeCollider = GetComponent<Collider>();

        // [추가] 이펙트 컨트롤러: 안 넣었으면 자식에서 자동으로 찾는다.
        if (effects == null)
            effects = GetComponentInChildren<SlimeEffectController>();

        // [추가] 리스폰 위치를 시작 위치로 저장. (나중에 체크포인트에서 갱신 가능)
        _respawnPosition = transform.position;

        // [추가] 시작은 "벽에 붙어있는" 상태로 가정한다.
        //  (게임 컨셉상 슬라임은 벽에서 조준을 시작한다. 공중에서 시작시키려면 이 부분을 바꾸면 된다.)
        moveState = SlimeState.Stuck;
        rb.useGravity = false;           // 붙어있는 동안엔 중력 영향 없음
        rb.linearVelocity = Vector3.zero;
        rb.isKinematic = true;           // [추가] 시작 Stuck도 완전 고정(떨림 방지)

        // LineRenderer 안전 처리: 안 연결돼 있으면 경고만 남기고 조용히 비활성화.
        if (trajectoryLineRenderer == null)
        {
            Debug.LogWarning($"[{name}] trajectoryLineRenderer가 연결되지 않았습니다. 궤적선은 그리지 않습니다.");
        }
        else
        {
            // 월드 좌표로 그릴 것이므로 useWorldSpace = true.
            // (false면 점 좌표가 LineRenderer 오브젝트의 로컬 좌표로 해석돼 위치가 어긋난다)
            trajectoryLineRenderer.useWorldSpace = true;
            trajectoryLineRenderer.positionCount = trajectoryPointCount;
            trajectoryLineRenderer.enabled = false; // 처음엔 숨김
        }
    }

    
    private void Update()
    {
        
        // [추가] 클리어했거나 리스폰 중이면 어떤 입력도 받지 않는다. (조준/발사 전면 차단)
        if (_isStageCleared || _isRespawning || _controlLocked)   // ★ _controlLocked 추가
            return;

        // 입력 처리는 세 개의 작은 메서드로 분리했다.
        // 이 세 줄만 보면 흐름이 한눈에 들어오고, 실제 "마우스냐 터치냐"는 아래 메서드 안에 숨겨져 있다.
        // [수정] 조준 중 판정을 moveState가 아니라 aimState로 한다.
        //  → 이동 상태가 Sliding이어도 aimState == Aiming이면 드래그/발사가 계속 진행된다.
        if (IsPointerDown())
            OnPointerDown(GetPointerScreenPosition());
        else if (aimState == AimState.Aiming && IsPointerHeld())
            OnPointerDrag(GetPointerScreenPosition());
        else if (aimState == AimState.Aiming && IsPointerUp())
            OnPointerUp(GetPointerScreenPosition());
    }

    // ======================================================================
    //  입력 계층 (나중에 터치로 바꿀 때 여기만 고치면 된다)
    // ======================================================================

    /// <summary>이번 프레임에 누르기 시작했는가?</summary>
    private bool IsPointerDown() => Input.GetMouseButtonDown(0);

    /// <summary>누른 상태를 유지하고 있는가?</summary>
    private bool IsPointerHeld() => Input.GetMouseButton(0);

    /// <summary>이번 프레임에 손을 뗐는가?</summary>
    private bool IsPointerUp() => Input.GetMouseButtonUp(0);

    /// <summary>현재 포인터의 화면 좌표(픽셀)를 준다.</summary>
    private Vector3 GetPointerScreenPosition() => Input.mousePosition;

    // ======================================================================
    //  드래그 / 발사 로직 (입력 방식과 무관하게 동작)
    // ======================================================================

    private void OnPointerDown(Vector3 screenPos)
    {
        // [수정] Stuck(붙음) 또는 Sliding(미끄럼) 이동 상태에서만 조준을 시작할 수 있다.
        //  Flying(날아가는 중)/Dead에는 클릭해도 여기서 바로 빠져나간다. (Flying 중 조준 불가)
        if (moveState != SlimeState.Stuck && moveState != SlimeState.Sliding)
            return;

        // 클릭한 곳에 이 슬라임이 있는지 검사한다. (아무 데나 클릭하면 반응하지 않게)
        // if (!IsPointerOnThisSlime(screenPos))
        //     return;

        // 슬라임이 움직이는 평면을 정의한다.
        // 법선(normal)은 Vector3.forward(=Z축), 평면 위의 한 점은 슬라임의 현재 위치.
        // => "슬라임의 Z값과 똑같은 Z에 있는, X/Y로만 뻗은 평면"이라는 뜻.
        // 이 평면을 기준으로 마우스를 해석하기 때문에 Z가 절대 튀지 않는다.
        _dragPlane = new Plane(Vector3.forward, transform.position);

        // [수정] 이동 상태(moveState)는 건드리지 않고, '조준 상태'만 Aiming으로 켠다.
        //  → moveState가 Sliding이면 그대로 Sliding으로 남아, FixedUpdate가 계속 미끄럼을 유지한다.
        aimState = AimState.Aiming;

        // [수정] 조준 시작 시 속도 정지는 'Stuck일 때만' 한다.
        //  - Stuck: 원래 멈춰 있으니 그대로 고정.
        //  - Sliding: 여기서 속도를 0으로 만들면 미끄럼이 멈춰버리므로 '절대 건드리지 않는다'.
        //             (미끄러지면서 조준하는 것이 이번 수정의 핵심)
        if (moveState == SlimeState.Stuck)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.useGravity = false;
        }

        _dragStartWorld = PointerToWorld(screenPos); // 당김의 기준점
        anchor = _dragStartWorld - transform.position;
        // [추가] 비주얼: 당김 시작 반응.
        if (visuals != null) visuals.BeginPull();

        // [추가] 새 드래그를 시작하니 MaxTrigger 차지 상태를 깨끗이 초기화한다.
        _maxPullHoldTimer = 0f;
        _maxTriggerReady = false;

        if (trajectoryLineRenderer != null)
            trajectoryLineRenderer.enabled = true; // 드래그 중에만 궤적선 표시
    }

    private void OnPointerDrag(Vector3 screenPos)
    {
        _dragStartWorld = transform.position + anchor;
        
        // 현재 마우스가 가리키는 평면 위 지점.
        Vector3 currentWorld = PointerToWorld(screenPos);

        // 이번 드래그로 만들어질 발사 속도를 계산한다.
        Vector3 launchVelocity = CalculateLaunchVelocity(currentWorld);

        // [추가] 현재 당김 정도(chargeRatio, 0~1)를 계산한다.
        Vector3 dragVector = _dragStartWorld - currentWorld;
        float clampedDistance = Mathf.Min(dragVector.magnitude, maxDragDistance);
        float chargeRatio = clampedDistance / maxDragDistance;

        // [추가] 비주얼: 발사(늘어날) 방향 = launchVelocity 방향, 세기 = chargeRatio.
        if (visuals != null) visuals.UpdatePull(launchVelocity, chargeRatio);

        // [추가] MaxTrigger 차지 로직:
        //  - maxPullThreshold 이상으로 '유지'하는 동안 타이머를 쌓는다.
        //  - 타이머가 maxTriggerHoldTime을 넘으면 직선 발사 준비 완료(_maxTriggerReady = true).
        //  - 당김을 threshold 아래로 줄이면 즉시 리셋 → 다시 일반 포물선 조준으로 돌아간다.
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

        // [추가] 차징 이펙트: 준비(_maxTriggerReady)가 '켜지는 순간' 재생, '꺼지는 순간' 정지.
        //  (매 프레임 재생되지 않게 _chargingFxPlaying으로 전이만 잡는다)
        if (_maxTriggerReady && !_chargingFxPlaying)
        {
            _chargingFxPlaying = true;
            if(charging_fx != null) charging_fx.PlayFeedbacks();
        }
        else if (!_maxTriggerReady && _chargingFxPlaying)
        {
            _chargingFxPlaying = false;
            if (charging_fx != null) charging_fx.StopFeedbacks();
        }

        // [추가] 준비 완료면 일직선 미리보기, 아니면 기존 포물선 미리보기.
        if (_maxTriggerReady)
            DrawStraightLinePreview(transform.position, launchVelocity);
        else
            DrawTrajectory(transform.position, launchVelocity);
    }

    private void OnPointerUp(Vector3 screenPos)
    {
        
        _dragStartWorld = transform.position + anchor;
        Vector3 currentWorld = PointerToWorld(screenPos);

        // [추가] 이번 발사의 chargeRatio(당김 정도, 0~1)를 계산한다.
        //  CalculateLaunchVelocity와 같은 방식으로 거리를 제한(clamp)해서 비율을 낸다.
        //  chargeRatio = 제한된 당김거리 / 최대 당김거리
        Vector3 dragVector = _dragStartWorld - currentWorld;
        float clampedDistance = Mathf.Min(dragVector.magnitude, maxDragDistance);
        float chargeRatio = clampedDistance / maxDragDistance;

        // [수정] 이번 발사가 'MaxTrigger 직선 발사'인지 결정한다.
        //  단순히 chargeRatio가 높다고 되는 게 아니라, maxTriggerHoldTime 동안 유지해
        //  _maxTriggerReady가 켜져야만 직선 발사다. (드래그를 살짝 줄이면 일반 발사)
        _currentLaunchIsStraightShot = _maxTriggerReady;

        // [수정] 남은 튕김 횟수: 직선 발사면 1회만, 일반 발사면 0회.
        _remainingMaxBounces = _currentLaunchIsStraightShot ? 1 : 0;

        // [수정] 기존 호환용 값도 '직선 발사일 때만' max로 취급한다. (단순 chargeRatio 판정 폐기)
        lastLaunchWasMaxPull = _currentLaunchIsStraightShot;

        // 발사 속도 계산은 기존 로직 그대로 재사용.
        Vector3 launchVelocity = CalculateLaunchVelocity(currentWorld);

        // [수정] 직선 발사 여부를 Launch에 함께 넘긴다. (중력 off / 배율 적용은 Launch가 처리)
        Launch(launchVelocity, _currentLaunchIsStraightShot);

        // [추가] 발사가 끝났으니 차지 상태 초기화.
        _maxPullHoldTimer = 0f;
        _maxTriggerReady = false;

        // [추가] 손을 놓았으니 차징 이펙트도 정지.
        //  [수정] 옛 effects.StopCharging() → 직접 필드 charging_fx.StopFeedbacks() 로 교체.
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

      

        // 2. 벽에 붙은 상태 해제
        rb.isKinematic = false;
        rb.useGravity = true;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        // 3. 날아가는 상태로 변경
        moveState = SlimeState.Flying;

        // 기존 직선 발사/튕김 정보 초기화
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

        // 5. MMF 피드백 재생
        if (hitfeedback != null)
            hitfeedback.PlayFeedbacks();
       
        slimeStats.TakeDamage(1);
       
    }
    
    public void PlaceAtAndStop(Vector3 position)
    {
        cam = Camera.main;
        // 1) 날아가던 관련 코루틴(직선발사 중력지연 등) 전부 정지
        StopAllCoroutines();

        _fixedZ = position.z;                 // 이 스테이지의 평면 Z 기준 갱신

        // 2) 물리 완전 정지
        rb.isKinematic = false;               // 속도 확실히 0으로 만들려고 잠깐 품
        rb.linearVelocity  = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.useGravity = false;
        rb.isKinematic = true;                // 완전 고정 (떨림 방지)

        // 3) 위치 스냅 (transform + rb 둘 다)
        transform.position = position;
        rb.position = position;
        rb.rotation = Quaternion.identity;

        // 4) 상태를 '붙어서 멈춤'으로 → FixedUpdate가 더 이상 안 건드림
        moveState = SlimeState.Stuck;
        aimState  = AimState.None;

        // 5) 발사/튕김 관련 플래그 초기화
        _currentLaunchIsStraightShot = false;
        _remainingMaxBounces = 0;
        lastLaunchWasMaxPull = false;
        _maxPullHoldTimer = 0f;
        _maxTriggerReady = false;
        Debug.Log("stop");
        if (visuals != null) visuals.ResetVisuals();
    }
    
    // ======================================================================
    //  [추가] 발사 실행 / 벽 충돌(붙기·튕기기) 로직
    // ======================================================================

    /// <summary>
    /// [추가] 실제 발사 처리. 조준을 끝낼 때(OnPointerUp) 호출된다.
    /// - 중력을 다시 켜고, 상태를 Flying으로 바꾼다.
    /// - Z 방향 속도는 0으로 고정한다. (X/Y 평면 게임)
    /// </summary>
    private void Launch(Vector3 velocity)
    {
        // 인자 없는 기존 호출은 '일반 발사'로 처리. (호환용)
        Launch(velocity, false);
    }

    /// <summary>
    /// [추가] 직선 발사 여부를 함께 받는 오버로드.
    /// - straightShot = true  : 중력 off, 속도에 straightShotSpeedMultiplier 적용 → 일직선 비행
    /// - straightShot = false : 기존처럼 중력 on, 포물선 비행
    /// </summary>
    private void Launch(Vector3 velocity, bool straightShot)
    {
        
        // [정리] 미끄럼은 이제 상태(Sliding)로만 관리하므로 코루틴 취소가 필요 없다.
        //  Launch가 상태를 Flying으로 바꾸는 순간, FixedUpdate의 Sliding 속도 유지 로직도 자동으로 멈춘다.

        // [추가] 붙어있을 때 kinematic으로 얼려뒀으므로, 발사 전에 다시 다이나믹으로 되돌린다.
        //  (velocity를 설정하기 '전에' 해야 속도가 정상 적용된다)
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

        // kinematic인 채로 법선 쪽으로만 살짝 빼고, 겹침 해소는 콜라이더를 잠깐 꺼서 한다.
        Vector3 pre = rb.position + unstickDir * 0.04f;
        pre.z = _fixedZ;
        rb.position = pre;
        transform.position = pre;

        PulseColliderOff(launchColliderOffTime);

        rb.isKinematic = false;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        velocity.z = 0f;                       // Z 속도 고정 (평면 밖으로 날아가지 않게)

        // [추가] 크기에 따른 발사 힘 배율 적용. (커질수록 덜/더 날아가게 단계별로)
        float launchForceMul = (sizeController != null) ? sizeController.LaunchForceMultiplier : 1f;
        velocity *= launchForceMul;

        _currentLaunchIsStraightShot = straightShot;

        if (straightShot)
        {
            // [추가] 직선 발사: 중력을 꺼서 포물선이 아닌 일직선으로 날아가게 한다.
            rb.useGravity = false;
            rb.linearVelocity = velocity * straightShotSpeedMultiplier; // 방향·크기 유지 + 배율
            if(_straightShotGravityCoroutine != null) StopCoroutine(_straightShotGravityCoroutine);
            _straightShotGravityCoroutine = StartCoroutine(EnableGravityAfterStraightShotDelay());
            
            if(strightlaunch_fx != null) strightlaunch_fx.PlayFeedbacks();
            animancer.Play(launchAnimation, 0.1f, FadeMode.FromStart); // 0,1f= fadeduration , 바뀌는데 걸리는 시간. fademode.fromstart면 시작한 부분에 시작. 
            // [추가] 중간 눌림 예약: 진행방향 저장 후 straightShotSquashDelay 뒤 한 번 눌림.
            //  (재발사 시엔 이 줄이 이전 예약을 교체 — 중력 코루틴과 같은 방식)


        }
        else
        {
            rb.useGravity = true;              // 일반 발사: 중력 적용(포물선)
            rb.linearVelocity = velocity;      // Unity 6: velocity → linearVelocity (구버전이면 rb.velocity)
            if(normaljump_fx != null) normaljump_fx.PlayFeedbacks();
        }

        rb.angularVelocity = Vector3.zero;     // 불필요한 회전 제거
        // [추가] 발사 시작 순간 슬라임 회전을 0(정면)으로 초기화한다.
        //  (이전 비행/튕김/미끄럼으로 기울어진 회전을 리셋해서 깔끔하게 날아가게)
        //  rb가 있으니 transform 대신 rb로 회전을 세팅해 물리와 어긋나지 않게 한다.
        rb.rotation = Quaternion.identity; 

        moveState = SlimeState.Flying;         // 몸: 날아가는 중
        aimState = AimState.None;              // [추가] 손을 놓았으니 조준 종료

        // [추가] 비주얼: 발사 방향으로 확 늘어났다 복귀. (직선발사의 '중간 눌림'은 아래 코루틴이 따로 처리)
        // [추가] 비주얼 처리
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
        // [추가] 크기에 따른 중력지연 배율. (커질수록 중력이 늦게/빨리 켜지게 단계별로)
        float gravityDelayMul = (sizeController != null) ? sizeController.StraightShotGravityDelayMultiplier : 1f;
        yield return new WaitForSeconds(straightShotGravityDelay * gravityDelayMul);

        _straightShotGravityCoroutine = null;

        // 아직 직선 발사로 날아가는 중일 때만 중력을 다시 켠다.
        if (moveState != SlimeState.Flying)
            yield break;

        if (!_currentLaunchIsStraightShot)
            yield break;

        rb.useGravity = true;

        // 이제부터는 일반 비행처럼 취급
        _currentLaunchIsStraightShot = false;
    }

    /// <summary>
    /// [추가] 직선발사 후 straightShotSquashDelay 뒤에 '중간 눌림'을 한 번 발동한다.
    /// - 아직 직선발사로 날아가는 중일 때만 발동. (그 전에 붙으면 moveState != Flying,
    ///   튕기면 _currentLaunchIsStraightShot == false 가 되어 자동 무시 → 취소 안 박아도 안전)
    /// - 크기에 따른 눌림/복귀 배율을 SizeController에서 읽어 비주얼에 넘긴다.
    /// </summary>


    /// <summary>
    /// [추가] 물리 스텝마다 호출.
    /// - Flying 중 '직전 속도'를 저장해둔다. (충돌 반사 계산에 사용)
    /// - Sliding 중 미끄럼 속도를 계속 유지한다. (마찰로 멈추지 않도록)
    /// - Flying/Sliding 둘 다 Z(위치·속도)를 고정한다.
    /// </summary>
    private void FixedUpdate()
    {
        // [수정] Z 고정은 Flying뿐 아니라 Sliding에서도 필요하다.
        //  그 외 이동 상태(Stuck/Dead)는 물리로 안 움직이므로 처리할 필요 없다.
        //  [참고] Sliding + 조준(aimState=Aiming) 중에도 moveState는 Sliding이므로,
        //   아래 미끄럼 유지 로직이 그대로 돌아가 '미끄러지면서 조준'이 가능하다.
        if (moveState != SlimeState.Flying && moveState != SlimeState.Sliding)
            return;

        if (moveState == SlimeState.Flying)
        {
            // 충돌 순간 rb.linearVelocity는 이미 튕겨나간(바뀐) 값일 수 있어서,
            // 그 '직전'의 진행 속도를 매 스텝 미리 저장해둔다. (튕김 방향 계산에 사용)
            //
            // [참고] 직선 발사(_currentLaunchIsStraightShot)는 Launch에서 useGravity=false로 두므로,
            //  여기서 특별한 처리 없이도 중력 없이 등속 직선으로 날아간다.
            //  (아래 Z 고정만 유지되면 X/Y 평면 위 완전한 직선이 된다)
            _velocityBeforeCollision = rb.linearVelocity;

            float dt = Time.fixedDeltaTime;
            float windMul = (sizeController != null) ? sizeController.WindInfluence : 1f;
            rb.linearVelocity += windforce * windMul * dt;

            // 기본 중력은 Rigidbody.useGravity가 적용. 크기 배율은 (gMul-1)만 추가로 더한다.
            if (rb.useGravity)
            {
                float gMul = (sizeController != null) ? sizeController.GravityMultiplier : 1f;
                rb.linearVelocity += Physics.gravity * (gMul - 1f) * dt;
            }

            rb.linearVelocity += StageGravity.SumAcceleration(transform.position) * dt;
        }
        else // Sliding
        {
            // [추가] 미끄럼 속도를 매 스텝 다시 꽂아준다.
            //  벽 마찰 등으로 속도가 깎여 멈추는 걸 막고, 일정한 속도로 계속 미끄러지게 한다.
            rb.linearVelocity = _slideVelocity;
        }

        // Z 고정 유지 (속도 / 위치 둘 다 안전하게 되돌린다).
        Vector3 v = rb.linearVelocity;
        if (v.z != 0f) { v.z = 0f; rb.linearVelocity = v; }

        Vector3 p = transform.position;
        if (p.z != _fixedZ) { p.z = _fixedZ; transform.position = p; }
    }

    /// <summary>
    /// [추가] 충돌 감지. 날아가는 중 벽에 부딪혔을 때만 붙기/튕기기를 처리한다.
    /// </summary>
    private void OnCollisionEnter(Collision collision)
    {
        // 날아가는 중(Flying)일 때만 벽 처리. (붙어있거나 조준 중엔 무시)
        if (moveState != SlimeState.Flying)
            return;

        if (Time.time < _ignoreWallUntil && collision.collider == _ignoredWallCollider)
            return;
    
        // [수정] 벽 판정 기준을 'Wallsurface 컴포넌트가 있는가'로 바꿨다.
        //  (기존 IsWall(Layer/Tag) 방식은 제거 — WallSurface가 벽 정보의 유일한 기준)
        //  GetComponentInParent: 콜라이더가 자식에 있어도 부모의 Wallsurface를 찾아준다.
        Wallsurface wall = collision.gameObject.GetComponentInParent<Wallsurface>();
        if (wall == null)
            return;

        // 충돌 지점과 법선. 붙는 위치 / 튕김 방향 계산에 쓴다.
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
        // [추가] 벽 종류(Type)에 따라 처리를 나눈다.
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
                // 튕김/직선발사 여부와 무관하게 무조건 즉시 붙는다.
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
            
            // 파편의 Rigidbody를 찾아서 힘을 줍니다 (슬라임 본인은 제외)
            if (fragRb != null && fragRb != this.rb)
            {
                fragRb.AddExplosionForce(20f, contactPoint, 3f, 0.5f, ForceMode.Impulse);
            }
        }
          
        // 벽을 부쉈으므로, 튕기거나 붙지 않고 그대로 뚫고 지나가게 하려면 여기서 return 합니다.
        // 만약 부수면서 슬라임도 한 번 튕기게 하고 싶다면 return을 지우세요
    }
  
    

    // ----------------------------------------------------------------------
    //  [추가] 벽 종류별 처리 함수들
    // ----------------------------------------------------------------------

    /// <summary>
    /// [추가] 일반 벽. (기존 규칙 그대로)
    /// - max pull 발사 → 튕김
    /// - 그 외 발사   → 붙기
    /// </summary>
    private void HandleNormalWall(Vector3 contactPoint, Vector3 normal, Wallsurface wall, Collider wallCollider)
    {
        // [수정] '남은 MaxTrigger 튕김'이 있으면 1회 튕기고 소비한다.
        //  소비 후에는 다음 벽부터 아래 else 경로(붙기)로 처리된다.
        if (_remainingMaxBounces > 0)
        {
            BounceAndConsumeMaxBounce(contactPoint, normal, wallCollider);
            return;
        }

        // 튕김이 남지 않았으면 일반 규칙: 벽에 붙는다.
        StickToWall(contactPoint, normal, wallCollider);
    }

    

    /// <summary>
    /// [추가] 미끄럼 벽.
    /// - max pull 발사 → 튕김 (일반 벽과 동일)
    /// - 그 외 발사   → 벽에 '붙지 않고' 표면을 따라 미끄러진다. (Sliding 상태)
    /// </summary>
    private void HandleSlipperyWall(Vector3 contactPoint, Vector3 normal, Wallsurface wall, Collider wallCollider)
    {
        // [수정] '남은 MaxTrigger 튕김'이 있으면 1회 튕기고 소비한다.
        //  소비 후 다음 벽부터는 아래 Sliding 경로로 처리된다.
        if (_remainingMaxBounces > 0)
        {
            BounceAndConsumeMaxBounce(contactPoint, normal, wallCollider);

            return;
        }

        // 튕김이 남지 않았으면 벽 표면을 따라 미끄러진다.
        StartSlidingOnWall(normal, wall);
    }

    /// <summary>
    /// [추가] 미끄럼 시작. 벽에 붙는 대신 표면을 따라 흐르게 만든다.
    /// - 상태를 Sliding으로 바꾼다.
    /// - 중력은 직접 쓰지 않고(useGravity=false), 우리가 계산한 속도로 흐르게 한다.
    /// - 미끄럼 방향은 '중력을 벽 표면에 투영'해서 구한다. (벽을 따라 흐르는 방향)
    /// - Z 속도는 0으로 고정, 궤적선은 숨긴다.
    /// </summary>
    private void StartSlidingOnWall(Vector3 normal, Wallsurface wall)
    {

        moveState = SlimeState.Sliding;

        rb.useGravity = false;              // 중력은 우리가 slideVelocity로 대신한다
        rb.angularVelocity = Vector3.zero;  // 불필요한 회전 제거

        // 중력(아래로 향하는 힘)에서 '벽을 뚫는 방향'을 빼고, '벽 표면을 따라 흐르는 방향'만 남긴다.
        Vector3 slideDirection = Vector3.ProjectOnPlane(Physics.gravity, normal).normalized;

        // 벽이 거의 수평이라 표면 방향이 애매(거의 0)하면 안전하게 아래로.
        if (slideDirection.sqrMagnitude < 0.0001f)
            slideDirection = Vector3.down;

        // 표면을 따라 흐르는 속도 + 벽에 살짝 붙어있도록 벽 안쪽(-normal)으로 눌러주는 힘.
        //  (벽 안쪽으로 미는 성분은 벽 콜라이더가 막아주므로, 실제로는 '접촉 유지' 효과만 낸다)
        Vector3 slideVelocity = slideDirection * slideSpeed - normal * slideStickForce;
        slideVelocity.z = 0f;               // Z 속도는 항상 0 (평면 유지)

        _slideVelocity = slideVelocity;     // FixedUpdate에서 계속 유지할 속도로 저장
        rb.linearVelocity = _slideVelocity; // 첫 속도 즉시 적용

        // [추가] 비주얼: 미끄럼 방향으로 살짝 늘어지며 흐르는 반응.
        if (visuals != null) visuals.OnSlide(slideDirection, slideSpeed);

        if (trajectoryLineRenderer != null)
            trajectoryLineRenderer.enabled = false; // 궤적선 숨김
    }

    /// <summary>
    /// [추가] 가시 벽. 절대 붙지 않는다. 항상 데미지 + 튕김.
    /// - 발사 세기(max pull 여부)와 상관없이 동일하게 처리한다.
    /// </summary>
    private void HandleSpikeWall(Vector3 contactPoint, Vector3 normal, Wallsurface wall)
    {
        // [수정] 실제 데미지 적용. Slimedamage가 float이므로 TakeDamage(float) 오버로드가 자동 호출된다.
        //  (SlimeStats가 0 이하가 되면 내부에서 알아서 Die() 처리 → 아래 IsDead가 true가 됨)
        // if (slimeStats != null)
        //     slimeStats.TakeDamage(wall.Slimedamage);
        //
        // // Spike는 '위험 벽'이라 남은 튕김과 상관없이 항상 튕긴다.
        // //  단, MaxTrigger의 남은 튕김이 있으면 여기서 그 1회를 소비해 준다.
        // if (_remainingMaxBounces > 0)
        //     _remainingMaxBounces = Mathf.Max(0, _remainingMaxBounces - 1);
        // _currentLaunchIsStraightShot = false;
        // lastLaunchWasMaxPull = false;
        //
        // // [추가] 죽든 안 죽든 일단 튕겨준다. (죽어도 튕기는 모습을 잠깐 보여주기 위함)
        // BounceOffWall(contactPoint,normal,  bounceSpeedMultiplier * wall.Bouncyness);
        //
        // // [추가] 죽었다면: 방금 튕긴 모습을 잠깐 보여준 뒤 respawnDelay 후 리스폰한다.
        // if (slimeStats != null && slimeStats.IsDead)
        //     StartRespawn();
        Knockback(transform.position);
    }

    // ======================================================================
    //  [추가] 게임 루프 (Respawn / Stage Clear)
    // ======================================================================

    /// <summary>
    /// [추가] 리스폰 시작. (Spike로 죽었을 때 등, delay 후 리스폰용)
    /// - 중복 실행을 막고, 죽음 상태로 바꾼 뒤 respawnDelay 후 Respawn을 예약한다.
    /// </summary>
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

    /// <summary>[추가] delay초 뒤 실제 Respawn을 호출하는 코루틴.</summary>
    private IEnumerator RespawnAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        _respawnCoroutine = null;
        Respawn();
    }

    /// <summary>
    /// [추가] 슬라임을 리스폰 위치로 되돌린다.
    /// - 낙사(RespawnZone)는 이 함수를 '즉시' 호출한다.
    /// - Spike 사망은 StartRespawn을 거쳐 delay 후 이 함수를 호출한다.
    /// - 지금은 테스트 편의를 위해 스탯을 완전 회복(ResetStats)한다.
    /// </summary>
    public void Respawn()
    {
        // 예약된 리스폰 코루틴이 있으면 정리. (즉시 리스폰이 예약 리스폰보다 먼저 올 수 있음)
        if (_respawnCoroutine != null)
        {
            StopCoroutine(_respawnCoroutine);
            _respawnCoroutine = null;
        }

        // 위치 복귀 (Z는 항상 고정값 유지).
        Vector3 pos = _respawnPosition;
        pos.z = _fixedZ;
        transform.position = pos;

        // 속도/회전/중력 초기화.
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.useGravity = false;
        rb.isKinematic = true;      // [추가] 리스폰 후 Stuck도 완전 고정(떨림 방지)

        // 상태를 Stuck으로 → 다시 조준/발사 가능.
        moveState = SlimeState.Stuck;
        aimState = AimState.None;   // [추가] 리스폰 시 조준 상태도 해제

        // 궤적선 숨김.
        if (trajectoryLineRenderer != null)
            trajectoryLineRenderer.enabled = false;

        // MaxTrigger / 튕김 관련 값 전부 초기화.
        _maxPullHoldTimer = 0f;
        _maxTriggerReady = false;
        _currentLaunchIsStraightShot = false;
        _remainingMaxBounces = 0;
        lastLaunchWasMaxPull = false;

        // 스탯 회복. (테스트 단계라 완전 회복. 체크포인트 체력 이월을 원하면 이 줄만 바꾸면 된다)
        if (slimeStats != null)
            slimeStats.ResetStats();

        // [추가] 비주얼: 변형을 기본 형태로 즉시 리셋.
        if (visuals != null) visuals.ResetVisuals();

        // 리스폰 완료.
        _isRespawning = false;
    }

    /// <summary>
    /// [추가] 스테이지 클리어 처리. (GoalTrigger가 호출)
    /// - 입력을 막고, 슬라임을 멈춘다.
    /// </summary>
    public void OnStageClear()
    {
        _isStageCleared = true; // Update 맨 앞에서 이 값을 보고 입력을 전부 막는다
        aimState = AimState.None; // [추가] 혹시 조준 중이었다면 해제

        // 슬라임 정지 + 중력 off.
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.useGravity = false;

        // 궤적선 숨김.
        if (trajectoryLineRenderer != null)
            trajectoryLineRenderer.enabled = false;

        // [추가] 비주얼: 기본 형태로 정리.
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

    /// <summary>
    /// [추가] 벽에서 '튕기는' 처리. (최대 당김 발사일 때)
    /// - 충돌 직전 속도를 벽 법선 기준으로 반사시킨다.
    /// - 중력 유지, 상태 Flying 유지, Z 속도 0 유지.
    /// 
    /// </summary>
    ///
    // [추가] 벽 방향에 맞춰 bounceFxPivot을 회전시킨 뒤, 그 안의 파티클을 '한 번' 재생한다.
    //  (MMF PlayFeedbacks 대신 ParticleSystem.Play 직접 호출)
  private void PlayBounceFx(Vector3 normal)
{
    if (bounceFxPivot == null) return;

    // Z는 0으로 고정
    normal.z = 0f;
    if (normal.sqrMagnitude < 1e-6f) normal = Vector3.up;

    // Shape Rotation X=90이므로 Transform.forward 기준으로 normal을 맞추기 위해
    // 회전 계산: normal을 XY→XZ로 매핑
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
    // 비눗방울에 흡수될 때: 얼리고 Stuck(붙음) 상태로.
    public void EnterBubble(GameObject bubble)
    {
        enter_buble.Play();
        
        _currentBubble = bubble;
           // 날아가던 직선발사 코루틴 등 정지
        if (_straightShotGravityCoroutine != null)   // 이미 있는 핸들
               StopCoroutine(_straightShotGravityCoroutine);

        rb.linearVelocity  = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.useGravity = false;
        rb.isKinematic = true;             // 완전 고정 (DOTween이 transform으로 옮김)

        moveState = SlimeState.Stuck;      // 벽에 붙은 것과 같은 상태
        aimState  = AimState.None;

        // 발사/튕김 플래그 초기화
        _currentLaunchIsStraightShot = false;
        _remainingMaxBounces = 0;
        lastLaunchWasMaxPull = false;

        if (visuals != null) visuals.ResetVisuals();
    }

// 비눗방울에서 발사될 때: 물리 되살리고 지정 속도로 날림.
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
        // 기본 튕김은 전역 배율(bounceSpeedMultiplier)을 쓰던 기존 동작 그대로.
        //  (PlayBounceFx는 2-인자 오버로드에서 한 번만 호출된다 → 중복 재생 방지)
        BounceOffWall(contact_point, normal, bounceSpeedMultiplier);
    }

    /// <summary>
    /// [추가] 튕김 배율을 직접 지정할 수 있는 오버로드.
    /// - 가시벽처럼 벽마다 다른 Bouncyness를 적용할 때 사용한다.
    /// - 위의 기본 BounceOffWall(normal)도 결국 이 함수를 호출하므로 로직이 한 곳에 모인다.
    /// </summary>
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

        // 오른쪽 벽.
        if (normal.x < -0.5f)
        {
            fxPrefab = rightBounceFxPrefab;
        }
        // 왼쪽 벽.
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

    /// <summary>
    /// [추가] MaxTrigger 직선 발사의 '1회 튕김'을 처리하고 소비하는 헬퍼.
    ///
    /// 왜 이 헬퍼가 필요한가?
    /// - 예전에는 lastLaunchWasMaxPull이 true이면 벽마다 계속 튕길 수 있었다.
    /// - 이제 직선 발사의 튕김은 '딱 1회'로 제한한다.
    /// - 그래서 튕긴 직후 관련 상태를 모두 정리해서, 다음 벽에서는 일반 발사처럼
    ///   (붙기 / Sliding) 처리되도록 만든다.
    /// </summary>
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

    /// <summary>
    /// 캡슐을 줄이지 않고, 콜라이더 중심이 벽에서 radius만큼 떨어지게 붙여 '촥' 느낌을 유지한다.
    /// </summary>
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

    // ======================================================================
    //  계산 파트
    // ======================================================================

    /// <summary>
    /// 드래그로 만들어질 발사 속도를 계산한다.
    ///
    /// 방향: (시작점 - 현재점). 즉 "당긴 반대 방향".
    ///       왼쪽아래로 당기면 이 벡터는 오른쪽위를 가리킨다 => 원하는 발사 방향.
    /// 크기: 당긴 거리 × launchPower. 단, 거리는 maxDragDistance로 제한한다.
    /// </summary>
    private Vector3 CalculateLaunchVelocity(Vector3 currentWorld)
    {
        Vector3 dragVector = _dragStartWorld - currentWorld;

        // 너무 멀리 당겨도 힘이 무한정 커지지 않도록 거리를 상한선까지만 사용.
        float clampedDistance = Mathf.Min(dragVector.magnitude, maxDragDistance);

        // 방향(단위벡터) × 제한된 거리 × 배율 = 최종 속도.
        // dragVector가 거의 0이면 normalized가 불안정하므로 그대로 0 속도를 반환.
        if (dragVector.sqrMagnitude < 0.0001f)
            return Vector3.zero;

        Vector3 velocity = dragVector.normalized * clampedDistance * launchPower;

        // X/Y 평면 게임이므로 Z 방향 속도는 0으로 확실히 막는다.
        velocity.z = 0f;
        return velocity;
    }

    /// <summary>
    /// 예상 궤적을 계산해서 LineRenderer에 점을 채운다.
    ///
    /// 물리 공식(등가속도 운동):
    ///   position(t) = startPos + velocity * t + 0.5 * gravity * t^2
    /// 즉 "처음 위치 + 속도로 간 거리 + 중력으로 떨어진 거리".
    /// 이 공식은 실제 Rigidbody가 중력만 받을 때의 움직임과 일치하므로,
    /// 궤적선과 실제 비행 경로가 잘 맞는다. (단, drag/linearDamping은 0이어야 정확)
    /// </summary>
    private void DrawTrajectory(Vector3 startPos, Vector3 velocity)
    {
        if (trajectoryLineRenderer == null) return;

        if (trajectoryLineRenderer.positionCount != trajectoryPointCount)
            trajectoryLineRenderer.positionCount = trajectoryPointCount;

        // 한 걸음씩 전진하며 점을 찍는다 (포탈 근처에서 휘어짐)
        Vector3 pos = startPos;
        Vector3 vel = velocity;
        float dt = trajectoryTimeStep;
        float gMul = (sizeController != null) ? sizeController.GravityMultiplier : 1f;

        for (int i = 0; i < trajectoryPointCount; i++)
        {
            _trajectoryPoints[i] = pos;   // 현재 위치 기록

            // 이 위치에서의 총 가속도 = 크기별 중력 + 모든 포탈
            Vector3 acc = Physics.gravity * gMul + StageGravity.SumAcceleration(pos);
            acc.z = 0f;

            vel += acc * dt;              // 속도 갱신
            pos += vel * dt;             // 위치 갱신
            pos.z = startPos.z;          // 평면 유지
        }


        // 계산한 점들을 한 번에 LineRenderer로 넘겨 선을 그린다.
        trajectoryLineRenderer.SetPositions(_trajectoryPoints);

        // 참고: 발사 힘이 약하면 velocity가 작아 점들이 촘촘히 모여 짧은 궤적,
        //       강하면 멀리 퍼져 긴 궤적으로 자연스럽게 보인다. (점 개수는 같아도)
    }

    /// <summary>
    /// [추가] MaxTrigger 준비 완료 시 보여주는 '일직선' 미리보기.
    ///
    /// - 포물선(DrawTrajectory)과 달리 중력을 무시하고 velocity 방향으로 곧게 뻗은 선을 그린다.
    /// - 플레이어는 이 직선을 보고 "직선 발사가 준비됐다"를 인지한다.
    /// - Z는 항상 슬라임 Z로 고정한다.
    /// </summary>
    private void DrawStraightLinePreview(Vector3 startPos, Vector3 velocity)
    {
        if (trajectoryLineRenderer == null)
            return;

        // [핵심] 점 개수를 직선용으로 바꾼다. (포물선은 trajectoryPointCount, 직선은 straightLinePointCount)
        //  positionCount를 바꾼 뒤 그만큼만 SetPosition으로 채운다. (배열 길이 불일치 오류 방지)
        if (trajectoryLineRenderer.positionCount != straightLinePointCount)
            trajectoryLineRenderer.positionCount = straightLinePointCount;

        // 발사 방향(velocity)에서 Z를 제거해 평면 방향만 쓴다.
        Vector3 dir = velocity;
        dir.z = 0f;

        // velocity가 거의 0(거의 안 당김)이면 방향을 못 정하므로 모든 점을 시작점에 몰아 짧게 처리.
        if (dir.sqrMagnitude < 0.0001f)
        {
            for (int i = 0; i < straightLinePointCount; i++)
                trajectoryLineRenderer.SetPosition(i, startPos);
            return;
        }

        dir.Normalize();

        // 시작점 → 방향으로 straightLinePreviewLength만큼 뻗은 끝점.
        Vector3 endPos = startPos + dir * straightLinePreviewLength;

        // 점들을 시작~끝 사이에 고르게 배치한다. (점이 2개면 시작/끝만)
        for (int i = 0; i < straightLinePointCount; i++)
        {
            float t = (straightLinePointCount <= 1) ? 0f : (float)i / (straightLinePointCount - 1);
            Vector3 p = Vector3.Lerp(startPos, endPos, t);
            p.z = startPos.z;   // Z 고정
            trajectoryLineRenderer.SetPosition(i, p);
        }
    }

    // ======================================================================
    //  좌표 변환 (헷갈리기 쉬운 부분! 주석 자세히)
    // ======================================================================

    /// <summary>
    /// 화면 좌표(픽셀) → 슬라임 평면 위의 월드 좌표로 변환한다.
    ///
    /// 왜 이렇게 하나?
    /// - 마우스는 2D 화면 위의 픽셀 위치만 안다. "카메라에서 얼마나 먼 깊이(Z)인지"는 모른다.
    /// - 그래서 Camera.ScreenToWorldPoint를 그냥 쓰면 Z를 임의로 넣어야 하고, 값이 튄다.
    /// - 대신 카메라에서 마우스 방향으로 '광선(Ray)'을 쏘고,
    ///   그 광선이 '슬라임 평면(_dragPlane)'과 만나는 지점을 구한다.
    /// - 이러면 결과 좌표의 Z는 항상 슬라임 평면 위 = 슬라임의 Z가 되어 절대 튀지 않는다.
    /// </summary>
    private Vector3 PointerToWorld(Vector3 screenPos)
    {
        Ray ray = cam.ScreenPointToRay(screenPos);

        // 광선이 평면과 만나는 지점까지의 거리(enter)를 구한다.
        if (_dragPlane.Raycast(ray, out float enter))
        {
            // ray.GetPoint(enter) = 광선을 enter만큼 따라간 지점 = 평면 위의 월드 좌표.
            return ray.GetPoint(enter);
        }

        // 만에 하나 평면과 안 만나면(거의 없음) 슬라임 위치로 안전하게 대체.
        return transform.position;
    }

    /// <summary>
    /// 클릭/터치한 화면 위치에 이 슬라임이 있는지 검사한다.
    /// 카메라에서 광선을 쏴서 이 오브젝트의 Collider에 맞았는지 본다.
    /// (슬라임에 Collider가 반드시 있어야 동작한다)
    /// </summary>
    private bool IsPointerOnThisSlime(Vector3 screenPos)
    {
        Ray ray = cam.ScreenPointToRay(screenPos);
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            // 맞은 것이 '나 자신 혹은 내 자식'인지 확인.
            return hit.transform == transform || hit.transform.IsChildOf(transform);
        }
        return false;
    }
}

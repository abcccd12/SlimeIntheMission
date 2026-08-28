using UnityEngine;

public class SlimeVisualController : MonoBehaviour
{
    private enum VisualMode
    {
        Idle,
        Pulling,
        Sliding,
        Flying,
        StuckOnWall
    }

    [Header("참조")] [Tooltip("변형시킬 비주얼 몸통(SlimeBody). 비우면 이 오브젝트를 사용.")] [SerializeField]
    private Transform body;

    [Tooltip("눈 컨트롤러(선택). 없으면 눈 처리는 생략.")] [SerializeField]
    private SlimeEyeController eyes;

    [Tooltip("벽에 붙을 때 bone을 변형하는 컴포넌트(선택).")] [SerializeField]
    private SlimeBoneDeformer boneDeformer;

    [Header("Idle (가만히 있을 때 흐물거림)")] [SerializeField]
    private float idleBounceAmplitude = 0.03f; // 위아래로 살짝 통통

    [SerializeField] private float idleBounceSpeed = 3f; // 통통 속도
    [SerializeField] private float idleWobbleAmount = 0.04f; // 숨쉬듯 늘었다 줄었다 하는 양

    [Header("Pull (당길 때 늘어남)")] [SerializeField]
    private float maxPullStretch = 0.4f; // 당김 stretch 상한(clamp)

    [SerializeField] private float pullStretchMultiplier = 0.5f; // chargeRatio → stretch 배율

    [Header("Launch (발사 순간)")] [SerializeField]
    private float launchStretchAmount = 0.5f; // 발사 방향으로 확 늘어나는 양

    [SerializeField] private float launchRecoverTime = 0.25f; // 원형으로 복귀하는 시간감

    [Header("Stick (벽에 촥 붙기)")] [SerializeField]
    private float stickSquashAmount = 0.45f; // 붙는 순간 납작해지는 양

    [SerializeField] private float stickSettleTime = 0.3f; // 붙고 나서 안정화까지 시간감
    [Range(0f, 1f)] [SerializeField] private float wallNormalBlend = 1f; // 벽 방향에 얼마나 맞출지

    [Header("Bounce (튕기는 벽)")] [SerializeField]
    private float bounceSquashAmount = 0.4f; // 충돌 순간 압축(splat)

    [SerializeField] private float bounceStretchAmount = 0.4f; // 직후 진행방향으로 늘어남(반동)
    [SerializeField] private float bounceRecoverTime = 0.25f; // 복귀 시간감

    [Header("Slide (미끄러질 때)")] [SerializeField]
    private float slideStretchAmount = 0.2f; // 아래로 살짝 늘어짐

    [SerializeField] private float slideWobbleAmount = 0.05f; // 흐르며 흔들리는 양

    [Header("Stuck Hold Visual")] [SerializeField]
    private float stuckHoldSquashAmount = 0.18f;

    [SerializeField] private float stuckHoldWobbleAmount = 0.03f;
    [Header("Deform Safety")]
    [SerializeField] private float minStretchLimit = -0.65f;
    [SerializeField] private float maxStretchLimit = 0.65f;

    [Header("[추가] 직선발사 중간 눌림")]
    [Tooltip("눌림 세기. 진행방향에 '수직'인 축이 이만큼 납작해진다. (크기 배율이 여기 곱해짐)")]
    [SerializeField] private float straightShotSquashAmount = 0.35f;
    [Tooltip("눌림 직후 진행방향으로 늘어나는 양. 0으로 두면 눌림만(1단계).")]
    [SerializeField] private float straightShotStretchAmount = 0.3f;
    [Tooltip("눌림/늘어남 복귀 시간감. (크기 배율이 여기 곱해짐)")]
    [SerializeField] private float straightShotRecoverTime = 0.18f;
    [Tooltip("눌림 → 늘어남 사이의 짧은 간격(초).")]
    [SerializeField] private float straightShotStretchGap = 0.05f;

    [Header("스프링(기본 말랑함) 튜닝")] [Tooltip("클수록 빠르게 원형으로 돌아온다(단단함). 작을수록 느긋(말랑).")] [SerializeField]
    private float springStiffness = 120f;

    [Tooltip("클수록 덜 출렁인다. 작을수록 통통 오버슛이 커진다.")] [SerializeField]
    private float springDamping = 14f;

    [Tooltip("stretch 축(_dir)이 목표 방향으로 회전하는 속도.")] [SerializeField]
    private float dirLerpSpeed = 12f;

    [Header("Wall Contact Visual Offset")] [SerializeField]
    private float contactStickOffsetMultiplier = 0.35f;

    [SerializeField] private float contactOffsetLerpSpeed = 18f;

    private Vector3 _contactOffsetDir = Vector3.zero;
    private float _contactOffsetAmount = 0f;
    private float _currentContactOffsetAmount = 0f;

    private Vector3 _baseScale; // 몸통 기본 스케일 (변형의 기준)
    private Vector3 _baseLocalPos; // 몸통 기본 로컬 위치
    private VisualMode _mode = VisualMode.Idle;

    private float _stretch; // 현재 stretch (스프링 값)
    private float _stretchVel; // 스프링 속도
    private float _targetStretch; // 지속 상태 목표 stretch
    private Vector3 _dir = Vector3.up; // 현재 stretch 축
    private Vector3 _targetDir = Vector3.up; // 목표 stretch 축

    private float _omega; // 고유 진동수
    private float _zeta; // 감쇠비
    private float _omega0; // 기본 진동수
    private float _zeta0; // 기본 감쇠비

    private float _idlePhase; // idle 흔들림용 시간
    private float _pullRatio; // 당김 정도(0~1)
    private Vector3 _pullDir = Vector3.up; // 당길 때 늘어날 방향(=발사 방향)
    private Vector3 _slideDir = Vector3.down;

    private float _pendingTimer = -1f;
    private float _pendingAmount;
    private Vector3 _pendingDir;
    private float _pendingRecover = 0.25f; // [추가] 예약 kick의 복귀 시간감 (이벤트마다 다르게 지정)

    private void Awake()
    {
        if (body == null) body = transform;
        _baseScale = body.localScale;
        _baseLocalPos = body.localPosition;

        _omega0 = Mathf.Sqrt(Mathf.Max(1f, springStiffness));
        _zeta0 = springDamping / (2f * _omega0);
        _omega = _omega0;
        _zeta = _zeta0;
    }

    private void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f || body == null) return;
        _idlePhase += dt;

        UpdateContinuousTarget();

        if (_pendingTimer >= 0f)
        {
            _pendingTimer -= dt;
            if (_pendingTimer <= 0f)
            {
                _pendingTimer = -1f;
                _dir = _pendingDir;
                _targetDir = _pendingDir;
                KickWithRecover(_pendingAmount, _pendingRecover, 0.4f); // [수정] 고정 bounceRecoverTime → 이벤트별 _pendingRecover
            }
        }

        if (_targetDir.sqrMagnitude > 1e-6f)
            _dir = Vector3.Slerp(_dir, _targetDir.normalized, 1f - Mathf.Exp(-dirLerpSpeed * dt));

        float accel = _omega * _omega * (_targetStretch - _stretch) - 2f * _zeta * _omega * _stretchVel;
        _stretchVel += accel * dt;
        _stretch += _stretchVel * dt;

        float extraStretch = 0f;
        Vector3 posOffset = Vector3.zero;
        if (_mode == VisualMode.Idle)
        {
            extraStretch = Mathf.Sin(_idlePhase * idleBounceSpeed) * idleWobbleAmount;
            posOffset.y = Mathf.Sin(_idlePhase * idleBounceSpeed) * idleBounceAmplitude;
        }
        else if (_mode == VisualMode.Sliding)
        {
            extraStretch = Mathf.Sin(_idlePhase * idleBounceSpeed * 1.5f) * slideWobbleAmount;
        }
        else if (_mode == VisualMode.StuckOnWall)
        {
            extraStretch = Mathf.Sin(_idlePhase * idleBounceSpeed * 1.2f) * stuckHoldWobbleAmount;
        }

        float finalStretch = _stretch + extraStretch;

        Vector3 contactOffset = _contactOffsetDir * _currentContactOffsetAmount;
        ApplyDeform(_dir, finalStretch, posOffset + contactOffset);

  

        _currentContactOffsetAmount = Mathf.Lerp(
            _currentContactOffsetAmount,
            _contactOffsetAmount,
            1f - Mathf.Exp(-contactOffsetLerpSpeed * dt)
        );
    }

    
    
    
    private void UpdateContinuousTarget()
    {
        switch (_mode)
        {
            case VisualMode.Idle:
                _targetStretch = 0f;
                _targetDir = Vector3.up;
                break;
            case VisualMode.Pulling:
                _targetStretch = -Mathf.Min(_pullRatio * pullStretchMultiplier, maxPullStretch);
                _targetDir = _pullDir;
                break;
            case VisualMode.Sliding:
                _targetStretch = slideStretchAmount; // 아래로 살짝 늘어짐
                _targetDir = _slideDir;
                break;
            case VisualMode.Flying:
                _targetStretch = 0f; // 날아가는 동안엔 서서히 원형으로 복귀
                break;
            case VisualMode.StuckOnWall:
                _targetStretch = -stuckHoldSquashAmount;
                _targetDir = _dir;
                break;
        }
    } // [수정] UpdateContinuousTarget 닫는 중괄호. (이게 빠져서 CS0106이 줄줄이 났었음)

    public void OnStick(Vector3 contactNormal)
    {
        _mode = VisualMode.StuckOnWall;

        Vector3 n = Flat(contactNormal, Vector3.up);

        _dir = n;
        _targetDir = n;

        _contactOffsetDir = Vector3.zero;
        _contactOffsetAmount = 0f;
        _currentContactOffsetAmount = 0f;

        if (boneDeformer != null) boneDeformer.OnStick(contactNormal);

        KickWithRecover(-stickSquashAmount, stickSettleTime, 0.55f);
    }

        private void ApplyDeform(Vector3 dirXY, float stretch, Vector3 posOffset)
        {
            dirXY.z = 0f;
            if (dirXY.sqrMagnitude < 1e-6f)
                dirXY = Vector3.up;

            dirXY.Normalize();

            stretch = Mathf.Clamp(stretch, minStretchLimit, maxStretchLimit); // stretch가 4까지가더라 슬라임찢어짐

            float along = Mathf.Max(0.2f, 1f + stretch);
            float perp = Mathf.Max(0.2f, 1f - stretch * 0.8f);

            Quaternion rot = Quaternion.FromToRotation(Vector3.up, dirXY);

            body.localRotation = rot;
            body.localScale = new Vector3(
                _baseScale.x * perp,
                _baseScale.y * along,
                _baseScale.z
            );

            body.localPosition = _baseLocalPos + posOffset;

            
        }

        public void BeginPull()
        {
            _mode = VisualMode.Pulling;
            _pullRatio = 0f;
            _omega = _omega0;
            _zeta = _zeta0;

            if (boneDeformer != null) boneDeformer.Release(); // 안빼니까 붙는본그대로날아감
        }

        public void UpdatePull(Vector3 launchDir, float chargeRatio)
        {
            _mode = VisualMode.Pulling;
            _pullDir = Flat(launchDir, Vector3.up);
            _pullRatio = Mathf.Clamp01(chargeRatio);
        }

        public void OnLaunch(Vector3 launchDir, float force)
        {
            _mode = VisualMode.Flying;
            _dir = Flat(launchDir, Vector3.up);
            _targetDir = _dir;
            KickWithRecover(launchStretchAmount, launchRecoverTime, 0.5f);

            if (boneDeformer != null) boneDeformer.Release();
        }
       
        public void OnBounce(Vector3 contactNormal, Vector3 outgoingVelocity)
        {
            _mode = VisualMode.Flying;
            Vector3 n = Flat(contactNormal, Vector3.up);

            _dir = n;
            _targetDir = n;
            KickWithRecover(-bounceSquashAmount, bounceRecoverTime, 0.35f); // 음수 = 눌림

            _pendingTimer = 0.06f;
            _pendingAmount = bounceStretchAmount; // 양수 = 늘어남
            _pendingDir = Flat(outgoingVelocity, n);
        _pendingRecover = bounceRecoverTime;  // 이거 고정값으로하니까 복귀이상함

            if (boneDeformer != null) boneDeformer.Release();
        }

        
        public void OnStraightShotLaunchCompressed(Vector3 travelDir, float squashMultiplier, float recoverMultiplier)
        {
            Debug.Log("MaxTrigger Bone Squash");

            _mode = VisualMode.Flying;

            if (boneDeformer != null)
            {
                boneDeformer.OnMaxTriggerSquash(travelDir, squashMultiplier);
            }

            _pendingTimer = -1f;
        }
   

        public void OnSlide(Vector3 slideDir, float slideSpeed)
        {
            _mode = VisualMode.Sliding;
            _slideDir = Flat(slideDir, Vector3.down);

            if (boneDeformer != null) boneDeformer.OnSlide(_slideDir);
        }

        public void ResetVisuals()
        {
            _mode = VisualMode.Idle;
            _stretch = 0f;
            _stretchVel = 0f;
            _targetStretch = 0f;
            _dir = Vector3.up;
            _targetDir = Vector3.up;
            _pendingTimer = -1f;
            _omega = _omega0;
            _zeta = _zeta0;

            if (body != null)
            {
                body.localRotation = Quaternion.identity;
                body.localScale = _baseScale;
                body.localPosition = _baseLocalPos;
            }

            if (boneDeformer != null) boneDeformer.ResetBones();
        }

        private void KickWithRecover(float amount, float recoverTime, float zeta)
        {
            _stretch = amount;
            _stretchVel = 0f;
            _omega = (recoverTime > 0.01f) ? (2f * Mathf.PI / recoverTime) : _omega0;
            _zeta = zeta; // 작을수록 반동(오버슛)이 강함
        }

        private static Vector3 Flat(Vector3 v, Vector3 fallback)
        {
            v.z = 0f;
            if (v.sqrMagnitude < 1e-6f) return fallback.normalized;
            return v.normalized;
        }
}

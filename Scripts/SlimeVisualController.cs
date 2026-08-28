using UnityEngine;

/// <summary>
/// [추가] 슬라임 "겉모습"만 말랑하게 보이게 하는 비주얼 전용 컨트롤러.
///
/// 핵심 원칙:
/// - 실제 물리(Rigidbody/Collider가 붙은 SlimeRoot)는 절대 찌그러뜨리지 않는다.
/// - 오직 자식 비주얼(body = SlimeBody)만 회전·스케일·위치로 변형한다.
/// - 변형은 항상 "기본 형태(_baseScale/_baseLocalPos)"를 기준으로 계산해 누적 꼬임을 막는다.
///
/// 동작 방식(가짜 소프트바디):
/// - "현재 stretch 값"이 "목표 stretch 값"을 스프링(감쇠 진동)처럼 따라간다.
///   → 감쇠가 약하면 통통 튀는 오버슛이 생겨 젤리처럼 보인다. (DOTween 불필요)
/// - 발사/부착/튕김 같은 순간 반응은 스프링에 "임펄스(값을 확 밀기)"로 준다.
///
/// 변형의 방향성:
/// - _dir(XY 평면 단위벡터) 축으로 몸을 회전시킨 뒤, 그 축으로 늘리고(along) 수직으로 누른다(perp).
///   → 어떤 방향(좌/우/위/아래 벽 normal)이든 방향성 있는 squash/stretch가 가능하다.
/// </summary>
public class SlimeVisualController : MonoBehaviour
{
    // 지금 비주얼이 어떤 "지속 상태"인지. (이벤트가 아니라 계속 이어지는 모드)
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

    // [추가] 벽 붙기 bone 변형 담당(선택). 없으면 bone 변형은 생략.
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

    // [추가] 직선발사 '중간 눌림' 설정 (진행방향에 수직으로 눌렸다가 진행방향으로 늘어남)
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

    // --- 내부 상태 ---
    private Vector3 _baseScale; // 몸통 기본 스케일 (변형의 기준)
    private Vector3 _baseLocalPos; // 몸통 기본 로컬 위치
    private VisualMode _mode = VisualMode.Idle;

    private float _stretch; // 현재 stretch (스프링 값)
    private float _stretchVel; // 스프링 속도
    private float _targetStretch; // 지속 상태 목표 stretch
    private Vector3 _dir = Vector3.up; // 현재 stretch 축
    private Vector3 _targetDir = Vector3.up; // 목표 stretch 축

    // 스프링 반응 계수 (이벤트마다 recoverTime으로 잠깐 바꿔 쓴다)
    private float _omega; // 고유 진동수
    private float _zeta; // 감쇠비
    private float _omega0; // 기본 진동수
    private float _zeta0; // 기본 감쇠비

    private float _idlePhase; // idle 흔들림용 시간
    private float _pullRatio; // 당김 정도(0~1)
    private Vector3 _pullDir = Vector3.up; // 당길 때 늘어날 방향(=발사 방향)
    private Vector3 _slideDir = Vector3.down;

    // Bounce/직선눌림 2단계용 예약 임펄스
    private float _pendingTimer = -1f;
    private float _pendingAmount;
    private Vector3 _pendingDir;
    private float _pendingRecover = 0.25f; // [추가] 예약 kick의 복귀 시간감 (이벤트마다 다르게 지정)

    private void Awake()
    {
        if (body == null) body = transform;
        _baseScale = body.localScale;
        _baseLocalPos = body.localPosition;

        // 기본 스프링 계수 계산. (stiffness/damping → 진동수/감쇠비)
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

        // 1) 지속 상태(모드)에 따른 목표 stretch/방향 결정.
        UpdateContinuousTarget();

        // 2) 예약된 bounce 반동 임펄스가 있으면 시간 되었을 때 발동.
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

        // 3) stretch 축(_dir)을 목표 방향으로 부드럽게 회전.
        if (_targetDir.sqrMagnitude > 1e-6f)
            _dir = Vector3.Slerp(_dir, _targetDir.normalized, 1f - Mathf.Exp(-dirLerpSpeed * dt));

        // 4) stretch 스프링 적분. (목표로 수렴하되 관성으로 오버슛)
        float accel = _omega * _omega * (_targetStretch - _stretch) - 2f * _zeta * _omega * _stretchVel;
        _stretchVel += accel * dt;
        _stretch += _stretchVel * dt;

        // 5) idle/slide의 "살아있는" 미세 흔들림을 최종값에 더한다. (스프링과 별개의 작은 떨림)
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

        // 6) 실제 몸통에 변형 적용.
        Vector3 contactOffset = _contactOffsetDir * _currentContactOffsetAmount;
        ApplyDeform(_dir, finalStretch, posOffset + contactOffset);

        // 7) 눈 갱신. (몸의 stretch 방향으로 살짝 쏠리게 + idle 미세 움직임 + blink)
  

        _currentContactOffsetAmount = Mathf.Lerp(
            _currentContactOffsetAmount,
            _contactOffsetAmount,
            1f - Mathf.Exp(-contactOffsetLerpSpeed * dt)
        );
    }

    
    
    
    /// <summary>모드별 지속 목표(stretch 크기/방향)를 정한다.</summary>
    private void UpdateContinuousTarget()
    {
        switch (_mode)
        {
            case VisualMode.Idle:
                _targetStretch = 0f;
                _targetDir = Vector3.up;
                break;
            case VisualMode.Pulling:
                // 당길수록 발사 방향으로 더 늘어난다. (상한 clamp)
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
                // 벽에 붙어 있는 동안 계속 살짝 눌린 상태 유지
                _targetStretch = -stuckHoldSquashAmount;
                _targetDir = _dir;
                break;
        }
    } // [수정] UpdateContinuousTarget 닫는 중괄호. (이게 빠져서 CS0106이 줄줄이 났었음)

    /// <summary>벽에 붙음: StuckOnWall 모드로 눌린 상태 유지 + bone 변형.</summary>
    public void OnStick(Vector3 contactNormal)
    {
        _mode = VisualMode.StuckOnWall;

        Vector3 n = Flat(contactNormal, Vector3.up);

        _dir = n;
        _targetDir = n;

        // body 위치 offset은 쓰지 않음. Collider와 Visual 분리 방지.
        _contactOffsetDir = Vector3.zero;
        _contactOffsetAmount = 0f;
        _currentContactOffsetAmount = 0f;

        // [추가] 벽 방향에 맞춰 bone들을 눌러 붙게 변형한다. (body 전체는 안 밀림)
        if (boneDeformer != null) boneDeformer.OnStick(contactNormal);

        // 처음 닿는 순간은 강하게 촥 눌림
        KickWithRecover(-stickSquashAmount, stickSettleTime, 0.55f);
    }

        /// <summary>
        /// dirXY 축으로 stretch만큼 늘리고 수직으로 누른다.
        /// 몸을 dir에 맞춰 Z축 회전시켜, 로컬 Y가 stretch 축이 되도록 한다.
        /// </summary>
        private void ApplyDeform(Vector3 dirXY, float stretch, Vector3 posOffset)
        {
            dirXY.z = 0f;
            if (dirXY.sqrMagnitude < 1e-6f)
                dirXY = Vector3.up;

            dirXY.Normalize();

            // 원인:
            // 스프링이 과하게 튀면 stretch가 4.79, -1.35 같은 말도 안 되는 값까지 감.
            //
            // 결과:
            // 슬라임이 위아래로 길게 찢어지는 버그가 생김.
            //
            // 해결:
            // 최종 적용 전에 stretch를 안전 범위로 제한한다.
            stretch = Mathf.Clamp(stretch, minStretchLimit, maxStretchLimit);

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
        // ======================================================================
        //  public 훅 (SlimeLaunchController가 상태 변화 지점에서 호출)
        // ======================================================================

        /// <summary>조준(당김) 시작.</summary>
        public void BeginPull()
        {
            _mode = VisualMode.Pulling;
            _pullRatio = 0f;
            // 새 조준이니 스프링 반응을 기본값으로 리셋.
            _omega = _omega0;
            _zeta = _zeta0;

            // [추가] 조준 시작하면 벽 붙기 bone 변형 해제.
            if (boneDeformer != null) boneDeformer.Release();
        }

        /// <summary>조준 중 매 프레임. launchDir=발사(늘어날) 방향, chargeRatio=당김 정도 0~1.</summary>
        public void UpdatePull(Vector3 launchDir, float chargeRatio)
        {
            _mode = VisualMode.Pulling;
            _pullDir = Flat(launchDir, Vector3.up);
            _pullRatio = Mathf.Clamp01(chargeRatio);
        }

        /// <summary>발사. 발사 방향으로 확 늘렸다가 스프링으로 복귀.</summary>
        public void OnLaunch(Vector3 launchDir, float force)
        {
            _mode = VisualMode.Flying;
            _dir = Flat(launchDir, Vector3.up);
            _targetDir = _dir;
            KickWithRecover(launchStretchAmount, launchRecoverTime, 0.5f);

            // [추가] 발사하면 벽 붙기 bone 변형을 부드럽게 해제.
            if (boneDeformer != null) boneDeformer.Release();
        }
       
        /// <summary>튕김. 충돌 순간 법선 축으로 '눌리고(splat)' → 잠깐 뒤 진행방향으로 반동 stretch.</summary>
        public void OnBounce(Vector3 contactNormal, Vector3 outgoingVelocity)
        {
            _mode = VisualMode.Flying;
            Vector3 n = Flat(contactNormal, Vector3.up);

            // 1단계: [수정] 법선 축으로 '압축(음수)' = 벽에 부딪혀 납작해지는 splat.
            _dir = n;
            _targetDir = n;
            KickWithRecover(-bounceSquashAmount, bounceRecoverTime, 0.35f); // 음수 = 눌림

            // 2단계: 아주 짧은 뒤 '진행방향으로 늘어남(양수)' = 반동. (예약)
            _pendingTimer = 0.06f;
            _pendingAmount = bounceStretchAmount; // 양수 = 늘어남
            _pendingDir = Flat(outgoingVelocity, n);
            _pendingRecover = bounceRecoverTime;  // [추가] 이 예약은 bounce 복귀시간으로 (기존 동작 유지)

            // [추가] 튕기면 벽 붙기 bone 변형 해제.
            if (boneDeformer != null) boneDeformer.Release();
        }

        
        public void OnStraightShotLaunchCompressed(Vector3 travelDir, float squashMultiplier, float recoverMultiplier)
        {
            Debug.Log("MaxTrigger Bone Squash");

            _mode = VisualMode.Flying;

            // SkinnedMeshRenderer라서 Transform scale보다 bone 변형을 사용.
            if (boneDeformer != null)
            {
                boneDeformer.OnMaxTriggerSquash(travelDir, squashMultiplier);
            }

            // 기존 pending stretch 제거
            _pendingTimer = -1f;
        }
        /// <summary>
        /// [추가] 직선발사 '중간 눌림'. SlimeLaunchController가 발사 후 delay 뒤에 호출한다.
        /// - 진행방향에 '수직'인 축을 눌러(음수) 그 축이 납작해진다. (진행방향엔 길어짐)
        /// - straightShotStretchAmount > 0 이면, 짧은 뒤 진행방향으로 늘어남(2단계).
        /// - squashMultiplier / recoverMultiplier = 크기 단계별 배율(SizeController에서 넘어옴).
        /// - mode는 안 바꿈 → Flying의 targetStretch=0 덕에 스프링이 알아서 원형 복귀.
        /// </summary>
   

        /// <summary>미끄럼 시작/유지. 아래(slideDir)로 살짝 늘어지며 흐른다.</summary>
        public void OnSlide(Vector3 slideDir, float slideSpeed)
        {
            _mode = VisualMode.Sliding;
            _slideDir = Flat(slideDir, Vector3.down);

            // [추가] 미끄러질 땐 벽 붙기 변형을 해제(중립)한다.
            if (boneDeformer != null) boneDeformer.OnSlide(_slideDir);
        }

        /// <summary>리스폰/정지 등: 변형을 기본 형태로 즉시 되돌린다.</summary>
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

            // [추가] bone 변형도 즉시 기본으로 리셋.
            if (boneDeformer != null) boneDeformer.ResetBones();
        }

        // ======================================================================
        //  내부 유틸
        // ======================================================================

        /// <summary>스프링에 즉발 임펄스: 값을 amount로 확 밀고, 이후 recoverTime 감각으로 복귀.</summary>
        private void KickWithRecover(float amount, float recoverTime, float zeta)
        {
            _stretch = amount;
            _stretchVel = 0f;
            // 복귀 시간감 → 진동수. (한 주기 ≈ recoverTime 정도로 감각 맞춤)
            _omega = (recoverTime > 0.01f) ? (2f * Mathf.PI / recoverTime) : _omega0;
            _zeta = zeta; // 작을수록 반동(오버슛)이 강함
        }

        /// <summary>Z를 없앤 XY 단위벡터. 거의 0이면 fallback 방향을 준다.</summary>
        private static Vector3 Flat(Vector3 v, Vector3 fallback)
        {
            v.z = 0f;
            if (v.sqrMagnitude < 1e-6f) return fallback.normalized;
            return v.normalized;
        }
}

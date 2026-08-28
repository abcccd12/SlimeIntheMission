using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 슬라임 몸통(SlimeBody) 안의 bone Transform들만 움직여서
/// "벽에 눌러 붙은" 표면 변형 / MaxTrigger 눌림을 표현하는 컴포넌트.
///
/// 핵심:
/// - Rigidbody / Collider / SlimeRoot / sizeRoot / faceRoot는 건드리지 않는다.
/// - 오직 bone.localPosition만 바꾼다.
/// - 모든 변형은 Awake에서 저장한 base localPosition 기준으로 계산한다.
/// </summary>
public class SlimeBoneDeformer : MonoBehaviour
{
    [Header("Bones")]
    [SerializeField] private Transform centerBone;
    [SerializeField] private Transform leftBone;
    [SerializeField] private Transform rightBone;
    [SerializeField] private Transform topBone;
    [SerializeField] private Transform bottomBone;
    [SerializeField] private Transform frontBone;
    [SerializeField] private Transform backBone;

    [Header("Stick Deform")]
    [Tooltip("벽에 가장 강하게 붙는 bone의 이동량.")]
    [SerializeField] private float mainStickAmount = 0.35f;

    [Tooltip("옆 bone이 벽 방향으로 따라오는 양.")]
    [SerializeField] private float sideStickAmount = 0.16f;

    [Tooltip("벽 반대쪽 bone이 둥글게 유지되도록 반대로 밀리는 양.")]
    [SerializeField] private float oppositePushAmount = 0.12f;

    [Tooltip("옆 bone이 서로 벌어지는 양.")]
    [SerializeField] private float verticalSpreadAmount = 0.08f;

    [Tooltip("붙을 때 bone이 target으로 가는 속도.")]
    [SerializeField] private float deformLerpSpeed = 18f;

    [Tooltip("떨어질 때 bone이 base로 돌아오는 속도.")]
    [SerializeField] private float releaseLerpSpeed = 12f;

    [Tooltip("붙어있는 동안 메인 bone이 미세하게 출렁이는 양.")]
    [SerializeField] private float stuckWobbleAmount = 0.025f;

    [SerializeField] private float stuckWobbleSpeed = 4f;

    [Header("Direction Fix")]
    [Tooltip("변형이 벽 반대로 튀어나오면 켜라.")]
    [SerializeField] private bool invertStickDirection = false;

    [Tooltip("좌/우 벽에서 붙는 bone이 반대면 켜라.")]
    [SerializeField] private bool swapLeftRight = false;

    [Header("MaxTrigger Direct Squash")]
    [Tooltip("MaxTrigger 발사 순간 topBone을 아래로 내리는 양.")]
    [SerializeField] private float maxTriggerTopDownAmount = 0.25f;

    [Tooltip("MaxTrigger 발사 순간 bottomBone을 위로 올리는 양.")]
    [SerializeField] private float maxTriggerBottomUpAmount = 0.10f;

    [Tooltip("MaxTrigger 발사 순간 left/right bone을 양옆으로 벌리는 양.")]
    [SerializeField] private float maxTriggerSideSpreadAmount = 0.10f;

    [Tooltip("MaxTrigger 눌림 target으로 가는 속도.")]
    [SerializeField] private float maxTriggerSquashLerpSpeed = 60f;

    [Tooltip("MaxTrigger 눌림을 유지하는 시간.")]
    [SerializeField] private float maxTriggerSquashHoldTime = 0.22f;

    [Tooltip("켜면 MaxTrigger 순간 bone을 target 위치로 즉시 이동시켜서 눌림이 바로 보인다.")]
    [SerializeField] private bool maxTriggerSnapFirstFrame = true;

    private float _maxTriggerSquashTimer;

    // bone별 기본 위치 / 목표 위치
    private readonly Dictionary<Transform, Vector3> _base = new Dictionary<Transform, Vector3>();
    private readonly Dictionary<Transform, Vector3> _target = new Dictionary<Transform, Vector3>();
    private readonly List<Transform> _bones = new List<Transform>();

    private bool _isStuck;
    private float _currentLerpSpeed = 12f;
    private float _wobblePhase;
    private Transform _activeMainBone;
    private Vector3 _activeWallDir;

    private void Awake()
    {
        Register(centerBone);
        Register(leftBone);
        Register(rightBone);
        Register(topBone);
        Register(bottomBone);
        Register(frontBone);
        Register(backBone);
    }

    private void Register(Transform b)
    {
        if (b == null) return;

        _base[b] = b.localPosition;
        _target[b] = b.localPosition;
        _bones.Add(b);
    }

    private void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        _wobblePhase += dt;

        if (_maxTriggerSquashTimer > 0f)
        {
            _maxTriggerSquashTimer -= dt;

            if (_maxTriggerSquashTimer <= 0f)
            {
                _maxTriggerSquashTimer = 0f;
                Release();
            }
        }

        float k = 1f - Mathf.Exp(-_currentLerpSpeed * dt);

        foreach (Transform bone in _bones)
        {
            if (bone == null) continue;

            Vector3 tgt = _target[bone];

            if (_isStuck && bone == _activeMainBone)
            {
                tgt += _activeWallDir *
                       (Mathf.Sin(_wobblePhase * stuckWobbleSpeed) * stuckWobbleAmount);
            }

            bone.localPosition = Vector3.Lerp(bone.localPosition, tgt, k);
        }
    }

    // ======================================================================
    // public 훅
    // ======================================================================

    /// <summary>
    /// 벽에 붙을 때 호출.
    /// contactNormal을 보고 어느 벽인지 판단한 뒤, 움직일 bone을 직접 지정한다.
    /// </summary>
    public void OnStick(Vector3 contactNormal)
    {
        _maxTriggerSquashTimer = 0f;

        Vector3 n = contactNormal;
        n.z = 0f;

        if (n.sqrMagnitude < 1e-6f)
        {
            Release();
            return;
        }

        n.Normalize();

        if (invertStickDirection)
            n = -n;

        Transform L = swapLeftRight ? rightBone : leftBone;
        Transform R = swapLeftRight ? leftBone : rightBone;

        if (Mathf.Abs(n.x) > Mathf.Abs(n.y))
        {
            // 좌/우 벽
            if (n.x < 0f)
            {
                // 오른쪽 벽
                ApplyStick(
                    main: L,
                    opposite: R,
                    sideA: topBone,
                    sideB: bottomBone,
                    wallDir: Vector3.right,
                    spreadA: Vector3.up,
                    spreadB: Vector3.down
                );
            }
            else
            {
                // 왼쪽 벽
                ApplyStick(
                    main: R,
                    opposite: L,
                    sideA: topBone,
                    sideB: bottomBone,
                    wallDir: Vector3.left,
                    spreadA: Vector3.up,
                    spreadB: Vector3.down
                );
            }
        }
        else
        {
            // 상/하 벽
            if (n.y < 0f)
            {
                // 천장
                ApplyStick(
                    main: bottomBone,
                    opposite: topBone,
                    sideA: L,
                    sideB: R,
                    wallDir: Vector3.up,
                    spreadA: Vector3.left,
                    spreadB: Vector3.right
                );
            }
            else
            {
                // 바닥 / 아래 벽
                ApplyStick(
                    main: topBone,
                    opposite: bottomBone,
                    sideA: L,
                    sideB: R,
                    wallDir: Vector3.down,
                    spreadA: Vector3.left,
                    spreadB: Vector3.right
                );
            }
        }

        _isStuck = true;
        _currentLerpSpeed = deformLerpSpeed;
    }

    /// <summary>
    /// MaxTrigger 직선 발사 순간 호출.
    /// 방향/속도/ratio 계산 없이, 정해진 bone을 정해진 방향으로 직접 움직인다.
    /// </summary>
    public void OnMaxTriggerSquash(Vector3 travelDir, float amountMultiplier = 1f)
    {
        // MaxTrigger도 벽 붙기와 같은 ApplyStick 방식으로 처리.
        // 바닥/아래 벽에 붙는 것처럼:
        // main = topBone
        // opposite = bottomBone
        // sideA/B = left/right
        // wallDir = Vector3.down
        //
        // 즉, 위쪽 bone을 아래로 강하게 누르고,
        // 아래쪽 bone은 반대로 살짝 밀고,
        // 좌우 bone은 아래쪽으로 따라오면서 좌우로 퍼진다.

        _maxTriggerSquashTimer = 0f;

        Transform L = swapLeftRight ? rightBone : leftBone;
        Transform R = swapLeftRight ? leftBone : rightBone;

        ApplyStick(
            main: topBone,
            opposite: bottomBone,
            sideA: L,
            sideB: R,
            wallDir: Vector3.down,
            spreadA: Vector3.left,
            spreadB: Vector3.right
        );

        _isStuck = false; // 벽에 붙은 상태는 아님. wobble은 끔.
        _activeMainBone = null;
        _activeWallDir = Vector3.zero;

        _currentLerpSpeed = maxTriggerSquashLerpSpeed;
        _maxTriggerSquashTimer = maxTriggerSquashHoldTime;

        if (maxTriggerSnapFirstFrame)
        {
            foreach (Transform bone in _bones)
            {
                if (bone == null) continue;
                bone.localPosition = _target[bone];
            }
        }

        Debug.Log(
            $"[BONE MAX APPLYSTICK] main=top, wallDir=down, hold={maxTriggerSquashHoldTime:F2}"
        );
    }
    /// <summary>
    /// 부드럽게 원래 형태로 되돌린다.
    /// </summary>
    public void Release()
    {
        _maxTriggerSquashTimer = 0f;

        foreach (Transform bone in _bones)
            _target[bone] = _base[bone];

        _isStuck = false;
        _activeMainBone = null;
        _activeWallDir = Vector3.zero;
        _currentLerpSpeed = releaseLerpSpeed;
    }

    /// <summary>
    /// 즉시 기본 형태로 되돌린다.
    /// </summary>
    public void ResetBones()
    {
        _maxTriggerSquashTimer = 0f;

        foreach (Transform bone in _bones)
        {
            if (bone == null) continue;

            _target[bone] = _base[bone];
            bone.localPosition = _base[bone];
        }

        _isStuck = false;
        _activeMainBone = null;
        _activeWallDir = Vector3.zero;
        _currentLerpSpeed = releaseLerpSpeed;
    }

    /// <summary>
    /// 미끄러질 때는 특별 변형 없이 중립으로 되돌린다.
    /// </summary>
    public void OnSlide(Vector3 slideDir)
    {
        Release();
    }

    // ======================================================================
    // 내부: bone target 설정
    // ======================================================================

    private void ApplyStick(
        Transform main,
        Transform opposite,
        Transform sideA,
        Transform sideB,
        Vector3 wallDir,
        Vector3 spreadA,
        Vector3 spreadB
    )
    {
        foreach (Transform bone in _bones)
            _target[bone] = _base[bone];

        SetTarget(main, wallDir * mainStickAmount);
        SetTarget(opposite, -wallDir * oppositePushAmount);
        SetTarget(sideA, wallDir * sideStickAmount + spreadA * verticalSpreadAmount);
        SetTarget(sideB, wallDir * sideStickAmount + spreadB * verticalSpreadAmount);
        SetTarget(centerBone, wallDir * (mainStickAmount * 0.15f));

        _activeMainBone = main;
        _activeWallDir = wallDir;
        Debug.Log($"main = {main}, opposite = {opposite}, sideA = {sideA}, sideB= {sideB}");
    }

    private void SetTarget(Transform bone, Vector3 offset)
    {
        if (bone == null) return;
        if (!_base.ContainsKey(bone)) return;

        _target[bone] = _base[bone] + offset;
    }
}
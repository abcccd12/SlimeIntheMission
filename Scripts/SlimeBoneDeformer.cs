using System.Collections.Generic;
using UnityEngine;

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
    [SerializeField] private float mainStickAmount = 0.35f;

    [SerializeField] private float sideStickAmount = 0.16f;

    [SerializeField] private float oppositePushAmount = 0.12f;

    [SerializeField] private float verticalSpreadAmount = 0.08f;

    [SerializeField] private float deformLerpSpeed = 18f;

    [SerializeField] private float releaseLerpSpeed = 12f;

    [SerializeField] private float stuckWobbleAmount = 0.025f;

    [SerializeField] private float stuckWobbleSpeed = 4f;

    [Header("Direction Fix")]
    [SerializeField] private bool invertStickDirection = false; // 반대로붙음 켜니까됨 이유모름

    [SerializeField] private bool swapLeftRight = false;

    [Header("MaxTrigger Direct Squash")]
    [SerializeField] private float maxTriggerTopDownAmount = 0.25f;

    [SerializeField] private float maxTriggerBottomUpAmount = 0.10f;

    [SerializeField] private float maxTriggerSideSpreadAmount = 0.10f;

    [SerializeField] private float maxTriggerSquashLerpSpeed = 60f;

    [SerializeField] private float maxTriggerSquashHoldTime = 0.22f;

    [SerializeField] private bool maxTriggerSnapFirstFrame = true; // lerp하니까 눌린게안보임

    private float _maxTriggerSquashTimer;

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
            if (n.x < 0f)
            {
                // 오른쪽벽
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
            if (n.y < 0f)
            {
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

    public void OnMaxTriggerSquash(Vector3 travelDir, float amountMultiplier = 1f)
    {
        // travelDir쓰려다 방향이상해서 그냥아래로누름
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

        _isStuck = false;
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

    public void OnSlide(Vector3 slideDir)
    {
        Release();
    }

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

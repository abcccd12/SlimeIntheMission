using UnityEngine;

/// <summary>
/// [추가] 슬라임 눈 전용 컨트롤러.
///
/// 역할:
/// - 눈이 몸(SlimeBody)의 변형 방향으로 살짝 쏠려 "같이 살아있는" 느낌을 준다.
/// - Idle일 때 아주 미세하게 움직인다.
/// - 가끔 깜빡인다(blink).
///
/// 설계:
/// - 복잡한 리깅 없이 localPosition / localScale 만으로 처리한다. (가볍게)
/// - 항상 "기본 위치/스케일(_lBase 등)"을 기준으로 계산해 누적 꼬임을 막는다.
/// - SlimeVisualController가 매 프레임 UpdateEyes(...)를 호출한다.
/// </summary>
public class SlimeEyeController : MonoBehaviour
{
    [Header("눈 오브젝트")]
    [SerializeField] private Transform leftEye;
    [SerializeField] private Transform rightEye;

    [Header("따라가기 / 살아있는 느낌")]
    [Tooltip("몸의 stretch 방향으로 눈이 쏠리는 정도. 클수록 많이 밀림.")]
    [SerializeField] private float eyeFollowStrength = 0.1f;
    [Tooltip("Idle일 때 눈이 미세하게 움직이는 양.")]
    [SerializeField] private float eyeIdleMotion = 0.02f;
    [SerializeField] private float eyeIdleSpeed = 2f;

    [Header("깜빡임(blink)")]
    [SerializeField] private float blinkIntervalMin = 2f;
    [SerializeField] private float blinkIntervalMax = 5f;
    [SerializeField] private float blinkDuration = 0.1f;

    // 기본 위치/스케일 (변형의 기준)
    private Vector3 _lBasePos, _rBasePos;
    private Vector3 _lBaseScale, _rBaseScale;

    private float _phase;         // idle 미세 움직임용 시간
    private float _blinkCountdown; // 다음 깜빡임까지 남은 시간
    private float _blinkTimer;     // 현재 깜빡이는 중이면 남은 시간

    private void Awake()
    {
        if (leftEye != null) { _lBasePos = leftEye.localPosition; _lBaseScale = leftEye.localScale; }
        if (rightEye != null) { _rBasePos = rightEye.localPosition; _rBaseScale = rightEye.localScale; }
        _blinkCountdown = Random.Range(blinkIntervalMin, blinkIntervalMax);
    }

    /// <summary>
    /// [추가] 눈 갱신. lean = 몸의 변형 방향*크기(눈이 쏠릴 방향), idle = 지금 idle 상태인지.
    /// </summary>
    public void UpdateEyes(Vector3 lean, bool idle, float dt)
    {
        _phase += dt;

        // 눈이 쏠릴 오프셋. (몸이 늘어난 방향으로 눈도 살짝 이동)
        Vector3 off = lean * eyeFollowStrength;
        off.z = 0f;
        if (idle)
            off.y += Mathf.Sin(_phase * eyeIdleSpeed) * eyeIdleMotion;

        if (leftEye != null) leftEye.localPosition = _lBasePos + off;
        if (rightEye != null) rightEye.localPosition = _rBasePos + off;

        // 깜빡임 처리: 평소엔 카운트다운, 0이 되면 blinkDuration 동안 눈을 납작하게.
        float blinkScaleY = 1f;
        if (_blinkTimer > 0f)
        {
            _blinkTimer -= dt;
            blinkScaleY = 0.1f; // 눈 감음 (세로로 납작)
        }
        else
        {
            _blinkCountdown -= dt;
            if (_blinkCountdown <= 0f)
            {
                _blinkTimer = blinkDuration;
                _blinkCountdown = Random.Range(blinkIntervalMin, blinkIntervalMax);
            }
        }

        if (leftEye != null)
            leftEye.localScale = new Vector3(_lBaseScale.x, _lBaseScale.y * blinkScaleY, _lBaseScale.z);
        if (rightEye != null)
            rightEye.localScale = new Vector3(_rBaseScale.x, _rBaseScale.y * blinkScaleY, _rBaseScale.z);
    }
}

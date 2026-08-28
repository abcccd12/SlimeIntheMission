using UnityEngine;

public class SlimeEyeController : MonoBehaviour
{
    [Header("눈 오브젝트")]
    [SerializeField] private Transform leftEye;
    [SerializeField] private Transform rightEye;

    [Header("따라가기 / 살아있는 느낌")]
    [SerializeField] private float eyeFollowStrength = 0.1f;
    [SerializeField] private float eyeIdleMotion = 0.02f;
    [SerializeField] private float eyeIdleSpeed = 2f;

    [Header("깜빡임(blink)")]
    [SerializeField] private float blinkIntervalMin = 2f;
    [SerializeField] private float blinkIntervalMax = 5f;
    [SerializeField] private float blinkDuration = 0.1f;

    private Vector3 _lBasePos, _rBasePos;
    private Vector3 _lBaseScale, _rBaseScale;

    private float _phase;
    private float _blinkCountdown;
    private float _blinkTimer;

    private void Awake()
    {
        if (leftEye != null) { _lBasePos = leftEye.localPosition; _lBaseScale = leftEye.localScale; }
        if (rightEye != null) { _rBasePos = rightEye.localPosition; _rBaseScale = rightEye.localScale; }
        _blinkCountdown = Random.Range(blinkIntervalMin, blinkIntervalMax);
    }

    public void UpdateEyes(Vector3 lean, bool idle, float dt)
    {
        _phase += dt;

        Vector3 off = lean * eyeFollowStrength;
        off.z = 0f;
        if (idle)
            off.y += Mathf.Sin(_phase * eyeIdleSpeed) * eyeIdleMotion;

        if (leftEye != null) leftEye.localPosition = _lBasePos + off;
        if (rightEye != null) rightEye.localPosition = _rBasePos + off;

        float blinkScaleY = 1f;
        if (_blinkTimer > 0f)
        {
            _blinkTimer -= dt;
            blinkScaleY = 0.1f; // 0하니까 눈사라짐
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

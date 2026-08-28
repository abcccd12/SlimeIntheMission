using UnityEngine;

/// <summary>
/// CameraTarget(빈 오브젝트) 전용 추적 스크립트.
///
/// 왜 이런 구조인가?
/// - Cinemachine 가상 카메라는 "슬라임"을 직접 Follow하지 않는다.
/// - 대신 이 스크립트가 붙은 "CameraTarget"을 Follow한다.
/// - 그래서 카메라의 느낌(위로는 부드럽게, 아래로는 잠깐 버티다 빠르게 추락 등)을
///   Cinemachine 설정이 아니라 여기 코드에서 자유롭게 조절할 수 있다.
///
/// 게임 규칙 요약:
/// - 3D 슬라임이 X/Y 평면에서 벽을 타고 위로 올라가는 수직 진행 게임.
/// - 위로 올라갈 때: CameraTarget이 부드럽게 따라 올라간다.
/// - 조금 떨어질 때: 바로 안 내려가고 잠깐 버틴다. (데드존)
/// - 많이 떨어질 때: 뒤늦게 빠르게 아래로 쫓아간다. (추락감)
///
/// 사용법:
/// 1) 씬에 빈 오브젝트를 만들고 이름을 CameraTarget으로 한다.
/// 2) 이 스크립트를 CameraTarget에 붙인다.
/// 3) target 칸에 슬라임을 연결한다.
/// 4) Cinemachine 가상 카메라의 Follow 칸에 이 CameraTarget을 넣는다.
/// </summary>
public class CameraTargetFollow : MonoBehaviour
{
    [Header("따라갈 대상")]
    [Tooltip("CameraTarget이 따라갈 대상. 보통 슬라임을 연결한다.")]
    [SerializeField] private Transform target;

    [Header("가로(X) 따라가기")]
    [Tooltip("X축을 얼마나 빠르게 따라갈지. 클수록 빠르게, 작을수록 부드럽고 느긋하게 따라간다.")]
    [SerializeField] private float xFollowSpeed = 3f;

    [Header("세로(Y) 따라가기")]
    [Tooltip("슬라임이 '위로' 올라갈 때 따라 올라가는 속도. 부드럽게 느껴질 정도로.")]
    [SerializeField] private float upFollowSpeed = 4f;

    [Tooltip("슬라임이 많이 떨어졌을 때 '아래로' 쫓아가는 속도. 추락감을 위해 upFollowSpeed보다 크게 두는 걸 추천.")]
    [SerializeField] private float downCatchUpSpeed = 8f;

    [Tooltip("슬라임이 이 거리(월드 단위)만큼 아래로 벌어지기 전까지는 카메라가 아래로 안 내려가고 버틴다.")]
    [SerializeField] private float downFollowDeadZone = 2f;

    [Tooltip("슬라임보다 카메라 초점을 살짝 위/아래로 옮기고 싶을 때. 0이면 슬라임과 같은 높이를 본다.")]
    [SerializeField] private float verticalOffset = 0f;

    [Header("Z 고정")]
    [Tooltip("CameraTarget의 Z값을 항상 이 값으로 고정한다. (X/Y 평면 게임이라 Z는 변하면 안 된다)")]
    [SerializeField] private float fixedZ = 0f;
    
    [Header("디버그")]
    [SerializeField] private Vector3 debugTargetPosition;
    [SerializeField] private Vector3 debugCameraTargetPosition;
    [SerializeField] private float debugDesiredY;
    [SerializeField] private float debugDropDistance;

    // ======================================================================
    //  실제 추적 로직
    // ======================================================================

    // 왜 LateUpdate인가?
    // - 슬라임은 보통 Update / FixedUpdate에서 움직인다.
    // - 카메라(추적)는 "모든 움직임이 끝난 뒤"의 최종 위치를 따라가야 흔들림이 없다.
    // - LateUpdate는 그 프레임의 Update들이 다 끝난 다음에 호출되므로 카메라 추적에 딱 맞다.
    private void LateUpdate()
    {
        // 대상이 없으면 아무것도 하지 않는다. (연결을 깜빡했을 때 오류 방지)
        if (target == null)
            return;

        // 현재 CameraTarget의 위치를 가져와서, 이 값을 조금씩 수정한 뒤 다시 적용한다.
        Vector3 pos = transform.position;

        // ---------------------------------------------------------------
        // 1) 가로(X): 부드럽게 따라간다.
        // ---------------------------------------------------------------
        // Mathf.Lerp(현재값, 목표값, t)에서 t가 클수록 목표에 빨리 다가간다.
        // t = 속도 * deltaTime 으로 두면, 속도가 클수록 빠르게 따라가는 '부드러운 추적'이 된다.
        // (Lerp는 t를 0~1로 자동 제한하므로 프레임이 튀어도 목표를 넘어가지 않는다)
        pos.x = Mathf.Lerp(pos.x, target.position.x, xFollowSpeed * Time.deltaTime);

        // ---------------------------------------------------------------
        // 2) 세로(Y): 위/아래를 다르게 처리한다. (이 게임의 핵심 카메라 느낌)
        // ---------------------------------------------------------------
        // 카메라가 맞추고 싶은 이상적인 Y (슬라임 높이 + 오프셋).
        float desiredY = target.position.y + verticalOffset;

        if (desiredY >= pos.y)
        {
            // (A) 슬라임이 카메라 초점보다 '위'에 있음 = 위로 올라가는 중.
            //     -> 부드럽게 따라 올라간다.
            pos.y = Mathf.Lerp(pos.y, desiredY, upFollowSpeed * Time.deltaTime);
        }
        else
        {
            // (B) 슬라임이 카메라 초점보다 '아래'에 있음 = 떨어지는 중.
            //     얼마나 아래로 벌어졌는지 거리를 잰다. (항상 양수)
            float dropDistance = pos.y - desiredY;

            if (dropDistance > downFollowDeadZone)
            {
                // 데드존을 넘어설 만큼 '많이' 떨어졌다.
                // -> 뒤늦게, 그리고 빠르게 아래로 쫓아간다. (downCatchUpSpeed 사용)
                pos.y = Mathf.Lerp(pos.y, desiredY, downCatchUpSpeed * Time.deltaTime);
            }
            // else: 아직 데드존 안(살짝만 떨어짐)이면 pos.y를 그대로 둔다.
            //       = 카메라가 내려가지 않고 "잠깐 버티는" 느낌.
        }

        // ---------------------------------------------------------------
        // 3) Z 고정: 평면 게임이므로 Z는 항상 같은 값으로 유지한다.
        // ---------------------------------------------------------------
        pos.z = fixedZ;

        // 계산이 끝난 위치를 실제로 적용한다.
        transform.position = pos;
        
        debugTargetPosition = target.position;
        debugCameraTargetPosition = transform.position;
        debugDesiredY = target.position.y + verticalOffset;
        debugDropDistance = transform.position.y - debugDesiredY;
    }
}

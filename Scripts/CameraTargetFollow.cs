using UnityEngine;

public class CameraTargetFollow : MonoBehaviour
{
    [Header("따라갈 대상")]
    [SerializeField] private Transform target;

    [Header("가로(X) 따라가기")]
    [SerializeField] private float xFollowSpeed = 3f;

    [Header("세로(Y) 따라가기")]
    [SerializeField] private float upFollowSpeed = 4f;

    [SerializeField] private float downCatchUpSpeed = 8f; // 4로하니까 추락감없음

    [SerializeField] private float downFollowDeadZone = 2f; // 바로내려가서 어지러움

    [SerializeField] private float verticalOffset = 0f;

    [Header("Z 고정")]
    [SerializeField] private float fixedZ = 0f;
    
    [Header("디버그")]
    [SerializeField] private Vector3 debugTargetPosition;
    [SerializeField] private Vector3 debugCameraTargetPosition;
    [SerializeField] private float debugDesiredY;
    [SerializeField] private float debugDropDistance;

    // lateupdate 안쓰니까 카메라덜덜거림 update에넣었었음
    private void LateUpdate()
    {
        if (target == null)
            return;

        Vector3 pos = transform.position;

        pos.x = Mathf.Lerp(pos.x, target.position.x, xFollowSpeed * Time.deltaTime);

        float desiredY = target.position.y + verticalOffset;

        if (desiredY >= pos.y)
        {
            pos.y = Mathf.Lerp(pos.y, desiredY, upFollowSpeed * Time.deltaTime);
        }
        else
        {
            float dropDistance = pos.y - desiredY;

            if (dropDistance > downFollowDeadZone)
            {
                pos.y = Mathf.Lerp(pos.y, desiredY, downCatchUpSpeed * Time.deltaTime);
            }
            // 아직 조금떨어진거. 여기내려가면 정신없음
        }

        pos.z = fixedZ;

        transform.position = pos;
        
        debugTargetPosition = target.position;
        debugCameraTargetPosition = transform.position;
        debugDesiredY = target.position.y + verticalOffset;
        debugDropDistance = transform.position.y - debugDesiredY;
    }
}

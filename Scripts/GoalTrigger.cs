using System;
using UnityEngine;

/// <summary>
/// Goal(도착 지점) 오브젝트에 붙이는 스크립트.
///
/// 동작:
/// - Goal에는 Collider가 있고 Is Trigger가 켜져 있다고 가정한다.
/// - 슬라임이 이 Trigger 안에 들어오면 스테이지 클리어 처리를 한다.
/// - 지금은 Debug.Log + 슬라임 정지만 하지만,
///   나중에 StageManager가 OnCleared 이벤트를 구독해 "다음 스테이지"로 넘길 수 있게 열어뒀다.
/// </summary>
public class GoalTrigger : MonoBehaviour
{
    // 한 번 클리어하면 다시 처리하지 않도록 막는 플래그.
    private bool _cleared;

    // [확장용] 나중에 StageManager 등이 여기에 함수를 연결(구독)하면,
    //  클리어 순간 자동으로 그 함수가 호출된다. 지금 당장은 안 써도 된다.
    //  예) goal.OnCleared += stageManager.LoadNextStage;
    public event Action OnCleared;

    private void OnTriggerEnter(Collider other)
    {
        // 이미 클리어했으면 무시.
        if (_cleared)
            return;

        // 들어온 것이 슬라임인지 확인. (콜라이더가 자식에 있어도 부모에서 컨트롤러를 찾는다)
        SlimeLaunchController slime = other.GetComponentInParent<SlimeLaunchController>();
        if (slime == null)
            return;

        _cleared = true;
        Debug.Log("Stage Clear");

        // 슬라임을 클리어 상태로 만든다. (입력/이동 정지)
        slime.OnStageClear();

        // 구독자가 있으면 알린다. (없으면 아무 일도 안 함)
        OnCleared?.Invoke();
    }
}

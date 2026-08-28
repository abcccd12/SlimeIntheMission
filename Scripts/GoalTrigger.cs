using System;
using UnityEngine;

public class GoalTrigger : MonoBehaviour
{
    private bool _cleared; // 이거없으면 트리거 여러번들어감

    public event Action OnCleared; // 나중에 매니저연결용 아직안씀

    private void OnTriggerEnter(Collider other)
    {
        if (_cleared)
            return;

        SlimeLaunchController slime = other.GetComponentInParent<SlimeLaunchController>();
        if (slime == null)
            return; // 예외처리 안했더니오류생김

        _cleared = true;
        Debug.Log("Stage Clear");

        slime.OnStageClear();

        OnCleared?.Invoke();
    }
}


using MoreMountains.Feedbacks;
using UnityEngine;

public class Wallsurface : MonoBehaviour
{


    // 비 오는 동안 true → Normal 벽이 Slippery처럼 동작
    
        
    public enum WallType
    {
        Normal,
        Slippery,
        Spike,
        Rayfire,
        Stick       // 닿으면 발사 종류와 상관없이 '무조건' 즉시 붙는다 (튜토 착지용/특수 벽)
    }
    
    public static bool GlobalSlippery = false;

// 게터가 전역 상태를 반영. (Spike/Rayfire는 그대로 두고 Normal만 미끄럽게)
    

    
    public enum  MaterialType
    {
        Concrete
    }
    
    [SerializeField] private WallType wallType = WallType.Normal;
    [SerializeField] private float slipdelay = 1f;
    [SerializeField] private float slimedamage = 1f;
    [SerializeField] private float bouncyness = 1f;
    
    public WallType Type => (GlobalSlippery && wallType == WallType.Normal)
        ? WallType.Slippery
        : wallType;
    
    public MaterialType matType;
    public MMF_Player destroyfeedback; 
    
    public float Slipdelay  => slipdelay;
    public float Slimedamage => slimedamage;
    public float Bouncyness => bouncyness;

    public void PlayDestroy()
    {
        destroyfeedback.PlayFeedbacks();
    }
    
}

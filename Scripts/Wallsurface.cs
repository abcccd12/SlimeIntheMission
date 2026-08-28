
using MoreMountains.Feedbacks;
using UnityEngine;

public class Wallsurface : MonoBehaviour
{


    // 비오면 일반벽이 미끄러워야해서 이렇게함. 가시는그대로
    
        
    public enum WallType
    {
        Normal,
        Slippery,
        Spike,
        Rayfire,
        Stick       // 튜토착지용. 튕기면 착지못함
    }
    
    public static bool GlobalSlippery = false;

    
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

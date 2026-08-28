using UnityEngine;
using DG.Tweening;

public class TutorialTrigger : MonoBehaviour
{
    [SerializeField] private TutorialSequence tutorial;
    [SerializeField] private string slimeTag = "Slime";
    [SerializeField] private GameObject slime;
    [SerializeField] private Rigidbody slimeRb;
    private bool hastriggerd = false;
    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<SlimeLaunchController>() == null) return; // 자식콜라이더라서 parent로찾아야됨
        if(hastriggerd) return; // 두번들어가서 대사두번나옴
        
        hastriggerd = true;
        
        
        
        
        Debug.Log("once");
        
        
        
        
        
        gameObject.SetActive(false);
        
        slime.GetComponent<Collider>().enabled = true;
        slime.GetComponent<SlimeLaunchController>().SetFlying();
        
        DOVirtual.DelayedCall(0.5f, () => {  //람다식을 이용해서 함수를 한번에 실행. 
            Debug.Log("two");
            
            tutorial.OnReachedTrigger();
            
            
        });
        
    }
}
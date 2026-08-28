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
        // 자식 콜라이더로 들어와도 부모의 슬라임을 찾는다
        if (other.GetComponentInParent<SlimeLaunchController>() == null) return;
        if(hastriggerd) return;
        
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
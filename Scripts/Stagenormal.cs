using Unity.VisualScripting;
using UnityEngine;
using MoreMountains.Feedbacks;

public class Stagenormal : MonoBehaviour
{
 //   [SerializeField] private MMF_Player baseMusic;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnTriggerEnter(Collider other)
    {
        if(other.CompareTag("Slime"))
        {
            //baseMusic.PlayFeedbacks();
            
        }
    }
}

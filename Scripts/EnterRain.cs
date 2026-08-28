using System;
using UnityEngine;
using MoreMountains.Feedbacks;
public class EnterRain : MonoBehaviour
{
    [SerializeField] private MMF_Player rainsound;
        
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
        if (other.CompareTag("Slime"))
        {
            rainsound.PlayFeedbacks();
            Wallsurface.GlobalSlippery = true; // 안켜니까 비오는데 안미끄러움
        }
    }

    private void OnTriggerExit(Collider other)
    {

        rainsound.StopFeedbacks();
        Wallsurface.GlobalSlippery = false; // 나갈때 안끄니까 계속미끄러움
        
    }
}

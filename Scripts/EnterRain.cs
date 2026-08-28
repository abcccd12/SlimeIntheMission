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
            Wallsurface.GlobalSlippery = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {

        rainsound.StopFeedbacks();
        Wallsurface.GlobalSlippery = false;
        
    }
}

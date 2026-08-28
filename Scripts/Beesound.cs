using System;
using MoreMountains.Feedbacks;
using UnityEngine;

public class Beesound : MonoBehaviour
{


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    [SerializeField] private MMF_Player sound;
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Slime"))
        {
            sound.PlayFeedbacks();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Slime"))
        {
            sound.StopFeedbacks();
        }
    }
}

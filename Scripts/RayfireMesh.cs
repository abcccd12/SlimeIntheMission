using System;
using UnityEngine;
using RayFire;

[RequireComponent(typeof(RayfireRigid))]
public class RayfireMesh : MonoBehaviour
{
    private RayfireRigid rayfire;
    private bool broken;

    private void Awake()
    {
        rayfire = GetComponent<RayfireRigid>();
    }

    private void OnCollisionEnter(Collision collision)
    {
        SlimeLaunchController slime = collision.collider.GetComponent<SlimeLaunchController>();
        if(broken) return;
        if (!collision.collider.CompareTag("Slime")) return;
        broken = true;
        
    }
}

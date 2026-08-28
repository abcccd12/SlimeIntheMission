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
        if(broken) return; // 이거안하니까 여러번부서짐
        if (!collision.collider.CompareTag("Slime")) return;
        broken = true;
        
    }
}

using System;
using UnityEngine;

public class Tutosetactive : MonoBehaviour
{
    [SerializeField] private GameObject[] objects;
    // Start is called once before the first execution of Update after the MonoBehaviour is created

    private void OnTriggerEnter(Collider other)
    {
        
        foreach (var o in objects )
        {
            o.SetActive(true); //set active하면 ture하는 순간 다른 콜라이더도 즉시발동 왜?? 아직못고침
        }
    }

    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}

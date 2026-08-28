using System;
using UnityEngine;

public class StageWind : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created

    
    public enum Windtype
    {
        light,
        normal,
        strong
    }

    [SerializeField] private Windtype _windtype;
    [SerializeField] private Vector2 windDirection = Vector2.right;

    [SerializeField] private float lightforce = 5f;
    [SerializeField] private float normalforce = 10f;
    [SerializeField] private float strongforce = 20f;

    private float Currentforce
    {
        get
        {
            switch (_windtype)
            {
                case Windtype.light: return lightforce;
                case Windtype.normal: return normalforce;
                case Windtype.strong: return strongforce;
                default: return 0f;
            }
        }
    }

    private Vector3 windvector
    {
        get
        { 
            return ((Vector3)windDirection.normalized) * Currentforce;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
       
        if(!other.CompareTag("Slime")) return;
        SlimeLaunchController slime = other.GetComponentInChildren<SlimeLaunchController>();
        if(slime == null) Debug.Log("slimenulll"); // 예외처리 안했더니오류생김 근데로그만침 왜
        slime.SetWind(windvector);
    }
    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Slime")) return;
        SlimeLaunchController slime = other.GetComponentInParent<SlimeLaunchController>();
        if (slime == null) return; // 나갈때 바람안꺼져서 계속밀림

        slime.SetWind(Vector3.zero);
    }

    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}

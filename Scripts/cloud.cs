using UnityEngine;

public class cloud : MonoBehaviour
{
    [SerializeField] private Collider col;
    public bool isActive = false;

    void Awake()
    {
        if (col == null)
            col = GetComponent<Collider>(); // 인스펙터에 안넣음

        col.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
       
    }

    void OnTriggerExit(Collider other)
    {
        col.isTrigger = false; // 안끄니까 계속통과함
         Debug.Log($"Cloud trigger, isTrigger={col.isTrigger}");
    }


    void OnCollisionExit(Collision collision)
    {
        
    }
}

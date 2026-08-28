using UnityEngine;

public class cloud : MonoBehaviour
{
    [SerializeField] private Collider col;
    public bool isActive = false;

    void Awake()
    {
        if (col == null)
            col = GetComponent<Collider>();

        col.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
       
    }

    void OnTriggerExit(Collider other)
    {
        col.isTrigger = false; 
         Debug.Log($"Cloud trigger, isTrigger={col.isTrigger}");
    }


    void OnCollisionExit(Collision collision)
    {
        
    }
}

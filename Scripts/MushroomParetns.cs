using UnityEngine;

public class MushroomParetns : MonoBehaviour
{
    [SerializeField] private ParentsMove parentsMove;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    private void OnTriggerEnter(Collider other){

        parentsMove.OnParentTriggerEnter(other);
        
    }
}

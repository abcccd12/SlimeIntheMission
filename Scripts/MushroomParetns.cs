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

        parentsMove.OnParentTriggerEnter(other); // 트리거는여기있는데 로직은부모에있음 왜이렇게했지
        
    }
}

using System;
using System.Collections;
using UnityEngine;
using Animancer;
using MoreMountains.Feedbacks;
using FIMSpace.FProceduralAnimation;
public class MoveBee : MonoBehaviour
{
    [SerializeField] private float movespeed = 100f;
    [SerializeField] private float arriveddistance = 0.05f;
    [SerializeField] private Transform left;
    [SerializeField] private Transform right;
    [SerializeField] private AnimancerComponent animancer;
    [SerializeField] private AnimationClip walk_anim;
    [SerializeField] private AnimationClip attack_anim;
    [SerializeField] private AnimationClip die_anim;
    [SerializeField] private Transform slime;

    [SerializeField]
    private SlimeLaunchController slimeController;

    [SerializeField] private MMF_Player mushroom_hitsound;
   
    
    private AnimancerState currentstate;
    
    private Transform currentTarget;
    private bool facingRight;
    private bool isAttacking;
    private bool isdead;
 

    private void Start()
    {
        currentTarget = left;
        if(left!=null)  left.SetParent(null);
        if(right!=null)  right.SetParent(null);
        LookAtTarget(); 
        currentstate = animancer.Play(walk_anim); 
       
        
    }

    private void Update()
    {
        if (isAttacking || isdead)
            return;
        
      
        Patrol();
    }

    private void Patrol()
    {
        bool arrived = MoveTo(currentTarget);
        if (arrived)
        {
            currentTarget = currentTarget == left ? right : left;
            LookAtTarget();
            
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
       
       
    }



    private void LookAtTarget()
    {
        
        facingRight = currentTarget.position.x > transform.position.x;

        float currentX = transform.eulerAngles.x;
        float currentZ = transform.eulerAngles.z;
        
        transform.rotation = facingRight
            ? Quaternion.Euler(currentX, 90f, currentZ)
            : Quaternion.Euler(currentX, -90f, currentZ);
    }
    
    private bool MoveTo(Transform target)
    {
        Vector3 targetPosition = new Vector3(target.position.x, target.position.y, target.position.z);
        transform.position = Vector3.MoveTowards(transform.position, targetPosition, movespeed * Time.deltaTime);
        float distance = Mathf.Abs(transform.position.x - targetPosition.x);

        return distance <= arriveddistance;
    }

    private void OnTriggerEnter(Collider other)
    {
       
        if(isAttacking) return;
        
        if(other.CompareTag("Slime"))
        {
            SlimeLaunchController slime = other.gameObject.GetComponent<SlimeLaunchController>();

             if (!isAttacking)
            {
                 StartCoroutine(Attack());
                
            }
            
        }
        
    }


   

    private IEnumerator Attack()
    {
        isAttacking = true;
        currentstate =  animancer.Play(attack_anim);
 
        yield return new WaitForSeconds(attack_anim.length - 0.2f);
        
        slimeController.Knockback(transform.position);
        
        currentstate =  animancer.Play(walk_anim);
     
        
        isAttacking = false;
    }
}
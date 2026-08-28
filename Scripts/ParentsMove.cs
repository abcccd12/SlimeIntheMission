using System;
using System.Collections;
using System.Diagnostics;
using System.Threading.Tasks;
using Unity.VisualScripting;
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.BehaviorDesigner.Runtime.Tasks.Actions.NavMeshTasks;
using UnityEngine;
using Animancer;
using MoreMountains.Feedbacks;
using FIMSpace.FProceduralAnimation;
using Opsive.BehaviorDesigner.Runtime.Tasks.Actions.Math;
using Debug = UnityEngine.Debug;

public class ParentsMove : MonoBehaviour
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
    private GameObject sslime;

    public enum Wallsurface
    {
        Floor, Celling, Wall, Wallright
    }

    [SerializeField] private Wallsurface type = Wallsurface.Floor;

   
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
       
        SlimeLaunchController slime = collision.gameObject.GetComponent<SlimeLaunchController>();
        if (collision.gameObject.CompareTag("Slime") && slime._currentLaunchIsStraightShot)
        {
            animancer.Play(die_anim);
            
            Debug.Log("sound");
            
        }
    }



    private void LookAtTarget()
    {
        
        facingRight = currentTarget.position.x > transform.position.x;
        float yRot = facingRight ? 90f : -90f;
        
        switch(type)
        {
            case Wallsurface.Floor:
                transform.rotation = Quaternion.Euler(0f, yRot, 0f);
                break;
            case Wallsurface.Celling:
                 transform.rotation = Quaternion.Euler(0f, yRot, 180f);
                 break;
            case Wallsurface.Wall:
                bool up = currentTarget.position.y > transform.position.y;
                if (up)
                {
                    transform.rotation = Quaternion.Euler(-90f, 0f, -90f);
                }
                else
                {
                    transform.rotation = Quaternion.Euler(90f, 180f, 90f);
                }

                break;
            case Wallsurface.Wallright:
                bool upp = currentTarget.position.y > transform.position.y;
                if (upp)
                {
                    transform.rotation = Quaternion.Euler(-90f, 0f, 90f);
                }
                else
                {
                    transform.rotation = Quaternion.Euler(90f, 180f, -90f);
                }
                break;
        }
        
    }
    

    public void OnParentTriggerEnter(Collider other)
    {
       
        if(isAttacking) return;
        
        if(other.CompareTag("Slime"))
        {
            SlimeLaunchController slime = other.gameObject.GetComponent<SlimeLaunchController>();

            if (slime._currentLaunchIsStraightShot)
            {
                Die();
                mushroom_hitsound.PlayFeedbacks();
         
                
            }
            else if (!isAttacking)
            {
                 StartCoroutine(Attack());
            }
            
        }
        
    }

    private void Die()
    {
        Collider myCollider = GetComponent<Collider>();
        if (myCollider != null) myCollider.enabled = false;
        isdead = true;
        // 2. 사망 애니메이션 즉시 재생 (0.25초 동안 부드럽게 전환)
        
        animancer.Play(die_anim, 0.25f);
        
        // 3. 2초 뒤에 '이 버섯 오브젝트 전체'를 씬에서 파괴
        Destroy(gameObject, 2f);
    }

    private bool MoveTo(Transform target)
    {
        Vector3 targetPosition = new Vector3(target.position.x, target.position.y, target.position.z);
        transform.position = Vector3.MoveTowards(transform.position, targetPosition, movespeed * Time.deltaTime);
        float distance = Mathf.Abs(transform.position.x - targetPosition.x);

        return distance <= arriveddistance;
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
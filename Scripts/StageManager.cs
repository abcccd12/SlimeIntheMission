using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using GeNa.Core;
using Sirenix.OdinInspector.Editor.GettingStarted;

public class StageManager : MonoBehaviour
{
    
    public static StageManager Instance { get; private set; }
    [SerializeField] private List<GameObject> carryover = new List<GameObject>();
    [SerializeField] private string spawnname = "SpawnPoint";


    
    private string nextstage;
    
    private void Awake()
    {
        if (!TutorialSequence.IsDone())
        {
            return;
        }
        
        Instance = this;
        DontDestroyOnLoad(gameObject);

    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += Onscene;
    }
    private void OnDisable()
    {
        SceneManager.sceneLoaded -= Onscene;
    }

    public void GotoStage(string Stage2)
    {
        foreach (GameObject go in carryover)
        {
            if(go == null) continue;
            go.transform.SetParent(null);
            //destroyonload는 최상위 계층만 사용가능하다. 
            DontDestroyOnLoad(go);
        }

        nextstage = Stage2;
        SceneManager.LoadScene(Stage2);
    }
    
    private void Onscene(Scene scene, LoadSceneMode mode)
    {

        GameObject spawn = GameObject.Find("SpawnPoint");//정적인 메소드. 이름으로찾는거 별로인거같은데 일단이렇게 
           
        
        if (spawn == null)
        {
          
            return;
        } 
        foreach (GameObject go in carryover)
        {
            SlimeLaunchController slime = go.GetComponentInChildren<SlimeLaunchController>();
            if (slime == null)
            {
                    Debug.Log("null"); // 예외처리 안했더니오류생김
                
            }
            if (slime != null)
            {
                slime.PlaceAtAndStop(spawn.transform.position);
                    Debug.Log("PlaceAtAndStop");
                
                continue;
            }
            go.transform.position = spawn.transform.position;

            Rigidbody rb = go.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero; // 구버전이면 rb.velocity
                rb.angularVelocity = Vector3.zero;
            }
        }
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    private void OnCollisionEnter(Collision collision)
    {
        
    }

    // Update is called once per frame
    void Update()
    {
    #if UNITY_EDITOR
        // F1을 누르면 현재 스테이지의 모든 적을 파괴하고 즉시 클리어
        
    
        // F2를 누르면 다음 씬으로 바로 점프
        if (Input.GetKeyDown(KeyCode.F2))
        {
            GotoStage("Stage2");
        }
    #endif
        
    }
}

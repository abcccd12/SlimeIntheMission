using UnityEngine;
using Febucci.TextAnimatorForUnity;
using System.Collections.Generic; 
using UnityEngine.SceneManagement;

public class TutorialSequence : MonoBehaviour
{
    [System.Serializable]
    public class TutorialStep
    {
        [Tooltip("이 스텝의 대사 페이지들(패널). 순서대로 탭으로 넘긴다.")]
        public GameObject[] pages;
    }

    [Header("참조")]
    [SerializeField] private SlimeLaunchController slime;
    [SerializeField] private Rigidbody slimeRb;
    [SerializeField] private Transform startPos;      // 하늘 시작점 (직접 연결)

    [Header("튜토리얼 스텝")]
    [SerializeField] private TutorialStep introStep;       // 착지 후 첫 대사
    [SerializeField] private TutorialStep[] triggerSteps;  // 트리거마다 하나씩
    [SerializeField] private GameObject mainGameUI;        // 튜토 끝난 뒤 본게임 UI
    [SerializeField] private TypewriterComponent typewrite;
    [SerializeField] private StageLoad stageLoad;
  
    private const string DoneKey = "TutorialDone";

    private bool _running;
    private bool _landed;
    private int _triggerIndex;

    // 현재 열린 대사
    private TutorialStep _currentStep;
    private int _pageIndex;
    private bool _dialogueOpen;
    private Vector3 _savedVelocity;
    
    private void Start()
    {
        
        if (IsDone()) { StartNormalGame(); return; }
        
        BeginTutorial();
        Debug.Log("begintuto ");
        
    }

    public static bool IsDone()
    {
        return PlayerPrefs.GetInt(DoneKey, 0) == 1;
    }
    
    private void MarkDone() => PlayerPrefs.SetInt(DoneKey, 1);
    private readonly Queue<TutorialStep> _pending = new Queue<TutorialStep>();

    // ── 1) 공중에서 물리 낙하 ──
    private void BeginTutorial()
    {
        _running = true;
        HideAllPages();
        if (mainGameUI != null) mainGameUI.SetActive(false);

        slime.transform.position = startPos.position;
        Debug.Log($"sllme transform{slime.transform}");
        slimeRb.isKinematic = false;

        slime.GetComponent<Collider>().enabled = false; // 낙하중에벽붙어서 꺼둠
        
        Debug.Log($"kinematic{slimeRb.isKinematic}");
        slimeRb.useGravity  = true;
        slimeRb.linearVelocity = Vector3.zero;

        
        
        slime.SetControlLocked(true);   // 낙하 중 클릭되서 막음
    }

    private void Update()
    {
        if (!_running) return;

        // ── 2) 착지(Stuck) 감지: 한 번만 ──
        if (!_landed)
        {
            if (slime.IsStuck)
            {
                _landed = true;
                OpenStep(introStep);    // 착지 후 첫 대사
            }
            return;
        }

        // 대사 열려있으면 탭으로 다음 페이지
        if (_dialogueOpen && Input.GetMouseButtonDown(0))
        {
            Debug.Log("typing");
            typewrite.StartDisappearingText();
             NextPage();
        }
        
        
    }

    // ── 3) 트리거가 호출 (올라가며 밟을 때) ──
    public void OnReachedTrigger()
    {
        if (_triggerIndex >= triggerSteps.Length) return;
        _pending.Enqueue(triggerSteps[_triggerIndex]);   // 순서대로 대기열에 쌓기
        _triggerIndex++;

        if (!_dialogueOpen)
            OpenNext();          // 대사 안 열려있으면 바로 첫 개 열기
    }
    private void OpenNext()
    {
        if (_pending.Count == 0) return;
        OpenStep(_pending.Dequeue());
    }

    // ── 대사 열기 / 넘기기 / 닫기 ──
    private void OpenStep(TutorialStep step)
    {
        if (step == null || step.pages == null || step.pages.Length == 0) return;

        _currentStep = step;
        _pageIndex = 0;
        _dialogueOpen = true;
       
        
        

        slime.SetControlLocked(true);   // 읽는 동안 조작 막기
        Time.timeScale = 0f; // 안멈추니까 대사중에슬라임움직임 짜증
        
        SetPage(0, true);
    }

    private void NextPage()
    {
        SetPage(_pageIndex, false);     // 현재 페이지 끄기
        _pageIndex++;

        if (_pageIndex < _currentStep.pages.Length)
            SetPage(_pageIndex, true);  // 다음 페이지
        else
            CloseStep();                // 마지막 → 닫기
    }

    private void CloseStep()
    {
        _dialogueOpen = false;
        _currentStep = null;

        if (_pending.Count > 0)
        {
            OpenNext();          // 다음 대사 (timeScale 계속 0 유지)
            return;
        }

        Time.timeScale = 1f;     // 대기 없음 → 슬라임 재개
        slime.SetControlLocked(false);

        if (_landed && _triggerIndex >= triggerSteps.Length)
            EndTutorial();
    }

    // ── 4) 종료 (다시 안 나옴) ──
    private void EndTutorial()
    {
        _running = false;
        MarkDone();
        
        
        StartNormalGame();
    }

    private void StartNormalGame()
    {
        if(!IsDone()) return;
        
        HideAllPages();
        if (mainGameUI != null) mainGameUI.SetActive(true);
        
        if (slime != null) slime.SetControlLocked(false);

        if (stageLoad == null)
            stageLoad = FindFirstObjectByType<StageLoad>();
        if (stageLoad != null)
            stageLoad.BeginMainChunks();
    }

    // ── 헬퍼 ──
    private void SetPage(int i, bool on)
    {
        if (_currentStep == null) return;
        if (i < 0 || i >= _currentStep.pages.Length) return;
        if (_currentStep.pages[i] != null) _currentStep.pages[i].SetActive(on);
    }

    private void HideAllPages()
    {
        HidePages(introStep);
        if (triggerSteps != null)
            foreach (var s in triggerSteps) HidePages(s);
    }
    private void HidePages(TutorialStep s)
    {
        if (s == null || s.pages == null) return;
        foreach (var p in s.pages) if (p != null) p.SetActive(false);
    }
}

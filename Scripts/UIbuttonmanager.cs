using System;
using UnityEngine;
using UnityEngine.SceneManagement;
public class UIbuttonmanager : MonoBehaviour

{

    public GameObject pausebutton;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public void Reset()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name); // scenemanger.getactive -> 현재 활성화 돼있는 씬들중? -> name가져와
            //로드하고. 
    }

    public void Quit()
    {
        Application.Quit();
    }
    public void BackToGamePress()
    {
        pausebutton.SetActive(false );
    }
    public void PausebuttonPress()
    {
        pausebutton.SetActive(true);
    }
    
}

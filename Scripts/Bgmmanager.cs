using System;
using UnityEngine;
using DG.Tweening;
public class Bgmmanager : MonoBehaviour
{
    public static Bgmmanager Instance;
    private AudioSource bgmSource;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            bgmSource = GetComponent<AudioSource>();
            bgmSource.loop = true;

        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void ChangeBGM(AudioClip newclip, float fadeduration = 1f)
    {
        if (bgmSource.clip == newclip) return;
        
        bgmSource.DOFade(0f, fadeduration / 2f).OnComplete(() =>
        {
            bgmSource.clip = newclip;
            bgmSource.Play();
            bgmSource.DOFade(1f, fadeduration / 2f);
        });
    }
}

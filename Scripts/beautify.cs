using UnityEngine;
using DG.Tweening;
using Beautify.Universal;

public class beautify : MonoBehaviour
{
    public void ChangeBright(float target, float duration)
    {
        DOTween.To(
        () => BeautifySettings.settings.brightness.value,
        value => BeautifySettings.settings.brightness.Override(value),
        target,
        duration
        ).SetEase(Ease.InOutSine); // linear하니까 티남
    }
    
    public void ChangeContrast(float target, float duration)
    {
        DOTween.To(
            () => BeautifySettings.settings.contrast.value,
            value => BeautifySettings.settings.contrast.Override(value),
            target,
            duration
        ).SetEase(Ease.InOutSine);
    }
    
}

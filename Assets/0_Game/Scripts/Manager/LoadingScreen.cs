using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class LoadingScreen : MonoBehaviour
{
    public static LoadingScreen ins;
    public Image imgProgress;

    private void Awake()
    {
        ins = this;
    }

    public void SetPercent(float to, float time)
    {
        imgProgress.DOFillAmount(to, time)
            .SetEase(Ease.Linear);
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }
}

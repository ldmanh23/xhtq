using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class CanvasGameplay : UICanvas
{

    public GameObject hand_Tut; 
    public TMP_Text timerTxt;

    public void BoosterHint()
    {
        IngameManager.ins.BoosterHint();
    }

    public void BoosterClear()
    {
        IngameManager.ins.BoosterClear();
    }

    public void BoosterSort()
    {
        IngameManager.ins.BoosterSort();
    }

    public void ShowHandTut(bool show)
    {
        if (hand_Tut == null)
        {
            return;
        }

        if(show)
            hand_Tut.SetActive(true);
        else
            hand_Tut.SetActive(false);
    }

    public void SetTimer(float seconds)
    {
        if (timerTxt == null)
        {
            return;
        }

        int totalSeconds = Mathf.CeilToInt(Mathf.Max(0, seconds));
        int minutes = totalSeconds / 60;
        int remainSeconds = totalSeconds % 60;
        timerTxt.text = minutes.ToString("00") + ":" + remainSeconds.ToString("00");
    }
}

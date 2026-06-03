using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class CanvasGameplay : UICanvas
{

    public GameObject hand_Tut; 
    public TMP_Text timerTxt;

    public TMP_Text txt_Hint;
    public TMP_Text txt_Sort;
    public TMP_Text txt_Clear;
    public GameObject obj_LockHint;
    public GameObject obj_LockSort;
    public GameObject obj_LockClear;

    public override void Open()
    {
        base.Open();
        UpdateVisualBtnBooster();
    }

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

    public void UpdateVisualBtnBooster()
    {
        obj_LockHint.SetActive(DataManager.ins.dt.level < Constant.levelUnlockBoosterHint);
        obj_LockSort.SetActive(DataManager.ins.dt.level < Constant.levelUnlockBoosterSort);
        obj_LockClear.SetActive(DataManager.ins.dt.level < Constant.levelUnlockBoosterClear);

        txt_Hint.text = DataManager.ins.dt.numberBoosterHint.ToString();
        txt_Sort.text = DataManager.ins.dt.numberBoosterSort.ToString();
        txt_Clear.text = DataManager.ins.dt.numberBoosterClear.ToString();
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

    public void UnLockRowLockBtn()
    {
        IngameManager.ins.UnlockTopLockedRows();
    }    
}

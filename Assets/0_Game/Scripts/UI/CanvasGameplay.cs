using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CanvasGameplay : UICanvas
{

    public GameObject hand_Tut; 

    public void BoosterHint()
    {
        IngameManager.ins.BoosterHint();
    }

    public void BoosterClear()
    {
        IngameManager.ins.BoosterClear();
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
}

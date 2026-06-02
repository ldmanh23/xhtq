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
      
    public void ShowHandTut(bool show)
    {
        if(show)
            hand_Tut.SetActive(true);
        else
            hand_Tut.SetActive(false);
    }
}

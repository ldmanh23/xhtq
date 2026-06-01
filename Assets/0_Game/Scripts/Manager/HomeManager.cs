using UnityEngine;

public class HomeManager : MonoBehaviour
{
    private void Start()
    {
        UIManager.ins.OpenUI(UIID.UICMainMenu);
    }
}

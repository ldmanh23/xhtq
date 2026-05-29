using UnityEngine;
using System.Collections.Generic;
using System;

public class Cache
{

    private static Dictionary<float, WaitForSeconds> m_WFS = new Dictionary<float, WaitForSeconds>();

    public static WaitForSeconds GetWFS(float key)
    {
        if(!m_WFS.ContainsKey(key))
        {
            m_WFS[key] = new WaitForSeconds(key);
        }

        return m_WFS[key];
    }

    //------------------------------------------------------------------------------------------------------------


    //private static Dictionary<Collider, Slot> m_Slot = new Dictionary<Collider, Slot>();

    //public static Slot GetSlot(Collider key)
    //{
    //    if (!m_Slot.ContainsKey(key))
    //    {
    //        m_Slot.Add(key, key.GetComponent<Slot>());
    //    }

    //    return m_Slot[key];
    //}

    //private static Dictionary<Collider, Character> m_Character = new Dictionary<Collider, Character>();

    //public static Character GetCharacter(Collider key)
    //{
    //    if (!m_Character.ContainsKey(key))
    //    {
    //        m_Character.Add(key, key.GetComponent<Character>());
    //    }

    //    return m_Character[key];
    //}


}

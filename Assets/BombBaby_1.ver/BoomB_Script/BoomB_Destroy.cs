using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BoomB_Destroy : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    // 在 Animation Event 中呼叫此函式即可摧毀該物件
    /// <summary>
    /// 摧毀當前物件的父物件
    /// </summary>
    public void DestroyParent()
    {
        if (transform.parent != null)
        Destroy(transform.parent.gameObject , 0.05f);
    }

}

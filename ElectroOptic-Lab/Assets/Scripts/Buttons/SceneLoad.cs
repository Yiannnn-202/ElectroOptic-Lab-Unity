using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoad: MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void Onclick_Btn_LoadScene_01()
    {
        SceneManager.LoadScene(1);
    }
    public void Onclick_Btn_LoadScene_02_preview()
    {
        SceneManager.LoadScene(2);
    }
    public void Onclick_Btn_LoadScene_02()
    {
        SceneManager.LoadScene(3);
    }
    public void Onclick_Btn_LoadScene_05()
    {
        SceneManager.LoadScene(4);
    }
    public void Onclick_Btn_Return()
    {
        SceneManager.LoadScene(0);
    }
}

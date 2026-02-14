using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Cardclick : MonoBehaviour
{
    public void GoToScene()
    {
        SceneManager.LoadScene("Scene2.The Lab");
    }
}

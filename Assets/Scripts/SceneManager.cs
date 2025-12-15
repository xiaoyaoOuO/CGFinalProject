using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SceneManager : MonoBehaviour
{
    //雪景和正常景观的切换
    public List<Material> materials;
    bool isSnowScene = false;
    public ParticleSystem snowParticleSystem;
    void OnEnable()
    {
        //注册事件，按下1为正常景观，按下2为雪景
        for (int i = 0; i < materials.Count; i++)
        {
            materials[i].SetFloat("_IsSnow", 0);
        }
        snowParticleSystem.Stop();
    }

    void Update()
    {
        if(Input.GetKeyDown(KeyCode.Alpha2))
        {
            isSnowScene = true;
            SetScene(isSnowScene);
        }
        if(Input.GetKeyDown(KeyCode.Alpha1))
        {
            isSnowScene = false;
            SetScene(isSnowScene);
        }
    }

    void SetScene(bool isSnow)
    {
        if (isSnow)
        {
            for (int i = 0; i < materials.Count; i++)
            {
                materials[i].SetFloat("_IsSnow", 1);
            }
            snowParticleSystem.Play();
        }
        else
        {
            for (int i = 0; i < materials.Count; i++)
            {
                materials[i].SetFloat("_IsSnow", 0);
            }
            snowParticleSystem.Stop();
            snowParticleSystem.Clear();
        }
    }   
}

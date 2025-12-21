using System.Collections.Generic;
using UnityEngine;

public class SceneManager : MonoBehaviour
{
    //雪景和正常景观的切换
    [Header("Materials to switch")]
    public List<Material> materials;
    public List<Material> GrassMaterials;
    public List<Material> BushMaterials;
    public List<Material> TreeMaterials;
    public List<Color> GrassOriginalColors;
    public List<Color> BushOriginalColors;
    public List<Color> TreeOriginalColors;
    [Header("Scene Objects")]
    public GameObject TerrainMeshes;
    public GameObject MyGrasses;
    public GameObject Bushes;
    public GameObject GrassesA;
    public GameObject GrassesB;
    public GameObject GrassesC;
    bool isSnowScene = false;
    public ParticleSystem snowParticleSystem;
    void OnEnable()
    {
        //注册事件，按下1为正常景观，按下2为雪景
        for (int i = 0; i < materials.Count; i++)
        {
            materials[i].SetFloat("_IsSnow", 0);
        }
        SetDefault(); 
        snowParticleSystem.Stop();
        TerrainMeshes.SetActive(false);
        MyGrasses.SetActive(true);
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
            SetMaterialsWhite();
            snowParticleSystem.Play();
            TerrainMeshes.SetActive(true);
            MyGrasses.SetActive(false);
            Bushes.SetActive(false);
            GrassesA.SetActive(false);
            GrassesB.SetActive(false);
            GrassesC.SetActive(false);
        }
        else
        {
            for (int i = 0; i < materials.Count; i++)
            {
                materials[i].SetFloat("_IsSnow", 0);
            }
            SetMaterialDefault();
            snowParticleSystem.Stop();
            snowParticleSystem.Clear();
            TerrainMeshes.SetActive(false);
            MyGrasses.SetActive(true);
            Bushes.SetActive(true);
            GrassesA.SetActive(true);
            GrassesB.SetActive(true);
            GrassesC.SetActive(true);
        }
    }

    void OnDisable()
    {
        SetMaterialDefault();
    }

    void SetDefault()
    {
        for(int i = 0; i < GrassMaterials.Count; i++)
        {
            GrassOriginalColors.Add(GrassMaterials[i].GetColor("_Color"));
        }
        for (int i = 0; i < BushMaterials.Count; i++)
        {
            BushOriginalColors.Add(BushMaterials[i].GetColor("_PrimaryColor"));
        }
        for (int i = 0; i < TreeMaterials.Count; i++)
        {
            TreeOriginalColors.Add(TreeMaterials[i].GetColor("_PrimaryColor"));
        }
    }

    void SetMaterialsWhite()
    {
        for (int i = 0; i < GrassMaterials.Count; i++)
        {
            GrassMaterials[i].SetColor("_Color", Color.white);
        }
        for (int i = 0; i < BushMaterials.Count; i++)
        {
            BushMaterials[i].SetColor("_PrimaryColor", Color.white);
        }
        for (int i = 0; i < TreeMaterials.Count; i++)
        {
            TreeMaterials[i].SetColor("_PrimaryColor", Color.white);
        }
    }

    void SetMaterialDefault()
    {
        for (int i = 0; i < GrassMaterials.Count; i++)
        {
            GrassMaterials[i].SetColor("_Color", GrassOriginalColors[i]);
        }
        for (int i = 0; i < BushMaterials.Count; i++)
        {
            BushMaterials[i].SetColor("_PrimaryColor", BushOriginalColors[i]);
        }
        for (int i = 0; i < TreeMaterials.Count; i++)
        {
            TreeMaterials[i].SetColor("_PrimaryColor", TreeOriginalColors[i]);
        }
    }
}

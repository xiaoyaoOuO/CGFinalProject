using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InteractWithCamera : MonoBehaviour
{
    [SerializeField]
    Renderer _renderer;
    // Update is called once per frame
    void Update()
    {
        _renderer.material.SetVector("_WorldSpaceCameraPos", transform.position);
    }
}

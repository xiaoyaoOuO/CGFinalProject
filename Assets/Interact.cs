using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Interact : MonoBehaviour
{
    [SerializeField]
    Renderer _renderer; 
    // Update is called once per frame
    void Update()
    {
        _renderer.material.SetVector("_PositionMoving", transform.position);
    }
}

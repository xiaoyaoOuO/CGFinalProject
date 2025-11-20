using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Interact : MonoBehaviour
{
    [SerializeField]
    Material _material;
    // Update is called once per frame
    void Update()
    {
        _material.SetVector("_PositionMoving", transform.position);
    }
}

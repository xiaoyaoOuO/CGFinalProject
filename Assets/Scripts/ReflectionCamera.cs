using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public enum ImageWidth
{
    _64 = 64,
    _128 = 128,
    _256 = 256,
    _512 = 512,
    _1024 = 1024,
}

[ExecuteInEditMode]
public class NewBehaviourScript : MonoBehaviour
{
    [SerializeField]
    private ImageWidth imageWidth = ImageWidth._1024;
    private Camera curCamera;
    private RenderTexture reflectImage;
    [SerializeField]
    private Material material;
    public Camera SourceCamera;
    public float m_settings = 0.03f;

    public static NewBehaviourScript AddTo(GameObject go, Camera sourceCamera)
    {
        if (go == null || sourceCamera == null)
            return null;
        var reflectionCamera = go.AddComponent<NewBehaviourScript>();
        reflectionCamera.enabled = false;
        reflectionCamera.SourceCamera = sourceCamera;
        reflectionCamera.enabled = true;
        return reflectionCamera;
    }
    
    public Matrix4x4 ReflectionMatrix(Transform transform)
    {
        var matrix = Matrix4x4.identity;
        if(transform == null)
            return matrix;
        Vector3 pos = transform.position;
        Vector3 normal = transform.up;

        float d = -Vector3.Dot(normal, pos);

        matrix.m00 = 1 - 2 * normal.x * normal.x;
        matrix.m01 = -2 * normal.x * normal.y;
        matrix.m02 = -2 * normal.x * normal.z;
        matrix.m03 = -2 * d * normal.x;

        matrix.m10 = -2 * normal.y * normal.x;
        matrix.m11 = 1 - 2 * normal.y * normal.y;
        matrix.m12 = -2 * normal.y * normal.z;
        matrix.m13 = -2 * d * normal.y;

        matrix.m20 = -2 * normal.z * normal.x;
        matrix.m21 = -2 * normal.z * normal.y;
        matrix.m22 = 1 - 2 * normal.z * normal.z;
        matrix.m23 = -2 * d * normal.z;

        matrix.m30 = 0;
        matrix.m31 = 0;
        matrix.m32 = 0;
        matrix.m33 = 1;
        return matrix;
    }

    private Vector4 CameraSpacePlane(Camera cam, Vector3 pos, Vector3 normal, float sideSign)
    {
        Vector3 offsetPos = pos + normal * 0.07f; // 小偏移，避免自裁切
        Matrix4x4 m = cam.worldToCameraMatrix;
        Vector3 cpos = m.MultiplyPoint(offsetPos);
        Vector3 cnormal = m.MultiplyVector(normal).normalized * sideSign;
        return new Vector4(cnormal.x, cnormal.y, cnormal.z, -Vector3.Dot(cpos, cnormal));
    }

    #region  Mono
    private void OnEnable()
    {
        if(curCamera == null)
        {
            var trans = transform.Find("ReflectionCamera");
            GameObject go;
            if (trans == null)
            {
                go = new GameObject("ReflectionCamera");
            }
            else
            {
                go = trans.gameObject;
            }
            go.transform.SetParent(transform);
            curCamera = go.GetComponent<Camera>();
            if (curCamera == null)
            {
                curCamera = go.AddComponent<Camera>();
            }
            curCamera.CopyFrom(SourceCamera);
            // 排除水层，避免反射相机捕捉到水面自身导致递归反射
            int waterLayer = LayerMask.NameToLayer("Water");
            if (waterLayer >= 0)
            {
                curCamera.cullingMask = SourceCamera.cullingMask & ~(1 << waterLayer);
            }
            else
            {
                // 如果没有名为"Water"的层，保持与 SourceCamera 相同的 mask 并给出警告
                curCamera.cullingMask = SourceCamera.cullingMask;
                Debug.LogWarning("Layer 'Water' not found. Reflection camera culling mask unchanged.", this);
            }
        }
        if(reflectImage != null)
        {
            RenderTexture.ReleaseTemporary(reflectImage);
        }
        int width = (int)imageWidth;
        reflectImage = RenderTexture.GetTemporary(width, (int)(width/curCamera.aspect),24);
        curCamera.targetTexture = reflectImage;
    }

    private void OnDisable()
    {
        if(curCamera != null)
        {
            curCamera.enabled = false;
        }
    }

    private void OnDestroy()
    {
        if (reflectImage != null)
        {
            RenderTexture.ReleaseTemporary(reflectImage);
        }
    }

    private void LateUpdate()
    {
        if (curCamera == null || reflectImage == null || SourceCamera == null)
            return;

        UpdateCameraParams(SourceCamera, curCamera);
        
        Matrix4x4 reflectionMat = ReflectionMatrix(transform);
        curCamera.worldToCameraMatrix = SourceCamera.worldToCameraMatrix * reflectionMat;
        curCamera.transform.position = reflectionMat.MultiplyPoint(SourceCamera.transform.position);

        Vector3 normal = transform.up;
        Vector3 pos = transform.position;
        Vector4 clipPlane = CameraSpacePlane(curCamera, pos, normal, 1.0f);
        curCamera.projectionMatrix = SourceCamera.CalculateObliqueMatrix(clipPlane);

        bool prevCulling = GL.invertCulling;
        try
        {
            GL.invertCulling = !prevCulling;
            curCamera.Render();
        }
        finally
        {
            GL.invertCulling = prevCulling;
        }
        
        // 在渲染完反射后立即更新材质纹理，确保当前帧生效
        if(material != null)
        {
            material.SetTexture("_ReflectionTexture", reflectImage);
        }
    }

    private void UpdateCameraParams(Camera srcCamera, Camera destCamera)
    {
        if (srcCamera == null || destCamera == null)
            return;
        destCamera.clearFlags = srcCamera.clearFlags;
        destCamera.backgroundColor = srcCamera.backgroundColor;
        destCamera.farClipPlane = srcCamera.farClipPlane;
        destCamera.nearClipPlane = srcCamera.nearClipPlane;
        destCamera.orthographic = srcCamera.orthographic;
        destCamera.fieldOfView = srcCamera.fieldOfView;
        destCamera.aspect = srcCamera.aspect;
        destCamera.orthographicSize = srcCamera.orthographicSize;
    }

    private Matrix4x4 CalculateObliqueMatrix(Vector4 clipPlane)
    {
        Matrix4x4 projection = curCamera.projectionMatrix;
        if (clipPlane == null || curCamera == null)
            return projection;
        Vector4 new_N = curCamera.worldToCameraMatrix.inverse.transpose * clipPlane;
        Vector4 q = Matrix4x4.identity * new Vector4(
            Mathf.Sign(new_N.x),
            Mathf.Sign(new_N.y),
            1.0f,
            1.0f
        );
        q = projection.inverse * q;

        var a = 2.0f / Vector4.Dot(new_N, q);
        var a_newN = new_N * a;
        var M4 = new Vector4(
            projection.m30,
            projection.m31,
            projection.m32,
            projection.m33
        );
        var newM3 = a_newN - M4;   

        projection.m20 = newM3.x;
        projection.m21 = newM3.y;
        projection.m22 = newM3.z;
        projection.m23 = newM3.w;
        return projection;
    }
    #endregion
}
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class Stabilization : Singleton<Stabilization>
{
    protected GameObject Plane;
    protected Material Material;

    public Texture2D MainTex { set { Material.SetTexture("_MainTex", value); } }
    public Matrix4x4 MainCamera { set { Material.SetMatrix("mainCamera", value.inverse); } }
    public Texture2D YTex { set { Material.SetTexture("_YTex", value); } }
    public Texture2D UTex { set { Material.SetTexture("_UTex", value); } }
    public Texture2D VTex { set { Material.SetTexture("_VTex", value); } }

    public Stabilization()
    {
        Material = new Material(Shader.Find("Custom/PTM"));
    }

    public void InitCamera(int width, int height, float fx, float fy, float cx, float cy)
    {
        Material.SetInt("width", width);
        Material.SetInt("height", height);
        Material.SetFloat("fx", fx);
        Material.SetFloat("fy", fy);
        Material.SetFloat("cx", cx);
        Material.SetFloat("cy", cy);
    }

    public void InitPlane(Matrix4x4 m)
    {
        Plane.transform.SetPositionAndRotation(m.MultiplyPoint(Vector3.zero), Quaternion.LookRotation(m.GetColumn(2), m.GetColumn(1)));
    }

    public void SetPlane(GameObject plane)
    {
        Plane = plane;
        Plane.GetComponent<MeshRenderer>().material = Material;
    }

    public void Stablize(Matrix4x4 m)
    {
        Material.SetMatrix("camera", m.inverse);
    }

    public void Stablize(Quaternion rotation, Vector3 position)
    {
        Matrix4x4 cam = Matrix4x4.identity;
        cam.SetTRS(position, rotation, Vector3.one);
        cam.m02 = -cam.m02;
        cam.m12 = -cam.m12;
        cam.m20 = -cam.m20;
        cam.m21 = -cam.m21;
        cam.m23 = -cam.m23;
        Material.SetMatrix("camera", cam.inverse);
    }
}

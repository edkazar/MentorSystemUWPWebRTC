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

    public Matrix4x4 PlanePose { get { return Plane.transform.localToWorldMatrix; } }
    public int Width { get; private set; }
    public int Height { get; private set; }
    public float Fx { get; private set; }
    public float Fy { get; private set; }
    public float Cx { get; private set; }
    public float Cy { get; private set; }

    private Matrix4x4 CurrentPose;
    public bool g_UpdatePose { get; set; }

    public Stabilization()
    {
        g_UpdatePose = false;
        Material = new Material(Shader.Find("Custom/PTM"));
    }

    public void InitCamera(int width, int height, float fx, float fy, float cx, float cy)
    {
        Material.SetInt("width", Width = width);
        Material.SetInt("height", Height = height);
        Material.SetFloat("fx", Fx = fx);
        Material.SetFloat("fy", Fy = fy);
        Material.SetFloat("cx", Cx = cx);
        Material.SetFloat("cy", Cy = cy);
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
        //CurrentPose = m;
    }

    // Where rotation is a Quaternion(rotX, rotY, rotZ, rotW)
    // and position is Vector3(posX, posY, posZ)
    public void Stablize(Quaternion rotation, Vector3 position)
    {
        Matrix4x4 cam = Matrix4x4.identity;
        cam.SetTRS(position, rotation, Vector3.one);
        cam.m02 = -cam.m02;
        cam.m12 = -cam.m12;
        cam.m20 = -cam.m20;
        cam.m21 = -cam.m21;
        cam.m23 = -cam.m23;
        //MatrixDistance(cam);
        Material.SetMatrix("camera", cam.inverse);
    }

    public float MatrixDistance(Matrix4x4 p_Pose)
    {
        float distanceR = 0.0f;
        float distanceT = 0.0f;

        distanceR += Mathf.Pow(CurrentPose.m00 - p_Pose.m00, 2);
        distanceR += Mathf.Pow(CurrentPose.m01 - p_Pose.m01, 2);
        distanceR += Mathf.Pow(CurrentPose.m02 - p_Pose.m02, 2);
        distanceR += Mathf.Pow(CurrentPose.m10 - p_Pose.m10, 2);
        distanceR += Mathf.Pow(CurrentPose.m11 - p_Pose.m11, 2);
        distanceR += Mathf.Pow(CurrentPose.m12 - p_Pose.m12, 2);
        distanceR += Mathf.Pow(CurrentPose.m20 - p_Pose.m20, 2);
        distanceR += Mathf.Pow(CurrentPose.m21 - p_Pose.m21, 2);
        distanceR += Mathf.Pow(CurrentPose.m22 - p_Pose.m22, 2);
        distanceR = Mathf.Sqrt(distanceR);

        distanceT += Mathf.Pow(CurrentPose.m03 - p_Pose.m03, 2);
        distanceT += Mathf.Pow(CurrentPose.m13 - p_Pose.m13, 2);
        distanceT += Mathf.Pow(CurrentPose.m23 - p_Pose.m23, 2);
        distanceT = Mathf.Sqrt(distanceT);

        //Refine this thresholds for better results on the background updating
        //1.73 - 1.75....0.173 - 0.182
        if ((1.73f < distanceR && distanceR < 1.75f) && (0.177f < distanceT && distanceT < 0.180f))
            g_UpdatePose = true;

        return 0;
    }
}

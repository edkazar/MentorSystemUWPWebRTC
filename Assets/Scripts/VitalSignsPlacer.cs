using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VitalSignsPlacer : MonoBehaviour
{
    public UnityEngine.Camera Camera;

    public string HRValue { get; set; }
    public string SpO2Value { get; set; }
    protected VitalSign HR;
    protected VitalSign SpO2;
    protected float Distantce = 5.0f;
    protected float cornerOffsetX = -2.5f;
    protected float cornerOffsetY = 1.5f;

    protected readonly float XOffset = -0.64f;
    protected readonly float YOffset = -0.125f;

    // Start is called before the first frame update
    void Start()
    {
        HRValue = "--";
        SpO2Value = "--";

        GameObject VitalSignPrefab = Resources.Load("VitalSign/VitalSign") as GameObject;
        HR = UnityEngine.Object.Instantiate(VitalSignPrefab, transform).GetComponentInChildren<VitalSign>();
        HR.Init(new Vector3(XOffset, YOffset + 0.00f, 0f), Color.green, "HR", "160", "75");

        SpO2 = UnityEngine.Object.Instantiate(VitalSignPrefab, transform).GetComponentInChildren<VitalSign>();
        SpO2.Init(new Vector3(XOffset, YOffset + 0.25f, 0f), Color.cyan, "SpO2", "100", "90");
        //Camera.transform.position
        //(Camera.transform.up * cornerOffsetY) + (Camera.transform.left * cornerOffsetX)
        //new Vector3(-2.5f, 1.5f, 0.0f)
        transform.SetPositionAndRotation((Camera.transform.position + Camera.transform.forward * Distantce) + (Camera.transform.up * cornerOffsetY) + (Camera.transform.right * cornerOffsetX),
        Quaternion.LookRotation(Camera.transform.forward, Camera.transform.up));
        gameObject.SetActive(true);
    }

    // Update is called once per frame
    void Update()
    {
        transform.SetPositionAndRotation((Camera.transform.position + Camera.transform.forward * Distantce) + (Camera.transform.up * cornerOffsetY) + (Camera.transform.right * cornerOffsetX),
        Quaternion.LookRotation(Camera.transform.forward, Camera.transform.up));
        HR.Value = HRValue;
        SpO2.Value = SpO2Value;

    }
}

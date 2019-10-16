using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VitalSign : MonoBehaviour
{
    protected const float size = 3.26f;

    protected float[] samples;
    protected float[] refGraph;
    protected int sampleRate;
    protected int nTotalSample;
    protected int nNullSample;
    protected float fRandomRange;
    protected float lastT = 0f;
    protected int lastPos = 0;

    protected Color Color;
    protected GameObject Graph;
    protected TextMesh Text;

    public string Value
    {
        set
        {
            int v;
            _Value = int.TryParse(value, out v) ? v : 0;
            Text.text = value;
        }
        
    }
    protected int _Value { get; set; }

    public void Init(Vector3 pos, Color color, string name, string high, string low)
    {
        Color = color;
        transform.localPosition = pos;
        Graph = transform.Find("Graph").gameObject;

        TextMesh text;
        text = transform.Find("Name").GetComponent<TextMesh>();
        text.text = name;
        text.color = color;

        text = transform.Find("High").GetComponent<TextMesh>();
        text.text = high;
        text.color = color;

        text = transform.Find("Low").GetComponent<TextMesh>();
        text.text = low;
        text.color = color;

        Text = transform.Find("Now").GetComponent<TextMesh>();
        Text.text = "--";
        Text.color = color;

        string file = Resources.Load<TextAsset>("VitalSign/" + name).text;
        string[] lines = file.Split('\n');
        fRandomRange = float.Parse(lines[0]);

        sampleRate = lines.Length - 1;
        nTotalSample = (int)(size * sampleRate);
        nNullSample = sampleRate / 4;
        samples = new float[nTotalSample];
        refGraph = new float[sampleRate];

        for (int i = 0; i < sampleRate; ++i)
        {
            refGraph[i] = float.Parse(lines[i + 1]);
        }
}

    private void Update()
    {
        //LCY.Utilities.DestroyChildren(Graph.transform);
        foreach (Transform child in Graph.transform)
            GameObject.Destroy(child.gameObject);

        float t = Time.time;
        int pos = GetPos(t);

        // update from lastPos
        if (lastPos < pos)
        {
            for (int i = lastPos + 1; i <= pos; ++i)
            {
                float t_ = Mathf.Lerp(lastT, t, (float)(i - lastPos) / (pos - lastPos));
                samples[i] = _Value == 0 ? 0f : (refGraph[GetRef(t_)] + Random());
            }
        }
        else
        {
            for (int i = lastPos + 1; i < nTotalSample; ++i)
            {
                float t_ = Mathf.Lerp(lastT, t, (float)(i - lastPos) / (pos + nTotalSample - lastPos));
                samples[i] = _Value == 0 ? 0f : (refGraph[GetRef(t_)] + Random());
            }
            for (int i = 0; i <= pos; ++i)
            {
                float t_ = Mathf.Lerp(lastT, t, (float)(i + nTotalSample - lastPos) / (pos + nTotalSample - lastPos));
                samples[i] = _Value == 0 ? 0f : (refGraph[GetRef(t_)] + Random());
            }
        }
        lastT = t;
        lastPos = pos;

        // line1: start ~ pos
        int start1 = Math.Max(0, pos + nNullSample - nTotalSample);
        Vector3[] line1 = new Vector3[pos - start1];
        for (int i = start1; i < pos; ++i)
        {
            line1[i - start1] = transform.localToWorldMatrix * new Vector3((float)i / nTotalSample, samples[i], 0f);
        }
        NewLine(Graph, "Line1", 0.003f, line1);

        // line2: pos + null ~ total
        if (pos + nNullSample < nTotalSample)
        {
            int start2 = (pos + nNullSample) % nTotalSample;
            Vector3[] line2 = new Vector3[nTotalSample - start2];
            for (int i = pos + nNullSample; i < nTotalSample; ++i)
            {
                line2[i - start2] = transform.localToWorldMatrix * new Vector3((float)i / nTotalSample, samples[i], 0f);
            }
            NewLine(Graph, "Line2", 0.003f, line2);
        }
    }

    protected int GetPos(float t)
    {
        return (int)(t * sampleRate) % (int)(size * sampleRate);
    }

    protected int GetRef(float t)
    {
        return (int)(t * sampleRate) % sampleRate;
    }

    protected float Random()
    {
        return UnityEngine.Random.Range(-fRandomRange, fRandomRange);
    }

    protected LineRenderer NewLine(GameObject parent, string name, float width, Vector3[] positions)
    {
        GameObject obj = new GameObject(name);
        obj.transform.parent = parent.transform;
        obj.transform.localScale = Vector3.one;
        obj.transform.localPosition = Vector3.zero;
        LineRenderer line = obj.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.positionCount = positions.Length;
        line.SetPositions(positions);
        line.widthMultiplier = width;
        line.startColor = line.endColor = Color;
        line.material = new Material(Shader.Find("Sprites/Default"));
        line.receiveShadows = false;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return line;
    }
}

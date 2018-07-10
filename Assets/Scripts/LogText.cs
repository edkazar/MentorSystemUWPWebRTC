using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Text;

public class LogText : MonoBehaviour {

    public int MaxNumLines = 16;
    private List<string> Messages = new List<string>();
    private Text _text;

	// Use this for initialization
	void Start () {
        
    }

    private void OnEnable()
    {
        _text = GetComponent<Text>();

        Messages.Clear();
        UpdateText();

        Application.logMessageReceived += Application_logMessageReceived;
    }

    private void OnDisable()
    {
        Application.logMessageReceived -= Application_logMessageReceived;
    }






    private void Application_logMessageReceived(string condition, string stackTrace, LogType type)
    {
        AddLog(condition);

        if (type == LogType.Error || type == LogType.Exception)
        {
            AddLog(stackTrace);
        }
    }

    // Update is called once per frame
    void Update () {
		
	}

    void AddLog(string msg)
    {
        Messages.Add(msg);
        while (Messages.Count > MaxNumLines)
        {
            Messages.RemoveAt(0);
        }
        UpdateText();
    }

    private void UpdateText()
    {
        StringBuilder sb = new StringBuilder();
        foreach (var msg in Messages)
        {
            sb.Append(msg);
            sb.AppendLine();
        }
        _text.text = sb.ToString();
    }
}

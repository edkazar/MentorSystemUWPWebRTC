using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MockLogGenerator : MonoBehaviour {

	// Use this for initialization
	void Start () {
        StartCoroutine(MockLog());
	}
	
	// Update is called once per frame
	void Update () {
		
	}

    IEnumerator MockLog()
    {
        while (true)
        {
            Debug.Log("test " + Time.time);

            yield return new WaitForSeconds(1.0f);
        }
    }
}

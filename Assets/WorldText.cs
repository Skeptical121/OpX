using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WorldText : MonoBehaviour
{
	float lifetime = 0;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    public void Update()
    {
		Vector3 delta = transform.position - Camera.main.transform.position;
		delta.y = 0;
		transform.forward = Vector3.Normalize(delta);
		transform.position += Vector3.up * Time.deltaTime;

		lifetime += Time.deltaTime;
		if (lifetime >= 5f)
		{
			Destroy(gameObject);
		}
    }
}

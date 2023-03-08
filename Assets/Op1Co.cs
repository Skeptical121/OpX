using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

public class Op1Co
{
	public int money;
	public float approvalRating = 50f;
	public float control;


	public float fundingRate = 10000f; // Funding rate per second

	public Entity personControl = Entity.Null;
	public float upDownRotation = 0f;

	public Vector3 overheadCameraPos;
	public Quaternion overheadCameraRot;

	private static GameObject worldText;
	private static Transform worldCanvas;

	public Op1Co()
	{
		worldText = (GameObject)Resources.Load("WorldText");
		worldCanvas = GameObject.Find("WorldCanvas").transform;
	}

	public void AddScore(float score)
	{
		float scoreAdjusted = score * 0.05f;
		approvalRating += (100f - approvalRating) * scoreAdjusted / (1 + scoreAdjusted);
	}

	public void DisplayScoreAdded(SawEvent sawEvent)
	{
		GameObject text = Object.Instantiate(worldText, worldCanvas);
		text.transform.position = sawEvent.repEvent.pos;
		text.GetComponent<Text>().text = "+" + (int)sawEvent.value;
		text.GetComponent<WorldText>().Update();
	}

	public void SecondTick()
	{
		fundingRate += (0.01f * approvalRating) / (1f - 0.01f * approvalRating) * 1000;
		approvalRating *= 0.99f;
		money += (int)fundingRate;
	}
	
	/*public void SetPlayerControl(Entity player)
	{
		Assert.IsTrue(personControl == Entity.Null);
		personControl = player;
		personControl.Modify((ref Person p) => { p.playerControlled = true; });
		overheadCameraPos = Camera.main.transform.position;
		overheadCameraRot = Camera.main.transform.rotation;
	}

	public void RemovePlayerControl()
	{
		personControl = Entity.Null;
		if (personControl != Entity.Null && personControl.Has<Person>())
			personControl.Modify((ref Person p) => { p.playerControlled = false; });
		Camera.main.transform.position = overheadCameraPos;
		Camera.main.transform.rotation = overheadCameraRot;
	}*/

	public void Update(Transform transform)
	{
		/*if (personControl != Entity.Null)
		{
			if (!personControl.Has<Person>() || Input.GetKeyDown(KeyCode.Escape))
			{
				RemovePlayerControl();
				return;
			}
			Person p = personControl.Get<Person>();
			upDownRotation = Mathf.Clamp(-88f, upDownRotation - Input.GetAxis("Mouse Y") * 3.5f, 88f);
			p.rot = Quaternion.Euler(upDownRotation, ((Quaternion)p.rot).eulerAngles.y + Input.GetAxis("Mouse X") * 3.5f, 0);
			p.pos += math.mul(p.rot, new float3(Input.GetAxisRaw("Horizontal"), 0, Input.GetAxisRaw("Vertical"))) * Person.SPEED * Time.deltaTime;

			Camera.main.transform.position = p.pos + new float3(0, Person.HEIGHT * 0.97f, 0);
			Camera.main.transform.rotation = p.rot;

			personControl.SetData(p);
		}
		else
		{*/
		if (Input.GetAxis("Mouse ScrollWheel") > 0)
		{
			transform.localPosition *= 0.83f;
		}
		else if (Input.GetAxis("Mouse ScrollWheel") < 0)
		{
			transform.localPosition /= 0.83f;
		}
		transform.forward = transform.parent.position - transform.position;
		if (Input.GetMouseButton(1))
		{
			transform.parent.eulerAngles += new Vector3(0, Input.GetAxis("Mouse X") * 5f, 0);
		}
		transform.parent.position += transform.parent.rotation *
			new Vector3(Input.GetAxisRaw("Horizontal") * 0.05f * transform.position.y, 0, Input.GetAxisRaw("Vertical") * 0.05f * transform.position.y);
		//}
	}
}

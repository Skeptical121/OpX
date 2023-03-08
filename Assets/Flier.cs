using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public struct Flier : IComponentData
{
	public float3 pos;
}

public struct SaverFlier : IComponentData
{

}

[UpdateInGroup(typeof(MainSimSystemGroup))]
public class FlierSystem : JobComponentSystemWithCallback
{
	protected override void OnCreate()
	{
		base.OnCreate();
	}

	protected override JobHandle OnUpdate(JobHandle inputDeps)
	{

		return base.OnUpdate(inputDeps);
	}

	public override void MainThreadSimulationCallbackTick()
	{

	}
}
using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

[UpdateInGroup(typeof(MainSimSystemGroup))]
public class SlowSystem : JobComponentSystem
{
	private EndMainSimEntityCommandBufferSystem barrier;
	protected override void OnCreate()
	{
		barrier = World.GetOrCreateSystem<EndMainSimEntityCommandBufferSystem>();
		base.OnCreate();
	}
	struct ResourcesForSaversCapacityTick : IJobForEach<ResourceStorage, BasicPosition>
	{
		public void Execute(ref ResourceStorage resourceStorage, ref BasicPosition basicPosition)
		{
			basicPosition.capacity = (int)math.ceil(resourceStorage.numResources / 5f);
		}
	}
	struct HotFloorCapacityTick : IJobForEach<HotFloor, BasicPosition>
	{
		public void Execute(ref HotFloor hotFloor, ref BasicPosition basicPosition)
		{
			basicPosition.capacity = (int)math.ceil(hotFloor.howBad / 2f);
		}
	}

	protected override JobHandle OnUpdate(JobHandle inputDeps)
	{
		inputDeps = new ResourcesForSaversCapacityTick
		{
		}.Schedule(this, inputDeps);
		inputDeps = new HotFloorCapacityTick
		{
		}.Schedule(this, inputDeps);
		barrier.AddJobHandleForProducer(inputDeps); // Same barrier as JobComponentSystemWithCallback
		return inputDeps;
	}

	public void MainThreadSimulationCallbackTick() // Same idea as JobComponentSystemWithCallback
	{
	}
}

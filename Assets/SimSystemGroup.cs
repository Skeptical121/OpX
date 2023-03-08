using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Experimental.PlayerLoop;


public abstract class JobComponentSystemWithCallback : JobComponentSystem
{
	private EntityCommandBufferSystem barrier;
	protected override void OnCreate()
	{
		if (IsRenderUpdate())
			barrier = World.GetOrCreateSystem<EndRenderSimEntityCommandBufferSystem>();
		else
			barrier = World.GetOrCreateSystem<EndMainSimEntityCommandBufferSystem>();
		base.OnCreate();
	}
	public virtual void FirstUpdate() { }
	public abstract void MainThreadSimulationCallbackTick();
	public virtual bool IsRenderUpdate()
	{
		return false;
	}

	protected override JobHandle OnUpdate(JobHandle inputDeps)
	{
		barrier.AddJobHandleForProducer(inputDeps);
		return inputDeps;
	}
}

public class SimSystemGroup : ComponentSystemGroup
{

}

[UpdateInGroup(typeof(SimSystemGroup))]
public class MainSimSystemGroup : ComponentSystemGroup
{

}

[UpdateInGroup(typeof(PresentationSystemGroup))]
public class RenderSystemGroup : ComponentSystemGroup
{

}

[UpdateInGroup(typeof(PresentationSystemGroup))]
[UpdateAfter(typeof(RenderSystemGroup))]
public class EndRenderSimEntityCommandBufferSystem : EntityCommandBufferSystem
{

}

[UpdateInGroup(typeof(SimSystemGroup))]
[UpdateAfter(typeof(MainSimSystemGroup))]
public class EndMainSimEntityCommandBufferSystem : EntityCommandBufferSystem
{

}
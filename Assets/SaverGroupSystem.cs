using System.Collections;
using System.Collections.Generic;
using Unity.Jobs;
using UnityEngine;

public class SaverGroupSystem : JobComponentSystemWithCallback
{
	protected override JobHandle OnUpdate(JobHandle inputDeps)
	{


		return base.OnUpdate(inputDeps);
	}

	public override void MainThreadSimulationCallbackTick()
	{
	}
}

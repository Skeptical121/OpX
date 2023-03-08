using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public struct ReportableEvent
{
	public static int CURR_ID = 0;
	public enum Type
	{
		Death
	}

	public int id;
	public float3 pos;
	public Type type;
}

public struct ReporterGroup : IComponentData
{

}

public struct ReporterGroupMember : IBufferElementData
{
	public Entity entity;
}

public struct CameraOperator : IComponentData, IPerson
{
	public enum State
	{
		Recording,
		FindingSaver, // Just follow saver around?
		GoingToSaver
	}

	public State state;
	public float3 lookAt;
}

public struct SawEvent
{
	public Entity whoSaw;
	public ReportableEvent repEvent;
	public float value; // Reportable value
}

[UpdateInGroup(typeof(MainSimSystemGroup))]
public class ReporterSystem : JobComponentSystemWithCallback
{
	// So... events happen like a person dieing
	NativeList<ReportableEvent> eventsLastTick;
	NativeList<ReportableEvent> eventsThisTick;
	NativeQueue<SawEvent> eventsSeen;

	public void ReportDeath(Entity person, float3 pos)
	{
		eventsThisTick.Add(new ReportableEvent
		{
			id = ReportableEvent.CURR_ID++,
			pos = pos,
			type = ReportableEvent.Type.Death
		});
	}

	[BurstCompile]
	struct EventPropegation : IJobForEachWithEntity<SimplePerson, CameraOperator>
	{
		const float MAX_SEE_DIST = 20f;

		[ReadOnly] public NativeArray<ReportableEvent> eventsLastTick;
		public NativeQueue<SawEvent>.ParallelWriter eventsSeen;

		public void Execute(Entity entity, int index, ref SimplePerson person, ref CameraOperator co)
		{
			for (int i = 0; i < eventsLastTick.Length; i++)
			{
				float distSqr = math.distancesq(eventsLastTick[i].pos, person.pos);
				if (distSqr < MAX_SEE_DIST * MAX_SEE_DIST)
				{
					float dist = math.sqrt(distSqr);
					float3 eventDir = (eventsLastTick[i].pos - person.pos) / dist;
					// float3 lookDir = math.mul(person.rot, new float3(0, 0, 1));
					float dot = math.dot(eventDir, person.forward);

					if (dot > 0.707f) // From -45 degrees to 45 degrees
					{
						// For now, assume valid...
						// But, in the future, we can do some more pruning, then do some kind of raycast to check...

						SawEvent sawEvent = new SawEvent
						{
							whoSaw = entity,
							repEvent = eventsLastTick[i],
							value = 100f * (MAX_SEE_DIST - math.distance(eventsLastTick[i].pos, person.pos)) / MAX_SEE_DIST
						};
						eventsSeen.Enqueue(sawEvent);
					}
				}
			}
		}
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		eventsThisTick = new NativeList<ReportableEvent>(Allocator.Persistent);
		eventsLastTick = new NativeList<ReportableEvent>(Allocator.Persistent);
		eventsSeen = new NativeQueue<SawEvent>(Allocator.Persistent);
	}

	protected override void OnDestroy()
	{
		eventsThisTick.Dispose();
		eventsLastTick.Dispose();
		eventsSeen.Dispose();
		base.OnDestroy();
	}

	protected override JobHandle OnUpdate(JobHandle inputDeps)
	{
		// NativeQueue<SawEvent>.ParallelWriter parallel = ;
		eventsLastTick.Clear();
		eventsLastTick.AddRange(eventsThisTick);
		eventsThisTick.Clear();
		inputDeps = new EventPropegation
		{
			eventsLastTick = eventsLastTick, // implicitly converted to array
			eventsSeen = eventsSeen.AsParallelWriter()
		}.Schedule(this, inputDeps);
		return base.OnUpdate(inputDeps);
	}

	public override void MainThreadSimulationCallbackTick()
	{
		// if (sawEvent)
		NativeHashMap<int, SawEvent> eventMostValued = new NativeHashMap<int, SawEvent>(1, Allocator.Temp);

		while (eventsSeen.TryDequeue(out SawEvent sawEvent))
		{
			if (eventMostValued.TryGetValue(sawEvent.repEvent.id, out SawEvent best))
			{
				if (sawEvent.value > best.value)
					eventMostValued[sawEvent.repEvent.id] = sawEvent;
			}
			else
			{
				eventMostValued[sawEvent.repEvent.id] = sawEvent;
			}
		}
		NativeArray<SawEvent> events = eventMostValued.GetValueArray(Allocator.Temp);
		for (int i = 0; i < events.Length; i++)
		{
			// Add to score?
			Game.op1Co.AddScore(events[i].value);

			// Display some UI thing...
			Game.op1Co.DisplayScoreAdded(events[i]);
		}

		eventsSeen.Clear();
	}
}

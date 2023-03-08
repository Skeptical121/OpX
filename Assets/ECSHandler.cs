using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

public class ECSHandler : MonoBehaviour
{
	public static float interp = 0; // From 0-1, the global interp value for rendering
	private List<JobComponentSystemWithCallback> systemsWithCallback = new List<JobComponentSystemWithCallback>();
	public static StringBuilder stringBuilder;

	// Start is called before the first frame update
	void Awake()
	{
		stringBuilder = new StringBuilder(256);
		ECSExtensions.EntityManager = World.Active.EntityManager;
		World.Active.GetExistingSystem<SimSystemGroup>().Enabled = false;
		// World.Active.GetExistingSystem<SlowSystem>().Enabled = false;
		// World.Active.GetExistingSystem<FluidSystem>().Enabled = false;
		// NativeQueue.ParallelWriter is non-deterministic. NativeStream and EntityCommandBuffer are deterministic though and will probably meet your needs.

		// Ideally we do this by reflection:
		foreach (Type type in typeof(JobComponentSystemWithCallback).Assembly.GetTypes().Where(type => type.IsSubclassOf(typeof(JobComponentSystemWithCallback))))
		{
			if (!type.IsAbstract)
			{
				systemsWithCallback.Add((JobComponentSystemWithCallback)World.Active.GetExistingSystem(type));
			}
		}
	}

	public void FirstUpdate()
	{
		for (int i = 0; i < systemsWithCallback.Count; i++)
		{
			systemsWithCallback[i].FirstUpdate();
		}
	}

	float lastUpdateTime = 0;
	// I believe this runs before ECS stuff is run
	void Update()
	{
		interp = Mathf.Clamp01((Time.time - lastUpdateTime) / Game.GetTickTime());
		for (int i = 0; i < systemsWithCallback.Count; i++)
		{
			if (systemsWithCallback[i].IsRenderUpdate())
				systemsWithCallback[i].MainThreadSimulationCallbackTick();
		}
	}

	public static int tickCount = 0;
	public static float gameTime = 0f;
	private static long timeSpentOnAll;

	void FixedUpdate()
	{
		long startFixedUpdate = Game.NanoTime();

		DebugDraw.ResetIndex();
		tickCount++;

		float tickTime = Game.GetTickTime();
		gameTime += tickTime;

		DebugDraw.tickTime.Add((Game.NanoTime() - startFixedUpdate) / 1000000000f);
		if (DebugDraw.tickTime.Count > 100)
		{
			DebugDraw.tickTime.RemoveAt(0);
		}


		SimSystemGroup simSystemGroup = World.Active.GetExistingSystem<SimSystemGroup>();

		if (tickCount % 50 == 0)
		{
			World.Active.GetExistingSystem<SlowSystem>().Enabled = true;
		}
		simSystemGroup.Enabled = true;
		simSystemGroup.Update();
		simSystemGroup.Enabled = false;
		World.Active.GetExistingSystem<SlowSystem>().Enabled = false;
		for (int i = 0; i < systemsWithCallback.Count; i++)
		{
			if (!systemsWithCallback[i].IsRenderUpdate())
				systemsWithCallback[i].MainThreadSimulationCallbackTick();
		}
		lastUpdateTime = Time.time;

		DebugDraw.DisplayMessage(stringBuilder.Append("Game Time = ").Append(Game.GetGameTime()).Append("s. Speed = ").Append(Time.timeScale));
		DebugDraw.DisplayMessage(stringBuilder.Append("Tick Time = ").Append(timeSpentOnAll / 1000000000f).Append("s"));
		DebugDraw.DisplayMessage(stringBuilder.Append("Num Deaths = ").Append(Game.numDeaths));
		DebugDraw.DisplayMessage(stringBuilder.Append("Num Person Pathfinds = ").Append(Game.numPersonPathFinds));
		DebugDraw.DisplayMessage(stringBuilder.Append("$").AppendFormat("{0:n0}", Game.op1Co.money));
		DebugDraw.DisplayMessage(stringBuilder.Append("Funding Rate = ").AppendFormat("{0:n0}", Game.op1Co.fundingRate));
		DebugDraw.DisplayMessage(stringBuilder.Append("Approval Rating = ").Append(Game.op1Co.approvalRating));
		DebugDraw.DisplayMessage(stringBuilder.Append("Control = ").Append(Game.op1Co.control));


		//DebugDraw.DisplayMessage("Game Time = " + Game.GetGameTime() + "s. Speed = " + Time.timeScale);
		//DebugDraw.DisplayMessage("Tick Time = " + timeSpentOnAll / 1000000000f + "s");
		//DebugDraw.DisplayMessage("Num Deaths = " + Game.numDeaths);
		//DebugDraw.DisplayMessage("Num Person Pathfinds = " + Game.numPersonPathFinds);
		//DebugDraw.DisplayMessage("$" + string.Format("{0:n0}", Game.op1Co.money));
		//DebugDraw.DisplayMessage("Funding Rate = $" + string.Format("{0:n0}", Game.op1Co.fundingRate));
		//DebugDraw.DisplayMessage("Approval Rating = " + Game.op1Co.approvalRating);
		//DebugDraw.DisplayMessage("Control = " + Game.op1Co.control);
		if (ObjectPlacer.MouseRayCast(out Unity.Physics.RaycastHit hI))
		{
			PFTile t = Map.GetTile(hI.Position);
			DebugDraw.DisplayMessage(stringBuilder.Append(Game.map.fluidMap.GetVelocity(hI.Position)));
			DebugDraw.DisplayMessage(stringBuilder.Append(t));
			if (t.IsValid())
			{
				// DebugDraw.DisplayMessage("Ground: " + World.Active.GetExistingSystem<FluidSystem>().ground[(int)(t.x * Map.SIZE_Z + t.z)]);
				DebugDraw.DisplayMessage(stringBuilder.Append("Height: ").Append(Game.map.fluidMap.GetHeight(hI.Position)));
				// float2x2 val = Game.map.fluidMap.GetDepthStuff(hI.Position);
				// DebugDraw.DisplayMessage("Depth Stuff: " + val.c0.x + ", " + val.c0.y + ", " + val.c1.x + ", " + val.c1.y);
				// float2x2 val2 = Game.map.fluidMap.GetHeightStuff(hI.Position);
				// DebugDraw.DisplayMessage("Height Stuff: " + val2.c0.x + ", " + val2.c0.y + ", " + val2.c1.x + ", " + val2.c1.y);
				// DebugDraw.DisplayMessage("Flow X: " + World.Active.GetExistingSystem<FluidSystem>().flowX[(int)(t.x * Map.SIZE_Z + t.z)]);
				// DebugDraw.DisplayMessage("Flow Z: " + World.Active.GetExistingSystem<FluidSystem>().flowZ[(int)(t.x * Map.SIZE_Z + t.z)]);
			}
		}
		timeSpentOnAll += Game.NanoTime() - startFixedUpdate;
	}
}

public static class ECSExtensions
{
	// Assumes one world:
	public static EntityManager EntityManager;

	public delegate void Update<T>(ref T c) where T : struct, IComponentData;

	//public static void ModifyComponentData<T>(this EntityManager EntityManager, Entity entity, Update<T> setVar) where T : struct, IComponentData
	//{
	//	T componentData = EntityManager.GetComponentData<T>(entity);
	//	setVar(ref componentData);
	//	EntityManager.SetComponentData(entity, componentData);
	//}

	/*public static void Remove<T, U>(this NativeMultiHashMap<T, U> map, T index, U value) where T : struct, IEquatable<T> where U : struct, IEquatable<U>
	{
		if (map.TryGetFirstValue(index, out U ent, out NativeMultiHashMapIterator<T> it))
		{
			do
			{
				if (value.Equals(ent))
				{
					map.Remove(it);
					break;
				}
			} while (map.TryGetNextValue(out ent, ref it));
		}
	}*/

	public static bool Remove<T>(this DynamicBuffer<T> buffer, T val) where T : struct, IBufferElementData, IEquatable<T>
	{
		for (int i = 0; i < buffer.Length; i++)
		{
			if (buffer[i].Equals(val))
			{
				buffer.RemoveAt(i);
				return true;
			}
		}
		return false;
	}

	public static bool Remove<T>(this NativeList<T> list, T val) where T : struct, IEquatable<T>
	{
		for (int i = 0; i < list.Length; i++)
		{
			if (list[i].Equals(val))
			{
				list.RemoveAtSwapBack(i);
				return true;
			}
		}
		return false;
	}

	public static void Destroy(this Entity entity)
	{
		EntityManager.DestroyEntity(entity);
	}

	public static void Modify<T>(this Entity entity, Update<T> setVar) where T : struct, IComponentData
	{
		T componentData = entity.Get<T>();
		setVar(ref componentData);
		entity.SetData(componentData);
	}

	public static DynamicBuffer<T> Buffer<T>(this Entity entity) where T : struct, IBufferElementData
	{
		return EntityManager.GetBuffer<T>(entity);
	}

	public static bool Has<T>(this Entity entity)
	{
		return EntityManager.HasComponent<T>(entity);
	}

	public static T Get<T>(this Entity entity) where T : struct, IComponentData
	{
		return EntityManager.GetComponentData<T>(entity);
	}

	public static void AddData<T>(this Entity entity, T data) where T : struct, IComponentData
	{
		EntityManager.AddComponentData(entity, data);
	}

	public static void SetData<T>(this Entity entity, T data) where T : struct, IComponentData
	{
		EntityManager.SetComponentData(entity, data);
	}

	public static void SetSharedData<T>(this Entity entity, T data) where T : struct, ISharedComponentData
	{
		EntityManager.SetSharedComponentData(entity, data);
	}

	// Not terribly efficient..
	public static U Get<T, U>(this NativeMultiHashMap<T, U> map, T key, int index) where T : struct, IEquatable<T> where U : struct
	{
		NativeMultiHashMap<T, U>.Enumerator enumerator = map.GetValuesForKey(key);

		int count = 0;
		foreach (U value in enumerator)
		{
			if (index == count++)
				return value;
		}
		Assert.Fail("Did not find index: " + index + " at key " + key);
		return default;
	}

	/*public static Entity AddSubRenderer(this Entity entity, float3 pos, quaternion rot, Mesh mesh, params[] Material realMat)
	{
		Entity renderer = EntityManager.CreateEntity(ConstructionSystem.subMeshRenderer);
		entity.Buffer<SubMeshRenderer>().Add(new SubMeshRenderer { renderer = renderer });
		renderer.SetData(new Translation { Value = pos });
		renderer.SetData(new Rotation { Value = rot });
		EntityManager.SetSharedComponentData(renderer, new RenderMesh { mesh = mesh, material = facadeOrConstructing ? RenderInfo.Facade : realMat });
		return renderer;
	}*/

	public static Entity AddSubRenderer(this Entity entity, float3 pos, quaternion rot, Mesh mesh, bool facadeOrConstructing, params Material[] mats)
	{
		Entity firstRenderer = Entity.Null;
		for (int n = 0; n < mesh.subMeshCount; n++)
		{
			Entity renderer = EntityManager.CreateEntity(ConstructionSystem.subMeshRenderer);
			entity.Buffer<SubMeshRenderer>().Add(new SubMeshRenderer { renderer = renderer });
			renderer.SetData(new Translation { Value = pos });
			renderer.SetData(new Rotation { Value = rot });
			renderer.SetSharedData(new RenderMesh { mesh = mesh, material = facadeOrConstructing ? RenderInfo.Facade : mats[n], subMesh = n });
			if (n == 0)
				firstRenderer = renderer;
		}
		return firstRenderer;
	}


	/*public static JobHandle ScheduleSingle<T>(this T job, EntityQuery query, JobHandle dependsOn) where T : struct, IJobChunk
	{
		var chunks = query.CreateArchetypeChunkArray(Allocator.TempJob, out JobHandle chunksHandle);
		dependsOn = JobHandle.CombineDependencies(dependsOn, chunksHandle);
		return new JobChunkSingleRunner<T>
		{
			RealJob = job,
			Chunks = chunks,
		}.Schedule(dependsOn);
	}

	[BurstCompile]
	struct JobChunkSingleRunner<T> : IJob where T : IJobChunk
	{
		public T RealJob;
		[DeallocateOnJobCompletion]
		public NativeArray<ArchetypeChunk> Chunks;

		public void Execute()
		{
			int firstEntityIndex = 0;
			for (int i = 0; i < Chunks.Length; ++i)
			{
				RealJob.Execute(Chunks[i], i, firstEntityIndex);
				firstEntityIndex += Chunks[i].Count;
			}
		}
	}*/
}
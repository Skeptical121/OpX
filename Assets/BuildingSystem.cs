using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

[UpdateInGroup(typeof(MainSimSystemGroup))]
public class BuildingSystem : JobComponentSystemWithCallback
{
	protected override void OnCreate()
	{
		World.Active.GetOrCreateSystem<Unity.Physics.Systems.BuildPhysicsWorld>();
		base.OnCreate();
	}
	
	/*struct InserterTick : IJobForEachWithEntity_ECC<Building, Inserter>
	{
		private const float ROT_SPEED = 0.5f;

		public Map map;
		public ComponentDataFromEntity<ResourceStorage> resourceStorage;
		[ReadOnly] public ComponentDataFromEntity<TrainStation> trainStation;
		public float tickTime;

		public void Execute(Entity inserterEntity, int index, [ReadOnly] ref Building building, ref Inserter inserter)
		{
			if ((inserter.rotation == 0 && !inserter.rotatingTo) || (inserter.rotation == math.PI && inserter.rotatingTo))
			{
				Entity entity = map.GetEntity(building.bottomLeftBack.GetToTile(inserter.rotatingTo ? inserter.dir : inserter.dir.Flip()));
				if (trainStation.HasComponent(entity))
					entity = trainStation[entity].trainAtStop;
				if (resourceStorage.HasComponent(entity))
				{
					ResourceStorage storage = resourceStorage[entity];
					ResourceStorage inserterStorage = resourceStorage[inserterEntity];
					if (!inserter.rotatingTo)
					{
						if (storage.CanTake(storage.type, 1f))
						{
							storage.TransferTo(ref inserterStorage, 1f);
							resourceStorage[inserterEntity] = inserterStorage;
							resourceStorage[entity] = storage;
							inserter.rotatingTo = true;
						}
					}
					else
					{
						if (storage.CanAdd(inserterStorage.type, inserterStorage.numResources))
						{
							inserterStorage.TransferTo(ref storage, inserterStorage.numResources);
							resourceStorage[inserterEntity] = inserterStorage;
							resourceStorage[entity] = storage;
							inserter.rotatingTo = false;
						}
					}
				}
			}
			else
			{
				if (inserter.rotatingTo)
					inserter.rotation = math.min(math.PI, inserter.rotation + ROT_SPEED * tickTime);
				else
					inserter.rotation = math.max(0, inserter.rotation - ROT_SPEED * tickTime);
			}
		}
	}*/

	[BurstCompile]
	struct DisplayResourceStorage : IJobForEach<ResourceStorage>
	{
		[NativeDisableParallelForRestriction] public ComponentDataFromEntity<NonUniformScale> nonUniformScale;

		public void Execute(ref ResourceStorage storage)
		{
			if (/*storage.display != Entity.Null && */nonUniformScale.HasComponent(storage.display))
			{
				nonUniformScale[storage.display] = new NonUniformScale { Value = new float3(1, storage.numResources / storage.maxResources, 1) };
			}
		}
	}

	protected override JobHandle OnUpdate(JobHandle inputDeps)
	{
		//inputDeps = new InserterTick
		//{
		//	map = Game.map,
		//	resourceStorage = GetComponentDataFromEntity<ResourceStorage>(),
		//	trainStation = GetComponentDataFromEntity<TrainStation>(true),
		//	tickTime = Game.GetTickTime()
		//}.ScheduleSingle(this, inputDeps);
		inputDeps = new DisplayResourceStorage
		{
			nonUniformScale = GetComponentDataFromEntity<NonUniformScale>()
		}.Schedule(this, inputDeps);
		return base.OnUpdate(inputDeps);
	}

	public override void MainThreadSimulationCallbackTick()
	{
		//if (Input.GetKeyDown(KeyCode.U))
		//{
			// The jobs are forced to be completed by this point
			// for (int i = 0; i < buildings.Length; i++)
			// {
				// World.Active.GetExistingSystem<TrainSystem>().AttemptSpawnTrain(buildings[i].Buffer<BuildingRail>()[0].entity);
			// }
		//}
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		// updatedOwners.Dispose();
		// buildings.Dispose();
	}


}

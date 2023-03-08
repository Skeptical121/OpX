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

public struct TrainPos
{
	public Entity rail;
	public float pos;
}

public struct Train : IComponentData
{
	public TrainPos claimed;
	public float minDistanceTravelledLastTick;
	// public TrainPos front; <-- doesn't need to exist, it would seem. (it's more like it shouldn't exist, because any cases where you try to use this seem to result in potential problems)
	public TrainPos back;

	public float length;
	public float speed;

	// Constants:
	public float maxSpeed;
	public float acceleration;
	public float deceleration;

	public int routeIndex;
	public float timeAtStation; // -1 if not at station
	public bool pathfinding;
}

[InternalBufferCapacity(0)]
public struct TrainPath : IBufferElementData
{
	public Entity rail;
}

public struct TrainStation : IComponentData
{
	public Entity trainAtStop;
	public Entity actualTrainStation; // The train station "contains" the rail
}

// A train can only stop at a train station if it has it as part of its train route:
[InternalBufferCapacity(0)]
public struct TrainRoute : IBufferElementData
{
	// This is the route itself:
	public Entity trainStation;
	public bool loading;
	public float howLong;
}

public struct RailSection : IComponentData
{
	public const float DEFAULT_LENGTH = 0.1f;
	public Entity railZone; // Can be multiple rail sections per rail zone, of course
}

public struct RailZone : IComponentData
{
	public byte numTaken; // ref count, essentially... but could just be 0 / 1
	public byte numRails; // ref count
}

/*
[InternalBufferCapacity(0)]
public struct RailZoneRail : IBufferElementData
{
	public Entity rail;
}*/

[UpdateInGroup(typeof(MainSimSystemGroup))]
public class TrainSystem : JobComponentSystemWithCallback
{
	private NativeList<Entity> trainsToPathFind;

	protected override void OnCreate()
	{
		base.OnCreate();
		trainsToPathFind = new NativeList<Entity>(Allocator.Persistent);
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		trainsToPathFind.Dispose();
	}

	struct FindTrainPathsToSet : IJobForEachWithEntity_EBBC<TrainPath, TrainRoute, Train>
	{
		public NativeList<Entity> trainsToPathFind;
		public void Execute(Entity entity, int index, DynamicBuffer<TrainPath> path, DynamicBuffer<TrainRoute> route, ref Train train)
		{
			if (route.Length > 0 && !train.pathfinding && train.timeAtStation == -1f && train.claimed.rail == path[path.Length - 1].rail && train.claimed.rail != route[train.routeIndex].trainStation)
			{
				// Pathfinding...
				trainsToPathFind.Add(entity);
				train.pathfinding = true;
			}
		}
	}

	public bool AttemptSpawnTrain(Entity backRail, Entity goalTrainStation)
	{
		RailZone zone = backRail.Get<RailSection>().railZone.Get<RailZone>();
		if (zone.numTaken >= 1)
			return false;

		float length = 2f;
		// tickTime is not needed?
		TrainPos back = new TrainPos { rail = backRail, pos = 0f };

		NativeList<TrainPath> path = new NativeList<TrainPath>(Allocator.Temp);
		// Hmm:
		Entity nextRail = backRail;
		for (int i = 0; i < 1; i++)
		{
			path.Add(new TrainPath { rail = nextRail });
			DynamicBuffer<NextSegment> nextArray = Game.map.GetNodeConnections(GetBufferFromEntity<NextSegment>(), nextRail.Get<Segment>().to);
			if (nextArray.Length == 0)
				break;
			nextRail = nextArray[0].segment;
		}

		MoveTrains moveTrains = new MoveTrains { railSection = GetComponentDataFromEntity<RailSection>(true), segment = GetComponentDataFromEntity<Segment>(true), railZone = GetComponentDataFromEntity<RailZone>(), trainStation = GetComponentDataFromEntity<TrainStation>() };
		if (moveTrains.CanAddDistance(path, back, length))
		{
			zone.numTaken++; // Set numTaken to 1...
			backRail.Get<RailSection>().railZone.SetData(zone);

			TrainPos claimed = back; // Set to front
			moveTrains.AddDistance(path, ref claimed, length, true, false);

			Entity train = EntityManager.CreateEntity(typeof(Train), typeof(TrainRoute), typeof(TrainPath), typeof(ResourceStorage), typeof(SubMeshRenderer));
			train.Buffer<TrainPath>().AddRange(path);

			PosRot posRot = back.rail.Buffer<PosRotRoute>().AsNativeArray().GetMapped(back.pos);
			Entity display = train.AddSubRenderer(posRot.pos, posRot.rot, RenderInfo.self.trainResourceDisplayObject, false, RenderInfo.ConveyorBelt);
			EntityManager.AddComponentData(display, new NonUniformScale { Value = new float3(1, 0, 1) });
			train.SetData(new ResourceStorage
			{
				maxResources = 60,
				numResources = 50,
				display = display,
				type = ResourceType.Food
			});

			train.SetData(new Train
			{
				claimed = claimed,
				back = back,
				maxSpeed = 10f,
				acceleration = 0.5f,
				deceleration = 0.5f,
				length = length,
				speed = 0f,
				minDistanceTravelledLastTick = 0f,
				routeIndex = 0,
				timeAtStation = -1f,
				pathfinding = false
			});

			// Test:
			train.Buffer<TrainRoute>().Add(new TrainRoute { trainStation = goalTrainStation, howLong = 5f, loading = false });
			train.Buffer<TrainRoute>().Add(new TrainRoute { trainStation = backRail, howLong = 5f, loading = false });

			train.AddSubRenderer(posRot.pos, posRot.rot, RenderInfo.self.trainObject, false, RenderInfo.Train);
			return true;
		}
		return false;
	}

	[BurstCompile]
	struct UpdateTrainRender : IJobForEach_BC<SubMeshRenderer, Train>
	{
		[ReadOnly] public BufferFromEntity<PosRotRoute> railRoute;
		[NativeDisableParallelForRestriction] public ComponentDataFromEntity<Translation> translation;
		[NativeDisableParallelForRestriction] public ComponentDataFromEntity<Rotation> rotation;

		public void Execute(DynamicBuffer<SubMeshRenderer> subMeshRenderers, ref Train train)
		{
			// No interp yet...
			// float interpVal = math.lerp(interp.fromPos, interp.toPos, t);
			// interpVal += interp.forwardOffset;

			PosRot posRot = railRoute[train.back.rail].AsNativeArray().GetMapped(train.back.pos);
			for (int i = 0; i < subMeshRenderers.Length; i++)
			{
				translation[subMeshRenderers[i].renderer] = new Translation { Value = posRot.pos };
				rotation[subMeshRenderers[i].renderer] = new Rotation { Value = posRot.rot };
			}
		}
	}

	[BurstCompile]
	struct MoveTrains : IJobForEachWithEntity_EBBC<TrainPath, TrainRoute, Train>
	{
		public float tickTime;
		// public ArchetypeChunkComponentType<Train> train;

		public ComponentDataFromEntity<TrainStation> trainStation;
		[ReadOnly] public ComponentDataFromEntity<RailSection> railSection;
		[ReadOnly] public ComponentDataFromEntity<Segment> segment;
		public ComponentDataFromEntity<RailZone> railZone;

		private float MinDistanceTravelled(float speed, float deceleration)
		{
			int numTicksToStopped = (int)(math.ceil(speed / (deceleration * tickTime)) - 1);
			return (numTicksToStopped * speed - numTicksToStopped * (numTicksToStopped + 1) * deceleration * tickTime / 2) * tickTime;
		}

		public void Execute(Entity entity, int index, DynamicBuffer<TrainPath> trainPath, [ReadOnly] DynamicBuffer<TrainRoute> trainRoute, ref Train train)
		{
			if (train.timeAtStation != -1f)
			{
				// New path set, leave right away?
				train.timeAtStation += tickTime;
				if (train.timeAtStation > trainRoute[train.routeIndex].howLong)
				{
					train.timeAtStation = -1f;
					TrainStation stop = trainStation[trainRoute[train.routeIndex].trainStation];
					stop.trainAtStop = Entity.Null;
					trainStation[trainRoute[train.routeIndex].trainStation] = stop;
					train.routeIndex = (train.routeIndex + 1) % trainRoute.Length;
				}
			}

			// So the test is, what if we increased speed?
			float newSpeed = train.speed + train.acceleration * tickTime;
			if (newSpeed > train.maxSpeed)
				newSpeed = train.maxSpeed;

			float minDistanceTravelledWithIncreaseSpeed = newSpeed * tickTime + MinDistanceTravelled(newSpeed, train.deceleration);
			// FIRST, check if it is valid
			float extra = minDistanceTravelledWithIncreaseSpeed - train.minDistanceTravelledLastTick;
			if (CanAddDistance(trainPath.AsNativeArray(), train.claimed, extra))
			{
				train.speed = newSpeed;
				train.minDistanceTravelledLastTick = minDistanceTravelledWithIncreaseSpeed - newSpeed * tickTime;
				AddDistance(trainPath.AsNativeArray(), ref train.claimed, extra, true, false);
			}
			else
			{
				train.speed -= train.deceleration * tickTime;
				if (train.speed < 0)
					train.speed = 0;
				train.minDistanceTravelledLastTick -= train.speed * tickTime;
			}
			int numPathToRemove = AddDistance(trainPath.AsNativeArray(), ref train.back, train.speed * tickTime, false, true);
			if (numPathToRemove > 0)
				trainPath.RemoveRange(0, numPathToRemove);

			if (train.timeAtStation == -1f)
			{
				if (trainPath.Length == 1 && train.speed == 0 && train.back.rail == trainRoute[train.routeIndex].trainStation)
				{
					train.timeAtStation = 0f;
					TrainStation stop = trainStation[trainRoute[train.routeIndex].trainStation];
					stop.trainAtStop = entity;
					trainStation[trainRoute[train.routeIndex].trainStation] = stop;
				}
			}
		}

		public bool CanAddDistance(NativeArray<TrainPath> trainPath, TrainPos trainPos, float extra)
		{
			for (int i = 0; i < trainPath.Length; i++)
			{
				if (trainPos.rail == trainPath[i].rail)
				{
					trainPos.pos += extra;
					while (trainPos.pos >= segment[trainPos.rail].distance)
					{
						trainPos.pos -= segment[trainPos.rail].distance;
						if (i + 1 >= trainPath.Length || railZone[railSection[trainPath[i + 1].rail].railZone].numTaken >= 1)
						{
							return false;
						}
						trainPos.rail = trainPath[++i].rail; // Should we allow for trains to leave the tracks? (or at least not crash the game for it?)
					}
					break;
				}
			}
			return true;
		}

		// Assumes validity
		public int AddDistance(NativeArray<TrainPath> trainPath, ref TrainPos trainPos, float extra, bool takeAhead, bool removeBehind)
		{
			int numPathToRemove = 0;
			for (int i = 0; i < trainPath.Length; i++)
			{
				if (trainPos.rail == trainPath[i].rail)
				{
					trainPos.pos += extra;
					while (trainPos.pos >= segment[trainPos.rail].distance/* && i + 1 < trainRoute.Length*/)
					{
						trainPos.pos -= segment[trainPos.rail].distance;
						if (removeBehind)
						{
							RailZone rz = railZone[railSection[trainPos.rail].railZone];
							rz.numTaken--;
							railZone[railSection[trainPos.rail].railZone] = rz;
							numPathToRemove = i + 1;
						}
						trainPos.rail = trainPath[++i].rail;
						if (takeAhead)
						{
							RailZone rz = railZone[railSection[trainPos.rail].railZone];
							rz.numTaken++;
							railZone[railSection[trainPos.rail].railZone] = rz;
						}
					}
					break;
				}
			}
			return numPathToRemove;
		}
	}

	struct TrainStationTake : IJobForEachWithEntity<TrainStation>
	{
		private const float TRANSFER_RATE = 2f;

		[NativeDisableParallelForRestriction] public ComponentDataFromEntity<ResourceStorage> resourceStorage;
		public float deltaTime;

		public void Execute(Entity entity, int index, ref TrainStation trainStation)
		{
			if (trainStation.trainAtStop != Entity.Null)
			{
				ResourceStorage stationStorage = resourceStorage[entity];
				ResourceStorage trainStorage = resourceStorage[trainStation.trainAtStop];
				/*if (trainStation.trainAtStop.)
				{
					if (stationStorage.CanTake(stationStorage.type, TRANSFER_RATE * deltaTime) && trainStorage.CanAdd(stationStorage.type, TRANSFER_RATE * deltaTime))
					{
						stationStorage.TransferTo(ref trainStorage, TRANSFER_RATE * deltaTime);
						resourceStorage[entity] = stationStorage;
						resourceStorage[trainStation.trainAtStop] = trainStorage;
					}
				}
				else
				{
					if (trainStorage.CanTake(trainStorage.type, TRANSFER_RATE * deltaTime) && stationStorage.CanAdd(trainStorage.type, TRANSFER_RATE * deltaTime))
					{
						trainStorage.TransferTo(ref stationStorage, TRANSFER_RATE * deltaTime);
						resourceStorage[entity] = stationStorage;
						resourceStorage[trainStation.trainAtStop] = trainStorage;
					}
				}*/
			}
		}
	}

	protected override JobHandle OnUpdate(JobHandle inputDeps)
	{

		JobHandle jobHandle = new MoveTrains
		{
			tickTime = Game.GetTickTime(),
			// train = GetArchetypeChunkComponentType<Train>(),
			railSection = GetComponentDataFromEntity<RailSection>(true),
			segment = GetComponentDataFromEntity<Segment>(true),
			railZone = GetComponentDataFromEntity<RailZone>(),
			trainStation = GetComponentDataFromEntity<TrainStation>()
		}.ScheduleSingle(this, inputDeps);

		jobHandle = new UpdateTrainRender
		{
			railRoute = GetBufferFromEntity<PosRotRoute>(true),
			translation = GetComponentDataFromEntity<Translation>(),
			rotation = GetComponentDataFromEntity<Rotation>()
		}.Schedule(this, jobHandle);

		jobHandle = new TrainStationTake
		{
			deltaTime = Game.GetTickTime(),
			resourceStorage = GetComponentDataFromEntity<ResourceStorage>()
		}.Schedule(this, jobHandle);

		jobHandle = new FindTrainPathsToSet
		{
			trainsToPathFind = trainsToPathFind
		}.ScheduleSingle(this, jobHandle);

		return base.OnUpdate(jobHandle);
	}

	public override void MainThreadSimulationCallbackTick()
	{
		for (int i = 0; i < trainsToPathFind.Length; i++)
		{
			PathFinder.TrainRoute(trainsToPathFind[i], trainsToPathFind[i].Get<Train>().claimed.rail, trainsToPathFind[i].Buffer<TrainRoute>()[trainsToPathFind[i].Get<Train>().routeIndex].trainStation);
		}
		trainsToPathFind.Clear();
	}
}

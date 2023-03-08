using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

public interface IPerson
{
}

public struct Saver : IComponentData, IPerson
{
	public enum State
	{
		Init,
		FindNearestResources,
		GettingResources,
		FindingHotFloor,
		GoingToHotFloor,
		CoolingHotFloor
	}

	public State state;
	public float timeSinceStateStart;
	public Entity saverGroup;
}

public struct SaverGroup : IComponentData
{
	// Statistics:
	public float saves;
	public float deaths;

	public float recentSaves;
	public float recentDeaths;

	// If you see a saver group... doing nothing / being overwhelmed, you'll know to redistribute saver groups.
}

public struct SaverGroupMember : IBufferElementData
{
	public Entity entity;
}

public struct Savee : IComponentData, IPerson
{
	public enum State
	{
		Idle
	}

	public State state;
}

// Extension to person
public struct ComplexPerson : IComponentData
{

	public float3 pos;

	public float energy;
	public float health;

	public quaternion rot;
	public Entity claimed; // Can only claim one thing at a time..
	public float searchDistance; // Increase search distance over time
	public float timeSincePathFind; // Only pathfind once a second...
	public bool pathfinding;
	public float yVelocity;
	// public float velocity; // People are pushed by water if the water is deep enough (once it does damage)
}

public struct SimplePerson : IComponentData
{
	public const float SPEED = 2f; // 2f;
	public const float HEIGHT = 1.75f;
	public const float RADIUS = 0.5f;

	public float yVelocity;
	public float3 pos;
	public float3 forward;
	public float health;
	public float hunger;
}

public struct SimplePersonCollideUpdate : IComponentData
{
	public float2 delta; // Only horizontal collision
	public float total;
}

[InternalBufferCapacity(1)]
public struct PersonPathPos : IBufferElementData
{
	public float3 goal;
}

[UpdateInGroup(typeof(MainSimSystemGroup))]
public class PersonSystem : JobComponentSystemWithCallback
{
	private NativeHashMap<uint, PFTile> deletedTiles;
	private NativeQueue<Entity> deaths;
	private NativeList<PersonPathFind> queuedPathFinds;

	protected override void OnCreate()
	{
		deletedTiles = new NativeHashMap<uint, PFTile>(100, Allocator.Persistent);
		deaths = new NativeQueue<Entity>(Allocator.Persistent);
		queuedPathFinds = new NativeList<PersonPathFind>(100, Allocator.Persistent);
		base.OnCreate();
	}

	protected override void OnDestroy()
	{
		deletedTiles.Dispose();
		deaths.Dispose();
		queuedPathFinds.Dispose();
		base.OnDestroy();
	}

	public void SpawnPeople<T>(PFTile tile, int howMany, T type) where T : struct, IComponentData, IPerson
	{
		// Everyone needs a resource storage? hmm
		EntityArchetype arch = EntityManager.CreateArchetype(
			typeof(SimplePerson), typeof(SimplePersonCollideUpdate), typeof(Translation), typeof(Rotation), typeof(RenderMesh), typeof(LocalToWorld), typeof(T)/*, typeof(ResourceStorage)*/);

		NativeArray<Entity> people = new NativeArray<Entity>(howMany, Allocator.Temp);
		EntityManager.CreateEntity(arch, people);
		for (int i = 0; i < howMany; i++)
		{
			float rand = Variance.Range(0f, Mathf.PI * 2);
			float dist = Mathf.Sqrt(Variance.Range(0f, 1f)) * PFTile.LENGTH * 0.5f;

			// When you reach 0 health you die, if you reach 100 hunger your health starts to reduce... then you die
			people[i].SetData(new SimplePerson { pos = Map.WorldPosition(tile) + new float3(Mathf.Cos(rand) * dist, 0, Mathf.Sin(rand) * dist), health = 100f, hunger = Variance.Range(0f, 75f) });
			// person.SetData(new PersonPathPos { goal = })
			people[i].SetData(new Translation { Value = people[i].Get<SimplePerson>().pos });
			// Game.personMap.Claim(people[i], -1, people[i].Get<SimplePerson>().pos);

			// people[i].SetData(new ResourceStorage { maxResources = 5 });

			people[i].SetSharedData(new RenderMesh { mesh = RenderInfo.self.personObject, material = RenderInfo.Person[typeof(T)] });
			people[i].SetData(type);


			//if (Game.op1Co.personControl == Entity.Null)
			//{
			//	Game.op1Co.SetPlayerControl(people[i]);
			//}
		}
	}

	/*[BurstCompile]
	struct MovePeople : IJobForEach_BCC<PersonPathPos, Person, Translation>
	{
		public const float HOW_CLOSE_TO_GOAL_SQUARED = 0.4f * 0.4f;
		public float tickTime;

		public void Execute(DynamicBuffer<PersonPathPos> path, ref Person person, ref Translation translation)
		{
			if (path.Length > 0)
			{
				person.pos += math.normalize(path[0].goal - person.pos) * Person.SPEED * tickTime;
				translation.Value = person.pos;
				if (math.distancesq(path[0].goal, person.pos) <= HOW_CLOSE_TO_GOAL_SQUARED)
				{
					path.RemoveAt(0);
				}
			}
		}
	}

	[BurstCompile]
	struct SaveeTick : IJobForEach_BCC<PersonPathPos, Person, Savee>
	{
		[ReadOnly] public ComponentDataFromEntity<HotFloor> hotFloor;
		[ReadOnly] public Map map;
		public byte randDir; // Hmm

		public void Execute(DynamicBuffer<PersonPathPos> path, ref Person person, ref Savee savee)
		{
			PFTile tile = Map.GetTile(person.pos);
			if (tile.y > 0)
			{
				tile.y--;
				Entity floor = map.GetEntity(tile);
				if (path.Length == 0 && hotFloor.HasComponent(floor))
				{
					tile.y++;
					PFTile next = tile.GetToTile((Dir)randDir);
					if (next.IsValid() && map.GetHeightIndex(next) == tile.y)
						path.Add(new PersonPathPos { goal = Map.WorldPosition(next) });
				}
			}
		}
	}*/

	/*struct SpreadBadAir : IJob
	{
		public DisasterMap map;
		public Unity.Mathematics.Random rand;
		public byte randDir; // Hmm
		public void Execute()
		{*/
	/*if (rand.NextFloat(0f, 1f) < 0.01f)
	{
		PFTile tile = new PFTile((ushort)Variance.NextInt(500), 0, (ushort)Variance.NextInt(500));
		AddBadAir(tile, 5f, 25f);
	}

	for (int i = 0; i < 100; i++)
	{
		PFTile tile = new PFTile((ushort)Variance.NextInt(500), 0, (ushort)Variance.NextInt(500));
		if (map.badAir.ContainsKey(tile.Index()))
		{
			PFTile next = tile.GetToTile((Dir)randDir);
			if (next.IsValid())
			{
				AddBadAir(next, 0.2f, 25f);
			}
		}
	}*/
	// }

	/*private void AddBadAir(PFTile tile, float amount, float max)
	{
		BadAir badAir;
		if (map.badAir.ContainsKey(tile.Index()))
			badAir = map.badAir[tile.Index()];
		else
			badAir = new BadAir { tile = tile, howBad = 0 };
		badAir.howBad = math.min(max, badAir.howBad + amount); // Technically it should spread with conservation...
		map.badAir[tile.Index()] = badAir;
		map.changedTiles[tile.Index()] = tile;
	}*/
	// }

	/*[BurstCompile]
	struct CameraOperatorTick : IJobForEachWithEntity_EBCC<PersonPathPos, Person, CameraOperator>
	{
		// public DisasterMap disasterMap;
		// public float tickTime;

		public void Execute(Entity entity, int index, DynamicBuffer<PersonPathPos> path, ref Person person, ref CameraOperator cameraOperator)
		{
			PFTile tile = Map.GetTile(person.pos);
			switch (cameraOperator.state)
			{
				case CameraOperator.State.Recording:



					person.rot = quaternion.LookRotation(math.normalizesafe(cameraOperator.lookAt - person.pos), new float3(0, 1, 0));
					break;
				case CameraOperator.State.FindingSaver:

					break;
				case CameraOperator.State.GoingToSaver:

					break;
			}
		}
	}*/

	struct PersonPathFind
	{
		public Entity entity;
		public float3 goal;
	}

	/*[BurstCompile]
	struct SaverTick : IJobForEachWithEntity_EBCC<PersonPathPos, Person, Saver>
	{
		public const float REDUCE_BAD_AIR_RATE = 10f;
		public const float RESOURCE_USE_RATE = 0.2f;

		public Map map;
		public DisasterMap disasterMap;
		public float tickTime;
		public Unity.Mathematics.Random rand;
		public ComponentDataFromEntity<ResourceStorage> resourceStorage;
		public ComponentDataFromEntity<BasicPosition> basicPosition;
		[ReadOnly] public BufferFromEntity<ResourcesForSaversRef> resourcesForSaversRef;
		[ReadOnly] public BufferFromEntity<FloorHeaterRef> floorHeaterRef;
		[ReadOnly] public BufferFromEntity<HotFloorRef> hotFloorRef;
		public ComponentDataFromEntity<HotFloor> hotFloor;
		public NativeHashMap<uint, PFTile> deletedTiles;
		public BufferFromEntity<PFRectConnection> pfRectConnection;
		public NativeList<PersonPathFind> queuedPathFinds;

		private void SetState(ref Person person, ref Saver saver, Saver.State state)
		{
			saver.state = state;
			saver.timeSinceStateStart = 0f;
			switch (state) {
				case Saver.State.FindNearestResources:
				case Saver.State.FindingHotFloor:
					person.timeSincePathFind = 50000f; // Pathfind right away
					person.searchDistance = 10f; // Start search distance small
					person.pathfinding = false; // hmm
					break;
			}
		}

		private void Claim(ref Person person, Entity entity)
		{
			// Shouldn't happen..
			if (person.claimed != Entity.Null)
				RemoveClaim(ref person);
			BasicPosition bp = basicPosition[entity];
			bp.claimed++;
			basicPosition[entity] = bp;
			person.claimed = entity;
		}

		private void RemoveClaim(ref Person person)
		{
			if (basicPosition.HasComponent(person.claimed)) // Does the entity exist?
			{
				BasicPosition bp = basicPosition[person.claimed];
				bp.claimed--;
				basicPosition[person.claimed] = bp;
				person.claimed = Entity.Null;
			}
		}

		private void Search<T>(Entity entity, ref Person person, ref Saver saver, Saver.State nextState, BufferFromEntity<T> bufferRef, float3 goalOffset) where T : struct, IBufferElementData, IBufferEntity
		{
			person.timeSincePathFind += tickTime;
			if (person.timeSincePathFind >= 2)
			{
				person.timeSincePathFind = 0;
				Entity res = map.FindNearest(basicPosition, pfRectConnection, bufferRef, person.pos, person.searchDistance);
				if (res != Entity.Null)
				{
					queuedPathFinds.Add(new PersonPathFind { entity = entity, goal = Map.WorldPosition(basicPosition[res].tile) + goalOffset });
					person.pathfinding = true;
					SetState(ref person, ref saver, nextState);
					Claim(ref person, res); // Claim it regardless, I guess..
				}
				person.searchDistance += 0.5f; // Increase search distance a little for next time...
			}
		}

		public void Execute(Entity entity, int index, DynamicBuffer<PersonPathPos> path, ref Person person, ref Saver saver)
		{
			PFTile tile = Map.GetTile(person.pos);
			if (!tile.IsValid())
				return;
			switch (saver.state)
			{
				case Saver.State.Init:
					{
						SetState(ref person, ref saver, Saver.State.FindNearestResources);
						break;
					}
				case Saver.State.FindNearestResources:
					{
						Search(entity, ref person, ref saver, Saver.State.GettingResources, resourcesForSaversRef, float3.zero);
						break;
					}
				case Saver.State.GettingResources:
					{
						if (path.Length == 0 && !person.pathfinding)
						{
							RemoveClaim(ref person);

							Entity entityStorage = map.GetEntity(tile);
							if (resourceStorage.HasComponent(entityStorage))
							{
								ResourceStorage storage = resourceStorage[entityStorage];
								ResourceStorage thisStorage = resourceStorage[entity];
								if (storage.TransferMax(ref thisStorage, ResourceType.FloorCooler))
								{
									resourceStorage[entityStorage] = storage;
									resourceStorage[entity] = thisStorage;
									SetState(ref person, ref saver, Saver.State.FindingHotFloor);
								}
								else
								{
									SetState(ref person, ref saver, Saver.State.FindNearestResources);
								}
							}
						}
						break;
					}
				case Saver.State.FindingHotFloor:
					{
						Search(entity, ref person, ref saver, Saver.State.GoingToHotFloor, hotFloorRef, new float3(0, PFTile.HEIGHT, 0));
						break;
					}
				case Saver.State.GoingToHotFloor:
					{
						if (path.Length == 0 && !person.pathfinding)
						{
							SetState(ref person, ref saver, Saver.State.CoolingHotFloor);
						}
						break;
					}
				case Saver.State.CoolingHotFloor:
					{
						ResourceStorage thisStorage = resourceStorage[entity];
						if (thisStorage.numResources > 0)
						{
							thisStorage.numResources = math.max(0, thisStorage.numResources - RESOURCE_USE_RATE * tickTime);
							resourceStorage[entity] = thisStorage;
							//if (CoolFloor(tile))
							//{
							//	RemoveClaim(ref person);
							//	SetState(ref person, ref saver, Saver.State.FindingHotFloor);
							//}
						}
						else
						{
							RemoveClaim(ref person);
							SetState(ref person, ref saver, Saver.State.FindNearestResources);
						}
						break;
					}
			}
		}

		private bool CoolFloor(PFTile tile, ref Saver saver, ref ResourceStorage thisStorage)
		{
			tile.y--; // The floor is below the person, obviously
			Entity floor = map.GetEntity(tile);
			if (hotFloor.HasComponent(floor))
			{
				HotFloor hf = hotFloor[floor];

				if (saver.timeSinceStateStart * REDUCE_BAD_AIR_RATE >= hf.howBad && thisStorage.numResources >= hf.howBad * RESOURCE_USE_RATE)
				{
					hf.howBad = -5; // Floor is cooled
					// Everyone on tile is saved:
					tile.y++;

				}

				hf.howBad = math.max(0, hf.howBad - REDUCE_BAD_AIR_RATE * tickTime);
				if (hf.howBad == 0)
				{
					// Add to tiles to remove...
					deletedTiles[tile.Index()] = tile;
				}
				hotFloor[floor] = hf;
				return hf.howBad == 0;
				//if (badAir.howBad <= 0)
				//{
				//	disasterMap.badAir.Remove(tile.Index());
				//	return true;
				//}
				//else
				//{
				//	disasterMap.badAir[tile.Index()] = badAir;
				//	return false;
				//}
			}
			return true;
		}
	}*/

	[BurstCompile]
	struct TakeDamage : IJobForEachWithEntity<SimplePerson> // Translation>
	{
		public Map map;
		// [ReadOnly] public ComponentDataFromEntity<HotFloor> hotFloor;
		public NativeQueue<Entity>.ParallelWriter deaths;
		public float tickTime;
		public void Execute(Entity entity, int index, ref SimplePerson person)//, ref Translation translation)
		{
			person.hunger = math.min(100f, person.hunger + 0.1f * tickTime); // 1000s to deal with hunger
			person.health = math.min(100f, person.health + 3f * tickTime); // 33s to heal?
			PFTile tile = Map.GetTile(person.pos);
			if (tile.IsValid())
			{
				float depth = map.fluidMap.GetHeight(person.pos) - map.GetHeight(tile);

				/*if (depth > 0.01f)
				{
					float2 vel = map.fluidMap.GetVelocity(person.pos) * math.min(1f, depth);
					// map.velocity[(int)(tile.x * Map.SIZE_Z + tile.z)] * math.min(1f, map.depth[(int)(tile.x * Map.SIZE_Z + tile.z)]);
					// Flow:
					// float velMag = math.length(vel);

					//if (velMag > Person.SPEED)
					person.pos += new float3(vel.x, 0, vel.y) * tickTime;// * (velMag - Person.SPEED) / velMag;
				}*/

				// Lose health because it takes energy to float..
				if (person.hunger == 100 || depth > SimplePerson.HEIGHT * 1.5f)
				{

					person.health = math.max(0, person.health - 5f * tickTime); // 50s to die...
					if (person.health == 0)
					{
						deaths.Enqueue(entity);
					}
				}

				/*float waterHeight = map.GetHeight(tile) + depth;//map.depth[(int)(tile.x * Map.SIZE_Z + tile.z)];
				if (person.pos.y < waterHeight)
				{
					person.yVelocity *= math.pow(0.1f, tickTime); // TODO_EFFICIENCY we could precalculate this number
					// person.yVelocity += 0.05f * 9.8f * tickTime;
					person.yVelocity += (1.2f * 9.8f * math.clamp(waterHeight - person.pos.y, 0, SimplePerson.HEIGHT) * tickTime) / SimplePerson.HEIGHT; // pgA
				}
				person.yVelocity -= 9.8f * tickTime;

				person.pos.y += person.yVelocity;
				// Snap up if under walking height:
				if (map.GetHeight(tile) > person.pos.y)
				{
					person.pos.y = map.GetHeight(tile);
					person.yVelocity = 0;
				}*/

				//translation.Value = person.pos;
			}

			

			//if (tile.y > 0)
			//{
			//	tile.y--;
			//	Entity floor = map.GetEntity(tile);
			//	if (hotFloor.HasComponent(floor))
			//	{
			//		person.health = math.max(0, person.health - hotFloor[floor].howBad * tickTime);
			//		if (person.health == 0)
			//		{
			//			deaths.Add(entity);
			//		}
			//	}
			//}
		}
	}

	protected override JobHandle OnUpdate(JobHandle inputDeps)
	{
		/*JobHandle jobHandle = new ChooseWhereToMove
		{
			map = Game.disasterMap,
			randDir = (byte)Variance.NextInt(4)
		}.Schedule(EntityManager.CreateEntityQuery(typeof(Savee), typeof(PersonPathPos), typeof(Person)), inputDeps);*/
		/*JobHandle jobHandle = new MovePeople
		{
			tickTime = Game.GetTickTime()
		}.Schedule(this, inputDeps);
		jobHandle = new SaveeTick
		{
			hotFloor = GetComponentDataFromEntity<HotFloor>(),
			randDir = (byte)Variance.NextInt(4),
			map = Game.map
		}.Schedule(this, jobHandle);
		jobHandle = new SpreadBadAir
		{
			map = Game.disasterMap,
			rand = new Unity.Mathematics.Random((uint)Variance.NextInt()),
			randDir = (byte)Variance.NextInt(4)
		}.Schedule(jobHandle);
		jobHandle = new SaverTick
		{
			map = Game.map,
			disasterMap = Game.disasterMap,
			resourceStorage = GetComponentDataFromEntity<ResourceStorage>(),
			tickTime = Game.GetTickTime(),
			rand = new Unity.Mathematics.Random((uint)Variance.NextInt()),
			hotFloor = GetComponentDataFromEntity<HotFloor>(),
			floorHeaterRef = GetBufferFromEntity<FloorHeaterRef>(),
			hotFloorRef = GetBufferFromEntity<HotFloorRef>(),
			resourcesForSaversRef = GetBufferFromEntity<ResourcesForSaversRef>(),
			basicPosition = GetComponentDataFromEntity<BasicPosition>(),
			deletedTiles = deletedTiles,
			pfRectConnection = GetBufferFromEntity<PFRectConnection>(),
			queuedPathFinds = queuedPathFinds
		}.ScheduleSingle(this, jobHandle);*/

		inputDeps = new TakeDamage
		{
			map = Game.map,
			deaths = deaths.AsParallelWriter(),
			// hotFloor = GetComponentDataFromEntity<HotFloor>(),
			tickTime = Game.GetTickTime()
		}.Schedule(this, inputDeps);


		// BadAirSaverTick...
		return base.OnUpdate(inputDeps);
	}

	public override void MainThreadSimulationCallbackTick()
	{
		NativeArray<PFTile> tiles = deletedTiles.GetValueArray(Allocator.Temp);
		for (int i = 0; i < tiles.Length; i++)
		{
			EntityManager.World.GetExistingSystem<ConstructionSystem>().DestroyEntity(Game.map.GetEntity(tiles[i]));
		}
		deletedTiles.Clear();
		while (deaths.TryDequeue(out Entity death))
		{
			// Kill person:
			if (death.Has<ComplexPerson>() && death.Get<ComplexPerson>().claimed.Has<BasicPosition>())
			{
				death.Get<ComplexPerson>().claimed.Modify((ref BasicPosition bp) => { bp.claimed--; });
			}
			// Create dead body:
			Entity deadBody = EntityManager.CreateEntity(ConstructionSystem.subMeshRenderer);
			deadBody.SetData(new Translation { Value = death.Get<SimplePerson>().pos });
			deadBody.SetData(new Rotation { Value = quaternion.Euler(0, Variance.Range(0f, math.PI * 2f), math.PI / 2f) });
			Material material = null;
			if (death.Has<Savee>())
				material = RenderInfo.Person[typeof(Savee)];
			else if (death.Has<Saver>())
				material = RenderInfo.Person[typeof(Saver)];
			else if (death.Has<CameraOperator>())
				material = RenderInfo.Person[typeof(CameraOperator)];
			deadBody.SetSharedData(new RenderMesh { mesh = RenderInfo.self.personObject, material = material });

			// Propegate event to reporters:
			World.GetExistingSystem<ReporterSystem>().ReportDeath(death, death.Get<SimplePerson>().pos);

			EntityManager.DestroyEntity(death);
			Game.numDeaths++;
		}
		deaths.Clear();

		for (int i = 0; i < queuedPathFinds.Length; i++)
		{
			PathFinder.PersonPathFind(queuedPathFinds[i].entity, Map.GetTile(queuedPathFinds[i].goal));
		}
		queuedPathFinds.Clear();
	}
}

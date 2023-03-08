using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.UI;


public struct TileVel
{
	public PFTile tile;
	public Entity goal;
}

[UpdateInGroup(typeof(MainSimSystemGroup))]
public class PersonTensionSystem : JobComponentSystemWithCallback
{
	EntityQuery peopleQuery;
	NativeMultiHashMap<int, Entity> peopleMap;
	Texture2D peopleDensity;
	public NativeList<TileVel> hungerList;

	protected override void OnCreate()
	{
		base.OnCreate();
		peopleQuery = GetEntityQuery(typeof(SimplePerson), typeof(SimplePersonCollideUpdate), typeof(Translation), typeof(Rotation));
		peopleMap = new NativeMultiHashMap<int, Entity>(50000, Allocator.Persistent);
		peopleDensity = new Texture2D((int)Map.SIZE_X, (int)Map.SIZE_Z, TextureFormat.RGBA32, false);
		hungerList = new NativeList<TileVel>(100, Allocator.Persistent);
	}


	public override void FirstUpdate()
	{
		base.FirstUpdate();
		GameObject.Find("Minimap2").GetComponent<RawImage>().texture = peopleDensity;
	}

	protected override void OnDestroy()
	{
		peopleMap.Dispose();
		hungerList.Dispose();
		base.OnDestroy();
	}

	[BurstCompile]
	struct ClearPersonMap : IJob
	{
		public NativeMultiHashMap<int, Entity> peopleMap;
		public void Execute()
		{
			peopleMap.Clear();
		}
	}

	[BurstCompile]
	struct CollisionTick : IJobForEachWithEntity<SimplePersonCollideUpdate>
	{
		const float COLLIDE_FORCE = 0.1f;
		const float EDGE_FORCE = 1f;
		public Map map;
		[ReadOnly] public NativeMultiHashMap<int, Entity> peopleMap;
		[ReadOnly] public ComponentDataFromEntity<SimplePerson> people;

		private float3 CollideCheck(Entity a, Entity b)
		{
			float rad = SimplePerson.RADIUS * 2;
			float distSqr = math.distancesq(people[a].pos, people[b].pos);
			if (distSqr < rad * rad)
			{
				float dist = math.sqrt(distSqr);
				float3 move = (people[a].pos - people[b].pos) / dist * (rad - dist) * COLLIDE_FORCE;
				move.y = math.length((people[a].pos - people[b].pos) / dist * (rad - dist) * COLLIDE_FORCE);
				// Circle c = circles[b];
				// c.vel = -move;
				// circles[b] = c;
				return move;
			}
			return 0;
		}

		public void Execute(Entity entity, int index, ref SimplePersonCollideUpdate spcu)
		{
			float3 modify = 0;
			/*SimplePerson person = people[entity];

			// Look in the 4 directions:
			PFTile curr = Map.GetTile(person.pos);
			byte oldWalkingHeight = map.GetHeightIndex(curr);

			PFTile adj = curr.GetToTile(Dir.Left);
			if (!adj.IsValid() || map.GetHeightIndex(adj) != oldWalkingHeight)
			{
				float dist = person.pos.x - (adj.x + 1) * PFTile.LENGTH;
				if (dist < SimplePerson.RADIUS)
					modify.x += (SimplePerson.RADIUS - dist) * EDGE_FORCE;
			}
			adj = curr.GetToTile(Dir.Right);
			if (!adj.IsValid() || map.GetHeightIndex(adj) != oldWalkingHeight)
			{
				float dist = adj.x * PFTile.LENGTH - person.pos.x;
				if (dist < SimplePerson.RADIUS)
					modify.x -= (SimplePerson.RADIUS - dist) * EDGE_FORCE;
			}
			adj = curr.GetToTile(Dir.Back);
			if (!adj.IsValid() || map.GetHeightIndex(adj) != oldWalkingHeight)
			{
				float dist = person.pos.z - (adj.z + 1) * PFTile.LENGTH;
				if (dist < SimplePerson.RADIUS)
					modify.z += (SimplePerson.RADIUS - dist) * EDGE_FORCE;
			}
			adj = curr.GetToTile(Dir.Forward);
			if (!adj.IsValid() || map.GetHeightIndex(adj) != oldWalkingHeight)
			{
				float dist = adj.z * PFTile.LENGTH - person.pos.z;
				if (dist < SimplePerson.RADIUS)
					modify.z -= (SimplePerson.RADIUS - dist) * EDGE_FORCE;
			}*/

			int3 mid = PersonMap.GetIntPos(people[entity].pos);
			// Up / down collision is not considered...
			for (int x = -1; x <= 1; x++)
			{
				for (int z = -1; z <= 1; z++)
				{
					int personMapIndex = PersonMap.GetIndex(new int3(mid.x + x, mid.y, mid.z + z));
					if (personMapIndex != -1)
					{
						if (peopleMap.TryGetFirstValue(personMapIndex, out Entity foundValue, out var iterator))
						{
							do
							{
								if (foundValue != entity) // Don't collide with yourself
								{
									modify += CollideCheck(entity, foundValue);
								}
							} while (peopleMap.TryGetNextValue(out foundValue, ref iterator));
						}
					}
				}
			}
			spcu.delta = new float2(modify.x, modify.z);
			spcu.total = modify.y;
		}
	}

	[BurstCompile]
	struct MoveTick : IJobForEachWithEntity<SimplePerson, SimplePersonCollideUpdate, Translation, Rotation>
	{
		public float tickTime;
		public Map map;
		[ReadOnly] public PersonMap personMap;
		public NativeMultiHashMap<int, Entity>.ParallelWriter peopleMap;
		[ReadOnly] public ComponentDataFromEntity<WalkInfo> walkInfo;
		// [ReadOnly] public ArchetypeChunkEntityType peopleType;


		private float3 GetNextPos(ref SimplePerson person, float3 newPos)
		{
			//PFTile tile = Map.GetTile(person.pos);
			//if (newPos.y < 0)
			//	newPos.y = 0;
			/*PFTile newTile = Map.GetTile(newPos);
			if (!newTile.Equals(tile))
			{
				// Just reset it, if the newTile is invalid...
				if (!newTile.IsValid())
				{
					return person.pos;
				}
				else
				{*/

			// byte oldWalkingHeight = map.GetHeightIndex(tile);
			float newWalkingHeight = map.GetHeight(walkInfo, newPos);
			float maxIncrease = SimplePerson.SPEED * 1.1f * tickTime + 9.8f * tickTime;
			if (newWalkingHeight > person.pos.y + maxIncrease)
			{
				// Attempt to fix: (Move along the edge)
				if (map.GetHeight(walkInfo, new float3(person.pos.x, newPos.y, newPos.z)) <= person.pos.y + maxIncrease) //== oldWalkingHeight)
					newPos = new float3(person.pos.x, newPos.y, newPos.z);
				else if (map.GetHeight(walkInfo, new float3(newPos.x, newPos.y, person.pos.z)) <= person.pos.y + maxIncrease) //== oldWalkingHeight)
					newPos = new float3(newPos.x, newPos.y, person.pos.z);
				else
					newPos = new float3(person.pos.x, newPos.y, person.pos.z);
			}
			// newTile = Map.GetTile(newPos);
			float height = map.GetHeight(walkInfo, newPos);
			if (newPos.y < height)
			{
				newPos.y = height;
				person.yVelocity = 0;
			}
				//}
			//}
			return newPos;
		}

		public void Execute(Entity entity, int index, ref SimplePerson person, [ReadOnly] ref SimplePersonCollideUpdate spcu, ref Translation translation, ref Rotation rotation)
		{
			PFTile tile = Map.GetTile(person.pos);
			float3 vel = personMap.GetVelocity(tile, person.hunger >= 75) * SimplePerson.SPEED;

			person.yVelocity -= 9.8f * tickTime;
			float depth = map.fluidMap.GetHeight(person.pos) - map.GetHeight(tile);
			if (depth > 0.01f)
			{

				float waterHeight = map.GetHeight(tile) + depth;//map.depth[(int)(tile.x * Map.SIZE_Z + tile.z)];
				if (person.pos.y < waterHeight)
				{
					float2 vel2 = map.fluidMap.GetVelocity(person.pos) * math.min(1f, waterHeight - person.pos.y);
					vel += new float3(vel2.x, 0, vel2.y);

					person.yVelocity *= math.pow(0.05f, tickTime); // TODO_EFFICIENCY we could precalculate this number
																   // person.yVelocity += 0.05f * 9.8f * tickTime;
					person.yVelocity += (1.5f * 9.8f * math.clamp(waterHeight - person.pos.y, 0, SimplePerson.HEIGHT) * tickTime) / SimplePerson.HEIGHT; // pgA
				}
			}

			// float2 vel2 = new float2(vel.x, vel.z);
			// vel /= math.max(1f, spcu.total * 200);
			if (spcu.total > 0.1f) // || math.dot(vel2, spcu.delta))
			{
				vel = 0;
			}
			else if (spcu.total > 0.01f)
			{
				vel *= (1f - (spcu.total - 0.01f) * 9f);
			}
			vel.y += person.yVelocity;


			float3 newPos = person.pos + vel * tickTime + new float3(spcu.delta.x, 0, spcu.delta.y);
			//circle.vel = 0; // Go ahead and reset it..

			newPos = GetNextPos(ref person, newPos);

			// float3 delta = newPos - person.pos - new float3(spcu.delta.x, 0, spcu.delta.y);

			// personMap.Claim(entity, person.pos, newPos);
			peopleMap.Add(PersonMap.GetIndex(PersonMap.GetIntPos(newPos)), entity);

			person.pos = newPos;

			translation.Value = person.pos;

			vel.y = 0;
			float velMagnitude = math.length(vel);
			if (velMagnitude >= 0.01f)
			{
				rotation.Value = math.slerp(rotation.Value, quaternion.LookRotation(vel / velMagnitude, new float3(0, 1, 0)), velMagnitude * 0.05f);
			}

			// Don't fall off
			// circle.pos = GetNextPos(ref map, circle, circle.pos, newPos);
		}
	}

	[BurstCompile]
	struct FoodServicerTick : IJobForEach<ResourceStorage, Building, FoodServicer>
	{
		[ReadOnly] public NativeMultiHashMap<int, Entity> peopleMap;
		[NativeDisableParallelForRestriction] public ComponentDataFromEntity<SimplePerson> people;
		public void Execute(ref ResourceStorage resourceStorage, [ReadOnly] ref Building building, [ReadOnly]ref FoodServicer foodServicer)
		{
			/*float3 pos = building.GetPos(new float3(-1, 0, 0));
			for (int i = 0; i < 12; i++)
			{
				int index = PersonMap.GetIndex(PersonMap.GetIntPos(pos));
				if (peopleMap.TryGetFirstValue(index, out Entity foundValue, out var iterator))
				{
					do
					{
						SimplePerson person = people[foundValue];
						person.hunger = 0; // hmm
						people[foundValue] = person;
					} while (peopleMap.TryGetNextValue(out foundValue, ref iterator));
				}
				pos.x += PFTile.LENGTH * 3 / 12;
			}*/
			for (int tileX = 0; tileX < 3; tileX++)
			{
				PFTile tile = building.GetTile(tileX, 0, 0);
				int x = tile.x;
				int z = tile.z;
				for (int x2 = 0; x2 < PersonMap.PEOPLE_PER_GRID; x2++)
				{
					for (int z2 = 0; z2 < PersonMap.PEOPLE_PER_GRID; z2++)
					{
						int3 pos = new int3(x * PersonMap.PEOPLE_PER_GRID + x2, tile.y, z * PersonMap.PEOPLE_PER_GRID + z2);
						int i = PersonMap.GetIndex(pos);
						if (peopleMap.TryGetFirstValue(i, out Entity foundValue, out var iterator))
						{
							do
							{
								SimplePerson person = people[foundValue];
								person.hunger = 0; // hmm
								people[foundValue] = person;
							} while (peopleMap.TryGetNextValue(out foundValue, ref iterator));
						}
					}
				}
			}
		}
	}

	// Slow in this implementation because of the y index...
	[BurstCompile]
	struct CountPeoplePerTile : IJobParallelFor
	{
		[ReadOnly] public NativeMultiHashMap<int, Entity> peopleMap;
		[ReadOnly] public ComponentDataFromEntity<SimplePerson> people;
		public NativeArray<Color32> peopleDensity;

		public void Execute(int index)
		{
			int x = (int)(index % Map.SIZE_X);
			int z = (int)(index / Map.SIZE_X);
			int total = 0;
			float totalHunger = 0; // Once a person reaches 100 hunger, they start to lose health and die
			for (int y = 0; y < Map.SIZE_Y; y++)
			{
				for (int x2 = 0; x2 < PersonMap.PEOPLE_PER_GRID; x2++)
				{
					for (int z2 = 0; z2 < PersonMap.PEOPLE_PER_GRID; z2++)
					{
						int3 pos = new int3(x * PersonMap.PEOPLE_PER_GRID + x2, y, z * PersonMap.PEOPLE_PER_GRID + z2);
						int i = PersonMap.GetIndex(pos);
						if (peopleMap.TryGetFirstValue(i, out Entity foundValue, out var iterator))
						{
							do
							{
								total++;
								totalHunger += people[foundValue].hunger;
							} while (peopleMap.TryGetNextValue(out foundValue, ref iterator));
						}
					}
				}
			}
			byte val = (byte)math.min(255, total * 30);
			byte hungerVal = (byte)math.min(255, totalHunger * 0.3f);
			peopleDensity[index] = new Color32(hungerVal, (byte)math.max(0, val - hungerVal), 0, val);
		}
	}

	struct UpdateVelocityMap : IJob
	{
		public Map map;
		public NativeArray<float2> velocityMap;
		public NativeArray<Goal> goal;
		public NativeList<TileVel> openList; // Fill open list to start...
		// NativeHashMap<uint, float> searchedList;


		public void Execute()
		{
			for (int i = 0; i < openList.Length; i++)
			{
				int index = openList[0].tile.HorizontalIndex();
				goal[index] = new Goal { dist = 0, goal = openList[0].goal };
			}

			int iter = 0;
			int k = 0;
			// Breadth first search...
			while (openList.Length > 0)
			{
				iter++;
				if (iter > 10000)
				{
					openList.Clear();
					break;
				}
				if (k >= openList.Length)
					k = 0;
				TileVel open = openList[k];
				openList.RemoveAtSwapBack(k);
				k++;
				int prevIndex = open.tile.HorizontalIndex();
				float nextDistance = goal[prevIndex].dist + PFTile.LENGTH;
				Entity nextGoal = goal[prevIndex].goal;

				WalkRule rule = map.tileInfo[open.tile.HorizontalIndex()].walkable;

				for (byte dir = 0; dir < 4; dir++)
				{
					if (rule.HasFlag((WalkRule)(1 << dir)))
					{
						PFTile next = open.tile.GetToTile((Dir)dir);
						if (next.IsValid()) // && map.GetHeightIndex(next) == map.GetHeightIndex(open.tile))
						{
							int index = next.HorizontalIndex();
							if (goal[index].goal == Entity.Null || nextDistance < goal[index].dist)
							{
								goal[index] = new Goal { dist = nextDistance, goal = nextGoal };
								velocityMap[index] = new float2(open.tile.x - next.x, open.tile.z - next.z);
								openList.Add(new TileVel { tile = next, goal = nextGoal });
							}
						}
					}
				}
			}
		}
	}

	const int TICKS_PER_TICK = 1;

	protected override JobHandle OnUpdate(JobHandle inputDeps)
	{

		if (ECSHandler.tickCount % TICKS_PER_TICK == 0)
		{
			inputDeps = new ClearPersonMap
			{
				peopleMap = peopleMap
			}.Schedule(inputDeps);

			inputDeps = new MoveTick
			{
				map = Game.map,
				personMap = Game.personMap,
				peopleMap = peopleMap.AsParallelWriter(),
				tickTime = Game.GetTickTime() * TICKS_PER_TICK,
				walkInfo = GetComponentDataFromEntity<WalkInfo>()
				//people = GetComponentDataFromEntity<SimplePerson>()
			}.Schedule(peopleQuery, inputDeps);

			inputDeps = new FoodServicerTick
			{
				peopleMap = peopleMap,
				people = GetComponentDataFromEntity<SimplePerson>()
			}.Schedule(this, inputDeps);

			//inputDeps = new CountPeoplePerTile
			//{
			//	peopleMap = peopleMap,
			//	peopleDensity = peopleDensity.GetRawTextureData<Color32>(),
			//	people = GetComponentDataFromEntity<SimplePerson>()
			//}.Schedule((int)(Map.SIZE_X * Map.SIZE_Z), 32, inputDeps);

			if (hungerList.Length > 0)
			{
				inputDeps = new UpdateVelocityMap
				{
					goal = Game.personMap.hungryGoal,
					velocityMap = Game.personMap.hungryVel,
					map = Game.map,
					openList = hungerList
				}.Schedule(inputDeps);
			}

			inputDeps = new CollisionTick
			{
				map = Game.map,
				// personMap = Game.personMap,
				peopleMap = peopleMap,
				people = GetComponentDataFromEntity<SimplePerson>()
			}.Schedule(peopleQuery, inputDeps);
		}
		return base.OnUpdate(inputDeps);
	}

	public override void MainThreadSimulationCallbackTick()
	{
		peopleDensity.Apply();
	}
}

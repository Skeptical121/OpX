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

public struct BasicPosition : IComponentData, ITilesTaken
{
	public PFTile tile;
	public int claimed; // This is so people don't go here if there isn't enough to do, essentially
	public int capacity;

	public bool IsValid()
	{
		return claimed < capacity;
	}

	public float3 CenterBottom()
	{
		return Map.WorldPosition(tile);
	}

	public List<PFTile> GetTilesTaken()
	{
		return new List<PFTile> { tile };
	}
}

public struct FloorHeater : IComponentData
{
	public float heatRate;
	public float deltaHeatRate;
}

public struct HotFloor : IComponentData
{
	public const float MAX_HEAT = 25f;
	public float howBad;
}
/*
[UpdateInGroup(typeof(MainSimSystemGroup))]
public class HotFloorRenderSystem : ComponentSystem
{
	protected override void OnUpdate()
	{
		if (ECSHandler.tickCount % 50 == 0)
		{
			Entities.ForEach((DynamicBuffer<SubMeshRenderer> renderer, ref HotFloor hotFloor) =>
			{
				RenderMesh rm = EntityManager.GetSharedComponentData<RenderMesh>(renderer[0].renderer);
				rm.material = RenderInfo.HotterFloors[(int)hotFloor.howBad];
				renderer[0].renderer.SetSharedData(rm);
			});
		}
	}
}


[UpdateInGroup(typeof(MainSimSystemGroup))]
public class HotFloorSystem : JobComponentSystemWithCallback
{
	private NativeList<PFTile> addTiles;
	protected override void OnCreate()
	{
		addTiles = new NativeList<PFTile>(Allocator.Persistent);
		base.OnCreate();
	}

	[BurstCompile]
	struct FloorHeaterTick : IJobForEach<FloorHeater, BasicPosition>
	{
		[ReadOnly] public Map map;
		public Unity.Mathematics.Random rand;
		public NativeList<PFTile> addTiles;
		public ComponentDataFromEntity<HotFloor> hotFloor;
		public float tickTime;

		public void Execute(ref FloorHeater floorHeater, [ReadOnly] ref BasicPosition basicPosition)
		{
			floorHeater.heatRate = math.max(0, floorHeater.heatRate + floorHeater.deltaHeatRate * tickTime);
			if (floorHeater.heatRate > 0 && rand.NextFloat(0f, 1f) < 0.2f)
			{
				PFTile tile = basicPosition.tile;
				tile.y--;
				Entity entity = map.GetEntity(tile);
				if (hotFloor.HasComponent(entity))
				{
					HotFloor hf = hotFloor[entity];
					if (hf.howBad < HotFloor.MAX_HEAT)
					{
						hf.howBad = math.min(HotFloor.MAX_HEAT, hf.howBad + floorHeater.heatRate * tickTime);
						hotFloor[entity] = hf;
					}
				}
				else
				{
					addTiles.Add(tile);
				}
			}
			// We could use tilesTaken to get the tile of the floorHeater..
		}
	}

	[BurstCompile]
	struct SpreadHotFloor : IJobForEachWithEntity<BasicPosition>
	{
		public const float SPREAD_CONSTANT = 0.5f; // Split over 4, remember.. assumes tickTime is small

		[ReadOnly] public Map map;
		public ComponentDataFromEntity<HotFloor> hotFloor;
		public Unity.Mathematics.Random rand;
		public float tickTime;
		public NativeList<PFTile> addTiles;

		public void Execute(Entity entity, int index, ref BasicPosition basicPosition)
		{
			HotFloor hfThis = hotFloor[entity];
			if (hfThis.howBad > 2f) // Can only spread if howBad is above a certain amount... and it happens fairly rarely
			{
				PFTile tile = basicPosition.tile.GetToTile((Dir)rand.NextInt(4));
				if (tile.y == map.GetWalkingHeight(tile) - 1)
				{
					Entity next = map.GetEntity(tile);
					if (hotFloor.HasComponent(next))
					{
						HotFloor hf = hotFloor[next];
						float diff = (hfThis.howBad - hf.howBad) * SPREAD_CONSTANT * tickTime;
						hf.howBad += diff;
						hfThis.howBad -= diff;
						hotFloor[next] = hf;
						hotFloor[entity] = hfThis;
					}
					else if (rand.NextFloat(0f, 1f) < 0.1f)
					{
						addTiles.Add(tile);
					}
				}
			}
		}
	}*/

		/*struct UpdateRender : IJob
		{
			public DisasterMap map;
			public NativeArray<Color32> colorData;
			public void Execute()
			{
				NativeArray<PFTile> changed = map.changedTiles.GetValueArray(Allocator.Temp);
				for (int i = 0; i < changed.Length; i++)
				{
					map.SetColor(colorData, changed[i]);
				}
				map.changedTiles.Clear();
			}
		}*/

		/*protected override JobHandle OnUpdate(JobHandle inputDeps)
	{
		inputDeps = new FloorHeaterTick
		{
			map = Game.map,
			rand = new Unity.Mathematics.Random((uint)Variance.NextInt()),
			addTiles = addTiles,
			hotFloor = GetComponentDataFromEntity<HotFloor>(),
			tickTime = Game.GetTickTime()
		}.ScheduleSingle(this, inputDeps);
		inputDeps = new SpreadHotFloor
		{
			map = Game.map,
			rand = new Unity.Mathematics.Random((uint)Variance.NextInt()),
			addTiles = addTiles,
			hotFloor = GetComponentDataFromEntity<HotFloor>(),
			tickTime = Game.GetTickTime()
		}.ScheduleSingle(EntityManager.CreateEntityQuery(typeof(HotFloor), typeof(BasicPosition)), inputDeps);
		//inputDeps = new UpdateRender
		//{
		//	map = Game.disasterMap,
		//	colorData = Game.tex.GetRawTextureData<Color32>()
		//}.Schedule(inputDeps);

		return base.OnUpdate(inputDeps);
	}

	private int lastUpdate = -1;
	public override void MainThreadSimulationCallbackTick()
	{
		if (addTiles.Length > 0)
		{
			for (int i = 0; i < addTiles.Length; i++)
			{
				ConstructionSystem.GetConstructor<HotFloorConstructor>().AttemptInitOnTiles(false, new BasicPosition { tile = addTiles[i] });
			}
			addTiles.Clear();
		}
		if (ECSHandler.tickCount % 50 == 0 && ECSHandler.tickCount <= 1500 && Variance.Chance(0.5f)) // 30 seconds
		{
			// Try to spawn a FloorHeater somewhere
			PFTile rand = new PFTile((ushort)Variance.NextInt(Map.SIZE_X), 0, (ushort)Variance.NextInt(Map.SIZE_Z));
			if (Game.map.GetWalkingHeight(rand) >= 1)
			{
				rand.y = Game.map.GetWalkingHeight(rand);
				ConstructionSystem.GetConstructor<FloorHeaterConstructor>().AttemptInitOnTiles(false, new BasicPosition { tile = rand }, new FloorHeater { heatRate = 200f, deltaHeatRate = Variance.Range(-2, 2f) });
			}
		}
		if (System.Environment.TickCount - lastUpdate >= 50)
		{
			Game.tex.Apply();
			lastUpdate = System.Environment.TickCount;
		}
	}
}*/
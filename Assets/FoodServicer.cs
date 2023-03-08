using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public struct FoodServicer : IComponentData
{
	// Check for people at all the locations (people will end up there through the velocity field)
	// Refill their hunger and stop them for a bit?
	
}

public class FoodServicerConstructor : BuildingConstructor
{
	public override void AddComponentTypes()
	{
		base.AddComponentTypes();
		types.Add(typeof(FoodServicer));
		types.Add(typeof(ResourceStorage));
		types.Add(typeof(BeltTransfer));
	}

	protected override void OnConstructed(Entity entity)
	{
		base.OnConstructed(entity);
		Building building = entity.Get<Building>();

		PFSegment segment = new PFSegment
		{
			from = building.GetNode(0, 0, -1, Dir.Forward, PFR.Belt),
			i = PFNext.Belt_Importer
		};

		Entity beltExport = ConstructionSystem.GetConstructor<BeltConstructor>().InitEntityDirectly(
			new Segment { segment = segment, to = segment.from.PFNextNode((byte)segment.i, ref Game.map) });
		entity.Buffer<BeltTransfer>().Add(new BeltTransfer { beltEntity = beltExport, exporter = false });

		World.Active.GetExistingSystem<PersonTensionSystem>().hungerList.Add(new TileVel { goal = entity, tile = building.GetTile(0, 0, 0) });
	}

	protected override void InitRender(Entity entity, bool facadeOrConstructing)
	{
		base.InitRender(entity, facadeOrConstructing);
		ResourceStorage storage = entity.Get<ResourceStorage>();
		storage.display = entity.AddSubRenderer(entity.Get<Building>().CenterBottom(), entity.Get<Building>().Rotation(), RenderInfo.self.resourceDisplayObject, facadeOrConstructing, RenderInfo.HotFloor);
		EntityManager.AddComponentData(storage.display, new NonUniformScale { Value = new float3(1, 0, 1) });
		entity.SetData(storage);
	}

	protected override void OnDestroy(Entity entity)
	{
		base.OnDestroy(entity);
	}

	protected override Mesh GetMesh(Entity entity)
	{
		return RenderInfo.self.foodServicerObject;
	}
}
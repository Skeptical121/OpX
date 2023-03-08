using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

struct TrainStationBuilding : IComponentData
{
	public Entity railTrainStation;
}

public class TrainStationConstructor : BuildingConstructor
{
	public override void AddComponentTypes()
	{
		base.AddComponentTypes();
		types.Add(typeof(ResourceStorage));
		types.Add(typeof(BeltTransfer));
		types.Add(typeof(TrainStationBuilding));
		// Hmm:
		// types.Add(typeof(Translation));
		// types.Add(typeof(Rotation));
		// types.Add(typeof(PhysicsCollider));
	}

	protected override void OnConstructed(Entity entity)
	{
		base.OnConstructed(entity);
		Building building = entity.Get<Building>();


		PFSegment rail = new PFSegment { from = building.GetNode(0, 0, -1, Dir.Forward, PFR.Rail), i = PFNext.Rail_TrainStation };
		Entity trainStation = ConstructionSystem.GetConstructor<RailConstructor>().InitEntityDirectly(
			new Segment { segment = rail, to = rail.from.PFNextNode((byte)rail.i, ref Game.map) });
		trainStation.AddData(new TrainStation { actualTrainStation = entity });

		entity.Modify((ref TrainStationBuilding tsb) => tsb.railTrainStation = trainStation );

		// Segment s = entity.Get<Segment>();

		// entity.SetData(new BasicPosition { tile = s.segment.from.tile.GetToTile(s.segment.from.dir) });
		// Game.map.AddOrRemoveEntity<ResourcesForSaversRef>(entity, true);
		// Game.disasterMap.resourceLocations.AddRange(new NativeArray<PFTile>(entity.Get<Segment>().GetTilesTaken().ToArray(), Allocator.Temp));

		PFSegment segment = new PFSegment
		{
			from = building.GetNode(1, 0, 0, Dir.Right, PFR.Belt), i = PFNext.Belt_Exporter
		};

		Entity beltExport = ConstructionSystem.GetConstructor<BeltConstructor>().InitEntityDirectly(
			new Segment { segment = segment, to = segment.from.PFNextNode((byte)segment.i, ref Game.map) });
		entity.Buffer<BeltTransfer>().Add(new BeltTransfer { beltEntity = beltExport, exporter = true });
	}

	protected override void OnDestroy(Entity entity)
	{
		ConstructionSystem.GetConstructor<RailConstructor>().Destroy(entity.Get<TrainStationBuilding>().railTrainStation);
		// Game.map.AddOrRemoveEntity<ResourcesForSaversRef>(entity, false);
		base.OnDestroy(entity);
	}

	protected override void InitRender(Entity entity, bool facadeOrConstructing)
	{
		base.InitRender(entity, facadeOrConstructing);
		// float3 pos = entity.Get<Segment>().segment.from.ConnectionPoint();
		// quaternion rot = quaternion.LookRotation(entity.Get<Segment>().segment.from.ConnectionNormal(), new float3(0, 1, 0));
		// entity.AddSubRenderer(pos, rot, RenderInfo.self.trainStationObject, facadeOrConstructing, RenderInfo.Building);
		ResourceStorage storage = entity.Get<ResourceStorage>();
		storage.display = entity.AddSubRenderer(entity.Get<Building>().CenterBottom(), entity.Get<Building>().Rotation(), RenderInfo.self.resourceDisplayObject, facadeOrConstructing, RenderInfo.HotFloor);
		EntityManager.AddComponentData(storage.display, new NonUniformScale { Value = new float3(1, 0, 1) });
		entity.SetData(storage);
	}

	// Override RailConstructor
	protected override BuildRule GetBuildRule()
	{
		return BuildRule.Normal;
	}

	protected override Mesh GetMesh(Entity entity)
	{
		return RenderInfo.self.trainStationObject;
	}
}

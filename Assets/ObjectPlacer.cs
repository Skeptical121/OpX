using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Ray = Unity.Physics.Ray;
using RaycastHit = Unity.Physics.RaycastHit;
using UnityEngine;
using Unity.Physics.Systems;

public static class ObjectPlacer
{
	private static string buildingType = "Wall";
	private static PFNode placingRail = PFNode.Invalid;

	private static PFTile placingTile = PFTile.Invalid;

    // Update is called once per frame
    public static void Update()
    {
		if (Input.GetMouseButtonDown(0))
		{
			Debug.Log("Mouse press");
			// Recover this tile...
			if (MouseRayCast(out RaycastHit hitInfo))
			{
				// Debug.Log("Ray cast: " + Map.GetTile(hitInfo.Position) + " at " + hitInfo.Position);
				hitInfo.Position += new float3(0, 0.01f, 0);

				if (buildingType.Equals("TrainStation"))
				{
					PFTile tile = Map.GetTile(hitInfo.Position);
					Entity entity = ConstructionSystem.GetConstructor<TrainStationConstructor>().AttemptInitOnTiles(true,
					new Building(tile, new uint3(2, 1, 3), Dir.Forward), new ResourceStorage { type = ResourceType.Food, numResources = 750, maxResources = 1000 });

					if (entity != Entity.Null)
						entity.Modify((ref Constructing c) => c.progress = -1);
				}
				else if (buildingType.Equals("FoodServicer"))
				{
					PFTile tile = Map.GetTile(hitInfo.Position);
					Entity entity = ConstructionSystem.GetConstructor<FoodServicerConstructor>().AttemptInitOnTiles(true,
					new Building(tile, new uint3(3, 1, 1), Dir.Forward), new ResourceStorage { numResources = 0, maxResources = 1000 });

					if (entity != Entity.Null)
						entity.Modify((ref Constructing c) => c.progress = -1);
				}
				else if (buildingType.Equals("Stairs"))
				{
					PFTile tile = Map.GetTile(hitInfo.Position);
					Entity entity = ConstructionSystem.GetConstructor<StairsConstructor>().AttemptInitOnTiles(true,
					new Building(tile, new uint3(1, 1, 1), Dir.Forward));

					if (entity != Entity.Null)
						entity.Modify((ref Constructing c) => c.progress = -1);
				}
				else if (buildingType.Equals("Wall"))
				{
					placingTile = Map.GetTile(hitInfo.Position);
				}
				else if (buildingType.Equals("Belt"))
				{
					PFNode node = Map.GetNode(hitInfo.Position, PFR.Belt);
					if (placingRail.IsValid(ref Game.map))
					{
						PathFinder.DestroyFakeContainer();
						PathFinder.BuildRoute<BeltConstructor>(placingRail, node);
						placingRail = PFNode.Invalid;
					}
					else
					{
						placingRail = node;
					}
				}
				else
				{
					PFNode node = Map.GetNode(hitInfo.Position, PFR.Rail);
					if (placingRail.IsValid(ref Game.map))
					{
						PathFinder.DestroyFakeContainer();
						PathFinder.BuildRoute<RailConstructor>(placingRail, node);
						placingRail = PFNode.Invalid;
					}
					else
					{
						placingRail = node;
					}
				}
			}
		}

		if (Input.GetMouseButtonUp(0))
		{
			if (MouseRayCast(out RaycastHit hitInfo))
			{
				if (buildingType.Equals("Wall") && placingTile.IsValid())
				{
					PFTile tile = Map.GetTile(hitInfo.Position);
					placingTile.y = Game.map.GetHeightIndex(placingTile);
					tile.y = Game.map.GetHeightIndex(tile);
					PathFinder.WallPathFind(placingTile, tile);
				}
			}
		}

		if (Input.GetKeyDown(KeyCode.Y))
		{
			buildingType = "TrainStation";
		}
		if (Input.GetKeyDown(KeyCode.P))
		{
			buildingType = "Belt";
		}
		if (Input.GetKeyDown(KeyCode.O))
		{
			buildingType = "FoodServicer";
		}
		if (Input.GetKeyDown(KeyCode.I))
		{
			buildingType = "Stairs";
		}
		if (Input.GetKeyDown(KeyCode.U))
		{
			buildingType = "Wall";
		}
	}
	public static bool MouseRayCast(out RaycastHit h)
	{
		UnityEngine.Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
		Ray r = new Ray { Origin = ray.origin, Displacement = ray.direction * 1000f };

		PhysicsWorld world = World.Active.GetOrCreateSystem<BuildPhysicsWorld>().PhysicsWorld;
		if (world.CollisionWorld.CastRay(new RaycastInput { Start = r.Origin, End = r.Origin + r.Displacement, Filter = CollisionFilter.Default }, out RaycastHit hit))
		{
			// Debug.Log(hit.RigidBodyIndex);
			// Debug.Log(world.CollisionWorld.Bodies[hit.RigidBodyIndex].Entity);
			h = hit;
			return true;
		}
		h = new RaycastHit();
		return false;
	}

	public static void FixedUpdate()
	{
		DebugDraw.DisplayMessage(ECSHandler.stringBuilder.Append("Building type = ").Append(buildingType));
		if (placingRail.IsValid(ref Game.map) && buildingType.Equals("Belt"))
		{
			if (MouseRayCast(out RaycastHit hitInfo))
			{

				// Issue pathfind request:
				PFNode node = Map.GetNode(hitInfo.Position, PFR.Belt);
				PathFinder.BuildRoute<BeltConstructor>(placingRail, node, true);
			}
		}
	}
}

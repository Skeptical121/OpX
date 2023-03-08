using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public struct Wall : IComponentData
{
	public float height;
	public float strength;
}

public class WallConstructor : BuildingConstructor
{
	public override void AddComponentTypes()
	{
		base.AddComponentTypes();
		types.Add(typeof(Wall));
	}

	protected override void OnConstructed(Entity entity)
	{
		base.OnConstructed(entity);

		// Update water table:
		PFTile tile = entity.Get<Building>().GetTile(0, 0, 0);
		FluidSim.SetGround(tile, Game.map.GetHeight(tile) + entity.Get<Wall>().height);
	}

	protected override Mesh GetMesh(Entity entity)
	{
		return MeshCreator.Rectangle(new float3(PFTile.LENGTH, entity.Get<Wall>().height, PFTile.LENGTH));
	}

	public bool AttemptAddWallHeight(PFTile tile, float addHeight)
	{
		tile = new PFTile(tile.x, Game.map.GetHeightIndex(tile), tile.z);
		Entity wallEntity = Game.map.GetEntity(tile, true);
		if (wallEntity != Entity.Null)
		{
			return AttemptSetWallHeight(tile, wallEntity.Get<Wall>().height + addHeight);
		}
		return AttemptSetWallHeight(tile, addHeight);
	}

	public bool AttemptSetWallHeight(PFTile tile, float newHeight)
	{
		tile = new PFTile(tile.x, Game.map.GetHeightIndex(tile), tile.z);
		Entity wallEntity = Game.map.GetEntity(tile, true);
		uint buildingHeight = (uint)math.ceil(newHeight / PFTile.HEIGHT);
		if (wallEntity == Entity.Null)
		{
			if (newHeight > 0)
				return AttemptInitOnTiles(false,
					new Building(tile, new uint3(1, buildingHeight, 1), Dir.Forward), new Wall { height = newHeight, strength = 1f }) != Entity.Null;
			else
				return true; // hmm
		}
		
		if (newHeight == 0)
		{
			Destroy(wallEntity);
			return true;
		}

		Building building = wallEntity.Get<Building>();
		List<PFTile> tilesTaken = new List<PFTile>();
		for (uint y = building.GetSize(1); y < buildingHeight; y++)
		{
			if (!Game.map.IsBuildable(new PFTile(building.GetTile(0, 0, 0).x, (byte)(building.GetTile(0, 0, 0).y + y), building.GetTile(0, 0, 0).z), GetBuildRule()))
			{
				return false;
			}
			tilesTaken.Add(new PFTile(building.GetTile(0, 0, 0).x, (byte)(building.GetTile(0, 0, 0).y + y), building.GetTile(0, 0, 0).z));
		}

		AddTilesTaken(wallEntity, tilesTaken); // Might be nothing...
		for (uint y = building.GetSize(1) - 1; y >= building.GetTile(0, 0, 0).y + buildingHeight; y--)
		{
			RemoveTile(wallEntity, new PFTile(building.GetTile(0, 0, 0).x, (byte)(building.GetTile(0, 0, 0).y + y), building.GetTile(0, 0, 0).z));
		}

		wallEntity.Modify((ref Wall wall) => { wall.height = newHeight; });

		building.SetSize(1, buildingHeight);
		wallEntity.SetData(building);

		// Update water table:
		FluidSim.SetGround(tile, Game.map.GetHeight(tile) + wallEntity.Get<Wall>().height);

		// Update render
		DeleteRender(wallEntity);
		InitRender(wallEntity, false);

		return true;
	}
}

public class WallSystem
{
	
}
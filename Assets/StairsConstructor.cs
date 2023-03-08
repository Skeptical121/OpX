using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public struct WalkInfo : IComponentData
{
	public Dir dir;
	public float low;
	public float high;
	public float height0; // Easily interpolated
	public float height1;

	public float GetHeight(float3 pos)
	{
		float val;
		if (dir == Dir.Forward || dir == Dir.Back)
			val = pos.z;
		else
			val = pos.x;
		return math.lerp(height0, height1, math.clamp((val - low) / (high - low), 0, 1)); // height0 + (val - low) / (high - low) * (height1 - height0);
	}
}

public class StairsConstructor : BuildingConstructor
{
	public override void AddComponentTypes()
	{
		base.AddComponentTypes();
		types.Add(typeof(WalkInfo));
	}

	protected override void InitRender(Entity entity, bool facadeOrConstructing)
	{
		base.InitRender(entity, facadeOrConstructing);
		// entity.Buffer<SubMeshRenderer>()[0].renderer.
	}

	protected override void OnConstructed(Entity entity)
	{
		base.OnConstructed(entity);
		Building b = entity.Get<Building>();
		PFTile tile = b.GetTile(0, 0, 0);
		WalkInfo w = new WalkInfo { dir = b.GetDir(), low = b.GetPos(false, -1), high = b.GetPos(false, 1), height0 = tile.y * PFTile.HEIGHT, height1 = (tile.y + 1) * PFTile.HEIGHT };
		entity.SetData(w);

		Game.map.SetWalkable(tile, w.dir, true);
		Game.map.SetWalkable(tile, w.dir.Flip(), true);
		Game.map.SetWalkable(tile, w.dir.RotateDir(true), false);
		Game.map.SetWalkable(tile, w.dir.RotateDir(false), false);


		// Debug.Log("Low: " + w.low + ", " + w.high + ", " + w.height0 + ", " + w.height1);



		// Game.map.SetNodeConnection(b.GetNode(0, 0, 0, Dir.Forward, PFR.Stairs));
		/*float3 worldPos = Map.WorldPosition(tile.x, tile.y, tile.z);
		for (float x = worldPos.x; x < worldPos.x + PFTile.LENGTH; x += 1f)
		{
			for (float z = worldPos.z; z < worldPos.z + PFTile.LENGTH; z += 1f)
			{
				Debug.Log(x + ", " + z + ": " + w.GetHeight(new float3(x, 0, z)));
			}
		}*/
	}

	protected override Mesh GetMesh(Entity entity)
	{
		return RenderInfo.self.stairsObject;
	}
}

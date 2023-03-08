using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

public struct Building : IComponentData, ITilesTaken
{
	private PFTile bottomLeftBack; // Assuming dir is forward?
	private uint3 size;
	private Dir dir;

	public Building(PFTile bottomLeftBack, uint3 size, Dir dir)
	{
		this.bottomLeftBack = bottomLeftBack;
		this.size = size;
		this.dir = dir;
	}

	public uint GetSize(int dim)
	{
		return size[dim];
	}
	public void SetSize(int dim, uint val)
	{
		size[dim] = val;
	}

	// Because buildings can be rotated, getting a tile isn't so trivial
	public PFTile GetTile(int x, int y, int z)
	{
		PFTile tile = bottomLeftBack;
		for (int i = 0; i < math.abs(z); i++)
			tile = tile.GetToTile(z >= 0 ? dir : dir.Flip());

		for (int i = 0; i < math.abs(x); i++)
			tile = tile.GetToTile(dir.RotateDir(x >= 0));

		for (int i = 0; i < math.abs(y); i++)
			tile = tile.GetToTile(y >= 0 ? Dir.Up : Dir.Down);
		return tile;
	}

	public PFNode GetNode(int x, int y, int z, Dir dirOffset, PFR pfr)
	{
		return new PFNode(GetTile(x, y, z), dir.Add((byte)dirOffset), pfr);
	}

	public float3 GetPos(float3 offset)
	{
		return CenterBottom() + math.mul(dir.Rotation(), offset * size * PFTile.LENGTH * 0.5f);
	}

	public float GetPos(bool isX, float offset)
	{
		float3 pos = GetPos(new float3(isX ? offset : 0, 0, isX ? 0 : offset));
		if (!isX == (dir == Dir.Forward || dir == Dir.Back))
			return pos.z;
		else
			return pos.x;
	}

	public float3 CenterBottom()
	{
		if (dir == Dir.Forward)
			return Map.WorldPosition(bottomLeftBack.x + size.x * 0.5f, bottomLeftBack.y, bottomLeftBack.z + size.z * 0.5f);
		else if (dir == Dir.Right)
			return Map.WorldPosition(bottomLeftBack.x + size.z * 0.5f, bottomLeftBack.y, bottomLeftBack.z - size.x * 0.5f);
		else if (dir == Dir.Back)
			return Map.WorldPosition(bottomLeftBack.x - size.x * 0.5f, bottomLeftBack.y, bottomLeftBack.z - size.z * 0.5f);
		else // Left
			return Map.WorldPosition(bottomLeftBack.x - size.z * 0.5f, bottomLeftBack.y, bottomLeftBack.z + size.x * 0.5f);
	}
	public quaternion Rotation()
	{
		return dir.Rotation();
	}
	public List<PFTile> GetTilesTaken()
	{
		return EntireBorderInfo.GetTilesIfValid(bottomLeftBack, size, BuildRule.Normal);
	}
	public Dir GetDir()
	{
		return dir;
	}
}

public enum ResourceType : short
{
	Food
}

public struct ResourceStorage : IComponentData
{
	public ResourceType type;
	public float numResources;
	public float maxResources;
	public Entity display;

	public bool TransferMax(ref ResourceStorage to, ResourceType type)
	{
		float amount = math.min(numResources, to.maxResources - to.numResources);
		if (amount > 0 && CanTake(type, amount) && to.CanAdd(type, amount))
		{
			TransferTo(ref to, amount);
			return true;
		}
		return false;
	}

	public bool CanTake(ResourceType type, float amount)
	{
		if (this.type == type)
			return numResources >= amount;
		else
			return false;
	}

	public bool CanAdd(ResourceType type, float amount)
	{
		if (this.type == type || numResources == 0)
			return numResources + amount <= maxResources;
		else
			return false;
	}

	public void TransferTo(ref ResourceStorage to, float amount)
	{
		numResources -= amount;
		to.type = type;
		to.numResources += amount;
	}
}
/*
public struct BuildingRail : IBufferElementData
{
	public Entity entity;
	public static List<PFNode> GetPFNodes(Entity entity, PFR pfr, bool outFacing)
	{
		DynamicBuffer<BuildingRail> rails = entity.Buffer<BuildingRail>();
		List<PFNode> nodes = new List<PFNode>(rails.Length);
		for (int i = 0; i < rails.Length; i++)
		{
			PFNode node;
			if (outFacing)
				node = rails[i].entity.Get<Segment>().to;
			else
				node = rails[i].entity.Get<Segment>().segment.from;

			if (node.pfr == pfr)
				nodes.Add(node);
		}
		return nodes;
	}
}*/

public abstract class BuildingConstructor : Constructor
{
	public override void AddComponentTypes()
	{
		types.Add(typeof(Building));
		types.Add(typeof(Translation));
		types.Add(typeof(Rotation));
		types.Add(typeof(PhysicsCollider));
	}

	protected override void OnConstructed(Entity entity)
	{
	}

	protected abstract Mesh GetMesh(Entity entity);

	protected override void InitRender(Entity entity, bool facadeOrConstructing)
	{
		Building building = EntityManager.GetComponentData<Building>(entity);
		Mesh mesh = GetMesh(entity);

		Entity renderer = entity.AddSubRenderer(building.CenterBottom(), building.Rotation(), mesh, facadeOrConstructing, facadeOrConstructing ? RenderInfo.Facade : RenderInfo.Building);
			
		EntityManager.CreateEntity(ConstructionSystem.subMeshRenderer);

		// TODO we need to rotate these points...
		BlobAssetReference<Unity.Physics.Collider> collider = Unity.Physics.MeshCollider.Create(
			new NativeArray<float3>(Array.ConvertAll(mesh.vertices, (item) => (float3)item + building.CenterBottom()), Allocator.Temp),
			new NativeArray<int>(mesh.triangles, Allocator.Temp));

		if (!facadeOrConstructing)
		{
			entity.SetData(new PhysicsCollider { Value = collider });
			// EntityManager.SetSharedComponentData(renderer, new RenderMesh { mesh = mesh, material = RenderInfo.Building });
		}
		else
		{
			// EntityManager.SetSharedComponentData(renderer, new RenderMesh { mesh = mesh, material = RenderInfo.Facade });
		}
	}

	protected override void OnDestroy(Entity entity)
	{
		for (int i = 0; i < entity.Buffer<BeltTransfer>().Length; i++)
		{
			World.Active.GetExistingSystem<ConstructionSystem>().DestroyEntity(entity.Buffer<BeltTransfer>()[i].beltEntity);
		}
	}

	protected override float GetConstructionCost()
	{
		return 100f;
	}
}
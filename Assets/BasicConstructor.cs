using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

public class HotFloorConstructor : BasicConstructor<HotFloor, HotFloorRef>
{
	protected override BuildRule GetBuildRule()
	{
		return BuildRule.HotFloor;
	}
	protected override Mesh GetMesh()
	{
		return RenderInfo.self.hotFloorObject;
	}
}
public class FloorHeaterConstructor : BasicConstructor<FloorHeater, FloorHeaterRef>
{
	protected override Mesh GetMesh()
	{
		return RenderInfo.self.floorHeaterObject;
	}
}

// BasicConstructors do not have a facade state!
public abstract class BasicConstructor<T, TRef> : Constructor where TRef : struct, IBufferElementData, IBufferEntity
{
	public override void AddComponentTypes()
	{
		types.Add(typeof(T));
		types.Add(typeof(BasicPosition));
	}

	protected abstract Mesh GetMesh();

	protected override void InitRender(Entity entity, bool facadeOrConstructing)
	{
		Entity renderer = EntityManager.CreateEntity(ConstructionSystem.subMeshRenderer);
		entity.Buffer<SubMeshRenderer>().Add(new SubMeshRenderer { renderer = renderer });

		renderer.SetData(new Translation { Value = entity.Get<BasicPosition>().CenterBottom() });

		EntityManager.SetSharedComponentData(renderer, new RenderMesh { mesh = GetMesh(), material = RenderInfo.HotFloor });
	}

	protected override void OnConstructed(Entity entity)
	{
		Game.map.AddOrRemoveEntity<TRef>(entity, true);
	}

	protected override void OnDestroy(Entity entity)
	{
		Game.map.AddOrRemoveEntity<TRef>(entity, false);
	}
}
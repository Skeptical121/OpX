using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

public interface ITilesTaken
{
	float3 CenterBottom();
	List<PFTile> GetTilesTaken();
}

public abstract class Constructor
{
	public static EntityManager EntityManager;

	public List<ComponentType> types = new List<ComponentType>();
	public ComponentTypes components;
	public ushort id;

	public abstract void AddComponentTypes();
	protected virtual BuildRule GetBuildRule()
	{
		return BuildRule.Normal;
	}

	public virtual void StartConstructing(Entity entity)
	{
		entity.Modify((ref Constructing c) =>
		{
			c.facade = false;
			c.progress = 0;
		});
		EntityManager.AddComponentData(entity, new Health { health = 5f });
		// No need to do validity check again...
	}

	public void Destroy(Entity entity)
	{
		OnDestroy(entity);
		DeleteRender(entity);
		// DynamicBuffer<TileTaken> tilesTaken = EntityManager.GetBuffer<TileTaken>(entity);
		if (Game.tilesTaken.ContainsKey(entity))
		{
			var enumerator = Game.tilesTaken.GetValuesForKey(entity);
			foreach (var tileTaken in enumerator)
			{
				Game.map.UnsetEntity(tileTaken, entity, GetBuildRule());
			}
		}
		Game.tilesTaken.Remove(entity);
		EntityManager.DestroyEntity(entity);
	}

	protected void DeleteRender(Entity entity)
	{
		for (int i = 0; i < entity.Buffer<SubMeshRenderer>().Length; i++)
		{
			EntityManager.DestroyEntity(entity.Buffer<SubMeshRenderer>()[i].renderer);
		}
		entity.Buffer<SubMeshRenderer>().Clear();
	}

	protected abstract void OnDestroy(Entity entity);
	protected virtual float GetConstructionCost()
	{
		return 50f;
	}
	protected abstract void OnConstructed(Entity entity);
	protected abstract void InitRender(Entity entity, bool facadeOrConstructing);


	// You can't create a facade where there is an entity already...
	public Entity AttemptInitOnTiles<T>(bool facade, T tilesTakenComponent, params IComponentData[] presetComponents) where T : struct, IComponentData, ITilesTaken
	{
		List<PFTile> tilesTaken = tilesTakenComponent.GetTilesTaken();
		if (tilesTaken != null && IsValid(tilesTaken))
		{
			Entity entity = facade ? InitFacade(tilesTakenComponent, presetComponents) : InitEntityDirectly(tilesTakenComponent, presetComponents);
			AddTilesTaken(entity, tilesTaken);
			return entity;
		}
		return Entity.Null;
	}

	protected void AddTilesTaken(Entity entity, List<PFTile> tilesTaken)
	{
		for (int i = 0; i < tilesTaken.Count; i++)
		{
			Assert.IsTrue(Game.map.IsBuildable(tilesTaken[i], GetBuildRule()));
			Game.map.SetEntity(tilesTaken[i], entity, GetBuildRule());
			Game.tilesTaken.Add(entity, tilesTaken[i]);
		}
	}

	protected void RemoveTile(Entity entity, PFTile tileTaken)
	{
		Assert.IsTrue(Game.map.GetEntity(tileTaken, true) == entity);
		Game.map.UnsetEntity(tileTaken, entity, GetBuildRule());
		Game.tilesTaken.Remove(entity, tileTaken);
	}

	private Entity InitFacade<T>(T tilesTakenComponent, IComponentData[] presetComponents) where T : struct, IComponentData, ITilesTaken
	{
		Entity entity = EntityManager.CreateEntity(EntityManager.World.GetExistingSystem<ConstructionSystem>().facadeArchetype);
		EntityManager.AddComponents(entity, components);
		EntityManager.SetComponentData(entity, new Constructing { facade = true, progress = 0, progressToComplete = GetConstructionCost() });
		EntityManager.SetComponentData(entity, new Constructable { constructableID = id });
		EntityManager.SetComponentData(entity, tilesTakenComponent);
		PresetComponents(entity, presetComponents);
		InitRender(entity, true); // If is visible
		return entity;
	}

	private void PresetComponents(Entity entity, IComponentData[] presetComponents)
	{
		for (int i = 0; i < presetComponents.Length; i++)
		{
			dynamic comp = presetComponents[i];
			EntityManager.SetComponentData(entity, comp); // Extension methods cannot be dynamically dispatched
		}
	}

	public Entity InitEntityDirectly<T>(T tilesTakenComponent, params IComponentData[] presetComponents) where T : struct, IComponentData, ITilesTaken
	{
		Entity entity = EntityManager.CreateEntity(typeof(SubMeshRenderer));
		EntityManager.AddComponents(entity, components);
		EntityManager.AddComponentData(entity, new Constructable { constructableID = id });
		EntityManager.SetComponentData(entity, tilesTakenComponent);
		PresetComponents(entity, presetComponents);

		// Alternative to FinishConstructing:
		OnConstructed(entity);
		InitRender(entity, false); // If visible
		return entity;
	}

	private bool IsValid(List<PFTile> tilesTaken)
	{
		Assert.IsTrue(tilesTaken.Count >= 1, "Must be at least one tile taken");
		for (int i = 0; i < tilesTaken.Count; i++)
		{
			if (!Game.map.IsBuildable(tilesTaken[i], GetBuildRule()))
				return false;
		}
		return true;
	}

	public void FinishConstructing(Entity entity)
	{
		// The entity changes into what it's supposed to be...
		EntityManager.RemoveComponent<Constructing>(entity);
		// EntityManager.AddComponents(entity, components);

		OnConstructed(entity);

		DeleteRender(entity);
		InitRender(entity, false);
	}
}
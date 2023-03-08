using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public struct Inserter : IComponentData
{
	public Dir dir;
	public float rotation;
	public bool rotatingTo;
}

public class InserterConstructor : BuildingConstructor
{
	public override void AddComponentTypes()
	{
		base.AddComponentTypes();
		types.Add(typeof(Inserter));
		types.Add(typeof(ResourceStorage));
	}

	protected override Mesh GetMesh(Entity entity)
	{
		throw new NotImplementedException();
	}

	protected override void OnConstructed(Entity entity)
	{
		base.OnConstructed(entity);
	}
}

public class StorageConstructor : BuildingConstructor
{
	public override void AddComponentTypes()
	{
		base.AddComponentTypes();
		types.Add(typeof(ResourceStorage));
	}

	protected override Mesh GetMesh(Entity entity)
	{
		throw new NotImplementedException();
	}

	protected override void OnConstructed(Entity entity)
	{
		base.OnConstructed(entity);
		// Game.disasterMap.resourceLocations.Add(entity.Get<Building>().bottomLeftBack);
	}
}
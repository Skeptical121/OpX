using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;

public class RailConstructor : SegmentConstructor
{
	// private EntityArchetype railArchetype;
	private EntityArchetype railZoneArch;

	public override void AddComponentTypes()
	{
		base.AddComponentTypes();
		railZoneArch = EntityManager.CreateArchetype(typeof(RailZone));
		types.Add(typeof(RailSection));
	}


	protected override BuildRule GetBuildRule()
	{
		return BuildRule.Rail;
	}

	protected override float GetConstructionCost()
	{
		return 3f;
	}

	protected override void OnConstructed(Entity entity)
	{
		Entity railZone = EntityManager.CreateEntity(railZoneArch); // , typeof(RailZoneRail));
		RailSection railSection = new RailSection { railZone = railZone };
		entity.SetData(railSection);
		railZone.SetData(new RailZone { numTaken = 0, numRails = 1 });
		base.OnConstructed(entity);
	}

	protected override void OnDestroy(Entity entity)
	{
		base.OnDestroy(entity);

		RailSection rail = entity.Get<RailSection>();
		RailZone railZone = rail.railZone.Get<RailZone>();
		railZone.numRails--;
		if (railZone.numRails == 0)
		{
			EntityManager.DestroyEntity(rail.railZone);
		}
	}
}

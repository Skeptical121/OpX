using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class BeltConstructor : SegmentConstructor
{
	public override void AddComponentTypes()
	{
		base.AddComponentTypes();
		types.Add(typeof(Belt));
		types.Add(typeof(BeltObject));
	}

	protected override float GetConstructionCost()
	{
		return 1f;
	}

	// So index isn't actually used.. returns true if loops
	private bool SetIndex(Entity start, Entity entity/*, int index*/)
	{
		if (start == entity) // Loops
			return true;
		// entity.Modify((ref Belt belt) => { belt.index = index });
		Entity next = entity.Get<Segment>().next;
		if (next != Entity.Null)
			return SetIndex(start, next/*, index + 1*/);
		// else
		// 	SetLast(entity, true);
		return false;
	}

	private void RemoveLastFromLoop(Entity entity)
	{
		if (World.Active.GetExistingSystem<BeltSystem>().endBelts.Remove(entity))
			return;
		Entity next = entity.Get<Segment>().next;
		if (next != Entity.Null)
			RemoveLastFromLoop(next);
	}

	private void SetLast(Entity entity, bool set)
	{
		if (entity != Entity.Null)
		{
			if (set)
				World.Active.GetExistingSystem<BeltSystem>().endBelts.Add(entity);
			else
				World.Active.GetExistingSystem<BeltSystem>().endBelts.Remove(entity);
		}
	}

	protected override void OnConstructed(Entity entity)
	{
		base.OnConstructed(entity);
		entity.SetData(new Belt { speed = 3f });

		Segment segment = entity.Get<Segment>();
		if (segment.previous == Entity.Null)
		{
			SetIndex(Entity.Null, entity/*, 0*/);
		}
		else
		{
			SetLast(segment.previous, false);
			if (SetIndex(segment.previous, entity/*, behind.Get<Belt>().index + 1*/))
			{
				// Loops mean that we need some arbitrary belt to be the "end"
				SetLast(entity, true);
			}
		}
		if (segment.next == Entity.Null)
		{
			SetLast(entity, true);
		}
	}

	protected override void OnDestroy(Entity entity)
	{
		// Don't have to change index for segment behind...
		Segment segment = entity.Get<Segment>();
		if (segment.next != Entity.Null)
		{
			if (SetIndex(entity, segment.next/*, 0*/))
			{ // In case it was a loop...
				RemoveLastFromLoop(segment.next);
			}
		}
		else
		{
			SetLast(entity, false);
		}
		SetLast(segment.previous, true); // Only update index for the one behind for JUST the one behind

		base.OnDestroy(entity);
	}
}
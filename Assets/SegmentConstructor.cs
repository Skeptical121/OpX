using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;


public struct Segment : IComponentData, ITilesTaken
{
	public float distance;
	public PFSegment segment;
	public PFNode to; // hmm
	public Entity previous;
	public Entity next;

	public Entity this[bool isTo]
	{
		get => isTo ? next : previous;
		set { if (isTo) next = value; else previous = value; }
	}

	public float3 CenterBottom()
	{
		throw new NotImplementedException();
	}

	public List<PFTile> GetTilesTaken()
	{
		NativeList<PFTile> tilesTaken = new NativeList<PFTile>(Allocator.Temp);
		segment.from.PFNextTilesTaken(tilesTaken, (byte)segment.i);
		List<PFTile> tiles = new List<PFTile>();
		for (int i = 0; i < tilesTaken.Length; i++)
		{
			tiles.Add(tilesTaken[i]);
		}
		return tiles;
	}
}

// Typically 1 or less...
[InternalBufferCapacity(1)]
public struct NextSegment : IBufferElementData, IEquatable<NextSegment>
{
	public Entity segment;

	public bool Equals(NextSegment other)
	{
		return segment == other.segment;
	}

	public override int GetHashCode()
	{
		return segment.GetHashCode();
	}
}

public abstract class SegmentConstructor : Constructor
{

	public override void AddComponentTypes()
	{
		types.Add(typeof(Segment));
		types.Add(typeof(PosRotRoute));
	}

	protected override BuildRule GetBuildRule()
	{
		return BuildRule.Normal;
	}

	protected override void InitRender(Entity entity, bool facadeOrConstructing)
	{
		PFSegment segment = entity.Get<Segment>().segment;
		entity.AddSubRenderer(segment.from.ConnectionPoint(), segment.from.dir.Rotation(), PresetHolder.GetPreset(segment.from.pfr, segment.i).mesh,
			facadeOrConstructing, RenderInfo.ConveyorBelt, RenderInfo.Building);
	}

	protected override void OnConstructed(Entity entity)
	{
		Segment segment = entity.Get<Segment>();

		PosRotRoute[] routeList = PresetHolder.GetPreset(segment.segment.from.pfr, segment.segment.i).route;
		NativeArray<PosRotRoute> route = new NativeArray<PosRotRoute>(routeList, Allocator.Temp);
		float3 offset = segment.segment.from.ConnectionPoint();
		quaternion rot = segment.segment.from.dir.Rotation();
		for (int i = 0; i < route.Length; i++)
		{
			PosRotRoute prr = route[i];
			prr.posRot.rot = math.mul(prr.posRot.rot, rot);
			prr.posRot.pos = math.mul(rot, prr.posRot.pos); // This should work even for multi-tile things
			prr.posRot.pos += offset;
			route[i] = prr;
		}
		entity.Buffer<PosRotRoute>().AddRange(route);

		segment.distance = route[route.Length - 1].dist;
		entity.SetData(segment);

		// railZone.Buffer<RailZoneRail>().Add(new RailZoneRail { rail = entity });
		if (GetBuildRule() != BuildRule.Rail)
		{
			if (segment.segment.i != PFNext.Belt_Exporter)
				Game.map.SetNodeConnection(segment.segment.from, entity, true, false);
			if (segment.segment.i != PFNext.Belt_Importer)
				Game.map.SetNodeConnection(segment.to, entity, true, true);
		}
	}

	protected override void OnDestroy(Entity entity)
	{
		Segment segment = entity.Get<Segment>();
		if (GetBuildRule() != BuildRule.Rail)
		{
			if (segment.segment.i != PFNext.Belt_Exporter)
				Game.map.SetNodeConnection(segment.segment.from, entity, false, false);
			if (segment.segment.i != PFNext.Belt_Importer)
				Game.map.SetNodeConnection(segment.to, entity, false, true);
		}
	}

}
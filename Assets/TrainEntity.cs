using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public struct TrainEntity : PathFinderSystem.IPathFindSection<TrainEntity>, PathFinderSystem.IPathFind<Entity, TrainEntity>
{
    public Entity entity;

	// IPathFindSection:
	public void Init(TrainEntity node, byte i)
	{
		entity = node.entity;
	}
	public TrainEntity GetNode()
	{
		return this;
	}

	// IPathFind:
	public float3 ConnectionPoint(ComponentDataFromEntity<Segment> segment)
	{
		return segment[entity].to.ConnectionPoint();
	}
	public Entity Index()
	{
		return entity;
	}
	public bool IsValid(ref Map map)
	{
		return true;
	}
	public byte NumPFNext(ref Map map, bool pathfinding, BufferFromEntity<NextSegment> next, ComponentDataFromEntity<Segment> segment)
	{
		return (byte)map.GetNodeConnections(next, segment[entity].to).Length;
	}
	public float PFNextCost(byte i, ref Map map, BufferFromEntity<NextSegment> next, ComponentDataFromEntity<Segment> segment)
	{
		return segment[map.GetNodeConnections(next, segment[entity].to)[i].segment].distance;
	}
	public TrainEntity PFNextNode(byte i, ref Map map, BufferFromEntity<NextSegment> next, ComponentDataFromEntity<Segment> segment)
	{
		return new TrainEntity { entity = map.GetNodeConnections(next, segment[entity].to)[i].segment };
	}
	public bool PFNextTilesTakenCheck(ref Map map, NativeList<PFTile> tilesTaken, byte i, BuildRule rule)
	{
		return true;
	}

	
}

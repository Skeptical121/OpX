using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

// People rarely travel very far...
[InternalBufferCapacity(0)]
public struct PFRectConnection : IBufferElementData
{
	public Entity other;
	// Duplicated data since the connection is stored twice:
	public float3 from;
	public float3 to;
}

public struct PFRect : IComponentData
{
	// This indicates a walking area, not the walking object itself

	// min.y == max.y
	public PFTile min; // Inclusive
	public PFTile max; // Inclusive

	public bool Contains(PFTile tile)
	{
		return tile.x >= min.x && tile.y == min.y && tile.z >= min.z && tile.x <= max.x && tile.z <= max.z;
	}
}
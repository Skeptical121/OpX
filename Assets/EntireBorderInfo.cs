using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

public class CornerInfo
{
	private float3 pos;
	private float3 offset;

	public CornerInfo(float3 pos, float3 offset)
	{
		this.pos = pos;
		this.offset = offset;
	}

	public bool MergeIfMergable(float3 otherCorner, float3 addNormal)
	{
		if (math.distancesq(pos, otherCorner) < 0.0001f)
		{
			AddToOffset(addNormal);
			return true;
		}
		else
		{
			return false;
		}
	}

	public float3 GetOffsetCorner(float padding)
	{
		return pos + offset * padding;
	}

	public void AddToOffset(float3 addNormal)
	{
		Assert.IsTrue(addNormal.y == 0, "addNormal.y must be 0");
		// Determine if the lines are parallel... offset is close enough to Offset.Normalize() to use here
		float3 bothOnPlane = math.cross(offset, addNormal);
		if (math.lengthsq(bothOnPlane) >= 0.0001f)
		{
			float3 tangentA = math.normalize(math.cross(offset, new float3(0, 1, 0)));
			float3 tangentB = math.normalize(math.cross(addNormal, new float3(0, 1, 0)));
			// So... now we need to see where they intersect...
			offset = VectorMath.GetIntersection(offset, addNormal, tangentA, tangentB);
		}
	}
}

public struct BorderInfo
{
	public PFNode pfn;
	public CornerInfo[] corners;
	public BorderInfo(PFNode pfn, CornerInfo c1, CornerInfo c2)
	{
		this.pfn = pfn;
		corners = new CornerInfo[] { c1, c2 };
	}
}

public class EntireBorderInfo
{

	/*private readonly List<CornerInfo> cornerInfo = new List<CornerInfo>();
	public readonly List<BorderInfo> borderInfo = new List<BorderInfo>();

	public EntireBorderInfo(PFTile startTile, Func<PFTile, bool> insideCondition, Func<PFTile, bool> validBorderCondition)
	{

		// Border Info does not risk getting created more than once I believe
		HashSet<PFTile> tilesToCheck = new HashSet<PFTile>();
		HashSet<PFTile> tilesChecked = new HashSet<PFTile>();
		tilesToCheck.Add(startTile);

		while (tilesToCheck.Count > 0)
		{
			PFTile check = tilesToCheck.First();

			tilesToCheck.Remove(check);
			tilesChecked.Add(check);
			for (byte dir = 0; dir < PFR.Horizontal.MaxDir(); dir++)
			{
				PFTile other = Game.map.GetToTile(check, dir);
				if (!insideCondition(other))
				{
					if (validBorderCondition(other))
					{
						PFNode bI = new PFNode(check, dir, (byte)PFR.Horizontal);
						float3 normal = bI.ConnectionNormal();
						float3[] corner = { Game.map.CornerPos(bI, 0), Game.map.CornerPos(bI, 1) };
						CornerInfo[] cornerPtr = { null, null };
						for (int c = 0; c < 2; c++)
						{
							foreach (CornerInfo cI in cornerInfo)
							{
								if (cI.MergeIfMergable(corner[c], normal))
								{
									cornerPtr[c] = cI;
								}
							}
							if (cornerPtr[c] == null)
							{
								cornerPtr[c] = new CornerInfo(corner[c], normal);
								cornerInfo.Add(cornerPtr[c]);
							}
						}
						borderInfo.Add(new BorderInfo(bI, cornerPtr[0], cornerPtr[1]));
					} // I don't think it needs to add the tile to tilesChecked here
				}
				else
				{
					if (!tilesToCheck.Contains(other) && !tilesChecked.Contains(other))
					{
						tilesToCheck.Add(other);
					}
				}
			}
		}
	}*/

	// It's okay if it is already in the set
	private static bool AttemptAddToSet(HashSet<PFTile> tilesTaken, PFTile tile, BuildRule rule)
	{
		if (!tile.IsValid())
			return false;
		else if (!Game.map.IsBuildable(tile, rule))
			return tilesTaken.Contains(tile); // Should this just return false?

		tilesTaken.Add(tile);
		return true;
	}

	public static List<PFTile> GetTilesIfValid(PFTile bottomLeftBack, uint3 size, BuildRule rule)
	{
		HashSet<PFTile> tilesTaken = new HashSet<PFTile>();
		for (uint x = bottomLeftBack.x; x < bottomLeftBack.x + size.x; x++)
		{
			for (uint z = bottomLeftBack.z; z < bottomLeftBack.z + size.z; z++)
			{
				for (uint y = bottomLeftBack.y; y < bottomLeftBack.y + size.y; y++)
				{
					if (!AttemptAddToSet(tilesTaken, new PFTile((ushort)x, (byte)y, (ushort)z), rule))
						return null;
				}
			}
		}
		return tilesTaken.ToList();
	}

	// The int variable is the offset
	/*public static void RadialAction(List<PFTile> tiles, int maxOffset, Action<PFTile, int> action)
	{
		OpenSetSearch(PFR.Horizontal, tiles, maxOffset, (tile, offset) => { action(tile, offset); return false; });
	}

	public static PFTile OpenSetSearch(PFR pfr, List<PFTile> tiles, int maxOffset, Func<PFTile, int, bool> conditionSatisfied)
	{
		foreach (PFTile tile in tiles)
		{
			if (conditionSatisfied(tile, 0))
				return tile;
		}
		HashSet<PFTile> closedSet = new HashSet<PFTile>();
		HashSet<PFTile> nextOpenSet = new HashSet<PFTile>(tiles);
		HashSet<PFTile> openSet;
		for (int i = 0; i < maxOffset; i++)
		{
			foreach (PFTile check in nextOpenSet)
			{
				closedSet.Add(check);
			}
			openSet = nextOpenSet;
			nextOpenSet = new HashSet<PFTile>();
			foreach (PFTile check in openSet)
			{
				for (byte dir = 0; dir < pfr.MaxDir(); dir++)
				{
					PFTile next = Game.map.GetToTile(check, dir);
					if (next.IsValid())
					{
						if (!nextOpenSet.Contains(next) && !closedSet.Contains(next))
						{
							if (conditionSatisfied(next, i + 1))
								return next;
							nextOpenSet.Add(next);
						}
					}
				}
			}
		}
		return PFTile.Invalid;
	}*/
}

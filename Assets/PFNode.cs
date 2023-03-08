using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public struct PFSegment : PathFinderSystem.IPathFindSection<PFNode>
{
	public PFNode from;
	public PFNext i; // This contains all the relevant information..

	// i = byte.MaxValue is the last segment that does not exist

	public PFNode GetNode()
	{
		return from;
	}

	public void Init(PFNode from, byte i)
	{
		this.from = from;
		this.i = (PFNext)i;
	}

	/*public PFNode GetNext()
	{
		return 
	}*/
	// Some identifier of what this is
}

public enum Dir : byte
{
	Forward,
	Right,
	Back,
	Left,
	Down,
	Up,
	MAX_DIRS
}

public enum PFNext : byte
{
	Rail_Straight = 0, Belt_Straight = 0,
	Rail_Left = 1, Belt_Left = 1,
	Rail_Right = 2, Belt_Right = 2,
	Rail_Up = 3, Belt_Up = 3,
	Rail_Down = 4, Belt_Down = 4,
	Rail_TrainStation = 5, Belt_Exporter = 5,
	Belt_Importer = 6
}

public struct PFNode : IEquatable<PFNode>, PathFinderSystem.IPathFind<ulong, PFNode>
{
	public PFTile tile;
	public Dir dir;
	public PFR pfr;

	public static PFNode Invalid { get => new PFNode(PFTile.Invalid, 0, 0); }

	public PFNode(PFTile tile, Dir dir, PFR pfr)
	{
		this.tile = tile;
		this.dir = dir;
		this.pfr = pfr;
	}

	/*public PFNode(PFTile tile, Dir dir, PFR pfr)
	{
		this.tile = tile;
		this.dir = dir;
		this.pfr = pfr;
	}*/

	public bool IsValid(ref Map map)
	{
		return (pfr != PFR.Person || tile.y == map.GetHeightIndex(tile)) && tile.IsValid();
	}

	public PFNode Flip()
	{
		// Assumes symmetry in the PFR rule...
		return new PFNode(tile.GetToTile(dir), dir.Flip(), pfr);
	}

	public byte NumPFNext(ref Map map, bool pathfinding, BufferFromEntity<NextSegment> next = default, ComponentDataFromEntity<Segment> segment = default)
	{
		switch(pfr)
		{
			case PFR.Rail: return (byte)(pathfinding ? 5 : 6);
			case PFR.Person: return (byte)(pathfinding ? 4 : 0);
			case PFR.Wall: return (byte)(pathfinding ? 4 : 0);
			case PFR.Belt: return (byte)(pathfinding ? 5 : 7);
			default: return 0;
		}
	}

	public PFNode PFNextNode(byte i, ref Map map, BufferFromEntity<NextSegment> next = default, ComponentDataFromEntity<Segment> segment = default)
	{
		switch (pfr)
		{
			case PFR.Rail:
				switch ((PFNext)i)
				{
					case PFNext.Rail_Straight: return new PFNode(tile.GetToTile(dir), dir, pfr);
					case PFNext.Rail_Left: return new PFNode(tile.GetToTile(dir).GetToTile(dir).GetToTile(dir.RotateDir(false)), dir.RotateDir(false), pfr);
					case PFNext.Rail_Right: return new PFNode(tile.GetToTile(dir).GetToTile(dir).GetToTile(dir.RotateDir(true)), dir.RotateDir(true), pfr);
					case PFNext.Rail_Up: return new PFNode(tile.GetToTile(dir).GetToTile(dir).GetToTile(Dir.Up), dir, pfr);
					case PFNext.Rail_Down: return new PFNode(tile.GetToTile(dir).GetToTile(dir).GetToTile(Dir.Down), dir, pfr);
					case PFNext.Rail_TrainStation: return new PFNode(tile.GetToTile(dir).GetToTile(dir).GetToTile(dir), dir, pfr);
					default: return Invalid;
				}
			case PFR.Person:
			case PFR.Wall:
				switch (i)
				{
					case 0: case 1: case 2: case 3: return new PFNode(tile.GetToTile((Dir)i), dir, pfr);
					default: return Invalid;
				}
			case PFR.Belt:
				switch ((PFNext)i)
				{
					case PFNext.Belt_Straight:
					case PFNext.Belt_Exporter:
					case PFNext.Belt_Importer: return new PFNode(tile.GetToTile(dir), dir, pfr);
					case PFNext.Belt_Left: return new PFNode(tile.GetToTile(dir), dir.RotateDir(false), pfr);
					case PFNext.Belt_Right: return new PFNode(tile.GetToTile(dir), dir.RotateDir(true), pfr);
					case PFNext.Belt_Up: return new PFNode(tile.GetToTile(dir).GetToTile(dir).GetToTile(Dir.Up), dir, pfr);
					case PFNext.Belt_Down: return new PFNode(tile.GetToTile(dir).GetToTile(dir).GetToTile(Dir.Down), dir, pfr);
					default: return Invalid;
				}
		}
		return Invalid;
	}
	public float PFNextCost(byte i, ref Map map, BufferFromEntity<NextSegment> next = default, ComponentDataFromEntity<Segment> segment = default)
	{
		switch (pfr)
		{
			case PFR.Rail:
				switch ((PFNext)i)
				{
					case PFNext.Rail_Straight: return PFTile.LENGTH + (tile.y - map.GetHeightIndex(tile.GetToTile(dir))) * PFTile.HEIGHT;
					case PFNext.Rail_Left: return PFTile.LENGTH * 3.2f + (tile.y - map.GetHeightIndex(tile.GetToTile(dir))) * PFTile.HEIGHT;
					case PFNext.Rail_Right: return PFTile.LENGTH * 3.2f + (tile.y - map.GetHeightIndex(tile.GetToTile(dir))) * PFTile.HEIGHT;
					case PFNext.Rail_Up: return PFTile.LENGTH * 2f + PFTile.HEIGHT * 1f + (tile.y - map.GetHeightIndex(tile.GetToTile(dir))) * PFTile.HEIGHT;
					case PFNext.Rail_Down: return PFTile.LENGTH * 2f + PFTile.HEIGHT * 1f + (tile.y - map.GetHeightIndex(tile.GetToTile(dir))) * PFTile.HEIGHT;
					default: return 0;
				}
			case PFR.Person:
			case PFR.Wall:
				return PFTile.LENGTH;
			case PFR.Belt:
				switch ((PFNext)i)
				{
					case PFNext.Belt_Straight: return PFTile.LENGTH + (tile.y - map.GetHeightIndex(tile.GetToTile(dir))) * PFTile.HEIGHT;
					case PFNext.Belt_Left: return PFTile.LENGTH * 1.6f + (tile.y - map.GetHeightIndex(tile.GetToTile(dir))) * PFTile.HEIGHT;
					case PFNext.Belt_Right: return PFTile.LENGTH * 1.6f + (tile.y - map.GetHeightIndex(tile.GetToTile(dir))) * PFTile.HEIGHT;
					case PFNext.Belt_Up: return PFTile.LENGTH * 2f + PFTile.HEIGHT * 1f + (tile.y - map.GetHeightIndex(tile.GetToTile(dir))) * PFTile.HEIGHT;
					case PFNext.Belt_Down: return PFTile.LENGTH * 2f + PFTile.HEIGHT * 1f + (tile.y - map.GetHeightIndex(tile.GetToTile(dir))) * PFTile.HEIGHT;
					default: return 0;
				}
		}
		return 0;
	}

	public bool PFNextTilesTakenCheck(ref Map map, NativeList<PFTile> tilesTaken, byte i, BuildRule rule)
	{
		PFNextTilesTaken(tilesTaken, i);
		for (int t = 0; t < tilesTaken.Length; t++)
		{
			if (!tilesTaken[t].IsValid() || !map.IsBuildable(tilesTaken[t], rule))
			{
				return false;
			}
		}
		return true;
	}

	public void PFNextTilesTaken(NativeList<PFTile> tilesTaken, byte i)
	{
		switch (pfr)
		{
			case PFR.Rail:
			{
				tilesTaken.Clear();
				PFTile next = tile.GetToTile(dir);
				tilesTaken.Add(next);
				switch ((PFNext)i)
				{
					case PFNext.Rail_Left:
						next.AddToTile(dir, tilesTaken).AddToTile(dir.RotateDir(false), tilesTaken);
						next.AddToTile(dir.RotateDir(false), tilesTaken);
						break;
					case PFNext.Rail_Right:
						next.AddToTile(dir, tilesTaken).AddToTile(dir.RotateDir(true), tilesTaken);
						next.AddToTile(dir.RotateDir(true), tilesTaken);
						break;
					case PFNext.Rail_Up:
						next.AddToTile(dir, tilesTaken).AddToTile(Dir.Up, tilesTaken);
						next.AddToTile(Dir.Up, tilesTaken);
						break;
					case PFNext.Rail_Down:
						next.AddToTile(dir, tilesTaken).AddToTile(Dir.Down, tilesTaken);
						next.AddToTile(Dir.Down, tilesTaken);
						break;
					case PFNext.Rail_TrainStation: // Somewhere for the train station to be...
						next.AddToTile(dir, tilesTaken).AddToTile(dir, tilesTaken).AddToTile(dir.RotateDir(true), tilesTaken);
						next.AddToTile(dir.RotateDir(true), tilesTaken).AddToTile(dir, tilesTaken);
						break;
				}
				break;
			}
			case PFR.Belt:
			{
				tilesTaken.Clear();
				PFTile next = tile.GetToTile(dir);
				tilesTaken.Add(next);
				switch ((PFNext)i)
				{
					case PFNext.Belt_Up:
						next.AddToTile(dir, tilesTaken).AddToTile(Dir.Up, tilesTaken);
						next.AddToTile(Dir.Up, tilesTaken);
						break;
					case PFNext.Belt_Down:
						next.AddToTile(dir, tilesTaken).AddToTile(Dir.Down, tilesTaken);
						next.AddToTile(Dir.Down, tilesTaken);
						break;
				}
				break;
			}
		}
	}

	public float3 ConnectionPoint(ComponentDataFromEntity<Segment> segment = default)
	{
		return Map.WorldPosition(tile, pfr) + ConnectionNormal() * PFTile.LENGTH * 0.5f;
	}

	public float3 ConnectionNormal()
	{
		switch (dir)
		{
			case Dir.Forward: return new float3(0, 0, 1);
			case Dir.Right: return new float3(1, 0, 0);
			case Dir.Back: return new float3(0, 0, -1);
			case Dir.Left: return new float3(-1, 0, 0);
			case Dir.Down: return new float3(0, -1, 0);
			case Dir.Up: return new float3(0, 1, 0);
			default: return new float3(0, 0, 0); // JOB_ASSERT
		}
	}


	public bool Equals(PFNode other)
	{
		return tile.Equals(other.tile) && dir == other.dir && pfr == other.pfr;
	}

	public override string ToString()
	{
		return tile.ToString() + ", dir = " + dir + ", " + pfr;
	}

	public ulong Index()
	{
		return (((ulong)tile.Index()) * (byte)Dir.MAX_DIRS + (byte)dir) * (byte)PFR.MAX_CONNECTION_TYPES + (byte)pfr;
	}
}


public struct PFTile : IEquatable<PFTile>
{
	public const float LENGTH = 4f;
	public const float HEIGHT = 4f;

	public ushort x;
	public byte y;
	public ushort z;

	public static PFTile Invalid { get => new PFTile(0, byte.MaxValue, 0); }

	public PFTile(ushort x, byte y, ushort z)
	{
		this.x = x;
		this.y = y;
		this.z = z;
	}

	public PFTile AddToTile(Dir dir, NativeList<PFTile> tilesTaken)
	{
		PFTile nextTile = GetToTile(dir);
		tilesTaken.Add(nextTile); // Add it even if it is invalid to indicate that we can't build it
		return nextTile;
	}

	public PFTile GetToTile(Dir dir)
	{
		if (!IsValid())
			return this;
		PFTile nextTile = this;
		bool isNextValid;
		switch (dir)
		{
			case Dir.Forward: isNextValid = ++nextTile.z != Map.SIZE_Z; break;
			case Dir.Right: isNextValid = ++nextTile.x != Map.SIZE_X; break;
			case Dir.Back: isNextValid = --nextTile.z != ushort.MaxValue; break;
			case Dir.Left: isNextValid = --nextTile.x != ushort.MaxValue; break;
			case Dir.Down: isNextValid = --nextTile.y != byte.MaxValue; break;
			case Dir.Up: isNextValid = ++nextTile.y != Map.SIZE_Y; break;
			default: isNextValid = true; break; // JOB_ASSERT
		}
		if (isNextValid)
			return nextTile;
		else
			return Invalid;
	}

	public override int GetHashCode()
	{
		return (int)Index();
	}

	public bool Equals(PFTile other)
	{
		return x == other.x && y == other.y && z == other.z;
	}

	public bool IsValid()
	{
		return y != byte.MaxValue;
	}

	public override string ToString()
	{
		return "{" + x + ", " + y + ", " + z + "}";
	}

	public uint Index()
	{
		return (y * Map.SIZE_X + x) * Map.SIZE_Z + z;
	}

	public int HorizontalIndex()
	{
		return (int)(x * Map.SIZE_Z + z);
	}
}

public enum PFR : byte
{
	// BeltNormal,
	// BeltUp,
	// BeltDown,
	Default,
	Rail,
	Person,
	Wall,
	Belt,
	Stairs,
	MAX_CONNECTION_TYPES
}

public static class PFRAndDirExtensions
{
	public static Dir RotateDir(this Dir d, bool right)
	{
		byte dir = (byte)((byte)d + (right ? 1 : -1));
		if (dir == byte.MaxValue)
			dir = 3;
		else if (dir == 4)
			dir = 0;
		return (Dir)dir;
	}

	public static Dir Flip(this Dir d)
	{
		if (d < Dir.Down)
			return (Dir)((byte)(d + 2) % 4);
		else
			return 9 - d;
	}

	public static Dir Add(this Dir d, byte add)
	{
		Assert.IsTrue(d < Dir.Down);
		return (Dir)((byte)(d + (byte)add) % 4);
	}

	public static quaternion Rotation(this Dir d)
	{
		return quaternion.Euler(0, math.PI / 2 * (byte)d, 0);
	}

	public static float3 Offset(this PFR pfr)
	{
		switch (pfr)
		{
			case PFR.Default: return new float3(0, 0, 0);
			case PFR.Rail:
			case PFR.Belt: return new float3(0, PFTile.HEIGHT * 0.1f, 0);
			// case PFR.RailUp: return new float3(0, -PFTile.HEIGHT * 0.4f, 0);
			// case PFR.RailDown: return new float3(0, -PFTile.HEIGHT * 0.9f, 0);
			default: return float3.zero;
		}
	}

	public static float3 DirOffset(this PFR pfr)
	{
		switch (pfr)
		{
			// case PFR.RailUp: return new float3(0, PFTile.HEIGHT * 0.8f, 0);
			// case PFR.RailDown: return new float3(0, -PFTile.HEIGHT * 0.8f, 0);
			default: return float3.zero;
		}
	}

	/*
	public static void SetNextPFR(this PFR pfr, NativeList<byte> outPFR)
	{
		outPFR.Clear(); // Capacity doesn't change, so this should be free
		switch (pfr)
		{
			case PFR.RailNormal: outPFR.Add((byte)PFR.RailNormal); outPFR.Add((byte)PFR.RailUp); outPFR.Add((byte)PFR.RailDown); return;
			case PFR.RailUp: outPFR.Add((byte)PFR.RailNormal); return;
			case PFR.RailDown: outPFR.Add((byte)PFR.RailNormal); return;
			default: outPFR.Add((byte)pfr); return;
		}
	}*/
}
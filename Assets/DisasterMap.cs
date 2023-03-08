using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

public interface IBufferEntity
{
	Entity Entity
	{
		get;
		set;
	}
}

[InternalBufferCapacity(0)]
public struct FloorHeaterRef : IBufferElementData, IBufferEntity
{
	public Entity Entity { get; set; }
}

[InternalBufferCapacity(0)]
public struct HotFloorRef : IBufferElementData, IBufferEntity
{
	public Entity Entity { get; set; }
	// public Entity entity;
	// public Entity Entity { get => entity; set => entity = value; }
}

[InternalBufferCapacity(0)]
public struct ResourcesForSaversRef : IBufferElementData, IBufferEntity
{
	public Entity Entity { get; set; }
	// public Entity entity;
	// public Entity Entity { get => entity; set => entity = value; }
}

// Non-pathfinding map, essentially. Used for abstractions
public struct DisasterMap
{

	public void Init()
	{
		// size = new uint3(16384, 16, 16384);
		rects = new NativeList<Rectangle>(100, Allocator.Persistent);
		IterativeSpawn();
	}

	public void Dispose()
	{
		rects.Dispose();
	}

	public void SetColor(NativeArray<Color32> colorData, PFTile tile)
	{
		colorData[tile.z * (int)Map.SIZE_X + tile.x] = /*badAir.ContainsKey(tile.Index()) ? new Color32(0, 0, (byte)(255 - badAir[tile.Index()].howBad * 10), 255) :*/ new Color32(128, 128, 128, 255);
	}

	public const int MAP_PADDING = -10;
	private bool IsRectValid(Rectangle rect)
	{
		if (rect.x0 < MAP_PADDING || rect.y0 < MAP_PADDING || rect.x1 >= Map.SIZE_X - MAP_PADDING || rect.y1 >= Map.SIZE_Z - MAP_PADDING)
			return false;

		for (int i = 0; i < rects.Length; i++)
		{
			if (rects[i].Intersects(rect))
				return false;
		}
		return true;
	}

	private int GetMid(int a, int b, int c, int d)
	{ // b > a, d > c
		if (b > d)
		{
			if (c > a)
				return (c + d) / 2;
			else
				return (a + d) / 2;
		}
		else
		{
			if (a > c)
				return (a + b) / 2;
			else
				return (c + b) / 2;
		}
	}


	private void IterativeSpawn()
	{

		List<Action> actions = new List<Action>();
		Spawn(new Rectangle(-1, -1, -1, -1, -1, -1, false), new int2((int)Map.SIZE_X / 2, (int)Map.SIZE_Z / 2), 9, 20, 1, 10, 0, 2, actions);
		while (actions.Count > 0)
		{
			List<Action> nextActions = new List<Action>(actions);
			actions.Clear();
			foreach (Action action in nextActions)
			{
				action();
			}
		}

		for (int i = 0; i < rects.Length; i++)
		{
			for (ushort x = (ushort)math.max(0, rects[i].x0); x <= math.min(Map.SIZE_X - 1, rects[i].x1); x++)
			{
				for (ushort z = (ushort)math.max(0, rects[i].y0); z <= math.min(Map.SIZE_Z - 1, rects[i].y1); z++)
				{
					Game.map.SetWalkingHeight(new PFTile(x, 0, z), (byte)/*math.max(Game.map.GetWalkingHeight(new PFTile(x, 0, z)), */(rects[i].yPos + 1)/*)*/);
				}
			}
		}
		/*for (ushort x = 74; x < Map.SIZE_X; x++)
		{
			Game.map.SetWalkingHeight(new PFTile(x, 0, 63), 7);
		}

		for (ushort x = 0; x < Map.SIZE_X; x++)
		{
			for (ushort z = 0; z < Map.SIZE_Z; z++)
			{
				int maxDistFromCenter = math.max(math.abs(x - (int)(Map.SIZE_X / 2)), math.abs(z - (int)(Map.SIZE_Z / 2)));
				Game.map.SetWalkingHeight(new PFTile(x, 0, z), (byte)(maxDistFromCenter / 5));
			}
		}*/


		Game.map.BuildPFRects();
		Game.map.SpawnMesh();
	}

	NativeList<Rectangle> rects; // = new List<RectInt>();
	public const int SQUARE_HEIGHT = 3;
	// public const int CONNECTOR_HEIGHT = 3;
	private void Spawn(Rectangle previous, int2 rectLocation, int radius, int maxOffset, int pathRadius, int numChildrenAttempts, int padding, int yPos, List<Action> actions)
	{
		if (radius <= 3)
			return;

		Rectangle rect = new Rectangle(rectLocation.x - radius, rectLocation.y - radius, rectLocation.x + radius, rectLocation.y + radius, yPos, SQUARE_HEIGHT, false);
		if (!IsRectValid(rect.Padding(padding, padding)))
			return;

		/*if (previous.x0 >= 0)
		{
			if (previous.x0 + pathRadius * 2 < rect.x1 && rect.x0 + pathRadius * 2 < previous.x1)
			{
				int mid = GetMid(previous.x0, previous.x1, rect.x0, rect.x1);
				Rectangle r = new Rectangle(mid - pathRadius, previous.y0 < rect.y0 ? previous.y1 + 1 : rect.y1 + 1, mid + pathRadius, previous.y0 < rect.y0 ? rect.y0 - 1 : previous.y0 - 1, yPos, CONNECTOR_HEIGHT, true);
				if (!IsRectValid(r.Padding(padding, 0)))
					return;
				rects.Add(r);
			}
			else if (previous.y0 + pathRadius * 2 < rect.y1 && rect.y0 + pathRadius * 2 < previous.y1)
			{
				int mid = GetMid(previous.y0, previous.y1, rect.y0, rect.y1);
				Rectangle r = new Rectangle(previous.x0 < rect.x0 ? previous.x1 + 1 : rect.x1 + 1, mid - pathRadius, previous.x0 < rect.x0 ? rect.x0 - 1 : previous.x0 - 1, mid + pathRadius, yPos, CONNECTOR_HEIGHT, true);
				if (!IsRectValid(r.Padding(0, padding)))
					return;
				rects.Add(r);
			}
			else
			{
				bool switchRects = Variance.Chance(0.5f);
				Rectangle a = previous;
				Rectangle b = rect;
				if (switchRects)
				{
					a = rect;
					b = previous;
				}

				int x = Variance.Range(((b.x1 > a.x0 && b.x1 < a.x1) ? b.x1 : a.x0) + pathRadius, ((b.x0 > a.x0 && b.x0 < a.x1) ? b.x0 : a.x1) - pathRadius);
				int y = Variance.Range(((a.y1 > b.y0 && a.y1 < b.y1) ? a.y1 : b.y0) + pathRadius, ((a.y0 > b.y0 && a.y0 < b.y1) ? a.y0 : b.y1) - pathRadius);

				Rectangle r1 = new Rectangle(x - pathRadius, b.y0 > a.y0 ? a.y1 + 1 : y - pathRadius, x + pathRadius, b.y0 > a.y0 ? y + pathRadius : a.y0 - 1, yPos, CONNECTOR_HEIGHT, true);
				Rectangle r2 = new Rectangle(b.x0 < a.x0 ? b.x1 + 1 : x + pathRadius + 1, y - pathRadius, b.x0 < a.x0 ? x - pathRadius - 1 : b.x0 - 1, y + pathRadius, yPos, CONNECTOR_HEIGHT, true);

				if (!IsRectValid(r1.Padding(padding, 0)) || !IsRectValid(r2.Padding(0, padding)))
					return;

				rects.Add(r1);
				rects.Add(r2);
			}
		}*/

		rects.Add(rect);

		int nextRadius = radius;
		if (Variance.Chance(1f))
		{
			nextRadius = radius - 1;// radius * 7 / 8;
			maxOffset -= 2;// maxOffset * 11 / 12;
									  // pathRadius -= 1;
									  // padding -= 1;
			numChildrenAttempts += 0;
			yPos++; // += (Variance.NextInt(2) * 2 - 1);
		}
		for (int i = 0; i < numChildrenAttempts; i++)
		{
			int r = Variance.NextInt(4);
			int2 nextLocation;
			if (r == 0)
				nextLocation = rectLocation + new int2(Variance.Range(-maxOffset, maxOffset), radius + nextRadius + 1);
			else if (r == 1)
				nextLocation = rectLocation + new int2(Variance.Range(-maxOffset, maxOffset), -radius - nextRadius - 1);
			else if (r == 2)
				nextLocation = rectLocation + new int2(radius + nextRadius + 1, Variance.Range(-maxOffset, maxOffset));
			else
				nextLocation = rectLocation + new int2(-radius - nextRadius - 1, Variance.Range(-maxOffset, maxOffset));


			//int2 nextLocation = rectLocation + new int2(Variance.Range(-maxOffset, maxOffset), Variance.Range(-maxOffset, maxOffset)); // new int2(rectLocation.x + (Variance.NextInt(2) * 2 - 1) * (radius + nextRadius + Variance.Range(1, maxOffset)),
			// rectLocation.y + (Variance.NextInt(2) * 2 - 1) * (radius + nextRadius + Variance.Range(1, maxOffset)));
			DisasterMap map = this;
			actions.Add(() =>
			{
				map.Spawn(rect, nextLocation, nextRadius, maxOffset, pathRadius, numChildrenAttempts, padding, yPos, actions);
			});
		}
	}
	/*
	private void SetRects()
	{
		NativeArray<Color32> colorData = Game.tex.GetRawTextureData<Color32>();
		for (int i = 0; i < rects.Length; i++)
		{
			for (int x = rects[i].x0; x <= rects[i].x1; x++)
			{
				for (int y = rects[i].y0; y <= rects[i].y1; y++)
				{
					PFTile tile = new PFTile((ushort)x, 0, (ushort)y);
					badAir[tile.Index()] = new BadAir { howBad = 5f, tile = tile };
					SetColor(colorData, tile);
				}
			}
		}
		Game.tex.Apply();
	}*/
}

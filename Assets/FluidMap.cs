using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

public struct FluidMap
{
	[ReadOnly] public NativeArray<WaterTable> waterTable;
	public int Index(int x, int z)
	{
		return (int)(x * Map.SIZE_Z + z);
	}

	public float GetHeight(float3 pos)
	{
		PFTile tile = Map.GetTile(pos);
		if (tile.IsValid() && tile.x >= 1 && tile.z >= 1)
		{
			float2 lerp = new float2((pos.x % PFTile.LENGTH) / PFTile.LENGTH, (pos.z % PFTile.LENGTH) / PFTile.LENGTH);
			// Interpolate!

			int index = Index(tile.x, tile.z);
			float2 xLerped = math.lerp(waterTable[index].height.c0, waterTable[index].height.c1, lerp.y);
			return math.lerp(xLerped.x, xLerped.y, lerp.x);
		}
		return 0;
	}

	/*public float2x2 GetDepthStuff(float3 pos)
	{
		PFTile tile = Map.GetTile(pos);
		if (tile.IsValid() && tile.x >= 1 && tile.z >= 1)
		{
			return waterTable[Index(tile.x, tile.z)].depth;
		}
		return new float2x2();
	}

	public float2x2 GetHeightStuff(float3 pos)
	{
		PFTile tile = Map.GetTile(pos);
		if (tile.IsValid() && tile.x >= 1 && tile.z >= 1)
		{
			return waterTable[Index(tile.x, tile.z)].height;
		}
		return new float2x2();
	}*/

	/*public float GetDepth(ushort x, ushort z)
	{
		if (x >= 0 && z >= 0 && x < Map.SIZE_X && z < Map.SIZE_Z)
			return waterTable[Index(x, z)].depth;
		else
			return 0f;
	}

	float GetUpwindDepth(int index1, int index2)
	{
		if (waterTable[index1].ground + waterTable[index1].depth > waterTable[index2].ground + waterTable[index2].depth)
			return waterTable[index1].depth;
		else
			return waterTable[index2].depth;
	}*/

	float VelocityX(int x, int z)
	{
		return waterTable[Index(x, z)].vel.x;// / (PFTile.LENGTH * GetUpwindDepth(Index(x, z), Index(x + 1, z)));
	}

	float VelocityZ(int x, int z)
	{
		return waterTable[Index(x, z)].vel.y;// / (PFTile.LENGTH * GetUpwindDepth(Index(x, z), Index(x, z + 1)));
	}

	public float2 GetVelocity(float3 pos)
	{
		PFTile tile = Map.GetTile(pos);
		if (tile.IsValid() && tile.x >= 1 && tile.z >= 1)
		{
			// Note this doesn't quite line up with the texture (as the texture only interps for 1/4 (of each side) of the tile for the velocity)
			float2 lerp = new float2((pos.x % PFTile.LENGTH) / PFTile.LENGTH, (pos.z % PFTile.LENGTH) / PFTile.LENGTH);

			float2 vel = new float2(math.lerp(VelocityX(tile.x - 1, tile.z), VelocityX(tile.x, tile.z), lerp.x), math.lerp(VelocityZ(tile.x, tile.z - 1), VelocityZ(tile.x, tile.z), lerp.y));
			return vel;

			// return new float2(math.lerp(velocityPipes[(int)((tile.x - 1) * SIZE_Z + tile.z)].x, velocityPipes[(int)(tile.x * SIZE_Z + tile.z)].x, lerp.x),
			//	math.lerp(velocityPipes[(int)(tile.x * SIZE_Z + (tile.z - 1))].y, velocityPipes[(int)(tile.x * SIZE_Z + tile.z)].y, lerp.y));
		}
		return new float2(0, 0);
	}
}

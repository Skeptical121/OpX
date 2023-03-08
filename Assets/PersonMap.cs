using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

// TODO_EFFICIENCY we could store this with less memory
public struct Goal
{
	public float dist;
	public Entity goal;
}

public struct PersonMap
{
	public const int PEOPLE_PER_GRID = 4;
	private NativeArray<float2> vel; // People move predictabely
	public NativeArray<float2> hungryVel;
	public NativeArray<Goal> hungryGoal;

	public void Init()
	{
		// people = new NativeMultiHashMap<int, Entity>(100, Allocator.Persistent); //new NativeArray<int>((int)(Map.SIZE_X * PEOPLE_PER_GRID * Map.SIZE_Z * PEOPLE_PER_GRID), Allocator.Persistent);
		// velField = new NativeArray<float2>((int)(Map.SIZE_X * PEOPLE_PER_GRID * Map.SIZE_Z * PEOPLE_PER_GRID), Allocator.Persistent);
		vel = new NativeArray<float2>((int)(Map.SIZE_X * Map.SIZE_Z), Allocator.Persistent);
		hungryVel = new NativeArray<float2>((int)(Map.SIZE_X * Map.SIZE_Z), Allocator.Persistent);
		hungryGoal = new NativeArray<Goal>((int)(Map.SIZE_X * Map.SIZE_Z), Allocator.Persistent);

		//for (int i = 0; i < people.Length; i++) {
		//	people[i] = -1;
		//}
	}
	public void Dispose()
	{
		// people.Dispose();
		vel.Dispose();
		hungryVel.Dispose();
		hungryGoal.Dispose();
		// velField.Dispose();
	}

	public void TestInit()
	{
		//for (int x = 0; x < Map.SIZE_X; x++)
		//{
		//	for (int y = 0; y < Map.SIZE_Z; y++)
		//	{
		//		vel[Index(x, y)] = math.normalize(new float2(73.5f, 63.5f) - new float2(x, y));
				/*float2x4 theVel = new float2x4();
				for (int i = 0; i < 4; i++)
				{
					theVel[i] = math.normalize(new float2(73.5f, 63.5f) - new float2(x + i / 2, y + i % 2)) * SimplePerson.SPEED;
				}
				vel[Index(x, y)] = theVel;*/
		//	}
		//}
	}

	public int Index(int x, int y)
	{
		return (int)(x * Map.SIZE_X + y);
	}

	public float3 GetVelocity(PFTile tile, bool hungry)
	{
		if (tile.IsValid())
		{
			if (hungry)
				return new float3(hungryVel[Index(tile.x, tile.z)].x, 0, hungryVel[Index(tile.x, tile.z)].y);
			else
				return new float3(vel[Index(tile.x, tile.z)].x, 0, vel[Index(tile.x, tile.z)].y);
		}
		return 0;
	}

	public static int3 GetIntPos(float3 pos)
	{
		return new int3((int)(pos.x * PEOPLE_PER_GRID / PFTile.LENGTH), (int)(pos.y / PFTile.HEIGHT), (int)(pos.z * PEOPLE_PER_GRID / PFTile.LENGTH));
	}


	public static int GetIndex(int3 intPos)
	{
		if (intPos.x < 0 || intPos.y < 0 || intPos.z < 0 || intPos.x >= Map.SIZE_X * PEOPLE_PER_GRID || intPos.y >= Map.SIZE_Y || intPos.z >= Map.SIZE_Z * PEOPLE_PER_GRID)
			return -1;

		int index = (int)((intPos.y * Map.SIZE_X * PEOPLE_PER_GRID + intPos.x) * Map.SIZE_Z * PEOPLE_PER_GRID + intPos.z);

		return index;
	}

	/*public static NativeMultiHashMap<int, Entity>.Enumerator GetPerson(int2 intPos)
	{
		int index = GetIndex(intPos);
		if (index == -1)
			return default;
		else
			return people.GetValuesForKey(index);
	}

	public void Claim(Entity person, float3 oldPos, float3 pos)
	{
		int oldIndex = GetIndex(GetIntPos(oldPos));
		int newIndex = GetIndex(GetIntPos(pos));
		if (newIndex != oldIndex)
		{
			if (oldIndex != -1)
			{
				people.Remove(newIndex, person);
			}
			if (newIndex != -1)
			{
				people.Add(newIndex, person);
			}
		}
	}*/

	/*public float2 GetExtraVelocity(float2 pos)
	{
		int index = GetIndex(pos);
		return velField[index];
		// int dir = people[index] & 7;
		// float angle = dir / 4 * math.PI;
		// return new float2(math.cos(angle), math.sin(angle)) * (people[index] & (1 << 4)) / (1 << 4);
	}*/

	/*public bool AttemptClaim(int person, float2 oldPos, float2 pos)
	{
		int oldIndex = GetIndex(GetIntPos(oldPos));
		int newIndex = GetIndex(GetIntPos(pos));
		if (!newIndex.Equals(oldIndex))
		{
			if (newIndex == -1)
				return false;

			if (people[newIndex] != -1)// (people[newInfo.x] & (1 << newInfo.y)) != 0)
			{
				//if (oldIndex >= 0)
				//{
				//	float2 diff = new float2(pos.x - oldPos.x, pos.y - oldPos.y);
				//	diff = math.normalize(diff);
				//	velField[newIndex] = diff;
				//}

				// Push them a little:
				//float dirVal = (math.atan2(diff.y, diff.x) / math.PI) * 4;
				//int extra = math.clamp((int)dirVal, 0, 7);

				// Debug.Log(dirVal + ", " + extra + "... " + (pos.y - oldPos.y) + ", " + (pos.x - oldPos.x));

				//people[newIndex] = (byte)(people[newIndex] & ~(7));
				//people[newIndex] = (byte)(people[newIndex] | extra);
				// Set speed:
				//people[newIndex] = (byte)(people[newIndex] | (1 << 4));

				return false;
			}

			if (oldPos.x >= 0)
			{
				people[oldIndex] = -1;
				// people[oldIndex] = (byte)(people[newIndex] & ~(1 << 3)); // Unset old tile
			}
			people[newIndex] = person;
			// people[newIndex] = (byte)(people[newIndex] | (1 << 3)); // Set new tile
		}
		return true;
	}*/

}

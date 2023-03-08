using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public static class VectorMath
{
	// lineVec1 and lineVec2 should be normalized unless you know what you're doing...
	public static float3 GetIntersection(float3 p1, float3 p2, float3 lineVec1, float3 lineVec2)
	{
		float3 lineVec3 = p2 - p1;
		float3 crossVec1and2 = math.cross(lineVec1, lineVec2);
		float3 crossVec3and2 = math.cross(lineVec3, lineVec2);

		// float planarFactor = Vector3.Dot(lineVec3, crossVec1and2); <- this will always be 0

		if (/*Mathf.Abs(planarFactor) < 0.0001f && */math.lengthsq(crossVec1and2) > 0.0001f)
		{
			float s = math.dot(crossVec3and2, crossVec1and2) / math.lengthsq(crossVec1and2); // lengthsq is SqrMagnitude
			return p1 + lineVec1 * s;
		}
		else
		{
			Assert.Fail("Invalid: " + p1 + ", " + p2 + ", " + lineVec1 + ", " + lineVec2 + "... make / use a different method if we want this to be valid behaviour");
			return (p1 + p2) / 2;
		}
	}

	// return value is from 0 - 2, 0 being straight, 2 being directly backwards
	public static float GetAngleMultiplier(float3 dir1, float3 dir2)
	{
		return 1 - math.dot(dir1, dir2);
	}

	
}

public struct Rectangle
{

	public int x0;
	public int y0;

	public int x1;
	public int y1;

	public int yPos;
	public int height;

	public bool oneBig;

	public Rectangle(int x0, int y0, int x1, int y1, int yPos, int height, bool oneBig)
	{
		this.x0 = x0;
		this.y0 = y0;
		this.x1 = x1;
		this.y1 = y1;
		this.yPos = yPos;
		this.height = height;
		this.oneBig = oneBig;
	}

	public Rectangle Padding(int paddingX, int paddingY)
	{
		return new Rectangle(x0 - paddingX, y0 - paddingY, x1 + paddingX, y1 + paddingY, yPos, height, oneBig);
	}

	public bool Intersects(Rectangle other)
	{
		return x0 <= other.x1 && other.x0 <= x1 && y0 <= other.y1 && other.y0 <= y1 && ((yPos <= other.yPos + other.height && other.yPos <= yPos + height) ||
			(oneBig && !other.oneBig && yPos <= other.yPos) || (!oneBig && other.oneBig && other.yPos <= yPos));
	}

	public override string ToString()
	{
		return "(" + x0 + ", " + y0 + "), (" + x1 + ", " + y1 + ")";
	}
}
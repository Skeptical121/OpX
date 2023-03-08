using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class MeshCreator
{

	public static int[] RectTri(int a, int b, int c, int d)
	{
		return new int[] { a, b, c, c, b, d };
	}

	public static void RectTri(List<int> array, int a, int b, int c, int d)
	{
		array.Add(a);
		array.Add(b);
		array.Add(c);
		array.Add(c);
		array.Add(b);
		array.Add(d);
	}

	public static void AddRectangle(List<Vector3> rectangles, List<Vector3> normals, Vector3 normal, params Vector3[] pos)
	{
		rectangles.Add(pos[0]);
		rectangles.Add(pos[1]);
		rectangles.Add(pos[2]);
		rectangles.Add(pos[3]);
		normals.Add(normal);
		normals.Add(normal);
		normals.Add(normal);
		normals.Add(normal);
	}

	public static T[] MergeArrays<T>(T[][] triangles)
	{
		int total = 0;
		for (int i = 0; i < triangles.Length; i++)
			total += triangles[i].Length;

		T[] returnTriangles = new T[total];
		total = 0;
		for (int i = 0; i < triangles.Length; i++)
		{
			for (int k = 0; k < triangles[i].Length; k++)
			{
				returnTriangles[total++] = triangles[i][k];
			}
		}
		return returnTriangles;
	}

	public static Mesh CombineMeshes(bool mergeSubMeshes, params Mesh[] meshes)
	{
		CombineInstance[] combine = new CombineInstance[meshes.Length];

		for (int i = 0; i < meshes.Length; i++)
		{
			combine[i].mesh = meshes[i];
			combine[i].transform = Matrix4x4.identity;
		}

		Mesh finalMesh = new Mesh();
		finalMesh.CombineMeshes(combine, mergeSubMeshes);
		return finalMesh;
	}

	// Centered at center bottom...
	public static Mesh Rectangle(float3 s)
	{
		return new Mesh
		{
			vertices = new Vector3[] { new Vector3(-s.x / 2, 0, -s.z / 2), new Vector3(s.x / 2, 0, -s.z / 2), new Vector3(-s.x / 2, 0, s.z / 2), new Vector3(s.x / 2, 0, s.z / 2),
									   new Vector3(-s.x / 2, 0, -s.z / 2), new Vector3(-s.x / 2, 0, s.z / 2), new Vector3(-s.x / 2, s.y, -s.z / 2), new Vector3(-s.x / 2, s.y, s.z / 2),
									   new Vector3(-s.x / 2, 0, s.z / 2), new Vector3(s.x / 2, 0, s.z / 2), new Vector3(-s.x / 2, s.y, s.z / 2), new Vector3(s.x / 2, s.y, s.z / 2),
									   new Vector3(s.x / 2, 0, s.z / 2), new Vector3(s.x / 2, 0, -s.z / 2), new Vector3(s.x / 2, s.y, s.z / 2), new Vector3(s.x / 2, s.y, -s.z / 2),
									   new Vector3(s.x / 2, 0, -s.z / 2), new Vector3(-s.x / 2, 0, -s.z / 2), new Vector3(s.x / 2, s.y, -s.z / 2), new Vector3(-s.x / 2, s.y, -s.z / 2),
									   new Vector3(-s.x / 2, s.y, -s.z / 2), new Vector3(-s.x / 2, s.y, s.z / 2), new Vector3(s.x / 2, s.y, -s.z / 2), new Vector3(s.x / 2, s.y, s.z / 2)},
			triangles = MergeArrays(new int[][] { RectTri(0, 1, 2, 3), RectTri(4, 5, 6, 7), RectTri(8, 9, 10, 11), RectTri(12, 13, 14, 15), RectTri(16, 17, 18, 19), RectTri(20, 21, 22, 23) }),
			normals = new Vector3[] { Vector3.down, Vector3.down, Vector3.down, Vector3.down, 
									  Vector3.left, Vector3.left, Vector3.left, Vector3.left, 
									  Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward,
									  Vector3.right, Vector3.right, Vector3.right, Vector3.right, 
									  Vector3.back, Vector3.back, Vector3.back, Vector3.back, 
									  Vector3.up, Vector3.up, Vector3.up, Vector3.up },
			uv = new Vector2[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0), 
								 new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0), 
								 new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0),
								 new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0),
								 new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0),
								 new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) }
		};
	}
}

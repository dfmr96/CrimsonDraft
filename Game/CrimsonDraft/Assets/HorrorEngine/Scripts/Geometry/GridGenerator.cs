using UnityEngine;

 /*
  * Source: https://catlikecoding.com/unity/tutorials/procedural-grid/ (MIT-0 License)
  */

namespace HorrorEngine
{
	[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
	public class GridGenerator : MonoBehaviour
	{

		public Vector2Int Size;

		private Mesh m_Mesh;
		private Vector3[] m_Vertices;

		private void Awake()
		{
			Generate(Size.x, Size.y);
		}

		public void Generate(int xSize, int ySize)
		{
			GetComponent<MeshFilter>().mesh = m_Mesh = new Mesh();
			m_Mesh.name = "Procedural Grid";

			m_Vertices = new Vector3[(xSize + 1) * (ySize + 1)];
			Vector2[] uv = new Vector2[m_Vertices.Length];
			Vector4[] tangents = new Vector4[m_Vertices.Length];
			Vector4 tangent = new Vector4(1f, 0f, 0f, -1f);
			for (int i = 0, y = 0; y <= ySize; y++)
			{
				for (int x = 0; x <= xSize; x++, i++)
				{
					m_Vertices[i] = new Vector3(x, y);
					uv[i] = new Vector2((float)x, (float)y);
					tangents[i] = tangent;
				}
			}
			m_Mesh.vertices = m_Vertices;
			m_Mesh.uv = uv;
			m_Mesh.tangents = tangents;

			int[] triangles = new int[xSize * ySize * 6];
			for (int ti = 0, vi = 0, y = 0; y < ySize; y++, vi++)
			{
				for (int x = 0; x < xSize; x++, ti += 6, vi++)
				{
					triangles[ti] = vi;
					triangles[ti + 3] = triangles[ti + 2] = vi + 1;
					triangles[ti + 4] = triangles[ti + 1] = vi + xSize + 1;
					triangles[ti + 5] = vi + xSize + 2;
				}
			}
			m_Mesh.triangles = triangles;
			m_Mesh.RecalculateNormals();
		}
	}
}
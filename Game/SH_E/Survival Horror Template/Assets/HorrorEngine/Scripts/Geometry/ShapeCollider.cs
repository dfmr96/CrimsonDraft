using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;

namespace HorrorEngine
{
    [RequireComponent(typeof(Shape))]
    [RequireComponent(typeof(MeshCollider))]
    public class ShapeCollider : MonoBehaviour
    {
        public enum ExtrusionMethod
        {
            Volume,
            Planes
        }

        [SerializeField] float m_Height = 1;
        [SerializeField] ExtrusionMethod m_Method;

        private void OnValidate()
        {
            UpdateCollider();
        }

        public void UpdateCollider()
        {
            Shape shape = GetComponent<Shape>();
            MeshCollider collider = GetComponent<MeshCollider>();
            if (!shape || !collider)
                return;

            Mesh mesh;
            if (m_Method == ExtrusionMethod.Volume)
            {
                int rimVertCount = shape.Points.Count;
                if (rimVertCount < 3)
                    return;

                CompositeShape comp = new CompositeShape(new Shape[] { shape });
                mesh = comp.GetMesh();
                ExtrudeRim(mesh, 0, rimVertCount, Vector3.up, m_Height);   
            }
            else
            {
                int rimVertCount = shape.Points.Count;
                if (rimVertCount < 2)
                    return;

                mesh = new Mesh();
                ExtrudePoints(shape.Points, mesh, m_Height, shape.CloseShape);
            }

            mesh.name = $"ShapeCollider_" + gameObject.name;
            collider.sharedMesh = mesh;

            MeshFilter filter = GetComponent<MeshFilter>();
            if (filter)
                filter.sharedMesh = mesh;
        }

        private void ExtrudePoints(List<Vector3> points, Mesh mesh, float d, bool close)
        {
            List<Vector3> vertices = new List<Vector3>();
            int pointCount = points.Count;

            // Vertices for the bottom face (Y=0)
            for (int i = 0; i < pointCount; i++)
            {
                vertices.Add(points[i]);
            }

            // Vertices for the top face (Y=Height)
            for (int i = 0; i < pointCount; i++)
            {
                vertices.Add(points[i] + Vector3.up * d);
            }

            List<int> triangles = new List<int>();
            for (int i = 0; i < pointCount-1; i++)
            {
                int p1 = i;                         // Bottom-Current
                int p2 = (i + 1) % pointCount;      // Bottom-Next
                int p3 = p1 + pointCount;           // Top-Current
                int p4 = p2 + pointCount;           // Top-Next

                AddFrontAndBackQuad(ref triangles, p1, p2, p3, p4);
            }

            if (close)
            {
                int i = pointCount - 1;
                int p1 = i;                         // Bottom-Current
                int p2 = (i + 1) % pointCount;      // Bottom-Next
                int p3 = p1 + pointCount;           // Top-Current
                int p4 = pointCount;                // Top-Next

                AddFrontAndBackQuad(ref triangles, p1, p2, p3, p4);
            }
           
            mesh.vertices = vertices.ToArray();
            mesh.triangles = triangles.ToArray();
        }

        private void AddFrontAndBackQuad(ref List<int> triangles, int p1, int p2, int p3, int p4)
        {
            triangles.Add(p2);
            triangles.Add(p3);
            triangles.Add(p1);
            
            triangles.Add(p1);
            triangles.Add(p3);
            triangles.Add(p2);

            triangles.Add(p4);
            triangles.Add(p3);
            triangles.Add(p2);

            triangles.Add(p2);
            triangles.Add(p3);
            triangles.Add(p4);
        }

        private void ExtrudeRim(Mesh m, int fromIndex, int toIndex, Vector3 n, float d)
        {
            Vector3[] vertices = m.vertices;

            List<Vector3> newVerts = new List<Vector3>();
            List<int> newTris = new List<int>();

            Vector3 v1E, v2E = Vector3.zero;
            int newIndex1 = 0;
            int newIndex2 = 0;
            int index = vertices.Length;
            int firstNewIndex = 0;
            Vector3 v1, v2 = Vector3.zero;
            for (int i = fromIndex; i < toIndex; ++i)
            {
                v1 = vertices[i];

                if (i == fromIndex)
                {
                    v1E = v1 + n * d;
                    newVerts.Add(v1E);
                    newIndex1 = index++;
                    firstNewIndex = newIndex1;
                }

                int nextIndex = i + 1;
                if (i == toIndex - 1) // Loop
                {
                    newIndex2 = firstNewIndex;
                    nextIndex = fromIndex;
                }
                else
                {
                    v2 = vertices[i + 1];
                    v2E = v2 + n * d;
                    newVerts.Add(v2E);
                    newIndex2 = index++;
                }

                newTris.Add(i);
                newTris.Add(nextIndex);
                newTris.Add(newIndex1);

                newTris.Add(nextIndex);
                newTris.Add(newIndex2);
                newTris.Add(newIndex1);
                
                v1E = v2E;
                newIndex1 = newIndex2;
            }

            m.vertices = m.vertices.Concat(newVerts).ToArray();
            m.triangles = m.triangles.Concat(newTris).ToArray();
            m.RecalculateNormals();
        }

    }

}
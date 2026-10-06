using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HorrorEngine
{
    [RequireComponent(typeof(Shape))]
    [RequireComponent(typeof(MeshCollider))]
    public class ShapeCollider : MonoBehaviour
    {
        [SerializeField] float m_Height = 1;

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

            int rimVertCount = shape.Points.Count;
            if (rimVertCount < 3)
                return;

            CompositeShape comp = new CompositeShape(new Shape[] { shape });
            Mesh mesh = comp.GetMesh();
            mesh.name = $"ShapeCollider_" + gameObject.name;
            Extrude(mesh, 0, rimVertCount, Vector3.up, m_Height);

            collider.sharedMesh = mesh;
        }


        public void Extrude(Mesh m, int fromIndex, int toIndex, Vector3 n, float d)
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
        }

    }

}
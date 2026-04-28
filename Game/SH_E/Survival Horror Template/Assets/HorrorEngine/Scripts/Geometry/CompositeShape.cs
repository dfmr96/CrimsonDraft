using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

/*
 * Processes array of shapes into a single mesh
 * Automatically determines which shapes are solid, and which are holes
 * Ignores invalid shapes (contain self-intersections, too few points, overlapping holes)
 *
 * Source : https://github.com/SebLague/Shape-Editor-Tool

    MIT License

    Copyright (c) 2020 Sebastian Lague

    Permission is hereby granted, free of charge, to any person obtaining a copy
    of this software and associated documentation files (the "Software"), to deal
    in the Software without restriction, including without limitation the rights
    to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
    copies of the Software, and to permit persons to whom the Software is
    furnished to do so, subject to the following conditions:

    The above copyright notice and this permission notice shall be included in all
    copies or substantial portions of the Software.

    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
    IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
    FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
    AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
    LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
    OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
    SOFTWARE.

 * 
 */

namespace HorrorEngine
{
    public partial class CompositeShape
    {
        public Vector3[] vertices;
        public int[] triangles;
        ShapeData[] shapes;
        float height = 0;

        public CompositeShape(ShapeData[] shapes)
        {
            this.shapes = shapes;
        }

        public CompositeShape(Shape[] shapes)
        {
            this.shapes = new ShapeData[shapes.Length];
            for (int i = 0; i < shapes.Length; ++i)
            {
                this.shapes[i] = new ShapeData()
                {
                    Points = shapes[i].Points.ToArray()
                };
            }
        }

        public Mesh GetMesh()
        {
            // Generate array of valid shape data
            CompositeShapeData[] eligibleShapes = shapes.Select(shape => new CompositeShapeData(shape.Points)).Where(x => x.IsValidShape).ToArray();
            Process(eligibleShapes);

            return new Mesh()
            {
                vertices = vertices,
                triangles = triangles,
                normals = vertices.Select(x => Vector3.up).ToArray()
            };
        }

        private void Process(CompositeShapeData[] eligibleShapes)
        {
            // Set parents for all shapes. A parent is a shape which completely contains another shape.
            for (int i = 0; i < eligibleShapes.Length; i++)
            {
                for (int j = 0; j < eligibleShapes.Length; j++)
                {
                    if (i == j)
                        continue;

                    if (eligibleShapes[i].IsParentOf(eligibleShapes[j]))
                    {
                        eligibleShapes[j].parents.Add(eligibleShapes[i]);
                    }
                }
            }

            // Holes are shapes with an odd number of parents.
            CompositeShapeData[] holeShapes = eligibleShapes.Where(x => x.parents.Count % 2 != 0).ToArray();
            foreach (CompositeShapeData holeShape in holeShapes)
            {
                // The most immediate parent (i.e the smallest parent shape) will be the one that has the highest number of parents of its own. 
                CompositeShapeData immediateParent = holeShape.parents.OrderByDescending(x => x.parents.Count).First();
                immediateParent.holes.Add(holeShape);
            }

            // Solid shapes have an even number of parents
            CompositeShapeData[] solidShapes = eligibleShapes.Where(x => x.parents.Count % 2 == 0).ToArray();
            foreach (CompositeShapeData solidShape in solidShapes)
            {
                solidShape.ValidateHoles();

            }
            // Create polygons from the solid shapes and their associated hole shapes
            Polygon[] polygons = solidShapes.Select(x => new Polygon(x.polygon.points, x.holes.Select(h => h.polygon.points).ToArray())).ToArray();
  
            // Flatten the points arrays from all polygons into a single array, and convert the vector2s to vector3s.
            vertices = polygons.SelectMany(x => x.points.Select(v2 => new Vector3(v2.x, height, v2.y))).ToArray();

            // Triangulate each polygon and flatten the triangle arrays into a single array.
            List<int> allTriangles = new List<int>();
            int startVertexIndex = 0;
            for (int i = 0; i < polygons.Length; i++)
            {
                Triangulator triangulator = new Triangulator(polygons[i]);
                int[] polygonTriangles = triangulator.Triangulate();

                for (int j = 0; j < polygonTriangles.Length; j++)
                {
                    allTriangles.Add(polygonTriangles[j] + startVertexIndex);
                }
                startVertexIndex += polygons[i].numPoints;
            }

            triangles = allTriangles.ToArray();
        }

        public bool ContainsPoint(Vector2 point)
        {
            for (int i = 0; i < triangles.Length; i += 3)
            {
                if (Maths2D.PointInTriangle(vertices[triangles[i]].ToXZ(), vertices[triangles[i + 1]].ToXZ(), vertices[triangles[i + 2]].ToXZ(), point))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
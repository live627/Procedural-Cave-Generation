using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class MeshGenerator : MonoBehaviour {
    public MeshFilter walls;

    public SquareGrid squareGrid;
    List<Vector3> vertices;
    List<int> triangles;

    Dictionary<int, int> EdgeIndicse = new Dictionary<int, int>();

    public void GenerateMesh(int[,] map, float squareSize) {
        squareGrid = new SquareGrid(map, squareSize);

        vertices = new List<Vector3>();
		triangles = new List<int>();
        EdgeIndicse.Clear();
        //triangleDict.Clear();

        for (int x = 0; x < squareGrid.squares.GetLength(0); x ++) {
			for (int y = 0; y < squareGrid.squares.GetLength(1); y ++) {
				TriangulateSquare(squareGrid.squares[x,y]);
			}
		}

		Mesh mesh = new Mesh();
		GetComponent<MeshFilter>().mesh = mesh;

		mesh.vertices = vertices.ToArray();
		mesh.triangles = triangles.ToArray();
		mesh.RecalculateNormals();

        CreateWallMesh ();
    }

    void CreateWallMesh() {
        List<Vector3> wallVertices = new List<Vector3> ();
        List<int> wallTriangles = new List<int> ();

        float wallHeight = 5;
        Mesh wallMesh = new Mesh ();

        int current = -1;
        int next = -1;
        while (EdgeIndicse.Count > 0) {
            current = next == -1 ? EdgeIndicse.First().Key : next;
            var val = EdgeIndicse[current];

            int startIndex = wallVertices.Count;
            wallVertices.Add(vertices[current]); // left
            wallVertices.Add(vertices[val]); // right
            wallVertices.Add(vertices[current] - Vector3.up * wallHeight); // bottom left
			wallVertices.Add(vertices[val] - Vector3.up * wallHeight); // bottom right

            wallTriangles.Add(startIndex + 0);
            wallTriangles.Add(startIndex + 2);
            wallTriangles.Add(startIndex + 3);

            wallTriangles.Add(startIndex + 3);
            wallTriangles.Add(startIndex + 1);
            wallTriangles.Add(startIndex + 0);

            EdgeIndicse.Remove(current);

            next = EdgeIndicse.ContainsKey(val) ? val : -1;
        }

        wallMesh.vertices = wallVertices.ToArray ();
		wallMesh.triangles = wallTriangles.ToArray ();
		walls.mesh = wallMesh;
    }

    void TriangulateSquare(Square square) {
        if (square.configuration == 0) return;
        List<Node> nodelist = new List<Node>(6);

        for (int i = 0; i < 4; ++i) {
            var i_prev = (3 + i)%4; // i->i_prev => 4->3, 3->2, 2->1, 1->4
            if (!square.CNodes[i].active) continue;

            if (nodelist.Count > 0) {
                if (nodelist[nodelist.Count - 1] == square.Nodes[i_prev])
                    nodelist.RemoveAt(nodelist.Count - 1);
                else
                    nodelist.Add(square.Nodes[i_prev]);
            } else
                nodelist.Add(square.Nodes[i_prev]);

            nodelist.Add(square.CNodes[i]);
            nodelist.Add(square.Nodes[i]);
        }
        if (nodelist[nodelist.Count - 1] == nodelist[0]) {
            nodelist.RemoveAt(nodelist.Count - 1);
            nodelist.RemoveAt(0);
        }

        MeshFromPoints(nodelist.ToArray());

        var pair = new int[2] {-1, -1};
        for (int i = 0; i < 4; ++i) {
            if (square.CNodes[i].active) continue;
            if (pair[0] == -1) {
                var i_prev = (3 + i)%4;
                var x = nodelist.IndexOf(square.Nodes[i_prev]);
                pair[0] = x;
            }

            if (pair[1] == -1) {
                var x = nodelist.IndexOf(square.Nodes[i]);
                pair[1] = x;
            }

            if (pair[0] != -1 && pair[1] != -1) {
                EdgeIndicse.Add(nodelist[pair[0]].vertexIndex, nodelist[pair[1]].vertexIndex);
                pair[0] = -1;
                pair[1] = -1;
            }
        }
        if (pair[0] != pair[1]) {
            Debug.DebugBreak();
        }

    }

    void MeshFromPoints(params Node[] points) {
        AssignVertices(points);
        if (points.Length < 3) throw new ArgumentException("cant generate triangle from less than 3 points");
        if (points.Length > 6) Debug.LogError("(points.Length > 6) this is not supposed to happen, but this method could handle it (points.Length = " + points.Length +")");

        for (int i = 0; i < points.Length - 2; ++i) {
            CreateTriangle(points[0], points[i+1], points[i+2]);
        }
    }

    void AssignVertices(Node[] points) {
        for (int i = 0; i < points.Length; i ++) {
            if (points[i].vertexIndex == -1) {
                points[i].vertexIndex = vertices.Count;
                vertices.Add(points[i].position);
            }
        }
    }

    void CreateTriangle(Node a, Node b, Node c) {
        triangles.Add(a.vertexIndex);
        triangles.Add(b.vertexIndex);
        triangles.Add(c.vertexIndex);
    }


    public class SquareGrid {
        public Square[,] squares;

        public SquareGrid(int[,] map, float squareSize) {
            int nodeCountX = map.GetLength(0);
            int nodeCountY = map.GetLength(1);
            float mapWidth = nodeCountX*squareSize;
            float mapHeight = nodeCountY*squareSize;

            ControlNode[,] controlNodes = new ControlNode[nodeCountX, nodeCountY];

            for (int x = 0; x < nodeCountX; x ++) {
                for (int y = 0; y < nodeCountY; y ++) {
                    Vector3 pos = new Vector3(-mapWidth/2 + x*squareSize + squareSize/2, 0,
                        -mapHeight/2 + y*squareSize + squareSize/2);
                    controlNodes[x, y] = new ControlNode(pos, map[x, y] == 1, squareSize);
                }
            }

            squares = new Square[nodeCountX - 1, nodeCountY - 1];
            for (int x = 0; x < nodeCountX - 1; x ++) {
                for (int y = 0; y < nodeCountY - 1; y ++) {
                    squares[x, y] = new Square(controlNodes[x, y + 1], controlNodes[x + 1, y + 1],
                        controlNodes[x + 1, y], controlNodes[x, y]);
                }
            }
        }
    }

    public class Square {
        public ControlNode[] CNodes; // order:  topLeft, topRight, bottomRight, bottomLeft;
        public Node[] Nodes; // order: centreTop, centreRight, centreBottom, centreLeft;

        public int configuration;

        public Square(params ControlNode[] cnodes) {
            if (cnodes.Length != 4)
                throw new ArgumentException("expected the 4 controll nodes: topLeft, topRight, bottomRight, bottomLeft");

            CNodes = cnodes;
            Nodes = new Node[4];
            Nodes[0] = CNodes[0].right;
            Nodes[1] = CNodes[2].above;
            Nodes[2] = CNodes[3].right;
            Nodes[3] = CNodes[3].above;

            foreach (var node in CNodes) {
                configuration = configuration << 1;
                if (node.active) configuration++;
            }
        }
    }

    public class Node {
        public Vector3 position;
        public int vertexIndex = -1;

        public Node(Vector3 _pos) { position = _pos; }
    }

    public class ControlNode : Node {
        public bool active;
        public Node above, right;

        public ControlNode(Vector3 _pos, bool _active, float squareSize) : base(_pos) {
            active = _active;
            above = new Node(position + Vector3.forward*squareSize/2f);
            right = new Node(position + Vector3.right*squareSize/2f);
        }
    }
}
				

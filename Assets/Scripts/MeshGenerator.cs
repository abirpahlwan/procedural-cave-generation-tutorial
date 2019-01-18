using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MeshGenerator : MonoBehaviour {

	struct Triangle {
		public int vertexIndexA;
		public int vertexIndexB;
		public int vertexIndexC;

		private int[] vertices;

		public Triangle (int a, int b, int c) {
			vertexIndexA = a;
			vertexIndexB = b;
			vertexIndexC = c;
			
			vertices = new int[3];
			vertices[0] = a;
			vertices[1] = b;
			vertices[2] = c;
		}

		public int this[int i]
		{
			get { return vertices[i]; }
		}

		public bool Contains(int vertexIndex) {
			return vertexIndex == vertexIndexA || vertexIndex == vertexIndexB || vertexIndex == vertexIndexC;
		}
	}

	public class Node {
		public Vector3 position;
		public int vertexindex = -1;

		public Node(Vector3 position) {
			this.position = position;
		}
	}

	public class ControlNode : Node {
		public bool active;
		public Node above;
		public Node right;

		public ControlNode(Vector3 position, bool active, float squareSize) : base(position) {
			this.active = active;
			this.above = new Node(position + Vector3.forward * squareSize / 2.0f);
			this.right = new Node(position + Vector3.right * squareSize / 2.0f);
		}
	}
	
	public class SquareGrid {
		public Square[,] squares;

		public SquareGrid(int[,] map, float squareSize) {
			int nodeCountX = map.GetLength(0);
			int nodeCountY = map.GetLength(1);

			float mapWidth = nodeCountX * squareSize;
			float mapHeight = nodeCountY * squareSize;
			
			ControlNode[,] controlNodes = new ControlNode[nodeCountX,nodeCountY];

			for (int x = 0; x < nodeCountX; x++) {
				for (int y = 0; y < nodeCountY; y++) {
					Vector3 pos = new Vector3(-mapWidth/2 + x * squareSize + squareSize/2, 0, -mapHeight/2 + y * squareSize + squareSize/2);
					controlNodes[x,y] = new ControlNode(pos, map[x,y] == 1, squareSize);
				}
			}
			
			squares = new Square[nodeCountX-1, nodeCountY-1];
			
			for (int x = 0; x < nodeCountX-1; x++) {
				for (int y = 0; y < nodeCountY-1; y++) {
					squares[x, y] = new Square(controlNodes[x, y+1], controlNodes[x+1, y+1], controlNodes[x+1, y], controlNodes[x,y]);
				}
			}
		}
	}

	public class Square
	{
		public ControlNode topLeft;
		public ControlNode topRight;
		public ControlNode bottomRight;
		public ControlNode bottomLeft;

		public Node centreTop;
		public Node centreRight;
		public Node centreBottom;
		public Node centreLeft;

		public int configuration;

		public Square(ControlNode topLeft, ControlNode topRight, ControlNode bottomRight, ControlNode bottomLeft) {
			this.topLeft = topLeft;
			this.topRight = topRight;
			this.bottomRight = bottomRight;
			this.bottomLeft = bottomLeft;
			
			centreTop = topLeft.right;
			centreRight = bottomRight.above;
			centreBottom = bottomLeft.right;
			centreLeft = bottomLeft.above;

			if (topLeft.active)
				configuration += 8;
			if (topRight.active)
				configuration += 4;
			if (bottomRight.active)
				configuration += 2;
			if (bottomLeft.active)
				configuration += 1;
		}
	}

	public SquareGrid squareGrid;
	public MeshFilter walls;
	private List<Vector3> vertices;
	private List<int> triangles;
	
	Dictionary<int, List<Triangle>> triangleDictionary = new Dictionary<int, List<Triangle>>();
	List<List<int>> outlines = new List<List<int>>();
	HashSet<int> checkedVertices = new HashSet<int>();

	public void GenerateMesh(int[,] map, float squareSize) {
		
		triangleDictionary.Clear();
		outlines.Clear();
		checkedVertices.Clear();
		
		squareGrid = new SquareGrid(map, squareSize);
		
		vertices = new List<Vector3>();
		triangles = new List<int>();
		
		for (int x = 0; x < squareGrid.squares.GetLength(0); x++) {
			for (int y = 0; y < squareGrid.squares.GetLength(1); y++) {
				TriangulateSquare(squareGrid.squares[x,y]);
			}
		}
		
		Mesh mesh = new Mesh();
		GetComponent<MeshFilter>().mesh = mesh;

		mesh.vertices = vertices.ToArray();
		mesh.triangles = triangles.ToArray();
		mesh.RecalculateNormals();

		CreateWallMesh();
	}

	private void CreateWallMesh() {
		CalculateMeshOutlines();
		
		List<Vector3> wallVertices = new List<Vector3>();
		List<int> wallTriangles = new List<int>();
		Mesh wallMesh = new Mesh();

		float wallHeight = 5;

		foreach (var outline in outlines) {
			for (int i = 0; i < outline.Count-1; i++) {
				int startIndex = wallVertices.Count;
				wallVertices.Add(vertices[outline[i]]);	// left
				wallVertices.Add(vertices[outline[i+1]]);	// right
				wallVertices.Add(vertices[outline[i]] - Vector3.up * wallHeight); // bottom left
				wallVertices.Add(vertices[outline[i + 1]] - Vector3.up * wallHeight); // bottom right
				
				wallTriangles.Add(startIndex + 0);
				wallTriangles.Add(startIndex + 2);
				wallTriangles.Add(startIndex + 3);
				
				wallTriangles.Add(startIndex + 3);
				wallTriangles.Add(startIndex + 1);
				wallTriangles.Add(startIndex + 0);
			}
		}

		wallMesh.vertices = wallVertices.ToArray();
		wallMesh.triangles = wallTriangles.ToArray();

		walls.mesh = wallMesh;
	}

	private void TriangulateSquare(Square square) {
		switch (square.configuration) {
			case 0:
				break;
			
			// 1 points:
			case 1:
				MeshFromPoints(square.centreLeft, square.centreBottom, square.bottomLeft);
				break;
			case 2:
				MeshFromPoints(square.bottomRight, square.centreBottom, square.centreRight);
				break;
			case 4:
				MeshFromPoints(square.topRight, square.centreRight, square.centreTop);
				break;
			case 8:
				MeshFromPoints(square.topLeft, square.centreTop, square.centreLeft);
				break;
			
			// 2 points:
			case 3:
				MeshFromPoints(square.centreRight, square.bottomRight, square.bottomLeft, square.centreLeft);
				break;
			case 6:
				MeshFromPoints(square.centreTop, square.topRight, square.bottomRight, square.centreBottom);
				break;
			case 9:
				MeshFromPoints(square.topLeft, square.centreTop, square.centreBottom, square.bottomLeft);
				break;
			case 12:
				MeshFromPoints(square.topLeft, square.topRight, square.centreRight, square.centreLeft);
				break;
			case 5:
				MeshFromPoints(square.centreTop, square.topRight, square.centreRight, square.centreBottom, square.bottomLeft, square.centreLeft);
				break;
			case 10:
				MeshFromPoints(square.topLeft, square.centreTop, square.centreRight, square.bottomRight, square.centreBottom, square.centreLeft);
				break;
			
			// 3 points:
			case 7:
				MeshFromPoints(square.centreTop, square.topRight, square.bottomRight, square.bottomLeft, square.centreLeft);
				break;
			case 11:
				MeshFromPoints(square.topLeft, square.centreTop, square.centreRight, square.bottomRight, square.bottomLeft);
				break;
			case 13:
				MeshFromPoints(square.topLeft, square.topRight, square.centreRight, square.centreBottom, square.bottomLeft);
				break;
			case 14:
				MeshFromPoints(square.topLeft, square.topRight, square.bottomRight, square.centreBottom, square.centreLeft);
				break;
			
			// 4 points:
			case 15:
				MeshFromPoints(square.topLeft, square.topRight, square.bottomRight, square.bottomLeft);
				checkedVertices.Add(square.topLeft.vertexindex);
				checkedVertices.Add(square.topRight.vertexindex);
				checkedVertices.Add(square.bottomRight.vertexindex);
				checkedVertices.Add(square.bottomLeft.vertexindex);
				break;
		}
	}

	private void MeshFromPoints(params Node[] nodes) {
		AssignVertices(nodes);

		if (nodes.Length >= 3)
			CreateTriangle(nodes[0], nodes[1], nodes[2]);
		if (nodes.Length >= 4)
			CreateTriangle(nodes[0], nodes[2], nodes[3]);
		if (nodes.Length >= 5)
			CreateTriangle(nodes[0], nodes[3], nodes[4]);
		if (nodes.Length >= 6)
			CreateTriangle(nodes[0], nodes[4], nodes[5]);
	}

	private void AssignVertices(Node[] nodes) {
		for (int i = 0; i < nodes.Length; i++) {
			if (nodes[i].vertexindex == -1) {
				nodes[i].vertexindex = vertices.Count;
				vertices.Add(nodes[i].position);
			}
		}
	}
	
	private void CreateTriangle(Node a, Node b, Node c) {
		triangles.Add(a.vertexindex);
		triangles.Add(b.vertexindex);
		triangles.Add(c.vertexindex);
		
		Triangle triangle = new Triangle(a.vertexindex, b.vertexindex, c.vertexindex);
		AddTriangleToDictionary(triangle.vertexIndexA, triangle);
		AddTriangleToDictionary(triangle.vertexIndexB, triangle);
		AddTriangleToDictionary(triangle.vertexIndexC, triangle);
	}

	private void AddTriangleToDictionary(int vertexIndexKey, Triangle triangle) {
		if (triangleDictionary.ContainsKey(vertexIndexKey)) {
			triangleDictionary[vertexIndexKey].Add(triangle);
		}
		else {
			List<Triangle> triangleList = new List<Triangle>();
			triangleList.Add(triangle);
			triangleDictionary.Add(vertexIndexKey, triangleList);
		}
	}

	private void FollowOutLine(int vertexIndex, int outlineIndex) {
		outlines[outlineIndex].Add(vertexIndex);
		checkedVertices.Add(vertexIndex);
		
		int nextVertexIndex = GetConnectedOutlineVertex(vertexIndex);

		if (nextVertexIndex != -1) {
			FollowOutLine(nextVertexIndex, outlineIndex);
		}
	}

	private void CalculateMeshOutlines() {
		for (int vertexIndex = 0; vertexIndex < vertices.Count; vertexIndex++) {
			if (!checkedVertices.Contains(vertexIndex)) {
				int newOutlineVertex = GetConnectedOutlineVertex(vertexIndex);
				if (newOutlineVertex != -1) {
					checkedVertices.Add(vertexIndex);

					List<int> newOutline = new List<int>();
					newOutline.Add(vertexIndex);
					outlines.Add(newOutline);
					FollowOutLine(newOutlineVertex, outlines.Count - 1);
					outlines[outlines.Count - 1].Add(vertexIndex);
				}
			}
		}
	}

	private int GetConnectedOutlineVertex(int vertexIndex) {
		List<Triangle> trianglesContainingVertex = triangleDictionary[vertexIndex];

		for (int i = 0; i < trianglesContainingVertex.Count; i++) {
			Triangle triangle = trianglesContainingVertex[i];

			for (int j = 0; j < 3; j++) {
				int vertexB = triangle[j];

				if (vertexB != vertexIndex && !checkedVertices.Contains(vertexB)) {
					if (IsOutLineEdge(vertexIndex, vertexB)) {
						return vertexB;
					}
				}
			}
		}

		return -1;
	}

	private bool IsOutLineEdge(int vertexA, int vertexB) {
		List<Triangle> trianglesContainingVertexA = triangleDictionary[vertexA];
		int sharedTriangleCount = 0;

		for (int i = 0; i < trianglesContainingVertexA.Count; i++) {
			if (trianglesContainingVertexA[i].Contains(vertexB)) {
				sharedTriangleCount++;
				if (sharedTriangleCount > 1) {
					break;
				}
			}
		}

		return sharedTriangleCount == 1;
	}
}

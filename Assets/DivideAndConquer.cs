using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static GK.ConvexHullCalculator;
using TMPro;

public class DivideAndConquer : MonoBehaviour
{
    public GameObject pointModel;
    public GameObject lineModel;
    public int nRand = 0;
    public List<Vector3> points;
    public GameObject textObject;
    public CameraController CC;

    private List<GameObject> pointObjects;
    private Mesh lineMesh;
    private Mesh mesh;
    private List<Hull> steps;
    private int step;
    private int curStep;

    private bool isPlaying;
    private bool isMoving;
    private float playSpeed = 2f;
    private float timeSinceLastUpdate;

    private TextMeshPro textHolder;

    private static GK.ConvexHullCalculator chc = new GK.ConvexHullCalculator();

    private class Face
    {
        public int a { get; private set; }
        public int b { get; private set; }
        public int c { get; private set; }

        public Face(int a, int b, int c)
        {
            this.a = a;
            this.b = b;
            this.c = c;
        }

        public override bool Equals(object obj)
        {
            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }

            Face other = (Face)obj;

            return (this.a == other.a || this.a == other.b || this.a == other.c)
                && (this.b == other.a || this.b == other.b || this.b == other.c)
                && (this.c == other.a || this.c == other.b || this.c == other.c);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }

    private class Triangle
    {
        public Vector3 a { get; private set; }
        public Vector3 b { get; private set; }
        public Vector3 c { get; private set; }
        public Vector3 center { get; private set; }
        public Vector3 normal { get; private set; }

        private bool isFlipped;
        private Color? color;

        public Triangle(Vector3 a, Vector3 b, Vector3 c)
        {
            this.a = a;
            this.b = b;
            this.c = c;

            this.center = (a + b + c) / 3;
            this.normal = Vector3.Cross(a - b, a - c);

            this.isFlipped = false;
            this.color = null;
        }

        public Triangle(Vector3 a, Vector3 b, Vector3 c, Vector3 target_c)
        {
            this.a = a;
            this.b = b;
            this.c = c;

            this.center = (a + b + c) / 3;
            this.normal = Vector3.Cross(a - b, a - c);

            if (Vector3.Dot(this.center - target_c, this.normal) < 0)
            {
                this.normal *= -1;
                this.isFlipped = true;
            }

            this.color = null;
        }

        public Mesh GenMesh( Color? color = null)
        {
            var output = new Mesh();

            output.vertices = new Vector3[] { a, b, c };

            if (color != null) output.colors = new Color[] { color.Value, color.Value, color.Value };
            else if (this.color != null) output.colors = new Color[] { this.color.Value, this.color.Value, this.color.Value };

            if (isFlipped)
            {
                output.triangles = new int[] { 0, 2, 1 };
            } else
            {
                output.triangles = new int[] { 0, 1, 2 };
            }

            return output;
        }

        public Triangle SetColor(Color color)
        {
            this.color = color;

            return this;
        }
    }

    private class Line
    {
        private Vector3 a;
        private Vector3 b;

        private Color? color;

        public Line(Vector3 a, Vector3 b)
        {
            this.a = a;
            this.b = b;

            this.color = null;
        }

        public Mesh GenMesh(float radius, Mesh mesh)
        {
            var out_mesh = new Mesh();
            var vertices = new List<Vector3>(mesh.vertices);
            var triangles = new List<int>(mesh.triangles);

            var scale = (b - a).magnitude / 2;

            Matrix4x4 scaleMatrix = Matrix4x4.Scale(new Vector3(radius, scale, radius));
            Matrix4x4 transMatrix = Matrix4x4.Translate((b + a) / 2);
            Matrix4x4 lookAtMatrix = Matrix4x4.Rotate(Quaternion.LookRotation(a - b)) * Matrix4x4.Rotate(Quaternion.Euler(90, 0, 0));

            Matrix4x4 transformationMatrix = transMatrix * lookAtMatrix * scaleMatrix;

            for (int i = 0; i < vertices.Count; ++i)
            {
                vertices[i] = transformationMatrix.MultiplyPoint(vertices[i]);
            }

            out_mesh.vertices = vertices.ToArray();
            out_mesh.triangles = triangles.ToArray();

            if (color != null)
            {
                var colors = new List<Color>();

                foreach (var v in vertices)
                {
                    colors.Add(color.Value);
                }

                out_mesh.colors = colors.ToArray();
            }

            return out_mesh;
        }

        public Line SetColor(Color color)
        {
            this.color = color;

            return this;
        }
    }

    private class Hull
    {
        public Mesh mesh { get; private set; }
        public List<Line> lines { get; private set; }
        public List<Triangle> triangles { get; private set; }
        public string description = "";

        public Hull(Hull hull, bool doLines = true, bool doTriangles = true)
        {
            this.mesh = new Mesh();
            this.lines = new List<Line>();

            this.mesh.vertices = new List<Vector3>(hull.mesh.vertices).ToArray();
            this.mesh.triangles = new List<int>(hull.mesh.triangles).ToArray();
            this.mesh.normals = new List<Vector3>(hull.mesh.normals).ToArray();

            if (doLines) this.lines = new List<Line>(hull.lines);
            if (doTriangles) this.triangles = new List<Triangle>(hull.triangles);
        }

        public Hull(Mesh mesh)
        {
            this.mesh = new Mesh();
            this.lines = new List<Line>();
            this.triangles = new List<Triangle>();

            this.mesh.vertices = new List<Vector3>(mesh.vertices).ToArray();
            this.mesh.triangles = new List<int>(mesh.triangles).ToArray();
            this.mesh.normals = new List<Vector3>(mesh.normals).ToArray();
        }

        public Hull(SlowHullData shd, List<Vector3> pts)
        {
            mesh = new Mesh();
            lines = new List<Line>();
            triangles = new List<Triangle>();

            List<Vector3> vertices = new List<Vector3>();
            List<int> faces = new List<int>();

            for (var i = 0; i < shd.points.Count; i += 3)
            {
                vertices.Add(pts[shd.points[i]]);
                vertices.Add(pts[shd.points[i + 1]]);
                vertices.Add(pts[shd.points[i + 2]]);

                faces.Add(i);
                faces.Add(i + 1);
                faces.Add(i + 2);
            }

            //mesh.vertices = vertices.ToArray();
            //mesh.triangles = faces.ToArray();

            List<Vector3> mesh_vertices = new List<Vector3>();
            List<int> mesh_faces = new List<int>();
            for (int i = 0; i < vertices.Count; ++i)
            {
                mesh_vertices.Add(vertices[faces[i]]);
                mesh_faces.Add(i);
            }

            mesh.vertices = mesh_vertices.ToArray();
            mesh.triangles = mesh_faces.ToArray();

            mesh.RecalculateNormals();
        }

        public Mesh getFull(Mesh lineMesh)
        {
            List<CombineInstance> combine = new List<CombineInstance>();

            var main_mesh = new CombineInstance();
            main_mesh.mesh = mesh;
            main_mesh.transform = Matrix4x4.identity;
            combine.Add(main_mesh);

            foreach (var line in lines)
            {
                var next_combine = new CombineInstance();

                next_combine.mesh = line.GenMesh(0.1f, lineMesh);
                next_combine.transform = Matrix4x4.identity;

                combine.Add(next_combine);
            }

            foreach (var triangle in triangles)
            {
                var next_combine = new CombineInstance();

                next_combine.mesh = triangle.GenMesh();
                next_combine.transform = Matrix4x4.identity;

                combine.Add(next_combine);
            }

            var out_mesh = new Mesh();
            out_mesh.CombineMeshes(combine.ToArray());

            return out_mesh;
        }

        public void AddLine(Vector3 a, Vector3 b, Color? color = null)
        {
            if (color == null) lines.Add(new Line(a, b));
            else lines.Add(new Line(a, b).SetColor(color.Value));
        }

        public void AddTriangle(Triangle t)
        {
            triangles.Add(t);
        }
    }

    private class SlowHullData
    {
        public List<Face> faces { get; private set; }
        public List<int> points { get; private set; }

        public SlowHullData(List<Face> faces, List<int> points)
        {
            this.faces = faces;
            this.points = points;
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        points = genRandomPoints(nRand);
        pointObjects = new List<GameObject>();
        lineMesh = lineModel.GetComponent<MeshFilter>().sharedMesh;
        mesh = GetComponent<MeshFilter>().mesh;
        textHolder = textObject.GetComponent<TextMeshPro>();
        steps = new List<Hull>();
        step = 0;

        isPlaying = false;
        isMoving = false;
        timeSinceLastUpdate = 0f;
        
        foreach (var point in points)
        {
            pointObjects.Add(Instantiate(pointModel, point, Quaternion.identity));
        }

        ConvexHull(points);
        UpdateMesh();
    }

    // Update is called once per frame
    void Update()
    {
        /*
        if (Input.GetMouseButton(0))
        {
            float speed = 600f;
            Vector3 rot = -new Vector3(Input.GetAxis("Mouse Y"), -Input.GetAxis("Mouse X"), 0) * Time.deltaTime * speed;

            transform.Rotate(rot, Space.World);

            foreach (var obj in pointObjects)
            {
                obj.transform.position = Quaternion.Euler(rot) * obj.transform.position;
            }
        }
        */

        if (isPlaying)
        {
            timeSinceLastUpdate += Time.deltaTime;

            while (timeSinceLastUpdate >= playSpeed)
            {
                timeSinceLastUpdate -= playSpeed;

                IncStep();
            }
        }
        else
        {
            timeSinceLastUpdate = 0f;
        }
        
        Touch trigger = Input.GetTouch(0);
        if (trigger.phase == TouchPhase.Began)
        {
            isMoving = true;
        } else if (trigger.phase == TouchPhase.Ended)
        {
            isMoving = false;
        }

        //if (Input.GetMouseButton(0))
        if (isMoving)
        {
            var prev = CC.previousDirection;
            var cur = CC.currentDirection;

            Quaternion rot = Quaternion.FromToRotation(prev, cur);
            Vector3 eul_rot = rot.eulerAngles; eul_rot.y *= 1;
            rot = Quaternion.Euler(-2*eul_rot);

            //transform.Rotate(rot.eulerAngles);
            //transform.rotation = Quaternion.Euler(transform.rotation.eulerAngles + rot.eulerAngles);
            transform.rotation = rot * transform.rotation;

            foreach(var obj in pointObjects)
            {
                obj.transform.position = rot * obj.transform.position;
            }
        }
    }

    public void Randomize()
    {
        foreach (var point in pointObjects)
        {
            Destroy(point);
        }

        transform.rotation = Quaternion.Euler(Vector3.zero);

        Start();
    }

    public void FastForward()
    {
        playSpeed /= 2;
        timeSinceLastUpdate = 0;
    }

    public void SlowDown()
    {
        playSpeed *= 2;
        timeSinceLastUpdate = 0;
    }

    public void Rewind()
    {
        step = 0;
        isPlaying = false;
        UpdateMesh();
    }

    public void IncStep()
    {
        step = Mathf.Min(steps.Count - 1, step + 1);
        UpdateMesh();
    }

    public void DecStep()
    {
        step = Mathf.Max(0, step - 1);
        UpdateMesh();
    }

    public void SetIsPlaying(bool isPlaying)
    {
        this.isPlaying = isPlaying;
    }

    private void UpdateMesh()
    {
        var next_mesh = steps[step].getFull(lineMesh);

        textHolder.text = steps[step].description;

        mesh.Clear();
        mesh.vertices = next_mesh.vertices;
        mesh.triangles = next_mesh.triangles;
        mesh.normals = next_mesh.normals;
        mesh.colors = next_mesh.colors;
        mesh.RecalculateNormals();
    }

    private void ConvexHull(List<Vector3> points)
    {
        steps = new List<Hull>();

        steps.Add(new Hull(new Mesh()));
        steps[steps.Count - 1].description = "Controls:\nPress the trigger to interact.\nPress and hold the trigger to rotate the figure.\n\nButton Index:\nTop Row: Step Back, Play, Pause, Step Forward\nBottom Row: Randomize, Slow Down, Speed Up, Rewind";

        Vector3[] copy = new Vector3[points.Count];
        points.CopyTo(copy);

        // Sort by x
        List<Vector3> verts = new List<Vector3>(copy);
        verts.Sort(CompareX);

        // Split up
        var level_0 = SplitArray(verts, 4);
        var tmp_level = new List<List<Vector3>>(level_0);
        
        var tree = new List<List<List<Vector3>>>();
        tree.Add(level_0);

        while (tmp_level.Count > 1)
        {
            var next_level = new List<List<Vector3>>();

            for (var i = 0; i < tmp_level.Count - 1; i += 2)
            {
                next_level.Add(new List<Vector3>(tmp_level[i].Concat(tmp_level[i + 1])));
            }

            if (tmp_level.Count % 2 == 1)
            {
                next_level.Add(tmp_level[tmp_level.Count - 1]);
            }

            tree.Add(next_level);
            tmp_level = next_level;
        }

        var hulls = new List<Hull>();
        mesh.Clear();

        for (var level_count = 0; level_count < tree.Count; ++level_count)
        {
            var level = tree[level_count];

            if (level.Count == 1)
            {
                var next_mesh = new Mesh();
                List<Vector3> vertices = new List<Vector3>();
                List<int> tris = new List<int>();
                List<Vector3> normals = new List<Vector3>();

                chc.GenerateHull(level[0], true, ref vertices, ref tris, ref normals);

                next_mesh.vertices = vertices.ToArray();
                next_mesh.triangles = tris.ToArray();
                next_mesh.normals = normals.ToArray();

                var next_step = new Hull(next_mesh);

                next_step.description = "Remove all interior vertices and the convex hull is complete.";

                steps.Add(next_step);
            } else if (level_count == 0)
            {
                foreach (var point_set in level)
                {
                    var slowHull = SlowHull(point_set);
                    var tempHull = new Hull(slowHull, point_set);

                    hulls.Add(tempHull);
                }

                List<CombineInstance> combine = new List<CombineInstance>();
                foreach (var hull in hulls)
                {
                    CombineInstance next_combine = new CombineInstance();
                    next_combine.mesh = hull.getFull(lineMesh);
                    next_combine.transform = Matrix4x4.identity;
                    combine.Add(next_combine);

                    var next_mesh = new Mesh();
                    next_mesh.CombineMeshes(combine.ToArray());

                    var next_step = new Hull(next_mesh);

                    next_step.description = "Sort the list of points by their x-positions. Seperate the list of points into smaller but approximately equal sized lists.\n\nBrute force create a convex hull on each smaller list.";

                    steps.Add(next_step);
                }
            } else
            {
                List<CombineInstance> combine = new List<CombineInstance>();
                foreach (var point_set in level)
                {
                    CombineInstance next_combine = new CombineInstance();

                    var next_mesh_c = new Mesh();
                    List<Vector3> vertices = new List<Vector3>();
                    List<int> tris = new List<int>();
                    List<Vector3> normals = new List<Vector3>();

                    chc.GenerateHull(point_set, true, ref vertices, ref tris, ref normals);

                    next_mesh_c.vertices = vertices.ToArray();
                    next_mesh_c.triangles = tris.ToArray();
                    next_mesh_c.normals = normals.ToArray();

                    next_combine.mesh = next_mesh_c;
                    next_combine.transform = Matrix4x4.identity;

                    combine.Add(next_combine);
                }

                var next_mesh = new Mesh();
                next_mesh.CombineMeshes(combine.ToArray());

                var next_step = new Hull(next_mesh);

                next_step.description = "Clean up interior vertices and repeat merging on your new set of convex hulls.";

                steps.Add(next_step);
            }

            for (var pair = 0; pair < level.Count - 1; pair += 2)
            {
                var left = ProjectPointsToZ(level[pair]);
                var right = ProjectPointsToZ(level[pair + 1]);
                int l_ind = 0;
                int r_ind = 0;

                for (var i = 0; i < left.Count; ++i)
                {
                    if (left[i].x > left[l_ind].x)
                        l_ind = i;
                }

                for (var i = 0; i < right.Count; ++i)
                {
                    if (right[i].x < right[r_ind].x)
                        r_ind = i;
                }

                bool change = true;
                while (change)
                {
                    change = false;

                    if (IsLeft(right[r_ind], left[(l_ind + left.Count - 1) % left.Count], left[l_ind]))
                    {
                        change = true;
                        l_ind = (l_ind + left.Count - 1) % left.Count;
                    }

                    if (IsLeft(right[(r_ind + right.Count + 1) % right.Count], left[l_ind], right[r_ind]))
                    {
                        change = true;
                        r_ind = (r_ind + right.Count + 1) % right.Count;
                    }
                }

                steps.Add(new Hull(steps[steps.Count - 1], false));
                steps[steps.Count - 1].AddLine(left[l_ind], right[r_ind], Color.blue);
                steps[steps.Count - 1].description = "For the next pair of hulls, sort the vertices of each hull radially. Project the hull vertices on to the xy plane and create a 2D convex hull in the plane for each 3D hull.\nFind the anchor points of each 2D hull and connect them to form a line to flip over.";

                level[pair] = SortRadially(level[pair], left[l_ind]);
                level[pair + 1] = SortRadially(level[pair + 1], right[r_ind]);

                var all_center = Vector3.zero;

                var left_center = Vector3.zero;
                foreach (var p in level[pair])
                {
                    left_center += p;
                    all_center += p;
                }
                left_center /= level[pair].Count;

                var right_center = Vector3.zero;
                foreach (var p in level[pair + 1])
                {
                    right_center += p;
                    all_center += p;
                }
                right_center /= level[pair + 1].Count;

                all_center /= (level[pair].Count + level[pair + 1].Count);
                //left_center = all_center;
                //right_center = all_center;

                l_ind = 0;
                r_ind = 0;
                var test = 20;
                do
                {
                    var left_point = FindBestLeft(level[pair], level[pair][l_ind], level[pair + 1][r_ind]);
                    var right_point = FindBestRight(level[pair + 1], level[pair][l_ind], level[pair + 1][r_ind]);

                    var l_tmp = new Triangle(level[pair + 1][r_ind], level[pair][l_ind], level[pair][left_point], left_center).SetColor(Color.yellow);
                    var r_tmp = new Triangle(level[pair + 1][r_ind], level[pair][l_ind], level[pair + 1][right_point], right_center).SetColor(Color.yellow);

                    var left_triangle = new Triangle(level[pair + 1][r_ind], level[pair][l_ind], level[pair][left_point], left_center);
                    if (IsInfront(level[pair + 1][right_point], left_triangle))
                    {
                        r_ind = right_point;

                        steps.Add(new Hull(steps[steps.Count - 1], false));

                        steps[steps.Count - 1].AddLine(l_tmp.a, l_tmp.b, Color.blue);
                        steps[steps.Count - 1].AddLine(l_tmp.b, l_tmp.c, Color.green);
                        steps[steps.Count - 1].AddLine(l_tmp.c, l_tmp.a, Color.green);
                        steps[steps.Count - 1].AddLine(r_tmp.b, r_tmp.c, Color.red);
                        steps[steps.Count - 1].AddLine(r_tmp.c, r_tmp.a, Color.red);

                        steps[steps.Count - 1].description = "Find triangles adjacent to the next edge by flipping over the edge.\n\n(You are essentially performing gift wrapping here)";

                        steps.Add(new Hull(steps[steps.Count - 1], false));

                        steps[steps.Count - 1].description = "Choose the outermost triangle to preserve convexity. Update which edge is being flipped over to be the one you just made.";

                        var l_pt = level[pair][l_ind];
                        var r_pt = level[pair + 1][r_ind];
                        steps[steps.Count - 1].AddLine(l_pt, r_pt, Color.blue);

                        if (!((l_pt == r_tmp.a && r_pt == r_tmp.b) || (r_pt == r_tmp.a && l_pt == r_tmp.b)))
                            steps[steps.Count - 1].AddLine(r_tmp.a, r_tmp.b, Color.red);

                        if (!((l_pt == r_tmp.b && r_pt == r_tmp.c) || (r_pt == r_tmp.b && l_pt == r_tmp.c)))
                            steps[steps.Count - 1].AddLine(r_tmp.b, r_tmp.c, Color.red);

                        if (!((l_pt == r_tmp.c && r_pt == r_tmp.a) || (r_pt == r_tmp.c && l_pt == r_tmp.a)))
                            steps[steps.Count - 1].AddLine(r_tmp.c, r_tmp.a, Color.red);
                        
                        steps[steps.Count - 1].AddTriangle(r_tmp);
                    } else
                    {
                        l_ind = left_point;

                        steps.Add(new Hull(steps[steps.Count - 1], false));

                        steps[steps.Count - 1].AddLine(l_tmp.a, l_tmp.b, Color.blue);
                        steps[steps.Count - 1].AddLine(l_tmp.b, l_tmp.c, Color.green);
                        steps[steps.Count - 1].AddLine(l_tmp.c, l_tmp.a, Color.green);
                        steps[steps.Count - 1].AddLine(r_tmp.b, r_tmp.c, Color.red);
                        steps[steps.Count - 1].AddLine(r_tmp.c, r_tmp.a, Color.red);

                        steps[steps.Count - 1].description = "Find triangles adjacent to the next edge by rotating around the edge.";

                        steps.Add(new Hull(steps[steps.Count - 1], false));

                        steps[steps.Count - 1].description = "Choose the outermost triangle to preserve convexity. Update which edge is being flipped over to be the one you just made.";

                        var l_pt = level[pair][l_ind];
                        var r_pt = level[pair + 1][r_ind];
                        steps[steps.Count - 1].AddLine(l_pt, r_pt, Color.blue);

                        if (!((l_pt == l_tmp.a && r_pt == l_tmp.b) || (r_pt == l_tmp.a && l_pt == l_tmp.b)))
                            steps[steps.Count - 1].AddLine(l_tmp.a, l_tmp.b, Color.green);

                        if (!((l_pt == l_tmp.b && r_pt == l_tmp.c) || (r_pt == l_tmp.b && l_pt == l_tmp.c)))
                            steps[steps.Count - 1].AddLine(l_tmp.b, l_tmp.c, Color.green);

                        if (!((l_pt == l_tmp.c && r_pt == l_tmp.a) || (r_pt == l_tmp.c && l_pt == l_tmp.a)))
                            steps[steps.Count - 1].AddLine(l_tmp.c, l_tmp.a, Color.green);
                        
                        steps[steps.Count - 1].AddTriangle(l_tmp);
                    }

                    if (--test < 0) break;
                } while (l_ind != 0 || r_ind != 0);

                if (pair + 2 < level.Count - 1)
                {
                    steps.Add(new Hull(steps[steps.Count - 1], false));
                    steps[steps.Count - 1].description = "Clean up all interior vertices and continue on to the next pair of hulls.";
                }
            }
        }
    }

    private bool IsLeft(Vector3 p, Vector3 o, Vector3 t)
    {
        Vector3 a = new Vector3(t.x - o.x, t.y - o.y, 0).normalized;
        Vector3 b = new Vector3(p.x - t.x, p.y - t.y, 0).normalized;

        return Vector3.Cross(a, b).z > 0;
    }

    private List<Vector3> ProjectPointsToZ (List<Vector3> points)
    {
        points.Sort(CompareX);

        var hull_2d = new List<Vector3>();

        for (var i = 0; i < 2 * points.Count; i++)
        {
            var j = i < points.Count ? i : 2 * points.Count - (i + 1);

            while (hull_2d.Count >= 2 && CheckMiddleZHull(hull_2d[hull_2d.Count - 2], hull_2d[hull_2d.Count - 1], points[j])) {
                hull_2d.RemoveAt(hull_2d.Count - 1);
            }

            hull_2d.Add(points[j]);
        }

        hull_2d.RemoveAt(hull_2d.Count - 1);

        return hull_2d;
    }

    private bool CheckMiddleZHull(Vector3 a, Vector3 b, Vector3 c)
    {
        var cross = (a.x - b.x) * (c.y - b.y) - (a.y - b.y) * (c.x - b.x);
        var dot = (a.x - b.x) * (c.x - b.x) + (a.y - b.y) * (c.y - b.y);
        return cross < 0 || (cross == 0 && dot <= 0);
    }

    private SlowHullData SlowHull(List<Vector3> points, int start = 0, int end = -1)
    {
        if (end < start) end = points.Count;

        var valid_faces = new List<Face>();
        var valid_points = new List<int>();

        for (int i = start; i < end; ++i)
        {
            for (int j = start; j < end; ++j)
            {
                if (i == j) continue;

                for (int k = start; k < end; ++k)
                {
                    if (i == k || j == k) continue;

                    var triangle = new Triangle(points[i], points[j], points[k]);
                    var success = true;

                    for (var l = start; l < end; l++)
                    {
                        if (l == i || l == j || l == k) continue;

                        if (Vector3.Dot(triangle.normal, points[l] - triangle.center) > 0) success = false;
                    }

                    foreach (var face in valid_faces)
                    {
                        if (face.Equals(new Face(i, j, k))) success = false;
                    }

                    if (success)
                    {
                        valid_faces.Add(new Face(i, j, k));
                        valid_points.Add(i);
                        valid_points.Add(j);
                        valid_points.Add(k);
                    }
                }
            }
        }

        return new SlowHullData(valid_faces, valid_points);
    }

    private List<List<Vector3>> SplitArray(List<Vector3> arr, int size)
    {
        List<List<Vector3>> new_arr = new List<List<Vector3>>();
        
        for (int i = 0; i < arr.Count; i += size)
        {
            if (arr.Count - (size + i) < size)
            {
                new_arr.Add(arr.GetRange(i, arr.Count - i));
                return new_arr;
            }

            new_arr.Add(arr.GetRange(i, 4));
        }

        return new_arr;
    }

    private List<Vector3> genRandomPoints(int n, Vector3? maximum = null)
    {
        if (maximum == null)
            maximum = new Vector3(10, 10, 10);

        Vector3 maxP = maximum.Value;

        List<Vector3> output = new List<Vector3>();
        for (int i = 0; i < n; ++i)
        {
            output.Add(new Vector3(Random.Range(0f, maxP.x), Random.Range(0f, maxP.y), Random.Range(0f, maxP.z)) - maxP / 2);
        }
        return output;
    }

    private static int CompareX(Vector3 p, Vector3 q)
    {
        return p.x - q.x > 0 ? 1 : -1;
    }

    private static int CompareY(Vector3 p, Vector3 q)
    {
        return p.y - q.y > 0 ? 1 : -1;
    }

    private static int CompareZ(Vector3 p, Vector3 q)
    {
        return p.z - q.z > 0 ? 1 : -1;
    }

    private List<Vector3> SortRadially(List<Vector3> points, Vector3 start)
    {
        List<Vector3> tmp = new List<Vector3>(points);
        Vector3 center = Vector3.zero;

        tmp.Sort(CompareY);
        center.y = (tmp[0].y + tmp[tmp.Count - 1].y) / 2;

        tmp.Sort(CompareZ); tmp.Reverse();
        center.z = (tmp[0].z + tmp[tmp.Count - 1].z) / 2;

        tmp = new List<Vector3>(points);

        List<KeyValuePair<int, float>> angle_map = new List<KeyValuePair<int, float>>();
        float? starting_angle = null;
        for (var point_ind = 0; point_ind < tmp.Count; ++point_ind)
        {
            var point = tmp[point_ind];

            float angle = Mathf.Atan2(point.z - center.z, point.y - center.y);

            if (starting_angle == null) starting_angle = angle;
            else if (angle < starting_angle) angle += Mathf.PI * 2;

            angle_map.Add(new KeyValuePair<int, float>(point_ind, angle));
        }
        
        angle_map.Sort((a, b) => { return a.Value - b.Value > 0 ? 1 : -1; });

        var output = new List<Vector3>();
        foreach (var kvp in angle_map)
        {
            output.Add(tmp[kvp.Key]);
        }

        while (!output[0].Equals(start))
        {
            var temp = output[0];
            output.RemoveAt(0);
            output.Add(temp);
        }

        return output;
    }

    private int FindBestLeft(List<Vector3> points, Vector3 left, Vector3 right)
    {
        int? best = null;

        Vector3 center = Vector3.zero;
        foreach (var p in points)
        {
            center += p;
        }
        center /= points.Count;

        for (var i = 0; i < points.Count; ++i)
        {
            if (points[i].Equals(left)) continue;
            if (best == null) { best = i; continue; }

            if (IsInfront(points[i], new Triangle(right, left, points[best.Value]))) best = i;
        }

        return best.Value;
    }

    private int FindBestRight(List<Vector3> points, Vector3 left, Vector3 right)
    {
        int? best = null;

        for (var i = 0; i < points.Count; ++i)
        {
            if (points[i].Equals(right)) continue;
            if (best == null) { best = i; continue; }

            if (IsInfront(points[i], new Triangle(right, left, points[best.Value]))) best = i;
        }

        return best.Value;
    }

    private bool IsInfront(Vector3 point, Triangle triangle)
    {
        var m = triangle.center;
        var n = triangle.normal;

        return Vector3.Dot(n, point - m) > 0;
    }
}

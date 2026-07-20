using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct Triangle
{
    public Vector4 v0, v1, v2;
    public Vector3 uv0, uv1, uv2;
    public Vector3 n0, n1, n2;
};

[Serializable]
public struct MathematicalPlane
{
    public Vector3 normal;
    public float distance;
};

[Serializable]
public struct StartPosition
{
    public Vector3 playerStart;
    public int sectorId;
};

[Serializable]
public struct PolygonMeta
{
    public int edgeStartIndex;
    public int edgeCount;

    public int triangleStartIndex;
    public int triangleCount;

    public int connectedSectorId;
    public int sectorId;

    public int collider;
    public int opaque;

    public int plane;
};

[Serializable]
public struct SectorMeta
{
    public int polygonStartIndex;
    public int polygonCount;

    public int sectorId;
};

public class LevelLoader : MonoBehaviour
{
    public string Name = "twohallways-clear";

    public bool debug = false;

    public float speed = 7f;
    public float jumpHeight = 2f;
    public float gravity = 5f;
    public float sensitivity = 10f;
    public float clampAngle = 90f;
    public float smoothFactor = 25f;

    Vector2 targetRotation;
    Vector3 targetMovement;
    Vector2 currentRotation;
    Vector3 currentForce;

    CharacterController Player;

    TopLevelLists LevelLists;
    List<Vector2> vertices = new List<Vector2>();
    List<Sector> sectors = new List<Sector>();
    List<StartSector> starts = new List<StartSector>();
    List<Vector3> ceilingverts = new List<Vector3>();
    List<int> ceilingtri = new List<int>();
    List<Vector3> floorverts = new List<Vector3>();
    List<int> floortri = new List<int>();
    Material opaquematerial;
    List<MeshCollider> CollisionSectors = new List<MeshCollider>();
    List<Mesh> CollisionMesh = new List<Mesh>();
    GameObject CollisionObjects;
    bool[] processbool;
    Vector4[] processvertices;
    Vector3[] processtextures;
    Vector3[] processnormals;
    Vector4[] temporaryvertices;
    Vector3[] temporarytextures;
    Vector3[] temporarynormals;
    List<List<SectorMeta>> ListOfSectorLists = new List<List<SectorMeta>>();
    List<List<Rect>> ListOfRectangleLists = new List<List<Rect>>();
    Camera Cam;
    Vector3 CamPoint;
    SectorMeta CurrentSector;
    SectorMeta NextSector;
    List<SectorMeta> Sectors = new List<SectorMeta>();
    List<SectorMeta> OldSectors = new List<SectorMeta>();
    List<Vector3> OutEdgeVertices = new List<Vector3>();
    bool radius;
    bool check;
    float planeDistance;
    double Ceiling;
    double Floor;
    MathematicalPlane LeftPlane;
    MathematicalPlane TopPlane;
    List<Vector3> flooruvs = new List<Vector3>();
    List<Vector3> ceilinguvs = new List<Vector3>();
    Rect combinedRectangle;
    Matrix4x4 view;
    Matrix4x4 projection;
    GraphicsBuffer triBuffer;
    List<Triangle> outTriangles = new List<Triangle>();
    List<Vector3> colliderVertices = new List<Vector3>();
    List<int> colliderTriangles = new List<int>();
    List<Rect> debugRectangles = new List<Rect>();
    Texture2D linetexture;

    [Serializable]
    public class Sector
    {
        public float floorHeight;
        public float ceilingHeight;
        public List<int> vertexIndices = new List<int>();
        public List<int> wallTypes = new List<int>(); // -1 for solid, sector index for portal
    }

    [Serializable]
    public class StartSector
    {
        public Vector3 location;
        public float angle;
        public int sector;
    }

    [Serializable]
    public class TopLevelLists
    {
        public List<Vector3> vertices = new List<Vector3>();
        public List<Vector3> textures = new List<Vector3>();
        public List<Vector3> normals = new List<Vector3>();
        public List<int> triangles = new List<int>();
        public List<int> edges = new List<int>();
        public List<MathematicalPlane> planes = new List<MathematicalPlane>();
        public List<PolygonMeta> polygons = new List<PolygonMeta>();
        public List<SectorMeta> sectors = new List<SectorMeta>();
        public List<StartPosition> positions = new List<StartPosition>();
    }

    void OnGUI()
    {
        if (debug)
        {
            GUI.color = Color.blue;

            for (int i = 0; i < debugRectangles.Count; i++)
            {
                Rect rectangle = debugRectangles[i];

                float xmin = (rectangle.xMin * 0.5f + 0.5f) * Screen.width;
                float xmax = (rectangle.xMax * 0.5f + 0.5f) * Screen.width;
                float ymin = (rectangle.yMin * 0.5f + 0.5f) * Screen.height;
                float ymax = (rectangle.yMax * 0.5f + 0.5f) * Screen.height;

                MakeVerticalLine(xmin, ymin, xmin, ymax, 5.0f); // left
                MakeVerticalLine(xmax, ymin, xmax, ymax, 5.0f); // right
                MakeHorizontalLine(xmin, ymin, xmax, ymin, 5.0f); // bottom
                MakeHorizontalLine(xmin, ymax, xmax, ymax, 5.0f); // top
            }
        }
    }

    void OnDestroy()
    {
        triBuffer?.Dispose();
    }

    void OnRenderObject()
    {
        triBuffer.SetData(outTriangles);

        opaquematerial.SetPass(0);
        Graphics.DrawProceduralNow(MeshTopology.Triangles, outTriangles.Count * 3);
    }

    void Start()
    {
        CollisionObjects = new GameObject("Collision Meshes");

        LevelLists = new TopLevelLists();

        LoadFromFile();

        CreateMaterial();

        BuildGeometry();

        BuildObjects();

        BuildColliders();

        PlayerStart();

        processbool = new bool[256];

        processvertices = new Vector4[256];

        processtextures = new Vector3[256];

        processnormals = new Vector3[256];

        temporaryvertices = new Vector4[256];

        temporarytextures = new Vector3[256];

        temporarynormals = new Vector3[256];

        for (int i = 0; i < 2; i++)
        {
            ListOfSectorLists.Add(new List<SectorMeta>());
        }

        for (int i = 0; i < 2; i++)
        {
            ListOfRectangleLists.Add(new List<Rect>());
        }

        for (int i = 0; i < LevelLists.sectors.Count; i++)
        {
            Physics.IgnoreCollision(Player, CollisionSectors[LevelLists.sectors[i].sectorId], true);
        }

        int strideTriangle = System.Runtime.InteropServices.Marshal.SizeOf(typeof(Triangle));

        triBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, (LevelLists.sectors.Count * 32) * 128, strideTriangle);

        opaquematerial.SetBuffer("outputTriangleBuffer", triBuffer);
    }

    void Update()
    {
        PlayerInput();

        if (Cam.transform.hasChanged)
        {
            view = Cam.worldToCameraMatrix;

            projection = GL.GetGPUProjectionMatrix(Cam.projectionMatrix, true);

            CamPoint = Cam.transform.position;

            GetSectors(CurrentSector);

            outTriangles.Clear();

            debugRectangles.Clear();

            GetPolygons(CurrentSector);

            Cam.transform.hasChanged = false;
        }
    }

    void Awake()
    {
        linetexture = new Texture2D(1, 1);

        linetexture.SetPixel(0, 0, Color.white);

        linetexture.Apply();

        Player = GameObject.Find("Player").GetComponent<CharacterController>();

        Player.GetComponent<CharacterController>().enabled = true;

        Cursor.lockState = CursorLockMode.Locked;

        Cam = Camera.main;
    }

    void FixedUpdate()
    {
        if (!Player.isGrounded)
        {
            currentForce.y -= gravity * Time.deltaTime;
        }
    }

    public void MakeHorizontalLine(float x1, float y1, float x2, float y2, float linethickness)
    {
        GUI.DrawTexture(new Rect(x1, y1, x2 - x1, linethickness), linetexture);
    }

    public void MakeVerticalLine(float x1, float y1, float x2, float y2, float linethickness)
    {
        GUI.DrawTexture(new Rect(x1, y1, linethickness, y2 - y1), linetexture);
    }

    public void CreateMaterial()
    {
        Shader shader = Resources.Load<Shader>("TriangleTexArray");

        opaquematerial = new Material(shader);

        opaquematerial.mainTexture = Resources.Load<Texture2DArray>("Textures");
    }

    public void PlayerInput()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Application.Quit();
        }
        if (Input.GetKeyDown(KeyCode.Space) && Player.isGrounded)
        {
            currentForce.y = jumpHeight;
        }

        float mousex = Input.GetAxisRaw("Mouse X");
        float mousey = Input.GetAxisRaw("Mouse Y");

        targetRotation.x -= mousey * sensitivity;
        targetRotation.y += mousex * sensitivity;

        targetRotation.x = Mathf.Clamp(targetRotation.x, -clampAngle, clampAngle);

        currentRotation = Vector2.Lerp(currentRotation, targetRotation, smoothFactor * Time.deltaTime);

        Cam.transform.localRotation = Quaternion.Euler(currentRotation.x, 0f, 0f);
        Player.transform.rotation = Quaternion.Euler(0f, currentRotation.y, 0f);

        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        targetMovement = (Player.transform.right * horizontal + Player.transform.forward * vertical).normalized;

        Player.Move((targetMovement + currentForce) * speed * Time.deltaTime);
    }

    public float GetPlaneSignedDistanceToPoint(MathematicalPlane plane, Vector3 point)
    {
        return Vector3.Dot(plane.normal, point) + plane.distance;
    }

    public void ClipEdgesWithRectangle(Rect rectangle, PolygonMeta portal)
    {
        OutEdgeVertices.Clear();

        int processverticescount = 0;
        int processboolcount = 0;

        for (int a = portal.edgeStartIndex; a < portal.edgeStartIndex + portal.edgeCount; a += 2)
        {
            Vector4 v0view = view * new Vector4(LevelLists.vertices[LevelLists.edges[a]].x, LevelLists.vertices[LevelLists.edges[a]].y, LevelLists.vertices[LevelLists.edges[a]].z, 1.0f);
            Vector4 v1view = view * new Vector4(LevelLists.vertices[LevelLists.edges[a + 1]].x, LevelLists.vertices[LevelLists.edges[a + 1]].y, LevelLists.vertices[LevelLists.edges[a + 1]].z, 1.0f);

            Vector4 v0clip = projection * v0view;
            Vector4 v1clip = projection * v1view;

            processvertices[processverticescount] = v0clip;
            processvertices[processverticescount + 1] = v1clip;
            processverticescount += 2;
            processbool[processboolcount] = true;
            processbool[processboolcount + 1] = true;
            processboolcount += 2;
        }

        for (int b = 0; b < 6; b++)
        {
            int intersection = 0;

            int temporaryverticescount = 0;

            Vector4 intersectionPoint0 = Vector4.zero;
            Vector4 intersectionPoint1 = Vector4.zero;

            for (int c = 0; c < processverticescount; c += 2)
            {
                if (processbool[c] == false && processbool[c + 1] == false)
                {
                    continue;
                }

                Vector4 v0 = processvertices[c];
                Vector4 v1 = processvertices[c + 1];

                float d0, d1;

                switch (b)
                {
                    case 0: // Left
                        d0 = v0.x - rectangle.xMin * v0.w;
                        d1 = v1.x - rectangle.xMin * v1.w;
                        break;

                    case 1: // Right
                        d0 = rectangle.xMax * v0.w - v0.x;
                        d1 = rectangle.xMax * v1.w - v1.x;
                        break;

                    case 2: // Bottom
                        d0 = v0.y - rectangle.yMin * v0.w;
                        d1 = v1.y - rectangle.yMin * v1.w;
                        break;

                    case 3: // Top
                        d0 = rectangle.yMax * v0.w - v0.y;
                        d1 = rectangle.yMax * v1.w - v1.y;
                        break;

                    case 4: // Near
                        d0 = v0.z;
                        d1 = v1.z;
                        break;

                    case 5: // Far
                        d0 = v0.w - v0.z;
                        d1 = v1.w - v1.z;
                        break;

                    default:
                        d0 = 0;
                        d1 = 0;
                        break;
                }

                bool b0 = d0 >= 0;
                bool b1 = d1 >= 0;

                if (b0 && b1)
                {
                    continue;
                }
                else if ((b0 && !b1) || (!b0 && b1))
                {
                    Vector4 point0;
                    Vector4 point1;

                    float t = d0 / (d0 - d1);

                    Vector4 intersectionPoint = Vector4.Lerp(v0, v1, t);

                    if (b0)
                    {
                        point0 = v0;
                        point1 = intersectionPoint;
                        intersectionPoint0 = intersectionPoint;
                    }
                    else
                    {
                        point0 = intersectionPoint;
                        point1 = v1;
                        intersectionPoint1 = intersectionPoint;
                    }

                    temporaryvertices[temporaryverticescount] = point0;
                    temporaryvertices[temporaryverticescount + 1] = point1;
                    temporaryverticescount += 2;

                    processbool[c] = false;
                    processbool[c + 1] = false;

                    intersection += 1;
                }
                else
                {
                    processbool[c] = false;
                    processbool[c + 1] = false;
                }
            }

            if (intersection == 2)
            {
                for (int d = 0; d < temporaryverticescount; d += 2)
                {
                    processvertices[processverticescount] = temporaryvertices[d];
                    processvertices[processverticescount + 1] = temporaryvertices[d + 1];
                    processverticescount += 2;
                    processbool[processboolcount] = true;
                    processbool[processboolcount + 1] = true;
                    processboolcount += 2;
                }

                processvertices[processverticescount] = intersectionPoint0;
                processvertices[processverticescount + 1] = intersectionPoint1;
                processverticescount += 2;
                processbool[processboolcount] = true;
                processbool[processboolcount + 1] = true;
                processboolcount += 2;
            }
        }

        for (int e = 0; e < processboolcount; e += 2)
        {
            if (processbool[e] == true && processbool[e + 1] == true)
            {
                Vector4 clip0 = processvertices[e];
                Vector4 clip1 = processvertices[e + 1];

                float invw0 = 1.0f / clip0.w;
                float invw1 = 1.0f / clip1.w;

                Vector3 ndc0 = new Vector3(clip0.x * invw0, clip0.y * invw0, clip0.z * invw0);
                Vector3 ndc1 = new Vector3(clip1.x * invw1, clip1.y * invw1, clip1.z * invw1);

                OutEdgeVertices.Add(ndc0);
                OutEdgeVertices.Add(ndc1);
            }
        }
    }

    public void ClipTrianglesWithRectangle(Rect rectangle, PolygonMeta polygon)
    {
        for (int a = polygon.triangleStartIndex; a < polygon.triangleStartIndex + polygon.triangleCount; a += 3)
        {
            Vector4 v0view = view * new Vector4(LevelLists.vertices[LevelLists.triangles[a]].x, LevelLists.vertices[LevelLists.triangles[a]].y, LevelLists.vertices[LevelLists.triangles[a]].z, 1.0f);
            Vector4 v1view = view * new Vector4(LevelLists.vertices[LevelLists.triangles[a + 1]].x, LevelLists.vertices[LevelLists.triangles[a + 1]].y, LevelLists.vertices[LevelLists.triangles[a + 1]].z, 1.0f);
            Vector4 v2view = view * new Vector4(LevelLists.vertices[LevelLists.triangles[a + 2]].x, LevelLists.vertices[LevelLists.triangles[a + 2]].y, LevelLists.vertices[LevelLists.triangles[a + 2]].z, 1.0f);

            Vector4 v0clip = projection * v0view;
            Vector4 v1clip = projection * v1view;
            Vector4 v2clip = projection * v2view;

            int processverticescount = 0;
            int processtexturescount = 0;
            int processnormalscount = 0;
            int processboolcount = 0;

            processvertices[processverticescount] = v0clip;
            processvertices[processverticescount + 1] = v1clip;
            processvertices[processverticescount + 2] = v2clip;
            processverticescount += 3;
            processtextures[processtexturescount] = LevelLists.textures[LevelLists.triangles[a]];
            processtextures[processtexturescount + 1] = LevelLists.textures[LevelLists.triangles[a + 1]];
            processtextures[processtexturescount + 2] = LevelLists.textures[LevelLists.triangles[a + 2]];
            processtexturescount += 3;
            processnormals[processnormalscount] = LevelLists.normals[LevelLists.triangles[a]];
            processnormals[processnormalscount + 1] = LevelLists.normals[LevelLists.triangles[a + 1]];
            processnormals[processnormalscount + 2] = LevelLists.normals[LevelLists.triangles[a + 2]];
            processnormalscount += 3;
            processbool[processboolcount] = true;
            processbool[processboolcount + 1] = true;
            processbool[processboolcount + 2] = true;
            processboolcount += 3;

            for (int b = 0; b < 6; b++)
            {
                int AddTriangles = 0;

                int temporaryverticescount = 0;
                int temporarytexturescount = 0;
                int temporarynormalscount = 0;

                for (int c = 0; c < processverticescount; c += 3)
                {
                    if (processbool[c] == false && processbool[c + 1] == false && processbool[c + 2] == false)
                    {
                        continue;
                    }

                    Vector4 v0 = processvertices[c];
                    Vector4 v1 = processvertices[c + 1];
                    Vector4 v2 = processvertices[c + 2];

                    Vector3 uv0 = processtextures[c];
                    Vector3 uv1 = processtextures[c + 1];
                    Vector3 uv2 = processtextures[c + 2];

                    Vector3 n0 = processnormals[c];
                    Vector3 n1 = processnormals[c + 1];
                    Vector3 n2 = processnormals[c + 2];

                    float d0, d1, d2;

                    switch (b)
                    {
                        case 0: // Left
                            d0 = v0.x - rectangle.xMin * v0.w;
                            d1 = v1.x - rectangle.xMin * v1.w;
                            d2 = v2.x - rectangle.xMin * v2.w;
                            break;

                        case 1: // Right
                            d0 = rectangle.xMax * v0.w - v0.x;
                            d1 = rectangle.xMax * v1.w - v1.x;
                            d2 = rectangle.xMax * v2.w - v2.x;
                            break;

                        case 2: // Bottom
                            d0 = v0.y - rectangle.yMin * v0.w;
                            d1 = v1.y - rectangle.yMin * v1.w;
                            d2 = v2.y - rectangle.yMin * v2.w;
                            break;

                        case 3: // Top
                            d0 = rectangle.yMax * v0.w - v0.y;
                            d1 = rectangle.yMax * v1.w - v1.y;
                            d2 = rectangle.yMax * v2.w - v2.y;
                            break;

                        case 4: // Near
                            d0 = v0.z;
                            d1 = v1.z;
                            d2 = v2.z;
                            break;

                        case 5: // Far
                            d0 = v0.w - v0.z;
                            d1 = v1.w - v1.z;
                            d2 = v2.w - v2.z;
                            break;

                        default:
                            d0 = 0;
                            d1 = 0;
                            d2 = 0;
                            break;
                    }

                    bool b0 = d0 >= 0;
                    bool b1 = d1 >= 0;
                    bool b2 = d2 >= 0;

                    if (b0 && b1 && b2)
                    {
                        continue;
                    }
                    else if ((b0 && !b1 && !b2) || (!b0 && b1 && !b2) || (!b0 && !b1 && b2))
                    {
                        Vector4 inV, outV1, outV2;
                        Vector3 inUV, outUV1, outUV2;
                        Vector3 inN, outN1, outN2;
                        float inD, outD1, outD2;

                        if (b0)
                        {
                            inV = v0;
                            inUV = uv0;
                            inN = n0;
                            inD = d0;
                            outV1 = v1;
                            outUV1 = uv1;
                            outN1 = n1;
                            outD1 = d1;
                            outV2 = v2;
                            outUV2 = uv2;
                            outN2 = n2;
                            outD2 = d2;
                        }
                        else if (b1)
                        {
                            inV = v1;
                            inUV = uv1;
                            inN = n1;
                            inD = d1;
                            outV1 = v2;
                            outUV1 = uv2;
                            outN1 = n2;
                            outD1 = d2;
                            outV2 = v0;
                            outUV2 = uv0;
                            outN2 = n0;
                            outD2 = d0;
                        }
                        else
                        {
                            inV = v2;
                            inUV = uv2;
                            inN = n2;
                            inD = d2;
                            outV1 = v0;
                            outUV1 = uv0;
                            outN1 = n0;
                            outD1 = d0;
                            outV2 = v1;
                            outUV2 = uv1;
                            outN2 = n1;
                            outD2 = d1;
                        }

                        float t1 = inD / (inD - outD1);
                        float t2 = inD / (inD - outD2);

                        temporaryvertices[temporaryverticescount] = inV;
                        temporaryvertices[temporaryverticescount + 1] = Vector4.Lerp(inV, outV1, t1);
                        temporaryvertices[temporaryverticescount + 2] = Vector4.Lerp(inV, outV2, t2);
                        temporaryverticescount += 3;
                        temporarytextures[temporarytexturescount] = inUV;
                        temporarytextures[temporarytexturescount + 1] = Vector3.Lerp(inUV, outUV1, t1);
                        temporarytextures[temporarytexturescount + 2] = Vector3.Lerp(inUV, outUV2, t2);
                        temporarytexturescount += 3;
                        temporarynormals[temporarynormalscount] = inN;
                        temporarynormals[temporarynormalscount + 1] = Vector3.Lerp(inN, outN1, t1).normalized;
                        temporarynormals[temporarynormalscount + 2] = Vector3.Lerp(inN, outN2, t2).normalized;
                        temporarynormalscount += 3;
                        processbool[c] = false;
                        processbool[c + 1] = false;
                        processbool[c + 2] = false;

                        AddTriangles += 1;
                    }
                    else if ((!b0 && b1 && b2) || (b0 && !b1 && b2) || (b0 && b1 && !b2))
                    {
                        Vector4 inV1, inV2, outV;
                        Vector3 inUV1, inUV2, outUV;
                        Vector3 inN1, inN2, outN;
                        float inD1, inD2, outD;

                        if (!b0)
                        {
                            outV = v0;
                            outUV = uv0;
                            outN = n0;
                            outD = d0;
                            inV1 = v1;
                            inUV1 = uv1;
                            inN1 = n1;
                            inD1 = d1;
                            inV2 = v2;
                            inUV2 = uv2;
                            inN2 = n2;
                            inD2 = d2;
                        }
                        else if (!b1)
                        {
                            outV = v1;
                            outUV = uv1;
                            outN = n1;
                            outD = d1;
                            inV1 = v2;
                            inUV1 = uv2;
                            inN1 = n2;
                            inD1 = d2;
                            inV2 = v0;
                            inUV2 = uv0;
                            inN2 = n0;
                            inD2 = d0;
                        }
                        else
                        {
                            outV = v2;
                            outUV = uv2;
                            outN = n2;
                            outD = d2;
                            inV1 = v0;
                            inUV1 = uv0;
                            inN1 = n0;
                            inD1 = d0;
                            inV2 = v1;
                            inUV2 = uv1;
                            inN2 = n1;
                            inD2 = d1;
                        }

                        float t1 = inD1 / (inD1 - outD);
                        float t2 = inD2 / (inD2 - outD);

                        Vector4 vA = Vector4.Lerp(inV1, outV, t1);
                        Vector4 vB = Vector4.Lerp(inV2, outV, t2);

                        Vector3 uvA = Vector3.Lerp(inUV1, outUV, t1);
                        Vector3 uvB = Vector3.Lerp(inUV2, outUV, t2);

                        Vector3 nA = Vector3.Lerp(inN1, outN, t1).normalized;
                        Vector3 nB = Vector3.Lerp(inN2, outN, t2).normalized;

                        temporaryvertices[temporaryverticescount] = inV1;
                        temporaryvertices[temporaryverticescount + 1] = inV2;
                        temporaryvertices[temporaryverticescount + 2] = vA;
                        temporaryverticescount += 3;
                        temporarytextures[temporarytexturescount] = inUV1;
                        temporarytextures[temporarytexturescount + 1] = inUV2;
                        temporarytextures[temporarytexturescount + 2] = uvA;
                        temporarytexturescount += 3;
                        temporarynormals[temporarynormalscount] = inN1;
                        temporarynormals[temporarynormalscount + 1] = inN2;
                        temporarynormals[temporarynormalscount + 2] = nA;
                        temporarynormalscount += 3;
                        temporaryvertices[temporaryverticescount] = vA;
                        temporaryvertices[temporaryverticescount + 1] = inV2;
                        temporaryvertices[temporaryverticescount + 2] = vB;
                        temporaryverticescount += 3;
                        temporarytextures[temporarytexturescount] = uvA;
                        temporarytextures[temporarytexturescount + 1] = inUV2;
                        temporarytextures[temporarytexturescount + 2] = uvB;
                        temporarytexturescount += 3;
                        temporarynormals[temporarynormalscount] = nA;
                        temporarynormals[temporarynormalscount + 1] = inN2;
                        temporarynormals[temporarynormalscount + 2] = nB;
                        temporarynormalscount += 3;
                        processbool[c] = false;
                        processbool[c + 1] = false;
                        processbool[c + 2] = false;

                        AddTriangles += 2;
                    }
                    else
                    {
                        processbool[c] = false;
                        processbool[c + 1] = false;
                        processbool[c + 2] = false;
                    }
                }

                if (AddTriangles > 0)
                {
                    for (int d = 0; d < temporaryverticescount; d += 3)
                    {
                        processvertices[processverticescount] = temporaryvertices[d];
                        processvertices[processverticescount + 1] = temporaryvertices[d + 1];
                        processvertices[processverticescount + 2] = temporaryvertices[d + 2];
                        processverticescount += 3;
                        processtextures[processtexturescount] = temporarytextures[d];
                        processtextures[processtexturescount + 1] = temporarytextures[d + 1];
                        processtextures[processtexturescount + 2] = temporarytextures[d + 2];
                        processtexturescount += 3;
                        processnormals[processnormalscount] = temporarynormals[d];
                        processnormals[processnormalscount + 1] = temporarynormals[d + 1];
                        processnormals[processnormalscount + 2] = temporarynormals[d + 2];
                        processnormalscount += 3;
                        processbool[processboolcount] = true;
                        processbool[processboolcount + 1] = true;
                        processbool[processboolcount + 2] = true;
                        processboolcount += 3;
                    }
                }
            }

            for (int e = 0; e < processboolcount; e += 3)
            {
                if (processbool[e] == true && processbool[e + 1] == true && processbool[e + 2] == true)
                {
                    Triangle tri = new Triangle();

                    tri.v0 = processvertices[e];
                    tri.v1 = processvertices[e + 1];
                    tri.v2 = processvertices[e + 2];
                    tri.uv0 = processtextures[e];
                    tri.uv1 = processtextures[e + 1];
                    tri.uv2 = processtextures[e + 2];
                    tri.n0 = processnormals[e];
                    tri.n1 = processnormals[e + 1];
                    tri.n2 = processnormals[e + 2];

                    outTriangles.Add(tri);
                }
            }
        }
    }

    public bool CheckRadius(SectorMeta asector, Vector3 campoint)
    {
        for (int i = asector.polygonStartIndex; i < asector.polygonStartIndex + asector.polygonCount; i++)
        {
            if (GetPlaneSignedDistanceToPoint(LevelLists.planes[LevelLists.polygons[i].plane], campoint) < -0.6f)
            {
                return false;
            }
        }
        return true;
    }

    public bool CheckSector(SectorMeta asector, Vector3 campoint)
    {
        for (int i = asector.polygonStartIndex; i < asector.polygonStartIndex + asector.polygonCount; i++)
        {
            if (GetPlaneSignedDistanceToPoint(LevelLists.planes[LevelLists.polygons[i].plane], campoint) < 0)
            {
                return false;
            }
        }
        return true;
    }

    public bool SectorsContains(int sectorID)
    {
        for (int i = 0; i < Sectors.Count; i++)
        {
            if (Sectors[i].sectorId == sectorID)
            {
                return true;
            }
        }
        return false;
    }

    public bool SectorsDoNotEqual()
    {
        if (Sectors.Count != OldSectors.Count)
        {
            return true;
        }

        for (int i = 0; i < Sectors.Count; i++)
        {
            if (Sectors[i].sectorId != OldSectors[i].sectorId)
            {
                return true;
            }
        }
        return false;
    }

    public void GetSectors(SectorMeta ASector)
    {
        int input = 0;
        int output = 1;

        Sectors.Clear();

        ListOfSectorLists[input].Clear();
        ListOfSectorLists[output].Clear();

        ListOfSectorLists[input].Add(ASector);

        for (int a = 0; a < OldSectors.Count; a++)
        {
            Physics.IgnoreCollision(Player, CollisionSectors[OldSectors[a].sectorId], true);
        }

        for (int b = 0; b < 4096; b++)
        {
            if (b % 2 == 0)
            {
                input = 0;
                output = 1;
            }
            else
            {
                input = 1;
                output = 0;
            }

            ListOfSectorLists[output].Clear();

            if (ListOfSectorLists[input].Count == 0)
            {
                break;
            }

            for (int c = 0; c < ListOfSectorLists[input].Count; c++)
            {
                SectorMeta sector = ListOfSectorLists[input][c];

                Sectors.Add(sector);

                Physics.IgnoreCollision(Player, CollisionSectors[sector.sectorId], false);

                for (int d = sector.polygonStartIndex; d < sector.polygonStartIndex + sector.polygonCount; d++)
                {
                    int connectedsector = LevelLists.polygons[d].connectedSectorId;

                    if (connectedsector == -1)
                    {
                        continue;
                    }

                    SectorMeta portalsector = LevelLists.sectors[connectedsector];

                    if (SectorsContains(portalsector.sectorId))
                    {
                        continue;
                    }

                    radius = CheckRadius(portalsector, CamPoint);

                    if (radius)
                    {
                        ListOfSectorLists[output].Add(portalsector);
                    }
                }

                check = CheckSector(sector, CamPoint);

                if (check)
                {
                    CurrentSector = sector;
                }
            }    
        }

        if (SectorsDoNotEqual())
        {
            OldSectors.Clear();

            for (int e = 0; e < Sectors.Count; e++)
            {
                OldSectors.Add(Sectors[e]);
            }
        }
    }

    public void GetPolygons(SectorMeta ASector)
    {
        int input = 0;
        int output = 1;

        ListOfSectorLists[input].Clear();
        ListOfSectorLists[output].Clear();

        ListOfRectangleLists[input].Clear();
        ListOfRectangleLists[output].Clear();

        ListOfRectangleLists[input].Add(new Rect(-1f, -1f, 2f, 2f));

        ListOfSectorLists[input].Add(ASector);

        for (int a = 0; a < 4096; a++)
        {
            if (a % 2 == 0)
            {
                input = 0;
                output = 1;
            }
            else
            {
                input = 1;
                output = 0;
            }

            ListOfRectangleLists[output].Clear();

            ListOfSectorLists[output].Clear();

            if (ListOfSectorLists[input].Count == 0)
            {
                break;
            }

            for (int b = 0; b < ListOfSectorLists[input].Count; b++)
            {
                SectorMeta sector = ListOfSectorLists[input][b];

                Rect rectangleIn = ListOfRectangleLists[input][b];

                debugRectangles.Add(rectangleIn);

                for (int c = sector.polygonStartIndex; c < sector.polygonStartIndex + sector.polygonCount; c++)
                {
                    PolygonMeta polygon = LevelLists.polygons[c];

                    planeDistance = GetPlaneSignedDistanceToPoint(LevelLists.planes[polygon.plane], CamPoint);

                    if (planeDistance <= 0)
                    {
                        continue;
                    }

                    int rendersector = polygon.opaque;

                    int connectedsector = polygon.connectedSectorId;

                    if (rendersector != -1)
                    {
                        ClipTrianglesWithRectangle(rectangleIn, polygon);

                        continue;
                    }

                    if (connectedsector != -1)
                    {
                        SectorMeta sectorpolygon = LevelLists.sectors[connectedsector];

                        int connectedstart = sectorpolygon.polygonStartIndex;

                        int connectedcount = sectorpolygon.polygonCount;

                        if (SectorsContains(sectorpolygon.sectorId))
                        {
                            ListOfRectangleLists[output].Add(rectangleIn);

                            NextSector = sectorpolygon;

                            ListOfSectorLists[output].Add(NextSector);

                            continue;
                        }

                        ClipEdgesWithRectangle(rectangleIn, polygon);

                        if (OutEdgeVertices.Count < 6 || OutEdgeVertices.Count % 2 == 1)
                        {
                            continue;
                        }

                        Rect rectangleOut = MakeRectangle(OutEdgeVertices);

                        if (IntersectRectangles(rectangleIn, rectangleOut, out combinedRectangle))
                        {
                            ListOfRectangleLists[output].Add(combinedRectangle);

                            NextSector = sectorpolygon;

                            ListOfSectorLists[output].Add(NextSector);
                        }
                    }
                }
            }
        }
    }

    public bool IntersectRectangles(Rect a, Rect b, out Rect combined)
    {
        float xmin = Mathf.Max(a.xMin, b.xMin);
        float ymin = Mathf.Max(a.yMin, b.yMin);
        float xmax = Mathf.Min(a.xMax, b.xMax);
        float ymax = Mathf.Min(a.yMax, b.yMax);

        combined = Rect.MinMaxRect(xmin, ymin, xmax, ymax);

        if (xmax <= xmin || ymax <= ymin)
        {
            return false;
        }

        return true;
    }

    public Rect MakeRectangle(List<Vector3> ndcEdges)
    {
        float xmin = float.PositiveInfinity;
        float ymin = float.PositiveInfinity;
        float xmax = float.NegativeInfinity;
        float ymax = float.NegativeInfinity;

        for (int i = 0; i < ndcEdges.Count; i++)
        {
            Vector3 v = ndcEdges[i];

            if (v.x < xmin)
            {
                xmin = v.x;
            }

            if (v.x > xmax)
            {
                xmax = v.x;
            }

            if (v.y < ymin)
            {
                ymin = v.y;
            }

            if (v.y > ymax)
            {
                ymax = v.y;
            }
        }

        return Rect.MinMaxRect(xmin, ymin, xmax, ymax);
    }

    public void PlayerStart()
    {
        if (LevelLists.positions.Count == 0)
        {
            Debug.LogError("No player starts available.");

            return;
        }

        int randomIndex = UnityEngine.Random.Range(0, LevelLists.positions.Count);

        StartPosition selectedPosition = LevelLists.positions[randomIndex];

        CurrentSector = LevelLists.sectors[selectedPosition.sectorId];

        Player.transform.position = new Vector3(selectedPosition.playerStart.z, selectedPosition.playerStart.y + 1.10f, selectedPosition.playerStart.x);
    }

    public void LoadFromFile()
    {
        TextAsset file = Resources.Load<TextAsset>(Name);
        if (file == null)
        {
            Debug.LogError("File not found in Resources!");
            return;
        }

        string[] lines = file.text.Split('\n');

        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].StartsWith("vertex"))
            {
                string[] parts = lines[i].Split('\t');

                if (parts.Length == 3)
                {
                    float y = float.Parse(parts[1]);

                    string[] xValues = parts[2].Split(' ');

                    for (int e = 0; e < xValues.Length; e++)
                    {
                        if (float.TryParse(xValues[e], out float x))
                        {
                            vertices.Add(new Vector2(x, y));
                        }
                    }
                }
            }

            if (lines[i].StartsWith("sector"))
            {
                Sector sector = new Sector();

                string[] parts = lines[i].Split('\t');

                if (parts.Length == 3)
                {
                    string[] heightParts = parts[1].Split(' ');

                    if (heightParts.Length == 2)
                    {
                        sector.floorHeight = float.Parse(heightParts[0]);

                        sector.ceilingHeight = float.Parse(heightParts[1]);
                    }

                    string[] values = parts[2].Split(' ');

                    int half = values.Length / 2;

                    for (int e = 0; e < values.Length; e++)
                    {
                        if (int.TryParse(values[e], out int val))
                        {
                            if (e < half)
                            {
                                sector.vertexIndices.Add(val);
                            }
                            else
                            {
                                sector.wallTypes.Add(val);
                            }
                        }
                    }
                }

                sectors.Add(sector);
            }

            if (lines[i].StartsWith("player"))
            {
                StartSector start = new StartSector();

                string[] parts = lines[i].Split('\t');

                if (parts.Length == 4)
                {
                    string[] locationParts = parts[1].Split(' ');

                    if (locationParts.Length == 2)
                    {
                        float x = float.Parse(locationParts[0]);

                        float y = float.Parse(locationParts[1]);

                        start.location = new Vector2(x, y);
                    }

                    start.angle = float.Parse(parts[2]);

                    start.sector = int.Parse(parts[3]);
                }

                starts.Add(start);
            }
        }

        Debug.Log($"Loaded {vertices.Count} vertices.");

        Debug.Log($"Loaded {sectors.Count} sectors.");

        Debug.Log($"Player start: location={starts[0].location}, angle={starts[0].angle}, sector={starts[0].sector}");
    }

    public void BuildGeometry()
    {
        int polygonStart = 0;

        for (int i = 0; i < sectors.Count; i++)
        {
            int polygonCount = 0;

            Sector sector = sectors[i];

            for (int e = 0; e < sector.vertexIndices.Count; e++)
            {
                int current = sector.vertexIndices[e];
                int next = sector.vertexIndices[(e + 1) % sector.vertexIndices.Count];

                int wall = sector.wallTypes[(e + 1) % sector.wallTypes.Count];

                double X1 = vertices[current].x / 2 * 2.5f;
                double Z1 = vertices[current].y / 2 * 2.5f;

                double X0 = vertices[next].x / 2 * 2.5f;
                double Z0 = vertices[next].y / 2 * 2.5f;

                if (wall == -1)
                {
                    double V0 = sector.floorHeight / 8 * 2.5f;
                    double V1 = sector.ceilingHeight / 8 * 2.5f;

                    int baseVert = LevelLists.vertices.Count;

                    int baseStartIndex = LevelLists.triangles.Count;

                    LevelLists.vertices.Add(new Vector3((float)Z1, (float)V0, (float)X1));
                    LevelLists.vertices.Add(new Vector3((float)Z1, (float)V1, (float)X1));
                    LevelLists.vertices.Add(new Vector3((float)Z0, (float)V1, (float)X0));
                    LevelLists.vertices.Add(new Vector3((float)Z0, (float)V0, (float)X0));

                    LevelLists.triangles.Add(baseVert);
                    LevelLists.triangles.Add(baseVert + 1);
                    LevelLists.triangles.Add(baseVert + 2);
                    LevelLists.triangles.Add(baseVert);
                    LevelLists.triangles.Add(baseVert + 2);
                    LevelLists.triangles.Add(baseVert + 3);

                    Vector3 v0 = LevelLists.vertices[baseVert];
                    Vector3 v1 = LevelLists.vertices[baseVert + 1];
                    Vector3 v2 = LevelLists.vertices[baseVert + 2];

                    Vector3 n = Vector3.Cross(v1 - v0, v2 - v0).normalized;

                    Vector3 leftPlaneNormal = (v2 - v1).normalized;
                    float leftPlaneDistance = -Vector3.Dot(leftPlaneNormal, v1);

                    Vector3 topPlaneNormal = (v1 - v0).normalized;
                    float topPlaneDistance = -Vector3.Dot(topPlaneNormal, v1);

                    LeftPlane = new MathematicalPlane { normal = leftPlaneNormal, distance = leftPlaneDistance };
                    TopPlane = new MathematicalPlane { normal = topPlaneNormal, distance = topPlaneDistance };

                    LevelLists.textures.Add(new Vector3(GetPlaneSignedDistanceToPoint(LeftPlane, LevelLists.vertices[baseVert]) / 2.5f, GetPlaneSignedDistanceToPoint(TopPlane, LevelLists.vertices[baseVert]) / 2.5f, 3));
                    LevelLists.textures.Add(new Vector3(GetPlaneSignedDistanceToPoint(LeftPlane, LevelLists.vertices[baseVert + 1]) / 2.5f, GetPlaneSignedDistanceToPoint(TopPlane, LevelLists.vertices[baseVert + 1]) / 2.5f, 3));
                    LevelLists.textures.Add(new Vector3(GetPlaneSignedDistanceToPoint(LeftPlane, LevelLists.vertices[baseVert + 2]) / 2.5f, GetPlaneSignedDistanceToPoint(TopPlane, LevelLists.vertices[baseVert + 2]) / 2.5f, 3));
                    LevelLists.textures.Add(new Vector3(GetPlaneSignedDistanceToPoint(LeftPlane, LevelLists.vertices[baseVert + 3]) / 2.5f, GetPlaneSignedDistanceToPoint(TopPlane, LevelLists.vertices[baseVert + 3]) / 2.5f, 3));

                    LevelLists.normals.Add(n);
                    LevelLists.normals.Add(n);
                    LevelLists.normals.Add(n);
                    LevelLists.normals.Add(n);

                    PolygonMeta transformedmesh = new PolygonMeta
                    {
                        plane = LevelLists.planes.Count,

                        collider = i,

                        opaque = i,

                        sectorId = i,

                        connectedSectorId = -1,

                        edgeStartIndex = -1,

                        edgeCount = -1,

                        triangleStartIndex = baseStartIndex,

                        triangleCount = 6
                    };

                    LevelLists.polygons.Add(transformedmesh);

                    MathematicalPlane plane = new MathematicalPlane
                    {
                        normal = n,
                        distance = -Vector3.Dot(n, v0)
                    };

                    LevelLists.planes.Add(plane);

                    polygonCount += 1;
                }
                else
                {
                    if (sector.ceilingHeight > sectors[wall].ceilingHeight)
                    {
                        if (sector.floorHeight < sectors[wall].ceilingHeight)
                        {
                            double C0 = sector.ceilingHeight / 8 * 2.5f;

                            if (sector.ceilingHeight > sectors[wall].ceilingHeight)
                            {
                                Ceiling = sectors[wall].ceilingHeight / 8 * 2.5f;
                            }
                            else
                            {
                                Ceiling = sector.ceilingHeight / 8 * 2.5f;
                            }

                            int baseVert = LevelLists.vertices.Count;

                            int baseStartIndex = LevelLists.triangles.Count;

                            LevelLists.vertices.Add(new Vector3((float)Z1, (float)Ceiling, (float)X1));
                            LevelLists.vertices.Add(new Vector3((float)Z1, (float)C0, (float)X1));
                            LevelLists.vertices.Add(new Vector3((float)Z0, (float)C0, (float)X0));
                            LevelLists.vertices.Add(new Vector3((float)Z0, (float)Ceiling, (float)X0));

                            LevelLists.triangles.Add(baseVert);
                            LevelLists.triangles.Add(baseVert + 1);
                            LevelLists.triangles.Add(baseVert + 2);
                            LevelLists.triangles.Add(baseVert);
                            LevelLists.triangles.Add(baseVert + 2);
                            LevelLists.triangles.Add(baseVert + 3);

                            Vector3 v0 = LevelLists.vertices[baseVert];
                            Vector3 v1 = LevelLists.vertices[baseVert + 1];
                            Vector3 v2 = LevelLists.vertices[baseVert + 2];

                            Vector3 n = Vector3.Cross(v1 - v0, v2 - v0).normalized;

                            Vector3 leftPlaneNormal = (v2 - v1).normalized;
                            float leftPlaneDistance = -Vector3.Dot(leftPlaneNormal, v1);

                            Vector3 topPlaneNormal = (v1 - v0).normalized;
                            float topPlaneDistance = -Vector3.Dot(topPlaneNormal, v1);

                            LeftPlane = new MathematicalPlane { normal = leftPlaneNormal, distance = leftPlaneDistance };
                            TopPlane = new MathematicalPlane { normal = topPlaneNormal, distance = topPlaneDistance };

                            LevelLists.textures.Add(new Vector3(GetPlaneSignedDistanceToPoint(LeftPlane, LevelLists.vertices[baseVert]) / 2.5f, GetPlaneSignedDistanceToPoint(TopPlane, LevelLists.vertices[baseVert]) / 2.5f, 3));
                            LevelLists.textures.Add(new Vector3(GetPlaneSignedDistanceToPoint(LeftPlane, LevelLists.vertices[baseVert + 1]) / 2.5f, GetPlaneSignedDistanceToPoint(TopPlane, LevelLists.vertices[baseVert + 1]) / 2.5f, 3));
                            LevelLists.textures.Add(new Vector3(GetPlaneSignedDistanceToPoint(LeftPlane, LevelLists.vertices[baseVert + 2]) / 2.5f, GetPlaneSignedDistanceToPoint(TopPlane, LevelLists.vertices[baseVert + 2]) / 2.5f, 3));
                            LevelLists.textures.Add(new Vector3(GetPlaneSignedDistanceToPoint(LeftPlane, LevelLists.vertices[baseVert + 3]) / 2.5f, GetPlaneSignedDistanceToPoint(TopPlane, LevelLists.vertices[baseVert + 3]) / 2.5f, 3));

                            LevelLists.normals.Add(n);
                            LevelLists.normals.Add(n);
                            LevelLists.normals.Add(n);
                            LevelLists.normals.Add(n);

                            PolygonMeta transformedmesh = new PolygonMeta
                            {
                                plane = LevelLists.planes.Count,

                                collider = i,

                                opaque = i,

                                sectorId = i,

                                connectedSectorId = -1,

                                edgeStartIndex = -1,

                                edgeCount = -1,

                                triangleStartIndex = baseStartIndex,

                                triangleCount = 6
                            };

                            LevelLists.polygons.Add(transformedmesh);

                            MathematicalPlane plane = new MathematicalPlane
                            {
                                normal = n,
                                distance = -Vector3.Dot(n, v0)
                            };

                            LevelLists.planes.Add(plane);

                            polygonCount += 1;
                        }
                        else
                        {
                            double C0 = sector.ceilingHeight / 8 * 2.5f;
                            double C1 = sector.floorHeight / 8 * 2.5f;

                            int baseVert = LevelLists.vertices.Count;

                            int baseStartIndex = LevelLists.triangles.Count;

                            LevelLists.vertices.Add(new Vector3((float)Z1, (float)C1, (float)X1));
                            LevelLists.vertices.Add(new Vector3((float)Z1, (float)C0, (float)X1));
                            LevelLists.vertices.Add(new Vector3((float)Z0, (float)C0, (float)X0));
                            LevelLists.vertices.Add(new Vector3((float)Z0, (float)C1, (float)X0));

                            LevelLists.triangles.Add(baseVert);
                            LevelLists.triangles.Add(baseVert + 1);
                            LevelLists.triangles.Add(baseVert + 2);
                            LevelLists.triangles.Add(baseVert);
                            LevelLists.triangles.Add(baseVert + 2);
                            LevelLists.triangles.Add(baseVert + 3);

                            Vector3 v0 = LevelLists.vertices[baseVert];
                            Vector3 v1 = LevelLists.vertices[baseVert + 1];
                            Vector3 v2 = LevelLists.vertices[baseVert + 2];

                            Vector3 n = Vector3.Cross(v1 - v0, v2 - v0).normalized;

                            Vector3 leftPlaneNormal = (v2 - v1).normalized;
                            float leftPlaneDistance = -Vector3.Dot(leftPlaneNormal, v1);

                            Vector3 topPlaneNormal = (v1 - v0).normalized;
                            float topPlaneDistance = -Vector3.Dot(topPlaneNormal, v1);

                            LeftPlane = new MathematicalPlane { normal = leftPlaneNormal, distance = leftPlaneDistance };
                            TopPlane = new MathematicalPlane { normal = topPlaneNormal, distance = topPlaneDistance };

                            LevelLists.textures.Add(new Vector3(GetPlaneSignedDistanceToPoint(LeftPlane, LevelLists.vertices[baseVert]) / 2.5f, GetPlaneSignedDistanceToPoint(TopPlane, LevelLists.vertices[baseVert]) / 2.5f, 3));
                            LevelLists.textures.Add(new Vector3(GetPlaneSignedDistanceToPoint(LeftPlane, LevelLists.vertices[baseVert + 1]) / 2.5f, GetPlaneSignedDistanceToPoint(TopPlane, LevelLists.vertices[baseVert + 1]) / 2.5f, 3));
                            LevelLists.textures.Add(new Vector3(GetPlaneSignedDistanceToPoint(LeftPlane, LevelLists.vertices[baseVert + 2]) / 2.5f, GetPlaneSignedDistanceToPoint(TopPlane, LevelLists.vertices[baseVert + 2]) / 2.5f, 3));
                            LevelLists.textures.Add(new Vector3(GetPlaneSignedDistanceToPoint(LeftPlane, LevelLists.vertices[baseVert + 3]) / 2.5f, GetPlaneSignedDistanceToPoint(TopPlane, LevelLists.vertices[baseVert + 3]) / 2.5f, 3));

                            LevelLists.normals.Add(n);
                            LevelLists.normals.Add(n);
                            LevelLists.normals.Add(n);
                            LevelLists.normals.Add(n);

                            PolygonMeta transformedmesh = new PolygonMeta
                            {
                                plane = LevelLists.planes.Count,

                                collider = i,

                                opaque = i,

                                sectorId = i,

                                connectedSectorId = -1,

                                edgeStartIndex = -1,

                                edgeCount = -1,

                                triangleStartIndex = baseStartIndex,

                                triangleCount = 6
                            };

                            LevelLists.polygons.Add(transformedmesh);

                            MathematicalPlane plane = new MathematicalPlane
                            {
                                normal = n,
                                distance = -Vector3.Dot(n, v0)
                            };

                            LevelLists.planes.Add(plane);

                            polygonCount += 1;
                        }
                    }
                    if (sectors[wall].ceilingHeight != sectors[wall].floorHeight)
                    {
                        if (sector.ceilingHeight > sectors[wall].ceilingHeight)
                        {
                            Ceiling = sectors[wall].ceilingHeight / 8 * 2.5f;
                        }
                        else
                        {
                            Ceiling = sector.ceilingHeight / 8 * 2.5f;
                        }
                        if (sector.floorHeight > sectors[wall].floorHeight)
                        {
                            Floor = sector.floorHeight / 8 * 2.5f;
                        }
                        else
                        {
                            Floor = sectors[wall].floorHeight / 8 * 2.5f;
                        }

                        int baseVert = LevelLists.vertices.Count;

                        int baseStartIndex = LevelLists.edges.Count;

                        LevelLists.vertices.Add(new Vector3((float)Z1, (float)Floor, (float)X1));
                        LevelLists.vertices.Add(new Vector3((float)Z1, (float)Ceiling, (float)X1));
                        LevelLists.vertices.Add(new Vector3((float)Z0, (float)Ceiling, (float)X0));
                        LevelLists.vertices.Add(new Vector3((float)Z0, (float)Floor, (float)X0));

                        LevelLists.edges.Add(baseVert);
                        LevelLists.edges.Add(baseVert + 1);
                        LevelLists.edges.Add(baseVert + 1);
                        LevelLists.edges.Add(baseVert + 2);
                        LevelLists.edges.Add(baseVert + 2);
                        LevelLists.edges.Add(baseVert + 3);
                        LevelLists.edges.Add(baseVert + 3);
                        LevelLists.edges.Add(baseVert);

                        Vector3 v0 = LevelLists.vertices[baseVert];
                        Vector3 v1 = LevelLists.vertices[baseVert + 1];
                        Vector3 v2 = LevelLists.vertices[baseVert + 2];

                        Vector3 n = Vector3.Cross(v1 - v0, v2 - v0).normalized;

                        LevelLists.textures.Add(Vector3.zero);
                        LevelLists.textures.Add(Vector3.zero);
                        LevelLists.textures.Add(Vector3.zero);
                        LevelLists.textures.Add(Vector3.zero);

                        LevelLists.normals.Add(Vector3.zero);
                        LevelLists.normals.Add(Vector3.zero);
                        LevelLists.normals.Add(Vector3.zero);
                        LevelLists.normals.Add(Vector3.zero);

                        PolygonMeta transformedmesh = new PolygonMeta
                        {
                            plane = LevelLists.planes.Count,

                            collider = -1,

                            opaque = -1,

                            sectorId = i,

                            connectedSectorId = wall,

                            edgeStartIndex = baseStartIndex,

                            edgeCount = 8,

                            triangleStartIndex = -1,

                            triangleCount = -1
                        };

                        LevelLists.polygons.Add(transformedmesh);

                        MathematicalPlane plane = new MathematicalPlane
                        {
                            normal = n,
                            distance = -Vector3.Dot(n, v0)
                        };

                        LevelLists.planes.Add(plane);

                        polygonCount += 1;
                    }

                    if (sector.floorHeight < sectors[wall].floorHeight)
                    {
                        if (sector.ceilingHeight > sectors[wall].floorHeight)
                        {
                            double F0 = sector.floorHeight / 8 * 2.5f;

                            if (sector.floorHeight > sectors[wall].floorHeight)
                            {
                                Floor = sector.floorHeight / 8 * 2.5f;
                            }
                            else
                            {
                                Floor = sectors[wall].floorHeight / 8 * 2.5f;
                            }

                            int baseVert = LevelLists.vertices.Count;

                            int baseStartIndex = LevelLists.triangles.Count;

                            LevelLists.vertices.Add(new Vector3((float)Z1, (float)F0, (float)X1));
                            LevelLists.vertices.Add(new Vector3((float)Z1, (float)Floor, (float)X1));
                            LevelLists.vertices.Add(new Vector3((float)Z0, (float)Floor, (float)X0));
                            LevelLists.vertices.Add(new Vector3((float)Z0, (float)F0, (float)X0));

                            LevelLists.triangles.Add(baseVert);
                            LevelLists.triangles.Add(baseVert + 1);
                            LevelLists.triangles.Add(baseVert + 2);
                            LevelLists.triangles.Add(baseVert);
                            LevelLists.triangles.Add(baseVert + 2);
                            LevelLists.triangles.Add(baseVert + 3);

                            Vector3 v0 = LevelLists.vertices[baseVert];
                            Vector3 v1 = LevelLists.vertices[baseVert + 1];
                            Vector3 v2 = LevelLists.vertices[baseVert + 2];

                            Vector3 n = Vector3.Cross(v1 - v0, v2 - v0).normalized;

                            Vector3 leftPlaneNormal = (v2 - v1).normalized;
                            float leftPlaneDistance = -Vector3.Dot(leftPlaneNormal, v1);

                            Vector3 topPlaneNormal = (v1 - v0).normalized;
                            float topPlaneDistance = -Vector3.Dot(topPlaneNormal, v1);

                            LeftPlane = new MathematicalPlane { normal = leftPlaneNormal, distance = leftPlaneDistance };
                            TopPlane = new MathematicalPlane { normal = topPlaneNormal, distance = topPlaneDistance };

                            LevelLists.textures.Add(new Vector3(GetPlaneSignedDistanceToPoint(LeftPlane, LevelLists.vertices[baseVert]) / 2.5f, GetPlaneSignedDistanceToPoint(TopPlane, LevelLists.vertices[baseVert]) / 2.5f, 2));
                            LevelLists.textures.Add(new Vector3(GetPlaneSignedDistanceToPoint(LeftPlane, LevelLists.vertices[baseVert + 1]) / 2.5f, GetPlaneSignedDistanceToPoint(TopPlane, LevelLists.vertices[baseVert + 1]) / 2.5f, 2));
                            LevelLists.textures.Add(new Vector3(GetPlaneSignedDistanceToPoint(LeftPlane, LevelLists.vertices[baseVert + 2]) / 2.5f, GetPlaneSignedDistanceToPoint(TopPlane, LevelLists.vertices[baseVert + 2]) / 2.5f, 2));
                            LevelLists.textures.Add(new Vector3(GetPlaneSignedDistanceToPoint(LeftPlane, LevelLists.vertices[baseVert + 3]) / 2.5f, GetPlaneSignedDistanceToPoint(TopPlane, LevelLists.vertices[baseVert + 3]) / 2.5f, 2));

                            LevelLists.normals.Add(n);
                            LevelLists.normals.Add(n);
                            LevelLists.normals.Add(n);
                            LevelLists.normals.Add(n);

                            PolygonMeta transformedmesh = new PolygonMeta
                            {
                                plane = LevelLists.planes.Count,

                                collider = i,

                                opaque = i,

                                sectorId = i,

                                connectedSectorId = -1,

                                edgeStartIndex = -1,

                                edgeCount = -1,

                                triangleStartIndex = baseStartIndex,

                                triangleCount = 6
                            };

                            LevelLists.polygons.Add(transformedmesh);

                            MathematicalPlane plane = new MathematicalPlane
                            {
                                normal = n,
                                distance = -Vector3.Dot(n, v0)
                            };

                            LevelLists.planes.Add(plane);

                            polygonCount += 1;
                        }
                        else
                        {
                            double F0 = sector.floorHeight / 8 * 2.5f;
                            double F1 = sector.ceilingHeight / 8 * 2.5f;

                            int baseVert = LevelLists.vertices.Count;

                            int baseStartIndex = LevelLists.triangles.Count;

                            LevelLists.vertices.Add(new Vector3((float)Z1, (float)F0, (float)X1));
                            LevelLists.vertices.Add(new Vector3((float)Z1, (float)F1, (float)X1));
                            LevelLists.vertices.Add(new Vector3((float)Z0, (float)F1, (float)X0));
                            LevelLists.vertices.Add(new Vector3((float)Z0, (float)F0, (float)X0));

                            LevelLists.triangles.Add(baseVert);
                            LevelLists.triangles.Add(baseVert + 1);
                            LevelLists.triangles.Add(baseVert + 2);
                            LevelLists.triangles.Add(baseVert);
                            LevelLists.triangles.Add(baseVert + 2);
                            LevelLists.triangles.Add(baseVert + 3);

                            Vector3 v0 = LevelLists.vertices[baseVert];
                            Vector3 v1 = LevelLists.vertices[baseVert + 1];
                            Vector3 v2 = LevelLists.vertices[baseVert + 2];

                            Vector3 n = Vector3.Cross(v1 - v0, v2 - v0).normalized;

                            Vector3 leftPlaneNormal = (v2 - v1).normalized;
                            float leftPlaneDistance = -Vector3.Dot(leftPlaneNormal, v1);

                            Vector3 topPlaneNormal = (v1 - v0).normalized;
                            float topPlaneDistance = -Vector3.Dot(topPlaneNormal, v1);

                            LeftPlane = new MathematicalPlane { normal = leftPlaneNormal, distance = leftPlaneDistance };
                            TopPlane = new MathematicalPlane { normal = topPlaneNormal, distance = topPlaneDistance };

                            LevelLists.textures.Add(new Vector3(GetPlaneSignedDistanceToPoint(LeftPlane, LevelLists.vertices[baseVert]) / 2.5f, GetPlaneSignedDistanceToPoint(TopPlane, LevelLists.vertices[baseVert]) / 2.5f, 2));
                            LevelLists.textures.Add(new Vector3(GetPlaneSignedDistanceToPoint(LeftPlane, LevelLists.vertices[baseVert + 1]) / 2.5f, GetPlaneSignedDistanceToPoint(TopPlane, LevelLists.vertices[baseVert + 1]) / 2.5f, 2));
                            LevelLists.textures.Add(new Vector3(GetPlaneSignedDistanceToPoint(LeftPlane, LevelLists.vertices[baseVert + 2]) / 2.5f, GetPlaneSignedDistanceToPoint(TopPlane, LevelLists.vertices[baseVert + 2]) / 2.5f, 2));
                            LevelLists.textures.Add(new Vector3(GetPlaneSignedDistanceToPoint(LeftPlane, LevelLists.vertices[baseVert + 3]) / 2.5f, GetPlaneSignedDistanceToPoint(TopPlane, LevelLists.vertices[baseVert + 3]) / 2.5f, 2));

                            LevelLists.normals.Add(n);
                            LevelLists.normals.Add(n);
                            LevelLists.normals.Add(n);
                            LevelLists.normals.Add(n);

                            PolygonMeta transformedmesh = new PolygonMeta
                            {
                                plane = LevelLists.planes.Count,

                                collider = i,

                                opaque = i,

                                sectorId = i,

                                connectedSectorId = -1,

                                edgeStartIndex = -1,

                                edgeCount = -1,

                                triangleStartIndex = baseStartIndex,

                                triangleCount = 6
                            };

                            LevelLists.polygons.Add(transformedmesh);

                            MathematicalPlane plane = new MathematicalPlane
                            {
                                normal = n,
                                distance = -Vector3.Dot(n, v0)
                            };

                            LevelLists.planes.Add(plane);

                            polygonCount += 1;
                        }
                    }
                }
            }

            if (sector.floorHeight != sector.ceilingHeight)
            {
                floorverts.Clear();
                ceilingverts.Clear();
                flooruvs.Clear();
                ceilinguvs.Clear();

                float tinyNumber = 1e-6f;

                for (int e = 0; e < sector.vertexIndices.Count; ++e)
                {
                    double YF = sector.floorHeight / 8 * 2.5f;
                    double YC = sector.ceilingHeight / 8 * 2.5f;
                    double X = vertices[sector.vertexIndices[e]].x / 2 * 2.5f;
                    double Z = vertices[sector.vertexIndices[e]].y / 2 * 2.5f;

                    float OX = (float)X / 2.5f * -1;
                    float OY = (float)Z / 2.5f;

                    floorverts.Add(new Vector3((float)Z, (float)YF, (float)X));
                    ceilingverts.Add(new Vector3((float)Z, (float)YC, (float)X));
                    flooruvs.Add(new Vector3(OY, OX, 0));
                    ceilinguvs.Add(new Vector3(OY, OX, 1));
                }

                floortri.Clear();

                for (int e = 0; e < floorverts.Count - 2; e++)
                {
                    Vector3 v0 = floorverts[0];
                    Vector3 v1 = floorverts[e + 1];
                    Vector3 v2 = floorverts[e + 2];

                    Vector3 e0 = v1 - v0;
                    Vector3 e1 = v2 - v1;
                    Vector3 e2 = v2 - v0;

                    if (e0.sqrMagnitude < tinyNumber || e1.sqrMagnitude < tinyNumber || e2.sqrMagnitude < tinyNumber)
                    {
                        continue;
                    }

                    Vector3 edges = Vector3.Cross(e0, e2);

                    if (edges.sqrMagnitude < tinyNumber)
                    {
                        continue;
                    }

                    floortri.Add(0);
                    floortri.Add(e + 1);
                    floortri.Add(e + 2);
                }

                ceilingverts.Reverse();
                ceilinguvs.Reverse();

                ceilingtri.Clear();

                for (int e = 0; e < ceilingverts.Count - 2; e++)
                {
                    Vector3 v0 = ceilingverts[0];
                    Vector3 v1 = ceilingverts[e + 1];
                    Vector3 v2 = ceilingverts[e + 2];

                    Vector3 e0 = v1 - v0;
                    Vector3 e1 = v2 - v1;
                    Vector3 e2 = v2 - v0;

                    if (e0.sqrMagnitude < tinyNumber || e1.sqrMagnitude < tinyNumber || e2.sqrMagnitude < tinyNumber)
                    {
                        continue;
                    }

                    Vector3 edges = Vector3.Cross(e0, e2);

                    if (edges.sqrMagnitude < tinyNumber)
                    {
                        continue;
                    }

                    ceilingtri.Add(0);
                    ceilingtri.Add(e + 1);
                    ceilingtri.Add(e + 2);
                }

                int baseFloor = LevelLists.vertices.Count;

                int floorStartIndex = LevelLists.triangles.Count;

                for (int e = 0; e < floorverts.Count; e++)
                {
                    LevelLists.vertices.Add(floorverts[e]);
                }

                for (int e = 0; e < flooruvs.Count; e++)
                {
                    LevelLists.textures.Add(flooruvs[e]);
                }

                for (int e = 0; e < floortri.Count; e++)
                {
                    LevelLists.triangles.Add(baseFloor + floortri[e]);
                }

                Vector3 f0 = floorverts[floortri[0]];
                Vector3 f1 = floorverts[floortri[1]];
                Vector3 f2 = floorverts[floortri[2]];

                Vector3 f = Vector3.Cross(f1 - f0, f2 - f0).normalized;

                for (int e = 0; e < floorverts.Count; e++)
                {
                    LevelLists.normals.Add(f);
                }

                PolygonMeta transformedfloormesh = new PolygonMeta
                {
                    plane = LevelLists.planes.Count,

                    collider = i,

                    opaque = i,

                    sectorId = i,

                    connectedSectorId = -1,

                    edgeStartIndex = -1,

                    edgeCount = -1,

                    triangleStartIndex = floorStartIndex,

                    triangleCount = floortri.Count
                };

                LevelLists.polygons.Add(transformedfloormesh);

                MathematicalPlane floorPlane = new MathematicalPlane
                {
                    normal = f,
                    distance = -Vector3.Dot(f, f0)
                };

                LevelLists.planes.Add(floorPlane);

                polygonCount += 1;

                int baseCeiling = LevelLists.vertices.Count;

                int ceilingStartIndex = LevelLists.triangles.Count;

                for (int e = 0; e < ceilingverts.Count; e++)
                {
                    LevelLists.vertices.Add(ceilingverts[e]);
                }

                for (int e = 0; e < ceilinguvs.Count; e++)
                {
                    LevelLists.textures.Add(ceilinguvs[e]);
                }

                for (int e = 0; e < ceilingtri.Count; e++)
                {
                    LevelLists.triangles.Add(baseCeiling + ceilingtri[e]);
                }

                Vector3 c0 = ceilingverts[ceilingtri[0]];
                Vector3 c1 = ceilingverts[ceilingtri[1]];
                Vector3 c2 = ceilingverts[ceilingtri[2]];

                Vector3 c = Vector3.Cross(c1 - c0, c2 - c0).normalized;

                for (int e = 0; e < ceilingverts.Count; e++)
                {
                    LevelLists.normals.Add(c);
                }

                PolygonMeta transformedceilingmesh = new PolygonMeta
                {
                    plane = LevelLists.planes.Count,

                    collider = i,

                    opaque = i,

                    sectorId = i,

                    connectedSectorId = -1,

                    edgeStartIndex = -1,

                    edgeCount = -1,

                    triangleStartIndex = ceilingStartIndex,

                    triangleCount = ceilingtri.Count
                };

                LevelLists.polygons.Add(transformedceilingmesh);

                MathematicalPlane ceilingPlane = new MathematicalPlane
                {
                    normal = c,
                    distance = -Vector3.Dot(c, c0)
                };

                LevelLists.planes.Add(ceilingPlane);

                polygonCount += 1;
            }

            SectorMeta sectorMeta = new SectorMeta
            {
                sectorId = i,
                polygonStartIndex = polygonStart,
                polygonCount = polygonCount,
            };

            LevelLists.sectors.Add(sectorMeta);
            polygonStart += polygonCount;
        }

        Debug.Log("Level built successfully!");
    }

    public void BuildObjects()
    {
        for (int i = 0; i < starts.Count; i++)
        {
            StartPosition start = new StartPosition
            {
                playerStart = new Vector3(starts[i].location.x / 2 * 2.5f, sectors[starts[i].sector].floorHeight / 8 * 2.5f, starts[i].location.y / 2 * 2.5f),

                sectorId = starts[i].sector
            };

            LevelLists.positions.Add(start);
        }
    }

    public void BuildColliders()
    {
        for (int i = 0; i < LevelLists.sectors.Count; i++)
        {
            colliderVertices.Clear();

            colliderTriangles.Clear();

            int triangleCount = 0;

            for (int e = LevelLists.sectors[i].polygonStartIndex; e < LevelLists.sectors[i].polygonStartIndex + LevelLists.sectors[i].polygonCount; e++)
            {
                if (LevelLists.polygons[e].collider != -1)
                {
                    for (int f = LevelLists.polygons[e].triangleStartIndex; f < LevelLists.polygons[e].triangleStartIndex + LevelLists.polygons[e].triangleCount; f += 3)
                    {
                        colliderVertices.Add(LevelLists.vertices[LevelLists.triangles[f]]);
                        colliderVertices.Add(LevelLists.vertices[LevelLists.triangles[f + 1]]);
                        colliderVertices.Add(LevelLists.vertices[LevelLists.triangles[f + 2]]);
                        colliderTriangles.Add(triangleCount);
                        colliderTriangles.Add(triangleCount + 1);
                        colliderTriangles.Add(triangleCount + 2);
                        triangleCount += 3;
                    }
                }
            }

            Mesh combinedmesh = new Mesh();

            CollisionMesh.Add(combinedmesh);

            combinedmesh.SetVertices(colliderVertices);

            combinedmesh.SetTriangles(colliderTriangles, 0);

            GameObject meshObject = new GameObject("Collision " + i);

            MeshCollider meshCollider = meshObject.AddComponent<MeshCollider>();

            meshCollider.sharedMesh = combinedmesh;

            CollisionSectors.Add(meshCollider);

            meshObject.transform.SetParent(CollisionObjects.transform);
        }
    }
}

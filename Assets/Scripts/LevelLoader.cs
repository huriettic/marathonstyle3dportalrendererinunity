using System;
using System.Collections.Generic;
using UnityEngine;

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
public struct PortalMeta
{
    public int edgeStartIndex;
    public int edgeCount;

    public int connectedSectorId;
    public int sectorId;

    public int plane;
    public int portalId;
};

[Serializable]
public struct SectorMeta
{
    public int portalStartIndex;
    public int portalCount;

    public int planeStartIndex;
    public int planeCount;

    public int rectangle;
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
    List<Vector3> ceilingVertices = new List<Vector3>();
    List<int> ceilingTriangles = new List<int>();
    List<Vector3> floorVertices = new List<Vector3>();
    List<int> floorTriangles = new List<int>();
    Material opaquematerial;
    GameObject CollisionObjects;
    bool[] boolEdges;
    Vector4[] processEdges;
    Vector4[] temporaryEdges;
    List<List<SectorMeta>> ListOfSectorLists = new List<List<SectorMeta>>();
    Vector4[][] ArrayOfRectangleArrays;
    Camera Cam;
    Vector3 CamPoint;
    SectorMeta CurrentSector;
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
    List<Vector3> floorTextures = new List<Vector3>();
    List<Vector3> ceilingTextures = new List<Vector3>();
    Matrix4x4 viewProjection;
    Matrix4x4 matrixIdentity;
    List<Vector4> debugRectangles = new List<Vector4>();
    int[] visibleSectors;
    Texture2D linetexture;
    List<Vector3> temporaryVertices = new List<Vector3>();
    List<Vector3> temporaryNormals = new List<Vector3>();
    List<Vector3> temporaryTextures = new List<Vector3>();
    List<int> temporaryTriangles = new List<int>();
    RenderParams rp;

    [Serializable]
    public class Sector
    {
        public float floorHeight;
        public float ceilingHeight;
        public List<int> vertexIndices = new List<int>();
        public List<int> wallTypes = new List<int>(); // -1 for solid, sector temporaryTriangles for portal
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
        public List<int> edges = new List<int>();
        public List<MathematicalPlane> planes = new List<MathematicalPlane>();
        public List<PortalMeta> portals = new List<PortalMeta>();
        public List<Mesh> render = new List<Mesh>();
        public List<MeshCollider> collision = new List<MeshCollider>();
        public List<SectorMeta> sectors = new List<SectorMeta>();
        public List<StartPosition> positions = new List<StartPosition>();
    }

    void OnGUI()
    {
        if (!debug)
        {
            return;
        }

        GUI.color = Color.blue;

        for (int i = 0; i < debugRectangles.Count; i++)
        {
            Vector4 rectangle = debugRectangles[i];

            float xmin = (rectangle.x * 0.5f + 0.5f) * Screen.width;
            float ymin = (rectangle.y * 0.5f + 0.5f) * Screen.height;
            float xmax = (rectangle.z * 0.5f + 0.5f) * Screen.width;
            float ymax = (rectangle.w * 0.5f + 0.5f) * Screen.height;

            MakeLeftLine(xmin, ymin, xmin, ymax, 5.0f); // left
            MakeRightLine(xmax, ymin, xmax, ymax, 5.0f); // right
            MakeBottomLine(xmin, ymin, xmax, ymin, 5.0f); // bottom
            MakeTopLine(xmin, ymax, xmax, ymax, 5.0f); // top
        }
    }

    void Start()
    {
        rp = new RenderParams();

        rp.matProps = new MaterialPropertyBlock();

        CollisionObjects = new GameObject("Collision Meshes");

        LevelLists = new TopLevelLists();

        matrixIdentity = Matrix4x4.identity;

        LoadFromFile();

        CreateMaterial();

        BuildGeometry();

        BuildObjects();

        PlayerStart();

        visibleSectors = new int[LevelLists.sectors.Count];

        boolEdges = new bool[128];

        processEdges = new Vector4[128];

        temporaryEdges = new Vector4[128];

        ArrayOfRectangleArrays = new Vector4[LevelLists.sectors.Count][];

        for (int i = 0; i < LevelLists.sectors.Count; i++)
        {
            ArrayOfRectangleArrays[i] = new Vector4[32];
        }

        for (int i = 0; i < 2; i++)
        {
            ListOfSectorLists.Add(new List<SectorMeta>());
        }

        for (int i = 0; i < LevelLists.sectors.Count; i++)
        {
            Physics.IgnoreCollision(Player, LevelLists.collision[LevelLists.sectors[i].sectorId], true);
        }
    }

    void Update()
    {
        PlayerInput();

        if (Cam.transform.hasChanged)
        {
            Matrix4x4 view = Cam.worldToCameraMatrix;

            Matrix4x4 projection = GL.GetGPUProjectionMatrix(Cam.projectionMatrix, true);

            viewProjection = projection * view;

            CamPoint = Cam.transform.position;

            GetSectors(CurrentSector);

            debugRectangles.Clear();

            Array.Clear(visibleSectors, 0, visibleSectors.Length);

            GetPolygons(CurrentSector);

            Cam.transform.hasChanged = false;
        }

        GetTriangles();
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

    public Vector4 ConvertWorldToClip(Matrix4x4 viewProj, Vector3 vertex)
    {
        return viewProj * new Vector4(vertex.x, vertex.y, vertex.z, 1.0f);
    }

    public Vector3 ConvertClipToNDC(Vector4 vertex)
    {
        float invw = 1.0f / vertex.w;

        return new Vector3(vertex.x * invw, vertex.y * invw, vertex.z * invw);
    }

    public void MakeLeftLine(float x1, float y1, float x2, float y2, float linethickness)
    {
        GUI.DrawTexture(new Rect(x1, y1, linethickness, y2 - y1), linetexture);
    }

    public void MakeRightLine(float x1, float y1, float x2, float y2, float linethickness)
    {
        GUI.DrawTexture(new Rect(x1 - linethickness, y1, linethickness, y2 - y1), linetexture);
    }

    public void MakeBottomLine(float x1, float y1, float x2, float y2, float linethickness)
    {
        GUI.DrawTexture(new Rect(x1, y1, x2 - x1, linethickness), linetexture);
    }

    public void MakeTopLine(float x1, float y1, float x2, float y2, float linethickness)
    {
        GUI.DrawTexture(new Rect(x1, y1 - linethickness, x2 - x1, linethickness), linetexture);
    }

    public void CreateMaterial()
    {
        Shader shader = Resources.Load<Shader>("TexArray");

        opaquematerial = new Material(shader);

        opaquematerial.mainTexture = Resources.Load<Texture2DArray>("Textures");
    }

    public void PlayerInput()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Application.Quit();
        }
        if (Input.GetKeyDown(KeyCode.R))
        {
            debug = !debug;
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

    public Vector4 MakeRectangleWithEdges(Vector4 rectangle, PortalMeta portal)
    {
        OutEdgeVertices.Clear();

        int processverticescount = 0;
        int processboolcount = 0;

        for (int a = portal.edgeStartIndex; a < portal.edgeStartIndex + portal.edgeCount; a += 2)
        {
            Vector4 v0clip = ConvertWorldToClip(viewProjection, LevelLists.vertices[LevelLists.edges[a]]);
            Vector4 v1clip = ConvertWorldToClip(viewProjection, LevelLists.vertices[LevelLists.edges[a + 1]]);

            processEdges[processverticescount] = v0clip;
            processEdges[processverticescount + 1] = v1clip;
            processverticescount += 2;
            boolEdges[processboolcount] = true;
            boolEdges[processboolcount + 1] = true;
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
                if (boolEdges[c] == false && boolEdges[c + 1] == false)
                {
                    continue;
                }

                Vector4 v0 = processEdges[c];
                Vector4 v1 = processEdges[c + 1];

                float minX = rectangle.x;
                float minY = rectangle.y;
                float maxX = rectangle.z;
                float maxY = rectangle.w;

                float d0, d1;

                switch (b)
                {
                    case 0: // Left
                        d0 = v0.x - minX * v0.w;
                        d1 = v1.x - minX * v1.w;
                        break;

                    case 1: // Right
                        d0 = maxX * v0.w - v0.x;
                        d1 = maxX * v1.w - v1.x;
                        break;

                    case 2: // Bottom
                        d0 = v0.y - minY * v0.w;
                        d1 = v1.y - minY * v1.w;
                        break;

                    case 3: // Top
                        d0 = maxY * v0.w - v0.y;
                        d1 = maxY * v1.w - v1.y;
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

                    temporaryEdges[temporaryverticescount] = point0;
                    temporaryEdges[temporaryverticescount + 1] = point1;
                    temporaryverticescount += 2;

                    boolEdges[c] = false;
                    boolEdges[c + 1] = false;

                    intersection += 1;
                }
                else
                {
                    boolEdges[c] = false;
                    boolEdges[c + 1] = false;
                }
            }

            if (intersection == 2)
            {
                for (int d = 0; d < temporaryverticescount; d += 2)
                {
                    processEdges[processverticescount] = temporaryEdges[d];
                    processEdges[processverticescount + 1] = temporaryEdges[d + 1];
                    processverticescount += 2;
                    boolEdges[processboolcount] = true;
                    boolEdges[processboolcount + 1] = true;
                    processboolcount += 2;
                }

                processEdges[processverticescount] = intersectionPoint0;
                processEdges[processverticescount + 1] = intersectionPoint1;
                processverticescount += 2;
                boolEdges[processboolcount] = true;
                boolEdges[processboolcount + 1] = true;
                processboolcount += 2;
            }
        }

        for (int e = 0; e < processboolcount; e += 2)
        {
            if (boolEdges[e] == true && boolEdges[e + 1] == true)
            {
                Vector4 clip0 = processEdges[e];
                Vector4 clip1 = processEdges[e + 1];

                Vector3 ndc0 = ConvertClipToNDC(clip0);
                Vector3 ndc1 = ConvertClipToNDC(clip1);

                OutEdgeVertices.Add(ndc0);
                OutEdgeVertices.Add(ndc1);
            }
        }

        if (OutEdgeVertices.Count < 6 || OutEdgeVertices.Count % 2 == 1)
        {
            return Vector4.zero;
        }

        float xmin = float.PositiveInfinity;
        float ymin = float.PositiveInfinity;
        float xmax = float.NegativeInfinity;
        float ymax = float.NegativeInfinity;

        for (int i = 0; i < OutEdgeVertices.Count; i++)
        {
            Vector3 ndc = OutEdgeVertices[i];

            if (ndc.x < xmin)
            {
                xmin = ndc.x;
            }
            if (ndc.x > xmax)
            {
                xmax = ndc.x;
            }
            if (ndc.y < ymin)
            {
                ymin = ndc.y;
            }
            if (ndc.y > ymax)
            {
                ymax = ndc.y;
            }
        }

        return new Vector4(xmin, ymin, xmax, ymax);
    }

    public bool CheckRadius(SectorMeta asector, Vector3 campoint)
    {
        for (int i = asector.planeStartIndex; i < asector.planeStartIndex + asector.planeCount; i++)
        {
            if (GetPlaneSignedDistanceToPoint(LevelLists.planes[i], campoint) < -0.6f)
            {
                return false;
            }
        }
        return true;
    }

    public bool CheckSector(SectorMeta asector, Vector3 campoint)
    {
        for (int i = asector.planeStartIndex; i < asector.planeStartIndex + asector.planeCount; i++)
        {
            if (GetPlaneSignedDistanceToPoint(LevelLists.planes[i], campoint) < 0)
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
            Physics.IgnoreCollision(Player, LevelLists.collision[OldSectors[a].sectorId], true);
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

                Physics.IgnoreCollision(Player, LevelLists.collision[sector.sectorId], false);

                for (int d = sector.portalStartIndex; d < sector.portalStartIndex + sector.portalCount; d++)
                {
                    int connectedsector = LevelLists.portals[d].connectedSectorId;

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

        ArrayOfRectangleArrays[ASector.sectorId][ASector.rectangle] = new Vector4(-1.0f, -1.0f, 1.0f, 1.0f);

        visibleSectors[ASector.sectorId] = ASector.rectangle + 1;

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

            ListOfSectorLists[output].Clear();

            if (ListOfSectorLists[input].Count == 0)
            {
                break;
            }

            for (int b = 0; b < ListOfSectorLists[input].Count; b++)
            {
                SectorMeta sector = ListOfSectorLists[input][b];

                Vector4 rectangleIn = ArrayOfRectangleArrays[sector.sectorId][sector.rectangle];

                debugRectangles.Add(rectangleIn);

                for (int c = sector.portalStartIndex; c < sector.portalStartIndex + sector.portalCount; c++)
                {
                    PortalMeta polygon = LevelLists.portals[c];

                    planeDistance = GetPlaneSignedDistanceToPoint(LevelLists.planes[polygon.plane], CamPoint);

                    if (planeDistance <= 0)
                    {
                        continue;
                    }

                    int connectedsector = polygon.connectedSectorId;

                    SectorMeta sectorpolygon = LevelLists.sectors[connectedsector];

                    int nextcount = visibleSectors[connectedsector];

                    int connectedstart = sectorpolygon.portalStartIndex;

                    int connectedcount = sectorpolygon.portalCount;

                    if (nextcount >= 32)
                    {
                        continue;
                    }

                    if (SectorsContains(sectorpolygon.sectorId))
                    {
                        ArrayOfRectangleArrays[connectedsector][nextcount] = rectangleIn;

                        visibleSectors[connectedsector] = nextcount + 1;

                        SectorMeta ContactSector = new SectorMeta
                        {
                            portalStartIndex = connectedstart,
                            portalCount = connectedcount,

                            rectangle = nextcount,
                            sectorId = connectedsector
                        };

                        ListOfSectorLists[output].Add(ContactSector);

                        continue;
                    }

                    Vector4 rectangleOut = MakeRectangleWithEdges(rectangleIn, polygon);

                    if (OutEdgeVertices.Count < 6 || OutEdgeVertices.Count % 2 == 1)
                    {
                        continue;
                    }

                    if (DegenerateRectangle(rectangleOut))
                    {
                        continue;
                    }

                    if (RectanglesDoNotOverlap(rectangleIn, rectangleOut))
                    {
                        continue;
                    }

                    ArrayOfRectangleArrays[connectedsector][nextcount] = rectangleOut;

                    visibleSectors[connectedsector] = nextcount + 1;

                    SectorMeta VisibleSector = new SectorMeta
                    {
                        portalStartIndex = connectedstart,
                        portalCount = connectedcount,

                        rectangle = nextcount,
                        sectorId = connectedsector
                    };

                    ListOfSectorLists[output].Add(VisibleSector);
                }
            }
        }
    }

    public void GetTriangles()
    {
        for (int a = 0; a < visibleSectors.Length; a++)
        {
            int count = visibleSectors[a];

            if (count == 0)
            {
                continue;
            }

            Mesh renderMesh = LevelLists.render[a];

            SectorMeta sector = LevelLists.sectors[a];

            Vector4[] rectanglesArray = ArrayOfRectangleArrays[sector.sectorId];

            rp.material = opaquematerial;

            rp.matProps.SetVectorArray("rectangles", rectanglesArray);

            rp.matProps.SetInt("count", count);

            renderMesh.RecalculateBounds();

            Graphics.RenderMesh(rp, renderMesh, 0, matrixIdentity);
        }
    }

    public bool DegenerateRectangle(Vector4 r)
    {
        return r.x >= r.z || r.y >= r.w || (r.z - r.x) < 0.001f || (r.w - r.y) < 0.001f;
    }

    public bool RectanglesDoNotOverlap(Vector4 a, Vector4 b)
    {
        return a.z < b.x || a.x > b.z || a.w < b.y || a.y > b.w;
    }

    public void PlayerStart()
    {
        if (LevelLists.positions.Count == 0)
        {
            Debug.LogError("No player starts available.");

            return;
        }

        int randomtemporaryTriangles = UnityEngine.Random.Range(0, LevelLists.positions.Count);

        StartPosition selectedPosition = LevelLists.positions[randomtemporaryTriangles];

        CurrentSector = LevelLists.sectors[selectedPosition.sectorId];

        Player.transform.position = new Vector3(selectedPosition.playerStart.z, selectedPosition.playerStart.y + 1.10f, selectedPosition.playerStart.x);
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
        int portalStart = 0;

        int planeStart = 0;

        int portalNumber = 0;

        for (int i = 0; i < sectors.Count; i++)
        {
            temporaryVertices.Clear();

            temporaryTextures.Clear();

            temporaryNormals.Clear();

            temporaryTriangles.Clear();

            int portalCount = 0;

            int planeCount = 0;

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

                    int baseVert = temporaryVertices.Count;

                    int baseStarttemporaryTriangles = temporaryTriangles.Count;

                    temporaryVertices.Add(new Vector3((float)Z1, (float)V0, (float)X1));
                    temporaryVertices.Add(new Vector3((float)Z1, (float)V1, (float)X1));
                    temporaryVertices.Add(new Vector3((float)Z0, (float)V1, (float)X0));
                    temporaryVertices.Add(new Vector3((float)Z0, (float)V0, (float)X0));

                    temporaryTriangles.Add(baseVert);
                    temporaryTriangles.Add(baseVert + 1);
                    temporaryTriangles.Add(baseVert + 2);
                    temporaryTriangles.Add(baseVert);
                    temporaryTriangles.Add(baseVert + 2);
                    temporaryTriangles.Add(baseVert + 3);

                    Vector3 v0 = temporaryVertices[baseVert];
                    Vector3 v1 = temporaryVertices[baseVert + 1];
                    Vector3 v2 = temporaryVertices[baseVert + 2];

                    Vector3 n = Vector3.Cross(v1 - v0, v2 - v0).normalized;

                    Vector3 leftPlaneNormal = (v2 - v1).normalized;
                    float leftPlaneDistance = -Vector3.Dot(leftPlaneNormal, v1);

                    Vector3 topPlaneNormal = (v1 - v0).normalized;
                    float topPlaneDistance = -Vector3.Dot(topPlaneNormal, v1);

                    LeftPlane = new MathematicalPlane { normal = leftPlaneNormal, distance = leftPlaneDistance };
                    TopPlane = new MathematicalPlane { normal = topPlaneNormal, distance = topPlaneDistance };

                    temporaryTextures.Add(new Vector3(GetPlaneSignedDistanceToPoint(LeftPlane, temporaryVertices[baseVert]) / 2.5f, GetPlaneSignedDistanceToPoint(TopPlane, temporaryVertices[baseVert]) / 2.5f, 3));
                    temporaryTextures.Add(new Vector3(GetPlaneSignedDistanceToPoint(LeftPlane, temporaryVertices[baseVert + 1]) / 2.5f, GetPlaneSignedDistanceToPoint(TopPlane, temporaryVertices[baseVert + 1]) / 2.5f, 3));
                    temporaryTextures.Add(new Vector3(GetPlaneSignedDistanceToPoint(LeftPlane, temporaryVertices[baseVert + 2]) / 2.5f, GetPlaneSignedDistanceToPoint(TopPlane, temporaryVertices[baseVert + 2]) / 2.5f, 3));
                    temporaryTextures.Add(new Vector3(GetPlaneSignedDistanceToPoint(LeftPlane, temporaryVertices[baseVert + 3]) / 2.5f, GetPlaneSignedDistanceToPoint(TopPlane, temporaryVertices[baseVert + 3]) / 2.5f, 3));

                    temporaryNormals.Add(n);
                    temporaryNormals.Add(n);
                    temporaryNormals.Add(n);
                    temporaryNormals.Add(n);

                    MathematicalPlane plane = new MathematicalPlane
                    {
                        normal = n,
                        distance = -Vector3.Dot(n, v0)
                    };

                    LevelLists.planes.Add(plane);

                    planeCount += 1;
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

                            int baseVert = temporaryVertices.Count;

                            int baseStarttemporaryTriangles = temporaryVertices.Count;

                            temporaryVertices.Add(new Vector3((float)Z1, (float)Ceiling, (float)X1));
                            temporaryVertices.Add(new Vector3((float)Z1, (float)C0, (float)X1));
                            temporaryVertices.Add(new Vector3((float)Z0, (float)C0, (float)X0));
                            temporaryVertices.Add(new Vector3((float)Z0, (float)Ceiling, (float)X0));

                            temporaryTriangles.Add(baseVert);
                            temporaryTriangles.Add(baseVert + 1);
                            temporaryTriangles.Add(baseVert + 2);
                            temporaryTriangles.Add(baseVert);
                            temporaryTriangles.Add(baseVert + 2);
                            temporaryTriangles.Add(baseVert + 3);

                            Vector3 v0 = temporaryVertices[baseVert];
                            Vector3 v1 = temporaryVertices[baseVert + 1];
                            Vector3 v2 = temporaryVertices[baseVert + 2];

                            Vector3 n = Vector3.Cross(v1 - v0, v2 - v0).normalized;

                            Vector3 leftPlaneNormal = (v2 - v1).normalized;
                            float leftPlaneDistance = -Vector3.Dot(leftPlaneNormal, v1);

                            Vector3 topPlaneNormal = (v1 - v0).normalized;
                            float topPlaneDistance = -Vector3.Dot(topPlaneNormal, v1);

                            LeftPlane = new MathematicalPlane { normal = leftPlaneNormal, distance = leftPlaneDistance };
                            TopPlane = new MathematicalPlane { normal = topPlaneNormal, distance = topPlaneDistance };

                            temporaryTextures.Add(new Vector3(GetPlaneSignedDistanceToPoint(LeftPlane, temporaryVertices[baseVert]) / 2.5f, GetPlaneSignedDistanceToPoint(TopPlane, temporaryVertices[baseVert]) / 2.5f, 3));
                            temporaryTextures.Add(new Vector3(GetPlaneSignedDistanceToPoint(LeftPlane, temporaryVertices[baseVert + 1]) / 2.5f, GetPlaneSignedDistanceToPoint(TopPlane, temporaryVertices[baseVert + 1]) / 2.5f, 3));
                            temporaryTextures.Add(new Vector3(GetPlaneSignedDistanceToPoint(LeftPlane, temporaryVertices[baseVert + 2]) / 2.5f, GetPlaneSignedDistanceToPoint(TopPlane, temporaryVertices[baseVert + 2]) / 2.5f, 3));
                            temporaryTextures.Add(new Vector3(GetPlaneSignedDistanceToPoint(LeftPlane, temporaryVertices[baseVert + 3]) / 2.5f, GetPlaneSignedDistanceToPoint(TopPlane, temporaryVertices[baseVert + 3]) / 2.5f, 3));

                            temporaryNormals.Add(n);
                            temporaryNormals.Add(n);
                            temporaryNormals.Add(n);
                            temporaryNormals.Add(n);

                            MathematicalPlane plane = new MathematicalPlane
                            {
                                normal = n,
                                distance = -Vector3.Dot(n, v0)
                            };

                            LevelLists.planes.Add(plane);

                            planeCount += 1;
                        }
                        else
                        {
                            double C0 = sector.ceilingHeight / 8 * 2.5f;
                            double C1 = sector.floorHeight / 8 * 2.5f;

                            int baseVert = temporaryVertices.Count;

                            int baseStarttemporaryTriangles = temporaryTriangles.Count;

                            temporaryVertices.Add(new Vector3((float)Z1, (float)C1, (float)X1));
                            temporaryVertices.Add(new Vector3((float)Z1, (float)C0, (float)X1));
                            temporaryVertices.Add(new Vector3((float)Z0, (float)C0, (float)X0));
                            temporaryVertices.Add(new Vector3((float)Z0, (float)C1, (float)X0));

                            temporaryTriangles.Add(baseVert);
                            temporaryTriangles.Add(baseVert + 1);
                            temporaryTriangles.Add(baseVert + 2);
                            temporaryTriangles.Add(baseVert);
                            temporaryTriangles.Add(baseVert + 2);
                            temporaryTriangles.Add(baseVert + 3);

                            Vector3 v0 = temporaryVertices[baseVert];
                            Vector3 v1 = temporaryVertices[baseVert + 1];
                            Vector3 v2 = temporaryVertices[baseVert + 2];

                            Vector3 n = Vector3.Cross(v1 - v0, v2 - v0).normalized;

                            Vector3 leftPlaneNormal = (v2 - v1).normalized;
                            float leftPlaneDistance = -Vector3.Dot(leftPlaneNormal, v1);

                            Vector3 topPlaneNormal = (v1 - v0).normalized;
                            float topPlaneDistance = -Vector3.Dot(topPlaneNormal, v1);

                            LeftPlane = new MathematicalPlane { normal = leftPlaneNormal, distance = leftPlaneDistance };
                            TopPlane = new MathematicalPlane { normal = topPlaneNormal, distance = topPlaneDistance };

                            temporaryTextures.Add(new Vector3(GetPlaneSignedDistanceToPoint(LeftPlane, temporaryVertices[baseVert]) / 2.5f, GetPlaneSignedDistanceToPoint(TopPlane, temporaryVertices[baseVert]) / 2.5f, 3));
                            temporaryTextures.Add(new Vector3(GetPlaneSignedDistanceToPoint(LeftPlane, temporaryVertices[baseVert + 1]) / 2.5f, GetPlaneSignedDistanceToPoint(TopPlane, temporaryVertices[baseVert + 1]) / 2.5f, 3));
                            temporaryTextures.Add(new Vector3(GetPlaneSignedDistanceToPoint(LeftPlane, temporaryVertices[baseVert + 2]) / 2.5f, GetPlaneSignedDistanceToPoint(TopPlane, temporaryVertices[baseVert + 2]) / 2.5f, 3));
                            temporaryTextures.Add(new Vector3(GetPlaneSignedDistanceToPoint(LeftPlane, temporaryVertices[baseVert + 3]) / 2.5f, GetPlaneSignedDistanceToPoint(TopPlane, temporaryVertices[baseVert + 3]) / 2.5f, 3));

                            temporaryNormals.Add(n);
                            temporaryNormals.Add(n);
                            temporaryNormals.Add(n);
                            temporaryNormals.Add(n);

                            MathematicalPlane plane = new MathematicalPlane
                            {
                                normal = n,
                                distance = -Vector3.Dot(n, v0)
                            };

                            LevelLists.planes.Add(plane);

                            planeCount += 1;
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

                        int baseStarttemporaryTriangles = LevelLists.edges.Count;

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

                        PortalMeta transformedportal = new PortalMeta
                        {
                            plane = LevelLists.planes.Count,

                            sectorId = i,

                            connectedSectorId = wall,

                            edgeStartIndex = baseStarttemporaryTriangles,

                            edgeCount = 8,

                            portalId = portalNumber
                        };

                        LevelLists.portals.Add(transformedportal);

                        MathematicalPlane plane = new MathematicalPlane
                        {
                            normal = n,
                            distance = -Vector3.Dot(n, v0)
                        };

                        LevelLists.planes.Add(plane);

                        portalCount += 1;

                        planeCount += 1;

                        portalNumber += 1;
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

                            int baseVert = temporaryVertices.Count;

                            int baseStarttemporaryTriangles = temporaryTriangles.Count;

                            temporaryVertices.Add(new Vector3((float)Z1, (float)F0, (float)X1));
                            temporaryVertices.Add(new Vector3((float)Z1, (float)Floor, (float)X1));
                            temporaryVertices.Add(new Vector3((float)Z0, (float)Floor, (float)X0));
                            temporaryVertices.Add(new Vector3((float)Z0, (float)F0, (float)X0));

                            temporaryTriangles.Add(baseVert);
                            temporaryTriangles.Add(baseVert + 1);
                            temporaryTriangles.Add(baseVert + 2);
                            temporaryTriangles.Add(baseVert);
                            temporaryTriangles.Add(baseVert + 2);
                            temporaryTriangles.Add(baseVert + 3);

                            Vector3 v0 = temporaryVertices[baseVert];
                            Vector3 v1 = temporaryVertices[baseVert + 1];
                            Vector3 v2 = temporaryVertices[baseVert + 2];

                            Vector3 n = Vector3.Cross(v1 - v0, v2 - v0).normalized;

                            Vector3 leftPlaneNormal = (v2 - v1).normalized;
                            float leftPlaneDistance = -Vector3.Dot(leftPlaneNormal, v1);

                            Vector3 topPlaneNormal = (v1 - v0).normalized;
                            float topPlaneDistance = -Vector3.Dot(topPlaneNormal, v1);

                            LeftPlane = new MathematicalPlane { normal = leftPlaneNormal, distance = leftPlaneDistance };
                            TopPlane = new MathematicalPlane { normal = topPlaneNormal, distance = topPlaneDistance };

                            temporaryTextures.Add(new Vector3(GetPlaneSignedDistanceToPoint(LeftPlane, temporaryVertices[baseVert]) / 2.5f, GetPlaneSignedDistanceToPoint(TopPlane, temporaryVertices[baseVert]) / 2.5f, 2));
                            temporaryTextures.Add(new Vector3(GetPlaneSignedDistanceToPoint(LeftPlane, temporaryVertices[baseVert + 1]) / 2.5f, GetPlaneSignedDistanceToPoint(TopPlane, temporaryVertices[baseVert + 1]) / 2.5f, 2));
                            temporaryTextures.Add(new Vector3(GetPlaneSignedDistanceToPoint(LeftPlane, temporaryVertices[baseVert + 2]) / 2.5f, GetPlaneSignedDistanceToPoint(TopPlane, temporaryVertices[baseVert + 2]) / 2.5f, 2));
                            temporaryTextures.Add(new Vector3(GetPlaneSignedDistanceToPoint(LeftPlane, temporaryVertices[baseVert + 3]) / 2.5f, GetPlaneSignedDistanceToPoint(TopPlane, temporaryVertices[baseVert + 3]) / 2.5f, 2));

                            temporaryNormals.Add(n);
                            temporaryNormals.Add(n);
                            temporaryNormals.Add(n);
                            temporaryNormals.Add(n);

                            MathematicalPlane plane = new MathematicalPlane
                            {
                                normal = n,
                                distance = -Vector3.Dot(n, v0)
                            };

                            LevelLists.planes.Add(plane);

                            planeCount += 1;
                        }
                        else
                        {
                            double F0 = sector.floorHeight / 8 * 2.5f;
                            double F1 = sector.ceilingHeight / 8 * 2.5f;

                            int baseVert = temporaryVertices.Count;

                            int baseStarttemporaryTriangles = temporaryTriangles.Count;

                            temporaryVertices.Add(new Vector3((float)Z1, (float)F0, (float)X1));
                            temporaryVertices.Add(new Vector3((float)Z1, (float)F1, (float)X1));
                            temporaryVertices.Add(new Vector3((float)Z0, (float)F1, (float)X0));
                            temporaryVertices.Add(new Vector3((float)Z0, (float)F0, (float)X0));

                            temporaryTriangles.Add(baseVert);
                            temporaryTriangles.Add(baseVert + 1);
                            temporaryTriangles.Add(baseVert + 2);
                            temporaryTriangles.Add(baseVert);
                            temporaryTriangles.Add(baseVert + 2);
                            temporaryTriangles.Add(baseVert + 3);

                            Vector3 v0 = temporaryVertices[baseVert];
                            Vector3 v1 = temporaryVertices[baseVert + 1];
                            Vector3 v2 = temporaryVertices[baseVert + 2];

                            Vector3 n = Vector3.Cross(v1 - v0, v2 - v0).normalized;

                            Vector3 leftPlaneNormal = (v2 - v1).normalized;
                            float leftPlaneDistance = -Vector3.Dot(leftPlaneNormal, v1);

                            Vector3 topPlaneNormal = (v1 - v0).normalized;
                            float topPlaneDistance = -Vector3.Dot(topPlaneNormal, v1);

                            LeftPlane = new MathematicalPlane { normal = leftPlaneNormal, distance = leftPlaneDistance };
                            TopPlane = new MathematicalPlane { normal = topPlaneNormal, distance = topPlaneDistance };

                            temporaryTextures.Add(new Vector3(GetPlaneSignedDistanceToPoint(LeftPlane, temporaryVertices[baseVert]) / 2.5f, GetPlaneSignedDistanceToPoint(TopPlane, temporaryVertices[baseVert]) / 2.5f, 2));
                            temporaryTextures.Add(new Vector3(GetPlaneSignedDistanceToPoint(LeftPlane, temporaryVertices[baseVert + 1]) / 2.5f, GetPlaneSignedDistanceToPoint(TopPlane, temporaryVertices[baseVert + 1]) / 2.5f, 2));
                            temporaryTextures.Add(new Vector3(GetPlaneSignedDistanceToPoint(LeftPlane, temporaryVertices[baseVert + 2]) / 2.5f, GetPlaneSignedDistanceToPoint(TopPlane, temporaryVertices[baseVert + 2]) / 2.5f, 2));
                            temporaryTextures.Add(new Vector3(GetPlaneSignedDistanceToPoint(LeftPlane, temporaryVertices[baseVert + 3]) / 2.5f, GetPlaneSignedDistanceToPoint(TopPlane, temporaryVertices[baseVert + 3]) / 2.5f, 2));

                            temporaryNormals.Add(n);
                            temporaryNormals.Add(n);
                            temporaryNormals.Add(n);
                            temporaryNormals.Add(n);

                            MathematicalPlane plane = new MathematicalPlane
                            {
                                normal = n,
                                distance = -Vector3.Dot(n, v0)
                            };

                            LevelLists.planes.Add(plane);

                            planeCount += 1;
                        }
                    }
                }
            }

            if (sector.floorHeight != sector.ceilingHeight)
            {
                floorVertices.Clear();
                ceilingVertices.Clear();
                floorTextures.Clear();
                ceilingTextures.Clear();

                float tinyNumber = 1e-6f;

                for (int e = 0; e < sector.vertexIndices.Count; ++e)
                {
                    double YF = sector.floorHeight / 8 * 2.5f;
                    double YC = sector.ceilingHeight / 8 * 2.5f;
                    double X = vertices[sector.vertexIndices[e]].x / 2 * 2.5f;
                    double Z = vertices[sector.vertexIndices[e]].y / 2 * 2.5f;

                    float OX = (float)X / 2.5f * -1;
                    float OY = (float)Z / 2.5f;

                    floorVertices.Add(new Vector3((float)Z, (float)YF, (float)X));
                    ceilingVertices.Add(new Vector3((float)Z, (float)YC, (float)X));
                    floorTextures.Add(new Vector3(OY, OX, 0));
                    ceilingTextures.Add(new Vector3(OY, OX, 1));
                }

                floorTriangles.Clear();

                for (int e = 0; e < floorVertices.Count - 2; e++)
                {
                    Vector3 v0 = floorVertices[0];
                    Vector3 v1 = floorVertices[e + 1];
                    Vector3 v2 = floorVertices[e + 2];

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

                    floorTriangles.Add(0);
                    floorTriangles.Add(e + 1);
                    floorTriangles.Add(e + 2);
                }

                ceilingVertices.Reverse();
                ceilingTextures.Reverse();

                ceilingTriangles.Clear();

                for (int e = 0; e < ceilingVertices.Count - 2; e++)
                {
                    Vector3 v0 = ceilingVertices[0];
                    Vector3 v1 = ceilingVertices[e + 1];
                    Vector3 v2 = ceilingVertices[e + 2];

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

                    ceilingTriangles.Add(0);
                    ceilingTriangles.Add(e + 1);
                    ceilingTriangles.Add(e + 2);
                }

                int baseFloor = temporaryVertices.Count;

                int floorStarttemporaryTriangles = temporaryTriangles.Count;

                for (int e = 0; e < floorVertices.Count; e++)
                {
                    temporaryVertices.Add(floorVertices[e]);
                }

                for (int e = 0; e < floorTextures.Count; e++)
                {
                    temporaryTextures.Add(floorTextures[e]);
                }

                for (int e = 0; e < floorTriangles.Count; e++)
                {
                    temporaryTriangles.Add(baseFloor + floorTriangles[e]);
                }

                Vector3 f0 = floorVertices[floorTriangles[0]];
                Vector3 f1 = floorVertices[floorTriangles[1]];
                Vector3 f2 = floorVertices[floorTriangles[2]];

                Vector3 f = Vector3.Cross(f1 - f0, f2 - f0).normalized;

                for (int e = 0; e < floorVertices.Count; e++)
                {
                    temporaryNormals.Add(f);
                }

                MathematicalPlane floorPlane = new MathematicalPlane
                {
                    normal = f,
                    distance = -Vector3.Dot(f, f0)
                };

                LevelLists.planes.Add(floorPlane);

                planeCount += 1;

                int baseCeiling = temporaryVertices.Count;

                int ceilingStartIndex = temporaryTriangles.Count;

                for (int e = 0; e < ceilingVertices.Count; e++)
                {
                    temporaryVertices.Add(ceilingVertices[e]);
                }

                for (int e = 0; e < ceilingTextures.Count; e++)
                {
                    temporaryTextures.Add(ceilingTextures[e]);
                }

                for (int e = 0; e < ceilingTriangles.Count; e++)
                {
                    temporaryTriangles.Add(baseCeiling + ceilingTriangles[e]);
                }

                Vector3 c0 = ceilingVertices[ceilingTriangles[0]];
                Vector3 c1 = ceilingVertices[ceilingTriangles[1]];
                Vector3 c2 = ceilingVertices[ceilingTriangles[2]];

                Vector3 c = Vector3.Cross(c1 - c0, c2 - c0).normalized;

                for (int e = 0; e < ceilingVertices.Count; e++)
                {
                    temporaryNormals.Add(c);
                }

                MathematicalPlane ceilingPlane = new MathematicalPlane
                {
                    normal = c,
                    distance = -Vector3.Dot(c, c0)
                };

                LevelLists.planes.Add(ceilingPlane);

                planeCount += 1;
            }

            SectorMeta sectorMeta = new SectorMeta
            {
                sectorId = i,
                rectangle = 0,
                portalStartIndex = portalStart,
                portalCount = portalCount,
                planeStartIndex = planeStart,
                planeCount = planeCount
            };

            LevelLists.sectors.Add(sectorMeta);

            Mesh renderMesh = new Mesh();

            renderMesh.SetVertices(temporaryVertices);

            renderMesh.SetUVs(0, temporaryTextures);

            renderMesh.SetNormals(temporaryNormals);

            renderMesh.SetTriangles(temporaryTriangles, 0);

            LevelLists.render.Add(renderMesh);

            Mesh collisionMesh = new Mesh();

            collisionMesh.SetVertices(temporaryVertices);

            collisionMesh.SetTriangles(temporaryTriangles, 0);

            GameObject meshObject = new GameObject("Collision " + i);

            MeshCollider meshCollider = meshObject.AddComponent<MeshCollider>();

            meshCollider.sharedMesh = collisionMesh;

            LevelLists.collision.Add(meshCollider);

            meshObject.transform.SetParent(CollisionObjects.transform);

            portalStart += portalCount;

            planeStart += planeCount;
        }

        Debug.Log("Level built successfully!");
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.XR;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
public class ControllerRaycaster : MonoBehaviour
{
    public enum MeshDensity : int
    {
        low = 32,
        medium = 64,
        high = 128,
        vHigh = 256,
        ultra = 512
    }

    // Enums for the resolution and scan size
    public MeshDensity meshDensity = MeshDensity.medium;
    public EnvironmentDepthAccess depthAccess;
    public Transform controllerTransform;
    [SerializeField] private TextMeshProUGUI _depthText;
    [SerializeField] private LineRenderer _lineRenderer;

    // Compute Buffers
    public ComputeShader computeShader;
    private ComputeBuffer vertexBuffer;
    private ComputeBuffer triangleBuffer;
    private ComputeBuffer positionBuffer;

    // List of cubes
    private List<GameObject> cubes = new List<GameObject>();
    public GameObject cubePrefab;
    public Transform cubeTransform;

    // Mesh Components
    Mesh mesh;
    MeshCollider collider;
    private int sampleSize = 64;

    // Camera settings
    [SerializeField] private Camera mainCamera;
    [SerializeField] private float fovMargin = 0.9f; // 90% of the field of view


    // Start is called before the first frame update
    void Start()
    {
        // Initialize the mesh components
        mesh = new Mesh();
        GetComponent<MeshFilter>().mesh = mesh;
        collider = GetComponent<MeshCollider>();

        // Display the initial mesh density
        meshDensity = MeshDensity.medium;
        _depthText.text = $"Mesh Density: {meshDensity} = {sampleSize} x {sampleSize} vertices\nTotal Points: {sampleSize * sampleSize}";
    }

    void Update()
    {
        // Change the mesh density based on the controller input
        if (OVRInput.GetDown(OVRInput.RawButton.RThumbstickRight) || OVRInput.GetDown(OVRInput.RawButton.RThumbstickUp))
        {
            meshDensity = meshDensity.Next();
            sampleSize = (int)meshDensity;
            sampleSize = (int)meshDensity;

            _depthText.text = $"Mesh Density: {meshDensity} = {sampleSize} x {sampleSize} vertices\nTotal Points: {sampleSize * sampleSize}";
        }

        if (OVRInput.GetDown(OVRInput.RawButton.RThumbstickLeft) || OVRInput.GetDown(OVRInput.RawButton.RThumbstickDown))
        {
            meshDensity = meshDensity.Last();
            sampleSize = (int)meshDensity;
            sampleSize = (int)meshDensity;

            _depthText.text = $"Mesh Density: {meshDensity} = {sampleSize} x {sampleSize} vertices\nTotal Points: {sampleSize * sampleSize}";
        }

        if (OVRInput.GetDown(OVRInput.RawButton.A))
        {
            GenerateMeshWithinBounds();
        }

        if (OVRInput.GetDown(OVRInput.RawButton.B))
        {
            GenerateMesh();
        }

        if (OVRInput.GetDown(OVRInput.RawButton.X))
        {
            SpawnCubesWithinBounds();
        }

        if (OVRInput.GetDown(OVRInput.RawButton.Y))
        {
            SpawnCubes();
        }

        if (OVRInput.GetDown(OVRInput.RawButton.RHandTrigger))
        {
            PerformControllerRaycast();
        }


        if (OVRInput.GetDown(OVRInput.RawButton.LHandTrigger))
        {
            ClearScene();
        }
    }

    List<Vector3> GeneratePointsWithinBounds()
    {
        // List to store the results of the raycast
        List<Vector3> confirmedPoints = new List<Vector3>();
        Vector3[] vertexPositions = GeneratePoints();

        // Calculate the bounds of the cube
        foreach (var point in vertexPositions)
        {
            Vector3 localPoint = cubeTransform.InverseTransformPoint(point);
            Bounds cubeBounds = new Bounds(Vector3.zero, cubeTransform.localScale);
            if (cubeBounds.Contains(localPoint))
            {
                confirmedPoints.Add(point);
            }
        }
        return confirmedPoints;
    }

    // FUNC: Spawn cubes within the bounds of the cube
    void SpawnCubesWithinBounds()
    {
        List<Vector3> confirmedPoints = GeneratePointsWithinBounds();
        foreach (var point in confirmedPoints)
        {
            cubes.Add(Instantiate(cubePrefab, point, Quaternion.LookRotation(point)));
        }
    }

    // FUNC: Generate a mesh within the bounds of the cube
    void GenerateMeshWithinBounds()
    {
        try
        {
            int vertexCount = (sampleSize + 1) * (sampleSize + 1);
            int triangleCount = sampleSize * sampleSize * 6;

            List<Vector3> confirmedPoints = GeneratePointsWithinBounds();
            Vector3[] vertexPositions = confirmedPoints.ToArray();

            _depthText.text = "Starting mesh generation on GPU...";

            // Reuse or create buffers
            CreateOrResizeBuffer<VertexData>(ref vertexBuffer, vertexCount, sizeof(float) * 6);
            CreateOrResizeBuffer<VertexData>(ref triangleBuffer, triangleCount, sizeof(int));
            CreateOrResizeBuffer<VertexData>(ref positionBuffer, vertexPositions.Length, sizeof(float) * 3);

            _depthText.text += "\nCreated/Resized GPU buffers for vertices, triangles, and positions";

            positionBuffer.SetData(vertexPositions);

            int kernel = computeShader.FindKernel("CSMain");
            computeShader.SetBuffer(kernel, "vertices", vertexBuffer);
            computeShader.SetBuffer(kernel, "triangles", triangleBuffer);
            computeShader.SetBuffer(kernel, "positions", positionBuffer);
            computeShader.SetInt("sampleSize", sampleSize);
            computeShader.SetInt("vertexCount", vertexCount);
            _depthText.text += "\nSet compute shader buffers and parameters";

            computeShader.Dispatch(kernel, Mathf.CeilToInt(vertexCount / 1024.0f), 1, 1);
            _depthText.text += "\nDispatched compute shader";

            VertexData[] vertexData = new VertexData[vertexCount];
            int[] triangleData = new int[triangleCount];
            vertexBuffer.GetData(vertexData);
            triangleBuffer.GetData(triangleData);
            _depthText.text += "\nRetrieved data from GPU buffers";

            Vector3[] vertices = new Vector3[vertexCount];
            Vector3[] normals = new Vector3[vertexCount];
            for (int i = 0; i < vertexCount; i++)
            {
                vertices[i] = vertexData[i].position;
                normals[i] = vertexData[i].normal;
            }
            _depthText.text += "\nAssigned positions and normals to vertices";

            mesh.Clear();
            mesh.vertices = vertices;
            mesh.triangles = triangleData;
            mesh.normals = normals;

            // Optimize the mesh
            mesh.Optimize();
            mesh.RecalculateBounds();

            _depthText.text += "\nSet and optimized vertices, triangles, and normals on the mesh";

            collider.sharedMesh = mesh;
            _depthText.text += "\nUpdated the mesh collider";

            _depthText.text += $"\nGenerated mesh with {vertexCount} vertices and {triangleCount} triangles";
        }
        catch (Exception e)
        {
            Debug.LogError($"Error in GenerateMeshWithinBounds: {e.Message}");
            _depthText.text = $"Error generating mesh: {e.Message}";
        }
    }

    Vector3[] GeneratePoints()
    {
        List<EnvironmentDepthAccess.DepthRaycastResult> results = raycastResults();
        Vector3[] vertexPositions = new Vector3[results.Count];
        for (int i = 0; i < results.Count; i++)
        {
            vertexPositions[i] = results[i].Position;
        }

        return vertexPositions;
    }

    // Returns a list of DepthRaycastResults from the depthAccess
    List<EnvironmentDepthAccess.DepthRaycastResult> raycastResults()
    {
        List<Vector2> viewSpaceCoords = GenerateViewSpaceCoords(sampleSize, sampleSize);
        List<EnvironmentDepthAccess.DepthRaycastResult> results;
        depthAccess.RaycastViewSpaceBlocking(viewSpaceCoords, out results);
        return results;
    }

    // FUNC: Generate a mesh on the GPU
    void GenerateMesh()
    {
        try
        {
            _depthText.text = "Calculating vertex and triangle count...";

            int vertexCount = (sampleSize + 1) * (sampleSize + 1);
            int triangleCount = sampleSize * sampleSize * 6;
            _depthText.text += $"\nCalculated vertex count: {vertexCount}, triangle count: {triangleCount}";

            Vector3[] vertexPositions = GeneratePoints();

            _depthText.text = "Starting mesh generation on GPU...";

            // Reuse or create buffers
            CreateOrResizeBuffer<VertexData>(ref vertexBuffer, vertexCount, sizeof(float) * 6);
            CreateOrResizeBuffer<VertexData>(ref triangleBuffer, triangleCount, sizeof(int));
            CreateOrResizeBuffer<VertexData>(ref positionBuffer, vertexPositions.Length, sizeof(float) * 3);

            _depthText.text += "\nCreated/Resized GPU buffers for vertices, triangles, and positions";

            positionBuffer.SetData(vertexPositions);

            int kernel = computeShader.FindKernel("CSMain");
            computeShader.SetBuffer(kernel, "vertices", vertexBuffer);
            computeShader.SetBuffer(kernel, "triangles", triangleBuffer);
            computeShader.SetBuffer(kernel, "positions", positionBuffer);
            computeShader.SetInt("sampleSize", sampleSize);
            computeShader.SetInt("vertexCount", vertexCount);
            _depthText.text += "\nSet compute shader buffers and parameters";

            computeShader.Dispatch(kernel, Mathf.CeilToInt(vertexCount / 1024.0f), 1, 1);
            _depthText.text += "\nDispatched compute shader";

            VertexData[] vertexData = new VertexData[vertexCount];
            int[] triangleData = new int[triangleCount];
            vertexBuffer.GetData(vertexData);
            triangleBuffer.GetData(triangleData);
            _depthText.text += "\nRetrieved data from GPU buffers";

            Vector3[] vertices = new Vector3[vertexCount];
            Vector3[] normals = new Vector3[vertexCount];
            for (int i = 0; i < vertexCount; i++)
            {
                vertices[i] = vertexData[i].position;
                normals[i] = vertexData[i].normal;
            }
            _depthText.text += "\nAssigned positions and normals to vertices";

            mesh.Clear();
            mesh.vertices = vertices;
            mesh.triangles = triangleData;
            mesh.normals = normals;

            // Optimize the mesh
            mesh.Optimize();
            mesh.RecalculateBounds();

            _depthText.text += "\nSet and optimized vertices, triangles, and normals on the mesh";

            collider.sharedMesh = mesh;
            _depthText.text += "\nUpdated the mesh collider";

            _depthText.text += $"\nGenerated mesh with {vertexCount} vertices and {triangleCount} triangles";
        }
        catch (Exception e)
        {
            Debug.LogError($"Error in GenerateMeshOnGPU: {e.Message}");
            _depthText.text = $"Error generating mesh: {e.Message}";
        }
    }

    struct VertexData
    {
        public Vector3 position;
        public Vector3 normal;
    }


    // FUNC: Perform a raycast using the controller position and forward direction
    // TODO: Implement the raycast using the compute shader
    void PerformControllerRaycast()
    {
        // Get controller position and forward direction
        Vector3 controllerPosition = controllerTransform.position;
        Vector3 raycastDirection = controllerTransform.forward;

        // Convert controller position to view space coordinates
        Vector2 viewSpaceCoord = Camera.main.WorldToViewportPoint(controllerPosition);

        // Perform the raycast using the compute shader
        EnvironmentDepthAccess.DepthRaycastResult result = depthAccess.RaycastViewSpaceBlocking(viewSpaceCoord);

        // Calculate the distance based on the position
        float distance = result.Position.z;

        // Calculate the intersection point based on depth
        Vector3 intersectionPoint = controllerPosition + raycastDirection * distance;

        // Display the result
        _depthText.text = $"Intersection: {intersectionPoint}";
        _depthText.text += $"\nDistance: {distance}";
        _depthText.text += $"\nPosition: {result.Position}";
        _depthText.text += $"\nNormal: {result.Normal}";

        // Draw a line from the controller to the intersection point
        _lineRenderer.SetPosition(0, controllerPosition);
        _lineRenderer.SetPosition(1, intersectionPoint);
    }

    private void CreateOrResizeBuffer<T>(ref ComputeBuffer buffer, int count, int stride)
    {
        if (buffer != null && buffer.count != count)
        {
            buffer.Release();
            buffer = null;
        }
        if (buffer == null)
        {
            buffer = new ComputeBuffer(count, stride);
        }
    }

    private void OnDisable()
    {
        ReleaseBuffers();
    }

    private void ReleaseBuffers()
    {
        if (vertexBuffer != null) vertexBuffer.Release();
        if (triangleBuffer != null) triangleBuffer.Release();
        if (positionBuffer != null) positionBuffer.Release();
        vertexBuffer = triangleBuffer = positionBuffer = null;
    }

    // FUNC: Clear the scene
    void ClearScene()
    {
        foreach (var cube in cubes)
        {
            Destroy(cube);
        }
        cubes.Clear();
        mesh.Clear();
    }

    // FUNC: Spawn cubes based on the depth raycast results
    void SpawnCubes()
    {
        List<Vector2> viewSpaceCoords = GenerateViewSpaceCoords(sampleSize, sampleSize);
        List<EnvironmentDepthAccess.DepthRaycastResult> results;
        depthAccess.RaycastViewSpaceBlocking(viewSpaceCoords, out results);

        foreach (var result in results)
        {
            cubes.Add(Instantiate(cubePrefab, result.Position, Quaternion.LookRotation(result.Position)));
        }
    }

    List<Vector2> GenerateViewSpaceCoords(int xSize, int zSize)
    {
        List<Vector2> coords = new List<Vector2>();

        // Get the camera's field of view and aspect ratio
        float fovY = mainCamera.fieldOfView * fovMargin;
        float fovX = Camera.VerticalToHorizontalFieldOfView(fovY, mainCamera.aspect) * fovMargin;

        // Calculate the dimensions of the view frustum at a distance of 1 unit
        float frustumHeight = 2.0f * Mathf.Tan(fovY * 0.5f * Mathf.Deg2Rad);
        float frustumWidth = 2.0f * Mathf.Tan(fovX * 0.5f * Mathf.Deg2Rad);

        // Calculate the step sizes
        float stepX = frustumWidth / (xSize - 1);
        float stepY = frustumHeight / (zSize - 1);

        // Generate coordinates
        for (int z = 0; z < zSize; z++)
        {
            for (int x = 0; x < xSize; x++)
            {
                // Calculate normalized device coordinates (NDC)
                float ndcX = (x * stepX - frustumWidth * 0.5f) / (frustumWidth * 0.5f);
                float ndcY = (z * stepY - frustumHeight * 0.5f) / (frustumHeight * 0.5f);

                // Convert NDC to view space coordinates
                float xCoord = (ndcX + 1) * 0.5f;
                float yCoord = (ndcY + 1) * 0.5f;

                coords.Add(new Vector2(xCoord, yCoord));
            }
        }

        return coords;
    }
}

// Extensions class to get the next and last enum values
public static class Extensions
{
    public static T Next<T>(this T src) where T : Enum
    {
        T[] Arr = (T[])Enum.GetValues(src.GetType());
        int j = Array.IndexOf<T>(Arr, src) + 1;
        return (j == Arr.Length) ? Arr[0] : Arr[j];
    }

    public static T Last<T>(this T src) where T : Enum
    {
        T[] Arr = (T[])Enum.GetValues(src.GetType());
        int j = Array.IndexOf<T>(Arr, src) - 1;
        return (j == -1) ? Arr[Arr.Length - 1] : Arr[j];
    }
}


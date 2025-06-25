using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]

public class RockGenerator : MonoBehaviour
{
    [Range(3, 15)] public int segments = 6;
    [Range(0, 100)] public float bottomRadius = 1f;
    [Range(0, 100)] public float topRadius = 0.3f;
    [Range(0, 100)] public float height = 2f;
    [Range(0, 25)] public float distortionStrength = 0.2f;
    [Range(0, 5)] public float topDistortionStrength = 0.2f;
    [Range(0, 30)] public float peakHeight = 0;

    [Range(1, 100)] public int additionalRocks = 1;
    [Range(0, 50)] public float additionalRocksSpacing = 1f;
    [Range(0.01f, 1f)] public float rocksFalloff = 0.01f;
    [Range(0f, 2f)] public float additionalRocksHeight = 1f;

    private MeshFilter meshFilter;
    private MeshCollider meshCollider;
    private Mesh mesh;

    private Vector3[] randomOffsets;
    private float[] randomScales;
    private float[] randomRotations;
    private int[] randomSegments;
    private int seed = 12345;

    public void RandomizeSeed()
    {
        seed = Random.Range(0, 999999);
        InitializeRandomData();
        GenerateRock();
    }

    private void OnValidate()
    {
        EnsureComponents();

        InitializeRandomData();
        GenerateRock();
    }

    private void EnsureComponents()
    {
        if (GetComponent<MeshFilter>() == null)
            meshFilter = gameObject.AddComponent<MeshFilter>();
        else
            meshFilter = GetComponent<MeshFilter>();

        if (GetComponent<MeshRenderer>() == null)
            gameObject.AddComponent<MeshRenderer>();

        if (GetComponent<MeshCollider>() == null)
            meshCollider = gameObject.AddComponent<MeshCollider>();
        else
            meshCollider = GetComponent<MeshCollider>();
    }

    private void InitializeRandomData()
    {
        Random.InitState(seed);

        randomOffsets = new Vector3[additionalRocks];
        randomScales = new float[additionalRocks];
        randomRotations = new float[additionalRocks];
        randomSegments = new int[additionalRocks];

        float maxOffset = additionalRocksSpacing * 2f;

        for (int i = 0; i < additionalRocks; i++)
        {
            float t = (additionalRocks > 1) ? i / (float)(additionalRocks - 1) : 0f;
            float scaleFactor = Mathf.Lerp(1f, rocksFalloff, t);

            float offsetDistance = Mathf.Lerp(0, maxOffset, t);
            Vector3 randomOffset = new Vector3(
                Random.Range(-offsetDistance, offsetDistance),
                0,
                Random.Range(-offsetDistance, offsetDistance)
            );

            float randomRotationY = Random.Range(0f, 360f);

            int segmentVariation = (segments >= 4) ? Random.Range(-1, 3) : 0;
            int modifiedSegments = Mathf.Max(3, segments + segmentVariation);

            randomOffsets[i] = randomOffset;
            randomScales[i] = scaleFactor;
            randomRotations[i] = randomRotationY;
            randomSegments[i] = modifiedSegments;
        }
    }

    private void GenerateRock()
    {
        EnsureComponents();

        if (mesh == null)
        {
            mesh = new Mesh();
            meshFilter.mesh = mesh;
        }
        else
        {
            mesh.Clear();
        }

        Vector3[] allVertices = new Vector3[0];
        int[] allTriangles = new int[0];

        for (int i = 0; i < additionalRocks; i++)
        {
            float heightMultiplier = (i == 0) ? 1f : additionalRocksHeight;

            Mesh singleMesh = GenerateSingleRock(
                randomScales[i],
                randomOffsets[i],
                randomRotations[i],
                randomSegments[i],
                heightMultiplier
            );

            allVertices = allVertices.Concat(singleMesh.vertices).ToArray();
            allTriangles = allTriangles.Concat(singleMesh.triangles.Select(t => t + allVertices.Length - singleMesh.vertices.Length)).ToArray();
        }

        mesh.vertices = allVertices;
        mesh.triangles = allTriangles;
        mesh.RecalculateNormals();

        if (meshCollider != null)
        {
            meshCollider.sharedMesh = null;  // Очистить старый meshCollider
            meshCollider.sharedMesh = mesh;  // Присвоить новый mesh
        }
    }

    private Mesh GenerateSingleRock(float scale, Vector3 positionOffset, float rotationY, int segments, float heightMultiplier)
    {
        Mesh rockMesh = new Mesh();

        int vertexCount = segments * 2 + 2;
        Vector3[] vertices = new Vector3[vertexCount];
        Vector2[] uvs = new Vector2[vertexCount];
        int[] triangles = new int[segments * 6 + segments * 3 + segments * 3];

        float angleStep = 2 * Mathf.PI / segments;
        Quaternion rotation = Quaternion.Euler(0, rotationY, 0);

        Vector3 tiltDirection = positionOffset.normalized;
        float tiltAngle = peakHeight * 1f;
        Quaternion tiltRotation = Quaternion.Euler(tiltAngle * tiltDirection.z, 0, -tiltAngle * tiltDirection.x);

        float h = height * scale * heightMultiplier;
        float crystalH = peakHeight * scale;
        float minHeight = 0;
        float maxHeight = h + crystalH;

        for (int i = 0; i < segments; i++)
        {
            float angle = i * angleStep;
            float randomOffset = Random.Range(-distortionStrength, distortionStrength);
            float adjustedRadius = (bottomRadius + randomOffset) * scale;
            float adjustedTopRadius = (topRadius + randomOffset * 0.5f) * scale;

            float x = Mathf.Cos(angle);
            float z = Mathf.Sin(angle);

            Vector3 baseVertex = new Vector3(x * adjustedRadius, 0, z * adjustedRadius);
            Vector3 topVertex = new Vector3(x * adjustedTopRadius, h, z * adjustedTopRadius);

            Vector3 topDistortion = Random.onUnitSphere * topDistortionStrength * scale;
            topDistortion.y = Mathf.Clamp(topDistortion.y, -0.3f, 0.5f);

            topVertex += topDistortion;

            baseVertex = tiltRotation * baseVertex;
            topVertex = tiltRotation * topVertex;

            baseVertex = rotation * baseVertex;
            topVertex = rotation * topVertex;

            vertices[i] = baseVertex + positionOffset;
            vertices[i + segments] = topVertex + positionOffset;

            float normalizedBaseHeight = (vertices[i].y - minHeight) / (maxHeight - minHeight);
            float normalizedTopHeight = (vertices[i + segments].y - minHeight) / (maxHeight - minHeight);

            uvs[i] = new Vector2(0, normalizedBaseHeight);
            uvs[i + segments] = new Vector2(0, normalizedTopHeight);
        }

        vertices[segments * 2] = rotation * (tiltRotation * Vector3.zero) + positionOffset;

        Vector3 centerTop = new Vector3(0, h + crystalH, 0);
        centerTop += Random.onUnitSphere * topDistortionStrength * scale;
        vertices[segments * 2 + 1] = rotation * (tiltRotation * centerTop) + positionOffset;

        uvs[segments * 2] = new Vector2(0, 0);
        uvs[segments * 2 + 1] = new Vector2(0, 1);

        int triIndex = 0;

        for (int i = 0; i < segments; i++)
        {
            int next = (i + 1) % segments;
            triangles[triIndex++] = i;
            triangles[triIndex++] = i + segments;
            triangles[triIndex++] = next;

            triangles[triIndex++] = next;
            triangles[triIndex++] = i + segments;
            triangles[triIndex++] = next + segments;
        }

        for (int i = 0; i < segments; i++)
        {
            int next = (i + 1) % segments;
            triangles[triIndex++] = segments * 2;
            triangles[triIndex++] = i;
            triangles[triIndex++] = next;
        }

        for (int i = 0; i < segments; i++)
        {
            int next = (i + 1) % segments;
            triangles[triIndex++] = segments * 2 + 1;
            triangles[triIndex++] = next + segments;
            triangles[triIndex++] = i + segments;
        }

        rockMesh.vertices = vertices;
        rockMesh.triangles = triangles;
        rockMesh.uv = uvs;
        rockMesh.RecalculateNormals();

        return rockMesh;
    }
}

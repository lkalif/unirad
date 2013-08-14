using UnityEngine;
using System;
using System.Collections;
using OM = OpenMetaverse;
using System.IO;
using System.Drawing;

public class RegionTerrain
{
    bool fetchingTerrainTexture = false;
    bool terrainTextureNeedsUpdate = false;
    Bitmap terrainImage = null;
    [NonSerialized]
    public bool Modified = true;
    bool terrainInProgress = false;
    float[,] heightTable = new float[256, 256];
    float terrainTimeSinceUpdate = 0f;
    OM.Simulator Sim;
    Instance Instance;
    Transform Region;
    int terrainSize = 256;
    int patchSizeX;
    int patchSizeY;
    Material material;

    GameObject[,] patches;

    public RegionTerrain(OM.Simulator sim, Transform region)
    {
        Sim = sim;
        Instance = Instance.Singleton();
        Region = region;
        var terrainObj = new GameObject("terrain_" + Sim.Handle.ToString());
        terrainObj.transform.parent = region;
        patchSizeX = terrainSize / 16 / 4;
        patchSizeY = terrainSize / 16 / 4;
        material = new Material(Shader.Find("Diffuse"));
        //patchSizeX = patchSizeY = 1;

        patches = new GameObject[patchSizeX, patchSizeY];
        for (int x = 0; x < patches.GetLength(0); x++)
        {
            for (int y = 0; y < patches.GetLength(1); y++)
            {
                patches[x, y] = new GameObject(string.Format("patch_{0}_{1}", x, y));
                patches[x, y].transform.position = new Vector3(x * 4f * 16f, 0, y * 4f * 16f);
                patches[x, y].transform.parent = terrainObj.transform;
            }
        }

    }

    public void Update()
    {
        terrainTimeSinceUpdate += Time.deltaTime;

        if (Modified && terrainTimeSinceUpdate > 10f)
        {
            if (!terrainInProgress)
            {
                terrainInProgress = true;
                ResetTerrain();
                UpdateTerrain();
            }
        }

        if (terrainTextureNeedsUpdate)
        {
            UpdateTerrainTexture();
        }

    }

    class PatchMesh
    {
        public Vector3[] vertices;
        public Vector3[] normals;
        public Vector2[] uvs;
        public int[] tris;
    }

    private void UpdateTerrain()
    {
        if (Sim == null || Sim.Terrain == null) return;
        Debug.Log("Updating terrain");
        OM.WorkPool.QueueUserWorkItem(sync =>
        {
            for (int x = 0; x < 256; x++)
            {
                for (int y = 0; y < 256; y++)
                {
                    float z = 0;
                    int patchNr = ((int)x / 16) * 16 + (int)y / 16;
                    if (Sim.Terrain[patchNr] != null
                        && Sim.Terrain[patchNr].Data != null)
                    {
                        float[] data = Sim.Terrain[patchNr].Data;
                        z = data[(int)x % 16 * 16 + (int)y % 16];
                    }
                    heightTable[x, y] = z;
                }
            }

            var patchMeshes = new PatchMesh[patchSizeX, patchSizeY];
            for (int patchY = 0; patchY < patchSizeY; patchY++)
            {
                for (int patchX = 0; patchX < patchSizeX; patchX++)
                {
                    int vertsX = 64 + 1;
                    int vertsY = 64 + 1;
                    Vector3[] vertices = new Vector3[vertsX * vertsY];
                    Vector3[] normals = new Vector3[vertsX * vertsY];
                    Vector2[] uvs = new Vector2[vertsX * vertsY];
                    int[] tris = new int[(vertsX - 1) * (vertsY - 1) * 2 * 3];

                    for (int y = 0; y < vertsY; y++)
                    {
                        for (int x = 0; x < vertsX; x++)
                        {
                            int i = x + y * vertsX;
                            int globalX = x + patchX * 64;
                            int globalY = y + patchY * 64;
                            globalX = Mathf.Clamp(globalX, 0, 255);
                            globalY = Mathf.Clamp(globalY, 0, 255);
                            float height = heightTable[globalY, globalX];

                            float u = (x + patchX * 64)/ 255f;
                            float v = (y + patchY * 64) / 255f;

                            vertices[i] = new Vector3(x, height, y);
                            normals[i] = Vector3.up;
                            uvs[i] = new Vector2(u, v);
                        }
                    }

                    for (int y = 0; y < vertsY - 1; y++)
                    {
                        for (int x = 0; x < vertsX - 1; x++)
                        {
                            int i = (x + y * (vertsX - 1)) * 6;

                            tris[i + 0] = x + y * vertsX;
                            tris[i + 1] = x + (y + 1) * vertsX;
                            tris[i + 2] = x + 1 + y * vertsX;

                            tris[i + 3] = x + (y + 1) * vertsX;
                            tris[i + 4] = x + 1 + (y + 1) * vertsX;
                            tris[i + 5] = x + 1 + y * vertsX;
                        }
                    }

                    patchMeshes[patchX, patchY] = new PatchMesh()
                    {
                        vertices = vertices,
                        normals = normals,
                        uvs = uvs,
                        tris = tris
                    };
                }
            }

            Loom.QueueOnMainThread(() =>
            {
                for (int y = 0; y < patchSizeY; y++)
                {
                    for (int x = 0; x < patchSizeX; x++)
                    {
                        Mesh terrainMesh = new Mesh();
                        var p = patchMeshes[x, y];
                        terrainMesh.vertices = p.vertices;
                        terrainMesh.normals = p.normals;
                        terrainMesh.uv = p.uvs;
                        terrainMesh.triangles = p.tris;
                        var patch = patches[x, y];
                        var mFilter = patch.GetComponent<MeshFilter>();
                        if (mFilter == null)
                        {
                            mFilter = patch.AddComponent<MeshFilter>();
                        }
                        var mCollider = patch.GetComponent<MeshCollider>();
                        if (mCollider == null)
                        {
                            mCollider = patch.AddComponent<MeshCollider>();
                        }
                        var mRenderer = patch.GetComponent<MeshRenderer>();
                        if (mRenderer == null)
                        {
                            mRenderer = patch.AddComponent<MeshRenderer>();
                            mRenderer.material = material;
                        }
                        mFilter.mesh = terrainMesh;
                        mFilter.mesh.RecalculateNormals();
                        mCollider.sharedMesh = terrainMesh;
                    }
                }
            });

            terrainInProgress = false;
            Modified = false;
            terrainTextureNeedsUpdate = true;
            terrainTimeSinceUpdate = 0f;
        });
    }

    void UpdateTerrainTexture()
    {
        if (!fetchingTerrainTexture)
        {
            fetchingTerrainTexture = true;
            OM.WorkPool.QueueUserWorkItem(sync =>
            {
                terrainImage = TerrainSplat.Splat(Instance.Client, heightTable,
                    new OM.UUID[] { Sim.TerrainDetail0, Sim.TerrainDetail1, Sim.TerrainDetail2, Sim.TerrainDetail3 },
                    new float[] { Sim.TerrainStartHeight00, Sim.TerrainStartHeight01, Sim.TerrainStartHeight10, Sim.TerrainStartHeight11 },
                    new float[] { Sim.TerrainHeightRange00, Sim.TerrainHeightRange01, Sim.TerrainHeightRange10, Sim.TerrainHeightRange11 });

                byte[] imageData = null;
                using (MemoryStream ms = new MemoryStream())
                {
                    terrainImage.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                    ms.Seek(0, SeekOrigin.Begin);
                    imageData = ms.ToArray();
                }

                Loom.QueueOnMainThread(() =>
                {
                    Texture2D te = new Texture2D(terrainImage.Width, terrainImage.Height);
                    te.LoadImage(imageData);
                    material.mainTexture = te;
                });
                fetchingTerrainTexture = false;
                terrainTextureNeedsUpdate = false;
            });
        }
    }

    public void ResetTerrain()
    {
        if (terrainImage != null)
        {
            terrainImage.Dispose();
            terrainImage = null;
        }

        fetchingTerrainTexture = false;
        Modified = true;
    }
}

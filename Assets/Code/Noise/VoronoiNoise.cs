using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public static class VoronoiNoise
{
    private const int QuadtreeSubdivisionThreshold = 10;
    private const float QuadtreeOverlap = 0f;

    public static float[,] GenerateNoiseMap(
        int width,
        int height,
        int numRegions,
        int randomSeed,
        int smoothingRadius,
        float[,] weightsMap = null)
    {
        UnityEngine.Random.InitState(randomSeed);

        // Generate seed regions
        RegionData[] regions = (weightsMap == null)
            ? GenerateRandomRegions(width, height, numRegions)
            : GenerateRandomRegions(width, height, numRegions, weightsMap);

        int totalPixels = width * height;
        var seedPositions = new NativeArray<float2>(numRegions, Allocator.TempJob);
        var regionMap1D = new NativeArray<int>(totalPixels, Allocator.TempJob);

        // Copy seed positions to native array
        for (int i = 0; i < numRegions; i++)
            seedPositions[i] = new float2(regions[i].position.x, regions[i].position.y);

        // Schedule region assignment job (parallel nearest seed)
        var sw = Stopwatch.StartNew();
        var regionJob = new CalculateRegionsJob
        {
            width = width,
            height = height,
            seedPositions = seedPositions,
            regionMap = regionMap1D
        };
        JobHandle handle = regionJob.Schedule(totalPixels, 256);
        handle.Complete();
        sw.Stop();
#if UNITY_EDITOR
        UnityEngine.Debug.Log($"[Voronoi] Regions in {sw.ElapsedMilliseconds} ms (parallel)");
#endif

        // Copy back to managed arrays
        int[,] regionMap = new int[width, height];
        for (int idx = 0; idx < totalPixels; idx++)
        {
            int x = idx % width;
            int y = idx / width;
            regionMap[x, y] = regionMap1D[idx];
        }

        // Compute maxDistances per region on main thread
        //foreach (var region in regions) region.maxDistance = 0f; // reset
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int i = regionMap[x, y];
                float dx = x - regions[i].position.x;
                float dy = y - regions[i].position.y;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d > regions[i].maxDistance)
                    regions[i].maxDistance = d;
            }

        // Dispose native arrays
        seedPositions.Dispose();
        regionMap1D.Dispose();

        // Recenter and subsequent steps
        sw.Restart();
        regions = RecalculateRegions(width, height, regions, regionMap);
        sw.Stop();
#if UNITY_EDITOR
        UnityEngine.Debug.Log($"[Voronoi] Recenter in {sw.ElapsedMilliseconds} ms");
#endif

        sw.Restart();
        float[,] gradients = CalculateGradients(width, height, regionMap, regions);
        sw.Stop();
#if UNITY_EDITOR
        UnityEngine.Debug.Log($"[Voronoi] Gradients in {sw.ElapsedMilliseconds} ms");
#endif

        sw.Restart();
        float[,] smoothed = SmoothWithIntegral(gradients, width, height, smoothingRadius);
        sw.Stop();
#if UNITY_EDITOR
        UnityEngine.Debug.Log($"[Voronoi] Smoothed in {sw.ElapsedMilliseconds} ms");
#endif

        return smoothed;
    }

    #region Region Assignment Job

    [BurstCompile]
    private struct CalculateRegionsJob : IJobParallelFor
    {
        public int width;
        public int height;
        [ReadOnly] public NativeArray<float2> seedPositions;
        public NativeArray<int> regionMap;

        public void Execute(int idx)
        {
            int x = idx % width;
            int y = idx / width;
            float2 p = new float2(x, y);

            int bestIndex = 0;
            float bestDistSqr = math.distancesq(p, seedPositions[0]);
            for (int i = 1; i < seedPositions.Length; i++)
            {
                float d2 = math.distancesq(p, seedPositions[i]);
                if (d2 < bestDistSqr)
                {
                    bestDistSqr = d2;
                    bestIndex = i;
                }
            }
            regionMap[idx] = bestIndex;
        }
    }

    #endregion

    #region Gradient Calculation

    private static float[,] CalculateGradients(int w, int h, int[,] map, RegionData[] regs)
    {
        float[,] grad = new float[w, h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = map[x, y];
                var r = regs[i];
                float maxD = r.maxDistance > 0f ? r.maxDistance : 1f;
                float dx = x - r.position.x;
                float dy = y - r.position.y;
                float d2 = dx * dx + dy * dy;
                float normalized = Mathf.Sqrt(d2) / maxD;
                grad[x, y] = r.isUnderwater
                    ? 0.5f * normalized
                    : 0.5f + 0.25f * (1f - normalized);
            }
        return grad;
    }

    #endregion

    #region Edge Smoothing

    private static float[,] SmoothWithIntegral(float[,] src, int w, int h, int r)
    {
        int iw = w + 1, ih = h + 1;
        var integral = new float[iw, ih];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                integral[x + 1, y + 1] = src[x, y] + integral[x, y + 1] + integral[x + 1, y] - integral[x, y];

        var outm = new float[w, h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int x0 = Math.Max(0, x - r), y0 = Math.Max(0, y - r);
                int x1 = Math.Min(w - 1, x + r), y1 = Math.Min(h - 1, y + r);
                int area = (x1 - x0 + 1) * (y1 - y0 + 1);
                float sum = integral[x1 + 1, y1 + 1] - integral[x0, y1 + 1] - integral[x1 + 1, y0] + integral[x0, y0];
                outm[x, y] = sum / area;
            }
        return outm;
    }

    private static float AverageValue(int width, int height, float[,] map, int cx, int cy, int r)
    {
        float sum = 0f;
        int count = 0;

        for (int dy = -r; dy <= r; dy++)
        {
            int y = cy + dy;
            if (y < 0 || y >= height) continue;

            for (int dx = -r; dx <= r; dx++)
            {
                int x = cx + dx;
                if (x < 0 || x >= width) continue;

                sum += map[x, y];
                count++;
            }
        }

        return (count > 0) ? (sum / count) : map[cx, cy];
    }

    #endregion

    #region Random Region Generation

    private static RegionData[] GenerateRandomRegions(int width, int height, int numPoints)
    {
        var regions = new RegionData[numPoints];
        var center = new Vector2(width / 2f, height / 2f);

        for (int i = 0; i < numPoints; i++)
        {
            var pos = new Vector2(UnityEngine.Random.Range(0f, width), UnityEngine.Random.Range(0f, height));
            bool underwater = Vector2.Distance(pos, center) > Mathf.Max(width, height) / 2f;
            regions[i] = new RegionData(pos, 0f, underwater);
        }

        return regions;
    }

    private static RegionData[] GenerateRandomRegions(int width, int height, int numPoints, float[,] weightsMap)
    {
        var regions = new RegionData[numPoints];
        var center = new Vector2(width / 2f, height / 2f);

        float totalWeight = weightsMap.Cast<float>().Sum();

        for (int i = 0; i < numPoints; i++)
        {
            regions[i] = new RegionData(GetRandomWeightedPoint(weightsMap, totalWeight), 0f,
                Vector2.Distance(regions[i].position, center) > Mathf.Max(width, height) / 3f);
        }

        return regions;
    }

    private static Vector2 GetRandomWeightedPoint(float[,] weights, float totalWeight)
    {
        float r = UnityEngine.Random.Range(0f, totalWeight);
        float cum = 0f;

        int maxX = weights.GetLength(0);
        int maxY = weights.GetLength(1);

        for (int x = 0; x < maxX; x++)
        {
            for (int y = 0; y < maxY; y++)
            {
                cum += weights[x, y];
                if (r <= cum)
                    return new Vector2(x, y);
            }
        }
        return Vector2.zero;
    }

    #endregion

    #region Region Recentering

    private static RegionData[] RecalculateRegions(int width, int height, RegionData[] regions, int[,] regionMap)
    {
        int num = regions.Length;
        var cells = new List<Vector2>[num];
        for (int i = 0; i < num; i++) cells[i] = new List<Vector2>();

        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                cells[regionMap[x, y]].Add(new Vector2(x, y));

        for (int i = 0; i < num; i++)
        {
            var pts = cells[i];
            if (pts.Count == 0) continue;

            // compute centroid
            float cx = pts.Average(p => p.x);
            float cy = pts.Average(p => p.y);
            var center = new Vector2(cx, cy);

            // recompute max distance
            float maxD = pts.Max(p => Vector2.Distance(p, center));
            regions[i] = new RegionData(center, maxD, regions[i].isUnderwater);
        }

        return regions;
    }

    #endregion
}


public class Quadtree
{
    private readonly Node root;
    private readonly int QuadtreeSubdivisionThreshold;

    public Quadtree(Rect bounds, int quadtreeSubdivisionThreshold)
    {
        root = new Node(bounds);
        QuadtreeSubdivisionThreshold = quadtreeSubdivisionThreshold;
    }

    public void Insert(Vector2 point, int regionIndex)
    {
        root.Insert(new PointIndex(point, regionIndex), QuadtreeSubdivisionThreshold);
    }

    public int FindNearestRegion(Vector2 queryPoint)
    {
        var found = root.FindNearest(queryPoint, float.MaxValue);
        if (found.HasValue)
            return found.Value.regionIndex;
        throw new Exception("No nearest point found in quadtree.");
    }

    private struct PointIndex
    {
        public Vector2 pos; public int regionIndex;
        public PointIndex(Vector2 p, int i) { pos = p; regionIndex = i; }
    }

    private class Node
    {
        public Rect bounds;
        public List<PointIndex> points = new List<PointIndex>();
        public Node[] children;

        public Node(Rect b) => bounds = b;

        public void Subdivide()
        {
            float hx = bounds.width / 2f;
            float hy = bounds.height / 2f;
            children = new Node[4];
            children[0] = new Node(new Rect(bounds.x, bounds.y, hx, hy));
            children[1] = new Node(new Rect(bounds.x + hx, bounds.y, hx, hy));
            children[2] = new Node(new Rect(bounds.x, bounds.y + hy, hx, hy));
            children[3] = new Node(new Rect(bounds.x + hx, bounds.y + hy, hx, hy));
        }

        public void Insert(PointIndex pi, int quadtreeSubdivisionThreshold)
        {
            if (children != null)
            {
                int idx = (pi.pos.x >= bounds.x + bounds.width / 2f ? 1 : 0) +
                          (pi.pos.y >= bounds.y + bounds.height / 2f ? 2 : 0);
                children[idx].Insert(pi, quadtreeSubdivisionThreshold);
            }
            else
            {
                points.Add(pi);
                if (points.Count > 1 && bounds.width > quadtreeSubdivisionThreshold)
                {
                    Subdivide();
                    foreach (var p in points)
                        Insert(p, quadtreeSubdivisionThreshold);
                    points.Clear();
                }
            }
        }

        public PointIndex? FindNearest(Vector2 queryPoint, float bestDist)
        {
            PointIndex? best = null;
            float bestSqr = bestDist * bestDist;

            // search children first if exist
            if (children != null)
            {
                foreach (var c in children)
                {
                    if (c == null) continue;
                    float sq = SquaredDistanceToRect(c.bounds, queryPoint);
                    if (sq < bestSqr)
                    {
                        var cand = c.FindNearest(queryPoint, Mathf.Sqrt(bestSqr));
                        if (cand.HasValue)
                        {
                            float candDist = Vector2.SqrMagnitude(cand.Value.pos - queryPoint);
                            if (candDist < bestSqr)
                            {
                                bestSqr = candDist;
                                best = cand;
                            }
                        }
                    }
                }
            }
            else
            {
                foreach (var p in points)
                {
                    float d2 = Vector2.SqrMagnitude(p.pos - queryPoint);
                    if (d2 < bestSqr)
                    {
                        bestSqr = d2;
                        best = p;
                    }
                }
            }

            return best;
        }

        public static float SquaredDistanceToRect(Rect rect, Vector2 pt)
        {
            float dx = Math.Max(Math.Max(rect.xMin - pt.x, 0f), pt.x - rect.xMax);
            float dy = Math.Max(Math.Max(rect.yMin - pt.y, 0f), pt.y - rect.yMax);
            return dx * dx + dy * dy;
        }
    }
}

public struct RegionData
{
    public Vector2 position;
    public float maxDistance;
    public bool isUnderwater;

    public RegionData(Vector2 pos, float maxDist, bool underwater)
    {
        position = pos;
        maxDistance = maxDist;
        isUnderwater = underwater;
    }
}

public struct Cell
{
    public List<Vector2> points;
}
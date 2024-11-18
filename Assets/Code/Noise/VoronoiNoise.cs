using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Unity.Burst;
using UnityEngine;

public static class VoronoiNoise
{
    public static float[,] GenerateNoiseMap(int width, int height, int numRegions, int randomSeed, int smoothingRadius)
    {
        float[,] noiseMap = new float[width, height];

        UnityEngine.Random.InitState(randomSeed);
        RegionData[] regions = GenerateRandomRegions(width,height,numRegions);

        Quadtree quadtree = new Quadtree(new Rect(0, 0, width, height));

        foreach (RegionData region in regions)
        {
            quadtree.Insert(region.position);
        }

        Stopwatch sw = Stopwatch.StartNew();
        sw.Start();
        noiseMap = CalculateRegions(width, height, quadtree, regions);
        sw.Stop();
        UnityEngine.Debug.Log($"Voronoi noise regions calculation: {sw.ElapsedMilliseconds} ms");

        sw.Restart();
        regions = RecalculateRegions(width,height,regions,noiseMap);
        sw.Stop();
        UnityEngine.Debug.Log($"Voronoi noise regions recalculation: {sw.ElapsedMilliseconds} ms");

        sw.Restart();
        noiseMap = CalculateGradients(width, height, noiseMap, regions);
        sw.Stop();
        UnityEngine.Debug.Log($"Voronoi noise gradients calculation: {sw.ElapsedMilliseconds} ms");

        sw.Restart();
        noiseMap = SmoothEdges(width, height, noiseMap, smoothingRadius);
        sw.Stop();
        UnityEngine.Debug.Log($"Voronoi noiseedges smoothing: {sw.ElapsedMilliseconds} ms");

        return noiseMap;
    }

    public static float[,] GenerateNoiseMap(int width, int height, float[,] weightsMap, int numRegions, int randomSeed, int smoothingRadius)
    {
        float[,] noiseMap = new float[width, height];

        UnityEngine.Random.InitState(randomSeed);
        RegionData[] regions = GenerateRandomRegions(width, height, numRegions, weightsMap);

        Quadtree quadtree = new Quadtree(new Rect(0, 0, width, height));

        foreach (RegionData region in regions)
        {
            quadtree.Insert(region.position);
        }

        Stopwatch sw = Stopwatch.StartNew();
        sw.Start();
        noiseMap = CalculateRegions(width, height, quadtree, regions);
        sw.Stop();
        UnityEngine.Debug.Log($"Voronoi noise regions calculation: {sw.ElapsedMilliseconds} ms");

        sw.Restart();
        regions = RecalculateRegions(width, height, regions, noiseMap);
        sw.Stop();
        UnityEngine.Debug.Log($"Voronoi noise regions recalculation: {sw.ElapsedMilliseconds} ms");

        sw.Restart();
        noiseMap = CalculateGradients(width, height, noiseMap, regions);
        sw.Stop();
        UnityEngine.Debug.Log($"Voronoi noise gradients calculation: {sw.ElapsedMilliseconds} ms");

        sw.Restart();
        noiseMap = SmoothEdges(width, height, noiseMap, smoothingRadius);
        sw.Stop();
        UnityEngine.Debug.Log($"Voronoi noiseedges smoothing: {sw.ElapsedMilliseconds} ms");

        return noiseMap;
    }

    private static float[,] CalculateRegions(int width, int height, Quadtree quadtree, RegionData[] regions)
    {
        float[,] regionsMap = new float[width, height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Vector2 queryPoint = new Vector2(x, y);
                Vector2 nearestSeedPoint = quadtree.FindNearest(queryPoint);
                int closestSeedIndex = FindIndex(regions, nearestSeedPoint);

                float distanceToPoint = Vector2.Distance(queryPoint, nearestSeedPoint);
                regions[closestSeedIndex].maxDistance = Mathf.Max(distanceToPoint, regions[closestSeedIndex].maxDistance);
                regionsMap[x, y] = closestSeedIndex;
            }
        }

        return regionsMap;
    }

    private static float[,] CalculateGradients(int width, int height, float[,] regionMap, RegionData[] regions)
    {
        float[,] gradientMap = new float[width, height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int closestSeedIndex = (int)regionMap[x, y];

                if (closestSeedIndex != -1)
                {
                    Vector2 queryPoint = new Vector2(x, y);
                    Vector2 nearestSeedPoint = regions[closestSeedIndex].position;
                    float distance = Vector2.Distance(queryPoint, nearestSeedPoint);

                    //float gradientValue = (distance / regions[closestSeedIndex].maxDistance) / 2 * (regions[closestSeedIndex].isUnderwater ? 1 : -1);
                    //float gradientValue = (distance / regions[closestSeedIndex].maxDistance) / 2 * (regions[closestSeedIndex].isUnderwater ? 1 : -1);
                    float gradientValue = 0;
                    if (regions[closestSeedIndex].isUnderwater)
                    {
                        gradientValue = 0.5f * (distance / regions[closestSeedIndex].maxDistance);
                    }
                    else
                    {
                        gradientValue = 0.5f * (1 - (distance / regions[closestSeedIndex].maxDistance)) / 2;
                    }
                    gradientMap[x, y] = (regions[closestSeedIndex].isUnderwater ? 0 : 0.5f) + gradientValue;
                }
                else
                {
                    gradientMap[x, y] = 1;
                    UnityEngine.Debug.LogWarning($"No valid seed point found for query point ({x}, {y}). Setting pixel to fallback color.");
                }
            }
        }

        return gradientMap;
    }

    private static float[,] SmoothEdges(int width, int height, float[,] noiseMap, int smoothingRadius)
    {
        float[,] smoothedNoise = new float[width, height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                smoothedNoise[x,y] = AverageValue(width, height, noiseMap, x, y, smoothingRadius);
            }
        }

        return smoothedNoise;
    }

    private static float AverageValue(int width, int height, float[,] noiseMap, int posX, int posY, int smoothingRadius)
    {
        float averageValue = 0;
        float numPixels = 0;
        for(int y = posY - smoothingRadius; y < posY + smoothingRadius; y++)
        {
            for (int x = posX - smoothingRadius; x < posX + smoothingRadius; x++)
            {
                if(x < width && y < height && x > 0 && y > 0)
                {
                    averageValue += noiseMap[x, y];
                    numPixels++;
                }
            }
        }
        averageValue = averageValue / numPixels;
        return averageValue;
    }

    private static RegionData[] GenerateRandomRegions(int width, int height, int numPoints)
    {
        RegionData[] regions = new RegionData[numPoints];

        for (int i = 0; i < numPoints; i++)
        {
            Vector2 randomPosition = new Vector2(UnityEngine.Random.Range(0, width), UnityEngine.Random.Range(0, height));
            float distanceToCenter = Vector2.Distance(new Vector2(width / 2,height / 2), randomPosition);
            //bool isUnderwater = i % 2 == 0;
            bool isUnderwater = (distanceToCenter > (width / 2)) && (distanceToCenter > (height / 2));
            RegionData newRegion = new RegionData(randomPosition, float.MinValue, isUnderwater);
            regions[i] = newRegion;
        }

        return regions;
    }

    private static RegionData[] GenerateRandomRegions(int width, int height, int numPoints, float[,] weightsMap)
    {
        RegionData[] regions = new RegionData[numPoints];

        for (int i = 0; i < numPoints; i++)
        {
            Vector2 randomPosition = GetRandomWeightedPoint(weightsMap);
            float distanceToCenter = Vector2.Distance(new Vector2(width / 2, height / 2), randomPosition);
            //bool isUnderwater = i % 2 == 0;
            bool isUnderwater = (distanceToCenter > (width / 3)) && (distanceToCenter > (height / 3));
            RegionData newRegion = new RegionData(randomPosition, float.MinValue, isUnderwater);
            regions[i] = newRegion;
        }

        return regions;
    }

    private static Vector2 GetRandomWeightedPoint(float[,] weightsMap)
    {
        float totalWeight = 0.0f;
        foreach (float f in weightsMap)
        {
            totalWeight += f;
        }

        float rand = UnityEngine.Random.Range(0, totalWeight);
        float cumulativeWeight = 0.0f;

        for(int y = 0; y < weightsMap.GetLength(0); y++)
        {
            for(int x = 0; x < weightsMap.GetLength(1); x++)
            {
                cumulativeWeight += weightsMap[x, y];
                if (rand <= cumulativeWeight)
                    return new Vector2(x, y);
            }
        }

        return Vector2Int.zero;
    }

    private static RegionData[] RecalculateRegions(int width, int height, RegionData[] regions, float[,] regionsMap)
    {
        Cell[] cells = new Cell[regions.Length];
        for (int i = 0; i < regions.Length; i++)
        {
            cells[i] = new Cell();
            cells[i].points = new List<Vector2>();
        }

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int id = (int)regionsMap[x,y];
                cells[id].points.Add(new Vector2(x, y));
            }
        }

        for (int i = 0; i < regions.Length; i++)
        {
            float centerX = cells[i].points.Average(p => p.x);
            float centerY = cells[i].points.Average(p => p.y);
            regions[i].position = new Vector2(centerX, centerY);

            float maxDistance = float.MinValue;
            foreach (Vector2 point in cells[i].points)
            {
                float distance = Vector2.Distance(point, regions[i].position);
                maxDistance = Math.Max(maxDistance, distance);
            }
            regions[i].maxDistance = maxDistance;
        }
        return regions;
    }

    private static int FindIndex(RegionData[] regionData, Vector2 position)
    {
        int index = -1;
        for (int i = 0; i < regionData.Length; i++)
        {
            if (regionData[i].position == position)
                return i;
        }
        return index;
    }
}

public class Quadtree
{
    private Node root;

    public Quadtree(Rect bounds)
    {
        root = new Node(bounds);
    }

    public void Insert(Vector2 point)
    {
        root.Insert(point);
    }

    public Vector2 FindNearest(Vector2 queryPoint)
    {
        var nearest = root.FindNearest(queryPoint, float.MaxValue);
        if (nearest != null)
        {
            return nearest.Value;
        }
        throw new Exception("No nearest point found, even after backtracking.");
    }

    private class Node
    {
        public Rect bounds;
        public List<Vector2> points = new List<Vector2>();
        public Node[] children;

        public Node(Rect bounds)
        {
            this.bounds = bounds;
        }

        public void Subdivide()
        {
            float halfWidth = bounds.width / 2;
            float halfHeight = bounds.height / 2;
            float overlap = 0.01f;  // Small overlap to prevent edge issues
            children = new Node[4];
            children[0] = new Node(new Rect(bounds.x - overlap, bounds.y - overlap, halfWidth + overlap, halfHeight + overlap));
            children[1] = new Node(new Rect(bounds.x + halfWidth, bounds.y - overlap, halfWidth + overlap, halfHeight + overlap));
            children[2] = new Node(new Rect(bounds.x - overlap, bounds.y + halfHeight, halfWidth + overlap, halfHeight + overlap));
            children[3] = new Node(new Rect(bounds.x + halfWidth, bounds.y + halfHeight, halfWidth + overlap, halfHeight + overlap));
        }

        public void Insert(Vector2 point)
        {
            if (children != null)
            {
                // Determine which child node the point should go into
                int index = (point.x >= bounds.x + bounds.width / 2 ? 1 : 0) +
                            (point.y >= bounds.y + bounds.height / 2 ? 2 : 0);
                children[index].Insert(point);
            }
            else
            {
                points.Add(point);
                if (points.Count > 1 && bounds.width > 10)  // Threshold to prevent over subdivision
                {
                    Subdivide();
                    foreach (var p in points)
                    {
                        Insert(p);
                    }
                    points.Clear();
                }
            }
        }

        public Vector2? FindNearest(Vector2 queryPoint, float minDist)
        {
            Vector2? nearest = null;
            float nearestDist = minDist;

            if (children != null)
            {
                foreach (var child in children)
                {
                    if (child != null && SquaredDistanceToRect(child.bounds, queryPoint) < nearestDist * nearestDist)
                    {
                        Vector2? candidate = child.FindNearest(queryPoint, nearestDist);
                        if (candidate.HasValue)
                        {
                            float dist = Vector2.Distance(queryPoint, candidate.Value);
                            if (dist < nearestDist)
                            {
                                nearest = candidate;
                                nearestDist = dist;
                            }
                        }
                    }
                }
            }
            else
            {
                foreach (var point in points)
                {
                    float dist = Vector2.Distance(queryPoint, point);
                    if (dist < nearestDist)
                    {
                        nearest = point;
                        nearestDist = dist;
                    }
                }
            }

            return nearest;
        }

        public static float SquaredDistanceToRect(Rect rect, Vector2 point)
        {
            float dx = Mathf.Max(rect.xMin - point.x, 0, point.x - rect.xMax);
            float dy = Mathf.Max(rect.yMin - point.y, 0, point.y - rect.yMax);
            return dx * dx + dy * dy;
        }
    }
}

public struct RegionData
{
    public Vector2 position;
    public float maxDistance;
    public bool isUnderwater;

    public RegionData(Vector2 position, float maxDistance, bool isUnderwater)
    {
        this.position = position;
        this.maxDistance = maxDistance;
        this.isUnderwater = isUnderwater;
    }
}

public struct Cell
{
    public List<Vector2> points;
}
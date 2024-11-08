using System.Drawing;
using Unity.Burst;
using UnityEngine;

public static class RiverBuilder
{
    private static Vector2Int _worldSize;
    public static float[,] GenerateRivers(Vector2Int worldSize, RiverGenerationParameters riverParameters, float[,] heightMap, float[,] moistureMap)
    {
        _worldSize = worldSize;
        UnityEngine.Random.InitState(0);

        float[,] attractivnesMap = GenerateAttractivnesMap(heightMap, moistureMap);
        River[] rivers = GenerateRiversStartPoints(riverParameters, heightMap, attractivnesMap);

        float[,] riversMap = RunRivers(rivers, attractivnesMap);

        return riversMap;
        //return attractivnesMap;
    }

    private static float[,] GenerateAttractivnesMap( float[,] heightMap, float[,] moistureMap)
    {
        float[,] attractivnesMap = new float[_worldSize.x, _worldSize.y];
        for (int y = 0; y < _worldSize.y; y++)
        {
            for (int x = 0; x < _worldSize.x; x++)
            {
                attractivnesMap[x, y] = heightMap[x, y] * 0.8f + moistureMap[x, y] * 0.2f;
            }
        }

        return attractivnesMap;
    }

    private static River[] GenerateRiversStartPoints(RiverGenerationParameters riverParameters, float[,] heightMap, float[,] attractivnesMap)
    {
        River[] rivers = new River[riverParameters.numRivers];

        for (int i = 0; i < riverParameters.numRivers; i++)
        {
            Vector2Int randomPoint = TryGeneratePoint(heightMap);
            Vector2Int direction = CalculateDirection(randomPoint, riverParameters.searchRadius, attractivnesMap);

            float randomValue = Random.Range(0.0f, 1.0f);
            int randomRiverLength = Mathf.RoundToInt(riverParameters.lengthDistribution.Evaluate(randomValue));
            int randomRiverSize = Mathf.RoundToInt(riverParameters.sizeDistribution.Evaluate(randomValue));

            River newRiver = new River(i, randomPoint, direction, randomRiverLength, randomRiverSize, riverParameters.radiusDecayChance);
            rivers[i] = newRiver;
        }
        return rivers;
    }

    private static Vector2Int TryGeneratePoint(float[,] heightMap)
    {
        Vector2Int randomPoint = Vector2Int.zero;
        int maxFailures = 1000;

        for (int i = 0; i <= maxFailures; i++)
        {
            int x = Random.Range(0, _worldSize.x);
            int y = Random.Range(0, _worldSize.y);
            if (heightMap[x,y] >= 0.54f && heightMap[x, y] <= 0.56f)
            {
                return new Vector2Int(x, y);
            }
        }

        Debug.Log("failure count exceeded 1000");
        return randomPoint;
    }

    private static Vector2Int CalculateDirection(Vector2Int position, int searchRadius, float[,] attractivnesMap)
    {
        Vector2Int bestPointPosition = Vector2Int.zero;
        float bestAttractivnes = float.MinValue;

        for (int y = position.y - searchRadius; y < position.y + searchRadius; y++)
        {
            for (int x = position.x - searchRadius; x < position.x + searchRadius; x++)
            {
                float randomBias = Random.Range(-0.01f, 0.0f);
                if(x > 0 && x < _worldSize.x && y > 0 && y < _worldSize.y && bestAttractivnes < (attractivnesMap[x,y] + randomBias) && position.x != x && position.y != y)
                {
                    bestPointPosition = new Vector2Int(x, y);
                    bestAttractivnes = attractivnesMap[x,y];
                }
            }
        }

        Vector2Int direction = bestPointPosition - position;
        return direction;
    }

    private static float[,] RunRivers(River[] rivers, float[,] attractivnesMap)
    {
        float[,] riverMap = new float[_worldSize.x, _worldSize.y];
        foreach (River river in rivers)
        {
            riverMap = RunRiver(river, riverMap, attractivnesMap);
        }
        return riverMap;
    }

    private static float[,] RunRiver(River river, float[,] riverMap, float[,] attractivnesMap)
    {
        while (river.length > 0) 
        { 
            riverMap = DrawLine(riverMap,river, river.radius);
            attractivnesMap = DrawLine(attractivnesMap, river, (int)(river.radius / 2), 0);

            float sizeDecay = Random.Range(0.0f,1.0f);
            if (sizeDecay <= river.radiusDecayChance && river.radius > 3)
                river.radius -= 1;

            int distance = Mathf.RoundToInt(Vector2Int.Distance(river.position, river.position + river.direction));
            river.length -= distance;
            river.position += river.direction;
            river.direction = CalculateDirection(river.position, 5, attractivnesMap);
        }

        return riverMap;
    }

    private static float[,] DrawLine(float[,] riverMap, River river, int radius = 5, float strength = 1.0f)
    {
        Vector2Int endPosition = river.position + river.direction;
        Vector2Int startPosition = river.position;

        if(startPosition.x > endPosition.x)
        {
            endPosition = river.position;
            startPosition = river.position + river.direction;
        }

        int dx = endPosition.x - startPosition.x;
        int dy = endPosition.y - startPosition.y;

        int dir = dy < 0 ? -1 : 1;
        dy *= dir;


        //TODO: do something if dx == 0
        if (dx != 0)
        {
            int y = startPosition.y;
            float p = 2 * ((float)dy / (float)dx);
            for (int i = 0; i < dx + 1; i++)
            {
                Vector2Int pointPosition = new Vector2Int(startPosition.x + i, y);
                riverMap = PutPoint(riverMap, pointPosition, radius, strength);
                if (p >= 0)
                {
                    y += dir;
                    p = p + 2 * dy - 2 * dx;
                }
                else
                {
                    p = p + 2 * dy;
                }
            }
        }
        return riverMap;
    }

    private static float[,] PutPoint(float[,] riverMap, Vector2Int position, int radius, float strength = 1.0f)
    {
        for (int y = position.y - radius; y < position.y + radius; y++)
        {
            for (int x = position.x - radius; x < position.x + radius; x++)
            {
                if (x > 0 && x < _worldSize.x && y > 0 && y < _worldSize.y)
                {
                    riverMap[x, y] = strength;
                }
            }
        }
        return riverMap;
    }
}

struct River
{
    public int ID;
    public Vector2Int position;
    public Vector2Int direction;
    public int length;
    public int radius;
    public float radiusDecayChance;

    public River(int ID, Vector2Int position, Vector2Int direction, int length, int radius, float radiusDecayChance)
    {
        this.ID = ID;
        this.position = position;
        this.direction = direction;
        this.length = length;
        this.radius = radius;
        this.radiusDecayChance = radiusDecayChance;
    }
}

[System.Serializable]
public struct RiverGenerationParameters
{
    public int numRivers;
    public AnimationCurve lengthDistribution;
    public AnimationCurve sizeDistribution;
    public float radiusDecayChance;
    public int searchRadius;
}

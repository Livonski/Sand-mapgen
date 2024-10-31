using UnityEngine;

public static class Noise
{
    public enum NormalizeMode {Local, Global, None};

    public static float[,] GenerateNoiseMap(int width, int height, int seed, float scale, int octaves, float persistance, float lacunarity, Vector2 offset, NormalizeMode normalizeMode)
    {
        float[,] noiseMap = new float[width, height];

        System.Random prng = new System.Random(seed);
        Vector2[] octavesOffsets = new Vector2[octaves];
        for (int i = 0; i < octaves; i++)
        {
            float offsetX = prng.Next(-100000, 100000) + offset.x;
            float offsetY = prng.Next(-100000, 100000) - offset.y;
            octavesOffsets[i] = new Vector2(offsetX, offsetY);
        }

        if (scale <= 0)
            scale = 0.0001f;

        float max = float.MinValue;
        float min = float.MaxValue;


        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float amplitude = 1;
                float frequency = 1;
                float noiseHeight = 0;


                for (int i = 0; i < octaves; i++)
                {
                    float sampleX = x / scale * frequency + octavesOffsets[i].x;
                    float sampleY = y / scale * frequency + octavesOffsets[i].y;
                    float perlinValue = Mathf.PerlinNoise(sampleX, sampleY) * 2 - 1;
                    noiseHeight += perlinValue * amplitude;

                    amplitude *= persistance;
                    frequency *= lacunarity;
                }

                if(noiseHeight > max)
                    max = noiseHeight;
                if (noiseHeight < min)
                    min = noiseHeight;

                noiseMap[x, y] = noiseHeight;
            }
        }

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                noiseMap[x,y] = Mathf.InverseLerp(min,max,noiseMap[x,y]);
            }
        }

        return noiseMap;
    }
    public static float[,] GenerateNoiseMap(int width, int height, NoiseParameters noiseParameters)
    {
        float[,] noiseMap = new float[width, height];

        System.Random prng = new System.Random(noiseParameters.seed);
        Vector2[] octavesOffsets = new Vector2[noiseParameters.octaves];

        float maxPossibleHeight = 0;
        float amplitude = 1;
        float frequency = 1;

        for (int i = 0; i < noiseParameters.octaves; i++)
        {
            float offsetX = prng.Next(-100000, 100000) + noiseParameters.offset.x;
            float offsetY = prng.Next(-100000, 100000) - noiseParameters.offset.y;
            octavesOffsets[i] = new Vector2(offsetX, offsetY);

            maxPossibleHeight += amplitude;
            amplitude *= noiseParameters.persistance;
        }

        if (noiseParameters.scale <= 0)
            noiseParameters.scale = 0.0001f;

        float maxLocalNoiseHeight = float.MinValue;
        float minLocalNoiseHeight = float.MaxValue;

        float halfWidth = width / 2f;
        float halfHeight = height / 2f;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                amplitude = 1;
                frequency = 1;
                float noiseHeight = 0;


                for (int i = 0; i < noiseParameters.octaves; i++)
                {
                    float sampleX = (x - halfWidth + octavesOffsets[i].x) / noiseParameters.scale * frequency;
                    float sampleY = (y - halfHeight + octavesOffsets[i].y) / noiseParameters.scale * frequency;
                    float perlinValue = Mathf.PerlinNoise(sampleX, sampleY) * 2 - 1;
                    noiseHeight += perlinValue * amplitude;

                    amplitude *= noiseParameters.persistance;
                    frequency *= noiseParameters.lacunarity;
                }

                if (noiseHeight > maxLocalNoiseHeight)
                    maxLocalNoiseHeight = noiseHeight;
                if (noiseHeight < minLocalNoiseHeight)
                    minLocalNoiseHeight = noiseHeight;

                noiseMap[x, y] = noiseHeight;
            }
        }

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                switch(noiseParameters.normalizeMode)
                {
                    case NormalizeMode.Local:
                    {
                    noiseMap[x, y] = Mathf.InverseLerp(minLocalNoiseHeight, maxLocalNoiseHeight, noiseMap[x, y]);
                    break;
                    }
                    case NormalizeMode.Global:
                    {
                        float normalizedHeight = (noiseMap[x, y] + 1) / (maxPossibleHeight / 0.9f);
                        noiseMap[x, y] = Mathf.Clamp(normalizedHeight, 0, int.MaxValue);
                        break;
                    }
                    case NormalizeMode.None:
                    {
                        break;
                    }
                }
            }
        }

        return noiseMap;
    }
}

[System.Serializable]
public struct NoiseParameters
{
    public float scale;

    public int octaves;
    [Range(0f, 1f)]
    public float persistance;
    public float lacunarity;

    public int seed;
    public Vector2 offset;

    public Noise.NormalizeMode normalizeMode;
}
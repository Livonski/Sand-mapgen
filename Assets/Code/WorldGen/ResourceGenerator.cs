using Unity.VisualScripting;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;

public class ResourceGenerator : MonoBehaviour
{
    [SerializeField] private Resource[] _resources;
    [SerializeField] private NoiseParameters _patchesNoise;

    HashSet<PointValue> _resourcesMap;
    private float[,] _densityMap;

    private Vector2Int _worldSize = Vector2Int.zero;
    private Texture2D _world;
    private WorldGenerator _worldGenerator;

    public HashSet<PointValue> GenerateResourcesMap(Vector2Int worldSize, Texture2D world)
    {
        _worldSize = worldSize;
        _world = world;
        _worldGenerator = GetComponent<WorldGenerator>();

        CalculatePreferedColors();
        InitializeMaps();
        GenerateResources();

        return _resourcesMap;
    }

    private void CalculatePreferedColors()
    {
        for (int i = 0; i < _resources.Length; i++)
        {
            _resources[i].preferedBiomesColors = new Color[_resources[i].preferedBiomes.Length];

            for (int j = 0; j < _resources[i].preferedBiomes.Length; j++)
            {
                _resources[i].preferedBiomesColors[j] = _worldGenerator.GetBiomeData(_resources[i].preferedBiomes[j]).biomeColor;
            }
        }
    }

    private void GenerateResources()
    {
        float avgPointGenTime = 0;

        float avgPatchGenTime = 0;
        int totalPatchesGenerated = 0;

        foreach (var resource in _resources)
        {
            if(resource.numPatches == 0)
                continue;
            Stopwatch sw = new Stopwatch();
            sw.Start();

            Vector2Int[] points = GeneratePoints(resource);

            sw.Stop();
            avgPointGenTime += sw.ElapsedMilliseconds;
            
            for (int i = 0; i < resource.numPatches; i++)
            {
                sw.Restart();

                Vector2Int patchPosition = points[i];
                Vector2Int patchSize = CalculateSize(resource, patchPosition);

                PutPoint(patchPosition, Mathf.Max(patchSize.x, patchSize.y));

                Vector2Int topLeftCorner = new Vector2Int(Mathf.Max(0, patchPosition.x - patchSize.x / 2), Mathf.Max(0, patchPosition.y - patchSize.y / 2));

                NoiseParameters noiseParameters = _patchesNoise;
                noiseParameters.seed = i;

                Color[] worldSlice = _world.GetPixels(topLeftCorner.x, topLeftCorner.y, patchSize.x, patchSize.y);
                ResourcePatch resourcePatch = new ResourcePatch(resource, noiseParameters, patchSize, patchPosition, worldSlice, resource.sineWaveParams);
                List<PointValue> patchMap = resourcePatch.GenerateResourcePatch();

                _resourcesMap = AddPoints(topLeftCorner, _resourcesMap, patchMap);

                sw.Stop();
                avgPatchGenTime += sw.ElapsedMilliseconds;
                totalPatchesGenerated++;
            }
        }

        UnityEngine.Debug.Log($"Average resources point generation time: {avgPointGenTime / _resources.Length} ms, total: {avgPointGenTime} ms");
        UnityEngine.Debug.Log($"Average resources patch generation time: {avgPatchGenTime / totalPatchesGenerated} ms, total: {avgPatchGenTime} ms");

    }

    private HashSet<PointValue> AddPoints(Vector2Int patchPosition, HashSet<PointValue> main, List<PointValue> newPatch)
    {
        foreach (PointValue point in newPatch)
        {
            Vector2Int globalPosition = patchPosition + point.position;
            PointValue newPoint = new PointValue(globalPosition, point.color);
            main.Add(newPoint);
        }
        return main;
    }

    private Vector2Int[] GeneratePoints(Resource resource)
    {
        Vector2Int[] points = new Vector2Int[resource.numPatches];

        HashSet<PossiblePoint> possiblePoints = GeneratePossiblePoints(resource);
        UnityEngine.Debug.Log($"number of possible points for {resource.name} {possiblePoints.Count}");

        for (int i = 0; i < resource.numPatches; i++)
        {
            points[i] = GetRandomWeightedPoint(possiblePoints.ToList<PossiblePoint>());
        }
        return points;
    }

    private Vector2Int GetRandomWeightedPoint(List<PossiblePoint> possiblePoints)
    {
        float totalWeight = 0.0f;
        foreach (PossiblePoint p in possiblePoints)
        {
            totalWeight += p.weight;
        }

        float rand = Random.Range(0, totalWeight);
        float cumulativeWeight = 0.0f;

        for (int i = 0; i < possiblePoints.Count; i++)
        {
            cumulativeWeight += possiblePoints[i].weight;
            if (rand <= cumulativeWeight)
                return possiblePoints[i].position;
        }

        return Vector2Int.zero;
    }

    private HashSet<PossiblePoint> GeneratePossiblePoints(Resource resource)
    {
        HashSet<PossiblePoint> possiblePoints = new HashSet<PossiblePoint>();
        for (int y = 0; y < _worldSize.y; y++)
        {
            for (int x = 0; x < _worldSize.x; x++)
            {
                if (resource.IsPreferedBiome(_world.GetPixel(x, y)))
                {
                    float weight = 1.0f - _densityMap[x,y];
                    PossiblePoint newPoint = new PossiblePoint(new Vector2Int(x, y), weight);
                    possiblePoints.Add(newPoint);
                }
            }
        }
        return possiblePoints;
    }

    private Vector2Int CalculateSize(Resource resource, Vector2Int position)
    {
        int sizeX = Mathf.FloorToInt(resource.sizeDistribution.Evaluate(Random.Range(0.0f, 1.0f)));
        int sizeY = Mathf.FloorToInt(resource.sizeDistribution.Evaluate(Random.Range(0.0f, 1.0f)));

        Vector2Int size = new Vector2Int(sizeX, sizeY);

        if ((position.x + size.x) > _worldSize.x)
            size.x = _worldSize.x - position.x;
        if ((position.y + size.y) > _worldSize.y)
            size.y = _worldSize.y - position.y;

        return size;
    }

    private void InitializeMaps()
    {
        _resourcesMap = new HashSet<PointValue>();
        _densityMap = new float[_worldSize.x, _worldSize.y];

        for (int y = 0; y < _worldSize.y; y++)
        {
            for (int x = 0; x < _worldSize.x; x++)
            {
                _densityMap[x, y] = 0;
            }
        }

    }

    private void PutPoint(Vector2Int position, int radius)
    {
        for (int y = position.y - radius; y < position.y + radius; y++)
        {
            for (int x = position.x - radius; x < position.x + radius; x++)
            {
                if (x > 0 && x < _worldSize.x && y > 0 && y < _worldSize.y)
                {
                    float strength = 1 - Vector2Int.Distance(position, new Vector2Int(x, y)) / radius;
                    strength = Mathf.Clamp01(strength + _densityMap[x, y]);
                    _densityMap[x,y] = strength;
                }
            }
        }
    }
}

[System.Serializable]
public struct Resource
{
    public string name;
    public Color color;

    public int density;
    public AnimationCurve sizeDistribution;

    public int numPatches;

    public string[] preferedBiomes;
    public Color[] preferedBiomesColors;

    public ResourceGenerationPattern generationPattern;
    public SineWaveParams sineWaveParams;

    public bool IsPreferedBiome(Color biomeColor)
    {
        foreach (var color in preferedBiomesColors)
        {
            if (color == biomeColor)
                return true;
        }
        return false;
    }
}

public class ResourcePatch
{
    private Resource _resource;

    private Color[] _worldSlice;

    private NoiseParameters _noiseParameters;
    private float[,] _patchNoise;

    private SineWaveParams _sineWaveParams;

    private Vector2Int _size;
    private Vector2Int _position;
    private Vector2Int _localCenterPoint;

    public ResourcePatch(Resource resource, NoiseParameters noiseParameters, Vector2Int patchSize, Vector2Int patchPosition, Color[] worldSlice, SineWaveParams sineWaveParams)
    {
        _resource = resource;
        _noiseParameters = noiseParameters;
        _size = patchSize;
        _position = patchPosition;
        _worldSlice = worldSlice;
        _sineWaveParams = sineWaveParams;
        _localCenterPoint = new Vector2Int(patchSize.x / 2, patchSize.y / 2);
    }

    public List<PointValue> GenerateResourcePatch()
    {
        List<PointValue> patchTexture = GeneratePatchArea();

        return patchTexture;
    }

    private List<PointValue> GeneratePatchArea()
    {
        //TODO: probably need to revrite this into interface/delegate system because it's horrible
        _patchNoise = Noise.GenerateNoiseMap(_size.x, _size.y, _noiseParameters);
        List<PointValue> output = new List<PointValue>();
        for (int y = 0; y < _size.y; y++)
        {
            for (int x = 0; x < _size.x; x++)
            {
                float distanceToCenter = Vector2Int.Distance(new Vector2Int(x, y), _localCenterPoint);
                float pixelValue = _patchNoise[x, y] * (1 - (distanceToCenter / Mathf.Max(_size.x, _size.y)));
                Color pixelColor = Color.red;
                switch (_resource.generationPattern)
                {
                    case ResourceGenerationPattern.area:
                        pixelColor = (_resource.IsPreferedBiome(_worldSlice[y * _size.x + x]) && pixelValue > 0.5f) ? _resource.color : Color.clear;
                        break;

                    case ResourceGenerationPattern.dots:
                        int randomValue = Random.Range(0, 100);
                        pixelColor = (_resource.IsPreferedBiome(_worldSlice[y * _size.x + x]) && pixelValue > 0.5f && randomValue >= (100 - _resource.density)) ? _resource.color : Color.clear;
                        break;

                    case ResourceGenerationPattern.stripes:
                        randomValue = Random.Range(0, 100);
                        float xWobble = Mathf.Sin(y * _sineWaveParams.wobbleFrequency.x) * _sineWaveParams.wobbleFrequency.x;
                        float yWobble = Mathf.Sin(x * _sineWaveParams.wobbleFrequency.y) * _sineWaveParams.wobbleFrequency.y;

                        float waveColor = Mathf.Sin((x + xWobble) * _sineWaveParams.frequency + (y + yWobble) * _sineWaveParams.frequency) * _sineWaveParams.amplitude;
                        waveColor = Mathf.InverseLerp(-_sineWaveParams.amplitude, _sineWaveParams.amplitude, waveColor);
                        pixelColor = _resource.IsPreferedBiome(_worldSlice[y * _size.x + x]) && pixelValue > 0.5f && waveColor > 0.7f && randomValue >= (100 - _resource.density) ? _resource.color : Color.clear;
                        break;
                }
                if (pixelColor != Color.red && pixelColor != Color.clear)
                    output.Add(new PointValue(new Vector2Int(x, y), pixelColor));
            }
        }
        return output;
    }
}

[System.Serializable]
public struct SineWaveParams
{
    public float frequency;
    public Vector2 wobbleStrength;
    public Vector2 wobbleFrequency;
    public float amplitude;
}

public enum ResourceGenerationPattern
{
    dots,
    area,
    stripes
}

public struct PointValue
{
    public Vector2Int position;
    public Color color;

    public PointValue(Vector2Int position, Color color)
    {
        this.position = position;
        this.color = color;
    }
}

public struct PossiblePoint
{
    public Vector2Int position;
    public float weight;

    public PossiblePoint(Vector2Int position, float weight)
    {
        this.position = position;
        this.weight = weight;
    }
}
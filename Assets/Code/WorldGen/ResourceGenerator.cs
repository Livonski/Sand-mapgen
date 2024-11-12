using Unity.VisualScripting;
using System.Collections.Generic;
using UnityEngine;

public class ResourceGenerator : MonoBehaviour
{
    [SerializeField] private int tmpSelector;
    [SerializeField] private Resource[] _resources;
    [SerializeField] private NoiseParameters _patchesNoise;

    Texture2D _resourcesMap;
    private Texture2D _densityMap;

    private Vector2Int _worldSize = Vector2Int.zero;
    private Texture2D _world;
    private WorldGenerator _worldGenerator;

    public Texture2D GenerateResourcesMap(Vector2Int worldSize, Texture2D world)
    {
        _worldSize = worldSize;
        _world = world;
        _worldGenerator = GetComponent<WorldGenerator>();

        CalculatePreferedColors();
        InitializeMaps();
        GenerateResources();

        return _resourcesMap;
    }

    public Texture2D GenerateDensityMap(Vector2Int worldSize, Texture2D world)
    {
        GenerateResourcesMap(worldSize, world);

        return _densityMap;
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
        foreach (var resource in _resources)
        {
            Vector2Int[] points = GeneratePoints(resource);
            for (int i = 0; i < resource.numPatches; i++)
            {
                Vector2Int patchPosition = points[i];
                Vector2Int patchSize = CalculateSize(resource, patchPosition);

                PutPoint(patchPosition,Mathf.Max(patchSize.x,patchSize.y));

                Vector2Int topLeftCorner = new Vector2Int(Mathf.Max(0, patchPosition.x - patchSize.x / 2), Mathf.Max(0, patchPosition.y - patchSize.y / 2));

                NoiseParameters noiseParameters = _patchesNoise;
                noiseParameters.seed = i;

                Color[] worldSlice = _world.GetPixels(topLeftCorner.x, topLeftCorner.y, patchSize.x, patchSize.y);
                ResourcePatch resourcePatch = new ResourcePatch(resource,noiseParameters, patchSize, patchPosition, worldSlice, resource.sineWaveParams);
                Debug.Log($"Generating {resource.name}, {i} patch at {patchPosition}, with size {patchSize}, topLeftCorner {topLeftCorner}");
                Color[] patchMap = resourcePatch.GenerateResourcePatch().GetPixels();

                _resourcesMap.SetPixels(topLeftCorner.x, topLeftCorner.y, patchSize.x, patchSize.y, patchMap);
                _resourcesMap.Apply();
            }
        }
    }

    private Vector2Int[] GeneratePoints(Resource resource)
    {
        Vector2Int[] points = new Vector2Int[resource.numPatches];
        List<Vector2Int> possiblePoints = GeneratePossiblePoints(resource);
        for (int i = 0; i < resource.numPatches; i++)
        {
            int rand = Random.Range(0, possiblePoints.Count);
            points[i] = possiblePoints[rand];
            possiblePoints.RemoveAt(rand);
        }
        return points;
    }

    private List<Vector2Int> GeneratePossiblePoints(Resource resource)
    {
        List<Vector2Int> possiblePoints = new List<Vector2Int>();
        for (int y = 0; y < _worldSize.y; y++)
        {
            for (int x = 0; x < _worldSize.x; x++)
            {
                if(resource.isPreferedBiome(_world.GetPixel(x, y)))
                    possiblePoints.Add(new Vector2Int(x,y));
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
        _resourcesMap = new Texture2D(_worldSize.x, _worldSize.y, TextureFormat.ARGB32, false);
        _densityMap = new Texture2D(_worldSize.x, _worldSize.y, TextureFormat.ARGB32, false);

        for (int y = 0; y < _worldSize.y; y++)
        {
            for (int x = 0; x < _worldSize.x; x++)
            {
                _resourcesMap.SetPixel(x, y, Color.clear);
                _densityMap.SetPixel(x, y, Color.black);
            }
        }

        _densityMap.Apply();
        _resourcesMap.Apply();
    }

    private void PutPoint(Vector2Int position, int radius)
    {
        for (int y = position.y - radius; y < position.y + radius; y++)
        {
            for (int x = position.x - radius; x < position.x + radius; x++)
            {
                if (x > 0 && x < _worldSize.x && y > 0 && y < _worldSize.y)
                {
                    float strength = 1 - Vector2Int.Distance(position,new Vector2Int(x,y)) / radius;
                    strength = Mathf.Clamp01(strength + _densityMap.GetPixel(x, y).r);
                    Color pixelColor = new Color(strength,strength,strength);
                    _densityMap.SetPixel(x, y, pixelColor);
                }
            }
        }
        _densityMap.Apply();
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

    public bool isPreferedBiome(Color biomeColor)
    {
        foreach(Color color in preferedBiomesColors)
        {
            if (biomeColor == color) 
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

    public Texture2D GenerateResourcePatch()
    {
        Texture2D patchTexture = GeneratePatchArea();
        patchTexture.Apply();
        
        return patchTexture;
    }

    private Texture2D GeneratePatchArea()
    {
        //TODO: probably need to revrite this into interface/delegate system because it's horrible
        _patchNoise = Noise.GenerateNoiseMap(_size.x, _size.y, _noiseParameters);
        Texture2D output = new Texture2D(_size.x, _size.y, TextureFormat.ARGB32, false);
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
                        pixelColor = (_resource.isPreferedBiome(_worldSlice[y * _size.x + x]) && pixelValue > 0.5f) ? _resource.color : Color.clear;
                        break;

                    case ResourceGenerationPattern.dots:
                        int randomValue = Random.Range(0, 100);
                        pixelColor = (_resource.isPreferedBiome(_worldSlice[y * _size.x + x]) && pixelValue > 0.5f && randomValue >= (100 - _resource.density)) ? _resource.color : Color.clear;
                        break;

                    case ResourceGenerationPattern.stripes:
                        randomValue = Random.Range(0, 100);
                        float xWobble = Mathf.Sin(y * _sineWaveParams.wobbleFrequency.x) * _sineWaveParams.wobbleFrequency.x;
                        float yWobble = Mathf.Sin(x * _sineWaveParams.wobbleFrequency.y) * _sineWaveParams.wobbleFrequency.y;

                        float waveColor = Mathf.Sin((x + xWobble) * _sineWaveParams.frequency + (y + yWobble) * _sineWaveParams.frequency) * _sineWaveParams.amplitude;
                        waveColor = Mathf.InverseLerp(-_sineWaveParams.amplitude,_sineWaveParams.amplitude, waveColor);
                        pixelColor = _resource.isPreferedBiome(_worldSlice[y * _size.x + x]) && pixelValue > 0.5f && waveColor > 0.7f && randomValue >= (100 - _resource.density) ? _resource.color : Color.clear;
                        break;
                }
                output.SetPixel(x, y, pixelColor);
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

using UnityEngine;
using System.Collections.Generic;
using System.Diagnostics;

public class WorldGenerator : MonoBehaviour
{
    [SerializeField] private Vector2Int _worldSize;

    [SerializeField] private RiverGenerationParameters _riverGenerationParameters;

    [Range(0.0f,1.0f)]
    [SerializeField] private float _temperatureNoiseStrength;
    [Range(0.0f, 1.0f)]
    [SerializeField] private float _heightNoiseStrength;

    [SerializeField] private NoiseParameters[] _noiseParameters;
    [SerializeField] private BiomeData[] _biomeData;

    [SerializeField] private int _TectonicPlatesNum;
    [SerializeField] private int _smoothingRadius;

    [SerializeField] private int _edgeThickness;

    [SerializeField] public bool _autoUpdate;
    [SerializeField] private enum DrawMode {FinishedMap, TemperatureMap, MoistureMap, HeightMap, VegetationMap, ResourcesMap, ResourcesDencityMap };
    [SerializeField] private DrawMode _drawMode;

    private SpriteRenderer _spriteRenderer;
    private Sprite _sprite;

    private Texture2D _world;

    private float[,] _heightMap;
    private float[,] _temperatureMap;
    private float[,] _moistureMap;
    private float[,] _vegetationMap;
    private List<Vector2Int> _riversMap;

    private List<PointValue> _resourcesMap;

    private void Start()
    {
        GenerateWorld();
    }

    public void GenerateWorld()
    {
        Stopwatch sw = Stopwatch.StartNew();
        sw.Start();
        CreateSprite();
        GenerateBiomeMaps();
        GenerateOutline();
        if(_drawMode != DrawMode.ResourcesMap && _drawMode != DrawMode.ResourcesDencityMap)
            GenerateResources();
        sw.Stop();
        UnityEngine.Debug.Log($"World generated in {sw.ElapsedMilliseconds} ms");
    }

    public void DrawMapInInspector()
    {
        switch (_drawMode)
        {
            case DrawMode.FinishedMap:
                GenerateWorld();
                break;

            case DrawMode.TemperatureMap:
                GenerateTemperatureMap();
                CreateSprite(_temperatureMap);
                break;

            case DrawMode.MoistureMap:
                GenerateMoistureMap();
                CreateSprite(_moistureMap);
                break;

            case DrawMode.HeightMap:
                GenerateHeightMap();
                CreateSprite(_heightMap);
                break;

            case DrawMode.VegetationMap:
                GenerateVegetationMap();
                CreateSprite(_vegetationMap);
                break;
            default:
                GenerateWorld();
                break;
        }
    }

    public Texture2D GetWorldTexture()
    {
        return _sprite.texture;
    }

    public BiomeData GetBiomeData(string name)
    {
        foreach(BiomeData biome in _biomeData)
        {
            if (biome.name == name)
                return biome;
        }
        UnityEngine.Debug.LogWarning($"Biome {name} not found");
        return _biomeData[0];
    }

    private void GenerateBiomeMaps()
    {
        // Debug stuff
        float minTemp = float.MaxValue;
        float minMoist = float.MaxValue;
        float minVeg = float.MaxValue;
        float minEle = float.MaxValue;

        float maxTemp = float.MinValue;
        float maxMoist = float.MinValue;
        float maxVeg = float.MinValue;
        float maxEle = float.MinValue;

        float avgTemp = 0;
        float avgMoist = 0;
        float avgVeg = 0;
        float avgEle = 0;

        GenerateMoistureMap();
        GenerateTemperatureMap();
        GenerateHeightMap();
        GenerateVegetationMap();

        for (int y = 0; y < _worldSize.y; y++)
        {
            for (int x = 0; x < _worldSize.x; x++)
            {

                Color biomeColor = EvaluateBiomes(x, y);
                if (biomeColor != Color.clear)
                    _world.SetPixel(x,y, biomeColor);

                // Debug stuff
                avgMoist += _moistureMap[x, y];
                avgTemp += _temperatureMap[x, y];
                avgVeg += _vegetationMap[x, y];
                avgEle += _heightMap[x, y];

                minTemp = Mathf.Min(minTemp, _temperatureMap[x, y]);
                minMoist = Mathf.Min(minMoist, _moistureMap[x, y]);
                minVeg = Mathf.Min(minVeg, _vegetationMap[x, y]);
                minEle = Mathf.Min(minEle, _heightMap[x, y]);

                maxTemp = Mathf.Max(maxTemp, _temperatureMap[x, y]);
                maxMoist = Mathf.Max(maxMoist, _moistureMap[x, y]);
                maxVeg = Mathf.Max(maxVeg, _vegetationMap[x, y]);
                maxEle = Mathf.Max(maxEle, _heightMap[x, y]);
                
            }
        }
        _world.Apply();

        avgTemp = avgTemp / (_worldSize.x * _worldSize.y);
        avgMoist = avgMoist / (_worldSize.x * _worldSize.y);
        avgVeg = avgVeg / (_worldSize.x * _worldSize.y);
        avgEle = avgEle / (_worldSize.x * _worldSize.y);

        UnityEngine.Debug.Log($"Temperature: {minTemp} - {maxTemp}, average: {avgTemp}");
        UnityEngine.Debug.Log($"Moisture: {minMoist} - {maxMoist}, average: {avgMoist}");
        UnityEngine.Debug.Log($"Vegetation: {minVeg} - {maxVeg}, average: {avgVeg}");
        UnityEngine.Debug.Log($"Elevation: {minEle} - {maxEle}, average: {avgEle}");
    }

    private Color EvaluateBiomes(int x, int y)
    {
        Color bestColor = Color.clear;
        float bestScore = float.MaxValue;


        for (int i = 0; i < _biomeData.Length; i++)
        {
            float temperature = _temperatureMap[x, y];
            float moisture = _moistureMap[x, y];
            float elevation = _heightMap[x, y];
            float vegetation = _vegetationMap[x, y];

            float score = _biomeData[i].GetBiomeScore(temperature, moisture, elevation, vegetation);

            if(score < bestScore && _biomeData[i].IsInRange(temperature, moisture, elevation, vegetation))
            {
                bestScore = score;
                bestColor = _biomeData[i].biomeColor;
            }
        }
        if (bestColor == Color.clear)
        {
            bestColor = Color.red;
        }
        return bestColor;
    }

    private void GenerateTemperatureMap()
    {
        _temperatureMap = Noise.GenerateNoiseMap(_worldSize.x, _worldSize.y, _noiseParameters[2]);
        float halfWorldSize = (float)_worldSize.y / 2;
        for (int y = 0; y < _worldSize.y; y++)
        {
            for (int x = 0; x < _worldSize.x; x++)
            {
                float gradientValue = 1 - Mathf.Abs((y - halfWorldSize) / halfWorldSize);
                _temperatureMap[x, y] = Mathf.Clamp01((_temperatureMap[x,y] * _temperatureNoiseStrength) + gradientValue * (1 - _temperatureNoiseStrength));
            }
        }
    }

    private void GenerateMoistureMap()
    {
        _moistureMap = Noise.GenerateNoiseMap(_worldSize.x, _worldSize.y, _noiseParameters[1]);
    }

    private void GenerateVegetationMap()
    {
        _vegetationMap = Noise.GenerateNoiseMap(_worldSize.x, _worldSize.y, _noiseParameters[3]);
    }

    private void GenerateHeightMap()
    {
        _heightMap = Noise.GenerateNoiseMap(_worldSize.x, _worldSize.y, _noiseParameters[0]);
        float[,] voronoiNoiseMap = VoronoiNoise.GenerateNoiseMap(_worldSize.x, _worldSize.y, _TectonicPlatesNum, _noiseParameters[0].seed, _smoothingRadius);
        for (int y = 0; y < _worldSize.y; y++)
        {
            for (int x = 0; x < _worldSize.x; x++)
            {
                _heightMap[x, y] = (_heightMap[x, y] * _heightNoiseStrength) + (voronoiNoiseMap[x,y] * (1 - _heightNoiseStrength));
            }
        }
        GenerateRivers();
        UnityEngine.Debug.Log($"num points in _rivers map {_riversMap.Count}");
        foreach (Vector2Int pos in _riversMap)
        {
            _heightMap[pos.x, pos.y] = _biomeData[1].elevationRange.x + 0.01f;
        }
    }

    private void GenerateRivers()
    {
        GenerateMoistureMap();
        Stopwatch sw = Stopwatch.StartNew();
        sw.Start();
        _riversMap = RiverBuilder.GenerateRivers(_worldSize, _riverGenerationParameters, _heightMap, _moistureMap);
        sw.Stop();
        UnityEngine.Debug.Log($"Rivers generated in {sw.ElapsedMilliseconds} ms");
    }

    private void GenerateOutline()
    {
        for (int y = 0; y < _worldSize.y; y++)
        {
            for (int x = 0; x < _worldSize.x; x++)
            {
                if (IsOnEdge(x, y, _edgeThickness))
                    _world.SetPixel(x, y, Color.black);
            }
        }
        _world.Apply();
    }

    private bool IsOnEdge(int x, int y, int edgeThickness)
    {
        Color currentColor = _world.GetPixel(x, y);

        if(currentColor == _biomeData[0].biomeColor || currentColor == _biomeData[1].biomeColor)
            return false;

        for (int dx = -edgeThickness; dx <= edgeThickness; dx++)
        {
            for (int dy = -edgeThickness; dy <= edgeThickness; dy++)
            {
                int nx = x + dx;
                int ny = y + dy;
                if (nx >= 0 && nx < _worldSize.x && ny >= 0 && ny < _worldSize.y)
                {
                    if ((dx != 0 || dy != 0) && (_world.GetPixel(nx,ny) == _biomeData[0].biomeColor || _world.GetPixel(nx, ny) == _biomeData[1].biomeColor))
                    {
                        return true;
                    }
                }
            }
        }
        return false;
    }

    private void GenerateResources()
    {
        Stopwatch sw = Stopwatch.StartNew();
        sw.Start();
        ResourceGenerator resourceGenerator = GetComponent<ResourceGenerator>();
        _resourcesMap = resourceGenerator.GenerateResourcesMap(_worldSize, _world);
        sw.Stop();
        UnityEngine.Debug.Log($"Resources generated in {sw.ElapsedMilliseconds} ms");
        UnityEngine.Debug.Log($"num points in resources map {_resourcesMap.Count}");
        foreach (PointValue pv in _resourcesMap)
        {
            _world.SetPixel(pv.position.x,pv.position.y,pv.color);
        }
        _world.Apply();
    }

    private void CreateSprite()
    {
        if (_spriteRenderer == null)
            _spriteRenderer = GetComponent<SpriteRenderer>();
        _world = new Texture2D(_worldSize.x, _worldSize.y, TextureFormat.ARGB32, false);
        Rect spriteRect = new Rect(Vector2.zero, _worldSize);
        Vector2 pivot = new Vector2(0.5f, 0.5f);

        _sprite = Sprite.Create(_world, spriteRect, pivot, _spriteRenderer.sprite.pixelsPerUnit);
        _spriteRenderer.sprite = _sprite;
    }
    private void CreateSprite(float[,] map)
    {
        if (_spriteRenderer == null)
            _spriteRenderer = GetComponent<SpriteRenderer>();
        _world = FArrToTexture2D(map);
        Rect spriteRect = new Rect(Vector2.zero, _worldSize);
        Vector2 pivot = new Vector2(0.5f, 0.5f);

        _sprite = Sprite.Create(_world, spriteRect, pivot, _spriteRenderer.sprite.pixelsPerUnit);
        _spriteRenderer.sprite = _sprite;
    }
    private void CreateSprite(Texture2D map)
    {
        if (_spriteRenderer == null)
            _spriteRenderer = GetComponent<SpriteRenderer>();
        Rect spriteRect = new Rect(Vector2.zero, _worldSize);
        Vector2 pivot = new Vector2(0.5f, 0.5f);

        _sprite = Sprite.Create(map, spriteRect, pivot, _spriteRenderer.sprite.pixelsPerUnit);
        _spriteRenderer.sprite = _sprite;
    }

    private Texture2D FArrToTexture2D(float[,] map)
    {
        Texture2D output = new Texture2D(_worldSize.x, _worldSize.y, TextureFormat.ARGB32, false);
        for (int y = 0; y < _worldSize.y; y++)
        {
            for (int x = 0; x < _worldSize.x; x++)
            {
                Color mapColor = new Color(map[x,y], map[x, y], map[x, y]);
                output.SetPixel(x, y, mapColor);
            }
        }
        output.Apply();
        return output;
    }
}

[System.Serializable]
public struct BiomeData
{
    public string name;

    public Vector2 temperatureRange;
    public Vector2 moistureRange;
    public Vector2 elevationRange;
    public Vector2 vegetationRange;

    [Range(0.01f,1.0f)]
    public float rarity;

    public Color biomeColor;

    public bool IsInRange(float temperature, float moisture, float elevation, float vegetation)
    {
        return temperature >= temperatureRange.x && temperature <= temperatureRange.y &&
               moisture >= moistureRange.x && moisture <= moistureRange.y &&
               elevation >= elevationRange.x && elevation <= elevationRange.y &&
               vegetation >= vegetationRange.x && vegetation <= vegetationRange.y;
    }

    public float GetBiomeScore(float pixelTemp, float pixelMoisture, float pixelElevation, float pixelVegetation)
    {
        float tempScore = Mathf.Abs(pixelTemp - ((temperatureRange.x + temperatureRange.y) / 2)) * rarity;
        float moistureScore = Mathf.Abs(pixelMoisture - ((moistureRange.x + moistureRange.y) / 2)) * rarity;
        float elevationScore = Mathf.Abs(pixelElevation - ((elevationRange.x + elevationRange.y) / 2));
        float vegetationScore = Mathf.Abs(pixelVegetation - ((vegetationRange.x + vegetationRange.y) / 2)) * rarity;
        return tempScore + moistureScore + vegetationScore; 
    }
}

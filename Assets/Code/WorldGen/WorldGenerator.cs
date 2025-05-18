using UnityEngine;
using System.Collections.Generic;
using System.Diagnostics;
using System;

public class WorldGenerator : MonoBehaviour
{
    [SerializeField] private Vector2Int _worldSize;

    [SerializeField] private RiverGenerationParameters _riverGenerationParameters;

    [Range(0.0f,1.0f)]
    [SerializeField] private float _temperatureNoiseStrength;
    [Range(0.0f, 1.0f)]
    [SerializeField] private float _heightNoiseStrength;

    [SerializeField] private NoiseParameters[] _noiseParameters;
    [SerializeField] private PerlinNoise _perlinNoise;

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

    private HashSet<PointValue> _resourcesMap;

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

        Stopwatch sw = Stopwatch.StartNew();
        sw.Start();

        int w = (int)_worldSize.x;
        int h = (int)_worldSize.y;
        int count = w * h;

        var pixels = new Color32[count];


        for (int y = 0; y < _worldSize.y; y++)
        {
            int rowOffset = y * w;
            for (int x = 0; x < _worldSize.x; x++)
            {
                int idx = rowOffset + x;

                Color biomeColor = EvaluateBiomes(x, y);
                if (biomeColor != Color.clear)
                {
                    pixels[idx] = new Color32(
                        (byte)(Mathf.Clamp01(biomeColor.r) * 255f),
                        (byte)(Mathf.Clamp01(biomeColor.g) * 255f),
                        (byte)(Mathf.Clamp01(biomeColor.b) * 255f),
                        255);
                }
                    //_world.SetPixel(x,y, biomeColor);

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

        _world.SetPixels32(pixels);
        _world.Apply(updateMipmaps: false, makeNoLongerReadable: false);

        avgTemp = avgTemp / (_worldSize.x * _worldSize.y);
        avgMoist = avgMoist / (_worldSize.x * _worldSize.y);
        avgVeg = avgVeg / (_worldSize.x * _worldSize.y);
        avgEle = avgEle / (_worldSize.x * _worldSize.y);

        UnityEngine.Debug.Log($"Temperature: {minTemp} - {maxTemp}, average: {avgTemp}");
        UnityEngine.Debug.Log($"Moisture: {minMoist} - {maxMoist}, average: {avgMoist}");
        UnityEngine.Debug.Log($"Vegetation: {minVeg} - {maxVeg}, average: {avgVeg}");
        UnityEngine.Debug.Log($"Elevation: {minEle} - {maxEle}, average: {avgEle}");

        sw.Stop();
        UnityEngine.Debug.Log($"Biomes evaluated in {sw.ElapsedMilliseconds} ms");
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
        Stopwatch sw = Stopwatch.StartNew();
        sw.Start();
        _temperatureMap = _perlinNoise.GenerateNoiseMap(_worldSize.x, _worldSize.y, _noiseParameters[2]);
        float halfWorldSize = (float)_worldSize.y / 2;
        for (int y = 0; y < _worldSize.y; y++)
        {
            for (int x = 0; x < _worldSize.x; x++)
            {
                float gradientValue = 1 - Mathf.Abs((y - halfWorldSize) / halfWorldSize);
                _temperatureMap[x, y] = Mathf.Clamp01((_temperatureMap[x,y] * _temperatureNoiseStrength) + gradientValue * (1 - _temperatureNoiseStrength));
            }
        }
        sw.Stop();
        UnityEngine.Debug.Log($"Temperature map generated in {sw.ElapsedMilliseconds} ms");
    }

    private void GenerateMoistureMap()
    {
        Stopwatch sw = Stopwatch.StartNew();
        sw.Start();
        _moistureMap = _perlinNoise.GenerateNoiseMap(_worldSize.x,_worldSize.y, _noiseParameters[1]);
        sw.Stop();
        UnityEngine.Debug.Log($"Moisture map generated in {sw.ElapsedMilliseconds} ms");
    }

    private void GenerateVegetationMap()
    {
        Stopwatch sw = Stopwatch.StartNew();
        sw.Start();
        _vegetationMap = _perlinNoise.GenerateNoiseMap(_worldSize.x, _worldSize.y, _noiseParameters[3]);
        sw.Stop();
        UnityEngine.Debug.Log($"Vegetation map generated in {sw.ElapsedMilliseconds} ms");
    }

    private void GenerateHeightMap()
    {
        Stopwatch sw = Stopwatch.StartNew();
        sw.Start();
        _heightMap = _perlinNoise.GenerateNoiseMap(_worldSize.x, _worldSize.y, _noiseParameters[0]);
        //float[,] voronoiNoiseMap = VoronoiNoise.GenerateNoiseMap(_worldSize.x, _worldSize.y, _TectonicPlatesNum, _noiseParameters[0].seed, _smoothingRadius);
        //float[,] voronoiNoiseMap = VoronoiNoise.GenerateNoiseMap(_worldSize.x, _worldSize.y, _heightMap, _TectonicPlatesNum, _noiseParameters[0].seed, _smoothingRadius);
        float[,] voronoiNoiseMap = VoronoiNoise.GenerateNoiseMap(_worldSize.x, _worldSize.y, _TectonicPlatesNum, _noiseParameters[0].seed, _smoothingRadius, _heightMap);
        for (int y = 0; y < _worldSize.y; y++)
        {
            for (int x = 0; x < _worldSize.x; x++)
            {
                _heightMap[x, y] = (_heightMap[x, y] * _heightNoiseStrength) + (voronoiNoiseMap[x,y] * (1 - _heightNoiseStrength));
            }
        }
        sw.Stop();
        UnityEngine.Debug.Log($"Height map generated in {sw.ElapsedMilliseconds} ms");
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
        int w = (int)_worldSize.x;
        int h = (int)_worldSize.y;
        int count = w * h;

        Color32[] pixels = _world.GetPixels32();

        for (int y = 0; y < h; y++)
        {
            int rowOffset = y * w;
            for (int x = 0; x < w; x++)
            {
                if (IsOnEdge(x, y, _edgeThickness))
                {
                    int idx = rowOffset + x;
                    pixels[idx] = new Color32(0, 0, 0, 255);
                }
            }
        }

        _world.SetPixels32(pixels);
        _world.Apply(updateMipmaps: false, makeNoLongerReadable: false);
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
        var sw = Stopwatch.StartNew();

        ResourceGenerator resourceGenerator = GetComponent<ResourceGenerator>();
        _resourcesMap = resourceGenerator.GenerateResourcesMap(_worldSize, _world);

        int w = (int)_worldSize.x;
        int h = (int)_worldSize.y;
        int count = w * h;

        Color32[] pixels = _world.GetPixels32();

        foreach (PointValue pv in _resourcesMap)
        {
            int x = pv.position.x;
            int y = pv.position.y;
            if (x < 0 || x >= w || y < 0 || y >= h)
                continue;

            int idx = y * w + x;
            // Если pv.color — Color, преобразуем:
            Color32 col32 = new Color32(
                    (byte)(Mathf.Clamp01(pv.color.r) * 255f),
                    (byte)(Mathf.Clamp01(pv.color.g) * 255f),
                    (byte)(Mathf.Clamp01(pv.color.b) * 255f),
                    (byte)(Mathf.Clamp01(pv.color.a) * 255f)
                  );
            pixels[idx] = col32;
        }

        _world.SetPixels32(pixels);
        _world.Apply(updateMipmaps: false, makeNoLongerReadable: false);

        sw.Stop();
        UnityEngine.Debug.Log($"Resources generated in {sw.ElapsedMilliseconds} ms");
        UnityEngine.Debug.Log($"num points in resources map {_resourcesMap.Count}");
    }

    private void CreateSprite()
    {
        if (_spriteRenderer == null)
            _spriteRenderer = GetComponent<SpriteRenderer>();
        _world = new Texture2D(_worldSize.x, _worldSize.y, TextureFormat.RGBA32, false);
        Rect spriteRect = new Rect(Vector2.zero, _worldSize);
        Vector2 pivot = new Vector2(0.5f, 0.5f);

        _sprite = Sprite.Create(_world, spriteRect, pivot, _spriteRenderer.sprite.pixelsPerUnit);
        _spriteRenderer.sprite = _sprite;
    }
    private void CreateSprite(float[,] map)
    {
        Stopwatch sw = Stopwatch.StartNew();
        sw.Start();

        if (_spriteRenderer == null)
            _spriteRenderer = GetComponent<SpriteRenderer>();
        _world = FArrToTexture2D(map);
        Rect spriteRect = new Rect(Vector2.zero, _worldSize);
        Vector2 pivot = new Vector2(0.5f, 0.5f);

        _sprite = Sprite.Create(_world, spriteRect, pivot, _spriteRenderer.sprite.pixelsPerUnit);
        _spriteRenderer.sprite = _sprite;

        sw.Stop();
        UnityEngine.Debug.Log($"Sprite generated in {sw.ElapsedMilliseconds} ms");
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
        int w = map.GetLength(0);
        int h = map.GetLength(1);
        int count = w * h;

        var output = new Texture2D(w, h, TextureFormat.RGBAFloat, false);

        var raw = new Vector4[count];
        for (int i = 0; i < count; i++)
        {
            float v = map[i % w, i / w];
            raw[i] = new Vector4(v, v, v, 1f);
        }

        output.SetPixelData(raw, 0);
        output.Apply(updateMipmaps: false, makeNoLongerReadable: false);

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

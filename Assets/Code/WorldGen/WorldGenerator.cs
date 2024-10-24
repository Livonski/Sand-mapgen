using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.XR;
using static UnityEditor.PlayerSettings.SplashScreen;

public class WorldGenerator : MonoBehaviour
{
    [SerializeField] private Vector2Int _worldSize;
    [SerializeField] private float _islandSize;

    [SerializeField] private int _numRivers;
    [SerializeField] private Vector2Int _riverSize;

    [SerializeField] private NoiseParameters[] _noiseParameters;
    [SerializeField] private BiomeData[] _biomeData;

    [SerializeField] public bool _autoUpdate;
    [SerializeField] private enum DrawMode {FinishedMap, TemperatureMap, MoistureMap, HeightMap, VegetationMap};
    [SerializeField] private DrawMode _drawMode;

    private SpriteRenderer _spriteRenderer;
    private Sprite _sprite;

    private Texture2D _world;

    private float[,] _heightMap;
    private float[,] _temperatureMap;
    private float[,] _moistureMap;
    private float[,] _vegetationMap;

    private void Start()
    {
        GenerateWorld();
    }

    public void GenerateWorld()
    {
        CreateSprite();
        //GenerateLandMap();
        GenerateBiomeMaps();
        GenerateRivers();
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
        }
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
            for (int x = 0; x < _worldSize.y; x++)
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

        Debug.Log($"Temperature: {minTemp} - {maxTemp}, average: {avgTemp}");
        Debug.Log($"Moisture: {minMoist} - {maxMoist}, average: {avgMoist}");
        Debug.Log($"Vegetation: {minVeg} - {maxVeg}, average: {avgVeg}");
        Debug.Log($"Elevation: {minEle} - {maxEle}, average: {avgEle}");
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
        Vector2 worldCenter = _worldSize / 2;
        float maxDistance = worldCenter.x * _islandSize;
        _heightMap = Noise.GenerateNoiseMap(_worldSize.x, _worldSize.y, _noiseParameters[0]);
        for (int y = 0; y < _worldSize.y; y++)
        {
            for (int x = 0; x < _worldSize.x; x++)
            {
                float dx = worldCenter.x - x;
                float dy = worldCenter.y - y;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                float p = 1 - ((distance - maxDistance) / maxDistance);
                float scalingFactor = distance <= maxDistance ? 1 : p;
                _heightMap[x, y] = Mathf.Clamp01(_heightMap[x, y] * scalingFactor); 
            }
        }
    }

    private void GenerateRivers()
    {
        int maxErrCount = 10;
        for (int i = 0; i < _numRivers; i++)
        {
            int errCount = 0;
            while (errCount < maxErrCount)
            {
                Debug.Log($"river{i}, {errCount} try");
                bool succes = TryGenerateRiver();
                if (succes)
                    errCount = maxErrCount;

                errCount++;
            }
        }
        _world.Apply();
    }

    private bool TryGenerateRiver()
    {
        Vector2Int[] directions = { new Vector2Int(-1, 1), new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(-1, 0), new Vector2Int(1, 0), new Vector2Int(-1, -1), new Vector2Int(0, -1), new Vector2Int(1, -1) };

        int x = Random.Range(0, _worldSize.x);
        int y = Random.Range(0, _worldSize.y);

        Vector2Int randDyr = directions[Random.Range(0,7)];

        int dyrX = randDyr.x;
        int dyrY = randDyr.y;

        int riverSize = Random.Range(_riverSize.x, _riverSize.y);

        Debug.Log($"pos: {x}, {y}, dyr: {dyrX},{dyrY}, size: {riverSize}");

        if(_world.GetPixel(x,y) != _biomeData[0].biomeColor && _world.GetPixel(x, y) != _biomeData[1].biomeColor)
            return false;

        int HighTechSafetyMeasures = 1024;
        while (riverSize > 0 && HighTechSafetyMeasures > 0)
        {
            x += dyrX;
            y += dyrY;
            HighTechSafetyMeasures--;
            if (x >= _worldSize.x || y >= _worldSize.y)
            {
                riverSize = 0;
                return false;
            }

            if (_world.GetPixel(x, y) != _biomeData[0].biomeColor && _world.GetPixel(x, y) != _biomeData[1].biomeColor)
            {
                _world.SetPixel(x, y, Color.white);
                riverSize--;
            }
        }
        if (HighTechSafetyMeasures <= 0)
            Debug.Log("HighTechSafetyMeasures saved you");

        return true;
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

    private Texture2D FArrToTexture2D(float[,] map)
    {
        Texture2D output = new Texture2D(_worldSize.x, _worldSize.y, TextureFormat.ARGB32, false);
        for (int y = 0; y < _worldSize.y; y++)
        {
            for (int x = 0; x < _worldSize.y; x++)
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
        float tempScore = Mathf.Abs(pixelTemp - ((temperatureRange.x + temperatureRange.y) / 2));
        float moistureScore = Mathf.Abs(pixelMoisture - ((moistureRange.x + moistureRange.y) / 2));
        float elevationScore = Mathf.Abs(pixelElevation - ((elevationRange.x + elevationRange.y) / 2));
        float vegetationScore = Mathf.Abs(pixelVegetation - ((vegetationRange.x + vegetationRange.y) / 2));
        return tempScore + moistureScore; 
    }
}

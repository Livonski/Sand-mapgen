using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Resources;
using UnityEngine;
using UnityEngine.UIElements;

public class ResourceGenerator : MonoBehaviour
{
    [SerializeField] private Resource[] _resources;
    [SerializeField] private NoiseParameters _patchesNoise;

    private Vector2Int _worldSize = Vector2Int.zero;
    private Texture2D _world;
    private WorldGenerator _worldGenerator;

    public Texture2D GenerateResourcesMap(Vector2Int worldSize, Texture2D world)
    {
        _worldSize = worldSize;
        _world = world;
        _worldGenerator = GetComponent<WorldGenerator>();

        CalculatePreferedColors();
        Texture2D resourcesMap = GenerateResources();
        resourcesMap.Apply();

        return resourcesMap;
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

    private Texture2D GenerateResources()
    {
        Texture2D resourcesMap = new Texture2D(_worldSize.x,_worldSize.y, TextureFormat.ARGB32, false);

        for (int y = 0; y < _worldSize.y; y++)
        {
            for(int x = 0; x < _worldSize.x; x++)
            {
                resourcesMap.SetPixel(x, y, Color.clear);
            }
        }

        foreach (var resource in _resources)
        {
            for (int i = 0; i < resource.numPatches; i++)
            {
                int sizeX = Random.Range(50, 150);
                int sizeY = Random.Range(50, 150);
                Vector2Int patchSize = new Vector2Int(sizeX, sizeY);

                int posX = Random.Range(0, _worldSize.x);
                int posY = Random.Range(0, _worldSize.y);
                Vector2Int patchPosition = new Vector2Int(posX, posY);
                if ((patchPosition.x + patchSize.x) > _worldSize.x)
                    patchSize.x = _worldSize.x - patchPosition.x;
                if ((patchPosition.y + patchSize.y) > _worldSize.y)
                    patchSize.y = _worldSize.y - patchPosition.y;

                Vector2Int topLeftCorner = new Vector2Int(Mathf.Max(0, patchPosition.x - patchSize.x / 2), Mathf.Max(0, patchPosition.y - patchSize.y / 2));

                NoiseParameters noiseParameters = _patchesNoise;
                noiseParameters.seed = i;

                Color[] worldSlice = _world.GetPixels(topLeftCorner.x, topLeftCorner.y, patchSize.x, patchSize.y);
                ResourcePatch resourcePatch = new ResourcePatch(resource,noiseParameters, patchSize, patchPosition, worldSlice);
                Debug.Log($"Generating {resource.name}, {i} patch at {patchPosition}, with size {patchSize}, topLeftCorner {topLeftCorner}");
                Color[] patchMap = resourcePatch.GenerateResourcePatch().GetPixels();

                resourcesMap.SetPixels(topLeftCorner.x, topLeftCorner.y, patchSize.x, patchSize.y, patchMap);
                resourcesMap.Apply();
            }
        }

        return resourcesMap;
    }
}

[System.Serializable]
public struct Resource
{
    public string name;
    public Color color;

    public int density;

    public int numPatches;

    public string[] preferedBiomes;
    public Color[] preferedBiomesColors;

    public ResourceGenerationPattern generationPattern;

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

    private Vector2Int _size;
    private Vector2Int _position;
    private Vector2Int _localCenterPoint;
    
    public ResourcePatch(Resource resource, NoiseParameters noiseParameters, Vector2Int patchSize, Vector2Int patchPosition, Color[] worldSlice)
    {
        _resource = resource;
        _noiseParameters = noiseParameters;
        _size = patchSize;
        _position = patchPosition;
        _worldSlice = worldSlice;
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
                        //TODO
                        pixelColor = (_resource.isPreferedBiome(_worldSlice[y * _size.x + x]) && pixelValue > 0.5f) ? _resource.color : Color.clear;
                        break;
                }
                output.SetPixel(x, y, pixelColor);
            }
        }
        return output;
    }
}


public enum ResourceGenerationPattern
{
    dots,
    area,
    stripes
}

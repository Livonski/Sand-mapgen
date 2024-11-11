using UnityEngine;

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
                int sizeX = Mathf.FloorToInt(resource.sizeDistribution.Evaluate(Random.Range(0.0f, 1.0f)));
                int sizeY = Mathf.FloorToInt(resource.sizeDistribution.Evaluate(Random.Range(0.0f, 1.0f)));
                Vector2Int patchSize = new Vector2Int(sizeX, sizeY);

                //TODO: better point generation
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
                ResourcePatch resourcePatch = new ResourcePatch(resource,noiseParameters, patchSize, patchPosition, worldSlice, resource.sineWaveParams);
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

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

public class ResourceGenerator : MonoBehaviour
{
    [SerializeField] private Resource[] _resources;

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
            _resources[i].amount = 0;

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
                int rand = Random.Range(0, 100);
                for(int i = 0; i < _resources.Length; i++)
                {
                    if (_resources[i].amount < _resources[i].preferedAmount && _resources[i].isPreferedBiome(_world.GetPixel(x, y)) && rand > 98)
                    {
                        resourcesMap.SetPixel(x, y, _resources[i].color);
                        _resources[i].amount++;
                        break;
                    }
                }
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

    public int preferedAmount;
    public int amount;

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

public enum ResourceGenerationPattern
{
    dots,
    area,
    stripes
}

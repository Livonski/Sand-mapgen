using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PerlinNoise : MonoBehaviour
{
    [SerializeField] private ComputeShader _perlinNoiseShader;

    private Vector2Int _textureSize;

    private RenderTexture _noiseTexture;
    private Texture2D _outputTexture;

    private int _kernelID;

    public float[,] GenerateNoiseMap(int width, int height, NoiseParameters noiseParameters)
    {
        _textureSize = new Vector2Int(width, height);
        _kernelID = _perlinNoiseShader.FindKernel("CSMain");

        InitializeTexture();
        UpdateShaderParameters(noiseParameters);

        int threadGroupsX = Mathf.CeilToInt(_textureSize.x / 8.0f);
        int threadGroupsY = Mathf.CeilToInt(_textureSize.y / 8.0f);

        _perlinNoiseShader.Dispatch(_kernelID, threadGroupsX, threadGroupsY, 1);
        CopyRenderTextureToTexture2D();
        float[,] output = Texture2DToFArr(_outputTexture);
        return output;
    }

    private void UpdateShaderParameters(NoiseParameters noiseParameters)
    {
        _perlinNoiseShader.SetInt("Width", _textureSize.x);
        _perlinNoiseShader.SetInt("Height", _textureSize.y);

        _perlinNoiseShader.SetFloat("Scale", noiseParameters.scale);
        _perlinNoiseShader.SetFloat("OffsetX", noiseParameters.offset.x);
        _perlinNoiseShader.SetFloat("OffsetY", noiseParameters.offset.y);

        _perlinNoiseShader.SetInt("Octaves", noiseParameters.octaves);

        _perlinNoiseShader.SetFloat("Persistance", noiseParameters.persistance);
        _perlinNoiseShader.SetFloat("Lacunarity", noiseParameters.lacunarity);

        _perlinNoiseShader.SetInt("Seed", noiseParameters.seed);

        _perlinNoiseShader.SetTexture(0, "OutputTexture", _noiseTexture);
    }

    private void InitializeTexture()
    {
        _noiseTexture = new RenderTexture(_textureSize.x, _textureSize.y, 0, RenderTextureFormat.ARGB32);
        _noiseTexture.enableRandomWrite = true;
        _noiseTexture.Create();

        _outputTexture = new Texture2D(_textureSize.x, _textureSize.y, TextureFormat.ARGB32, false);

        _perlinNoiseShader.SetTexture(_kernelID, "OutputTexture", _noiseTexture);
    }

    private void CopyRenderTextureToTexture2D()
    {
        RenderTexture.active = _noiseTexture;
        _outputTexture.ReadPixels(new Rect(0, 0, _textureSize.x, _textureSize.y), 0, 0);
        _outputTexture.Apply();
        RenderTexture.active = null;
    }

    private float[,] Texture2DToFArr(Texture2D map)
    {
        float[,] output = new float[map.width, map.height];
        for (int y = 0; y < map.height; y++)
        {
            for (int x = 0; x < map.width; x++)
            {
                output[x,y] = map.GetPixel(x, y).r;
            }
        }
        return output;
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static Noise;

public class PerlinNoiseDisplay : MonoBehaviour
{
    [SerializeField] private ComputeShader _perlinNoiseShader;
    [SerializeField] private NoiseParameters _noiseParameters;
    [SerializeField] private Vector2Int _textureSize;

    [SerializeField] private SpriteRenderer _spriteRenderer;
    private enum PerlinNoiseType {CPU, GPU };
    [SerializeField] private PerlinNoiseType _noiseType;


    private RenderTexture _noiseTexture;
    private Texture2D _outputTexture;

    private int kernelID;

    private void Start()
    {
        kernelID = _perlinNoiseShader.FindKernel("CSMain");
        if (_noiseType == PerlinNoiseType.GPU)
        {
            InitializeTexture();
            UpdateShaderParameters();

            int threadGroupsX = Mathf.CeilToInt(_textureSize.x / 8.0f);
            int threadGroupsY = Mathf.CeilToInt(_textureSize.y / 8.0f);

            _perlinNoiseShader.Dispatch(kernelID, threadGroupsY, threadGroupsX, 1);

            CopyRenderTextureToTexture2D();

            ApplyTextureToSprite();
            DebugInfo();
        }
        else
        {
            InitializeTexture();
            float[,] _noiseMap = Noise.GenerateNoiseMap(_textureSize.x, _textureSize.y, _noiseParameters);
            _outputTexture = FArrToTexture2D(_noiseMap, _textureSize);

            ApplyTextureToSprite();
        }
    }

    private void Update()
    {
        if (_noiseType == PerlinNoiseType.GPU)
        {
            UpdateShaderParameters();

            int threadGroupsX = Mathf.CeilToInt(_textureSize.x / 8.0f);
            int threadGroupsY = Mathf.CeilToInt(_textureSize.y / 8.0f);

            _perlinNoiseShader.Dispatch(kernelID, threadGroupsY, threadGroupsX, 1);

            CopyRenderTextureToTexture2D();
        }
    }

    private void InitializeTexture()
    {
        _noiseTexture = new RenderTexture(_textureSize.x, _textureSize.y, 0, RenderTextureFormat.ARGB32);
        _noiseTexture.enableRandomWrite = true;
        _noiseTexture.Create();

        _outputTexture = new Texture2D(_textureSize.x, _textureSize.y, TextureFormat.ARGB32, false);

        _perlinNoiseShader.SetTexture(kernelID, "OutputTexture", _noiseTexture);
    }

    private void UpdateShaderParameters()
    {
        _perlinNoiseShader.SetInt("Width", _textureSize.x);
        _perlinNoiseShader.SetInt("Height", _textureSize.y);

        _perlinNoiseShader.SetFloat("Scale", _noiseParameters.scale);
        _perlinNoiseShader.SetFloat("OffsetX", _noiseParameters.offset.x);
        _perlinNoiseShader.SetFloat("OffsetY", _noiseParameters.offset.y);

        _perlinNoiseShader.SetInt("Octaves", _noiseParameters.octaves);

        _perlinNoiseShader.SetFloat("Persistance", _noiseParameters.persistance);
        _perlinNoiseShader.SetFloat("Lacunarity", _noiseParameters.lacunarity);

        _perlinNoiseShader.SetTexture(0, "OutputTexture", _noiseTexture);
    }

    private void CopyRenderTextureToTexture2D()
    {
        RenderTexture.active = _noiseTexture;
        _outputTexture.ReadPixels(new Rect(0, 0, _textureSize.x, _textureSize.y), 0, 0);
        _outputTexture.Apply();
        //NormalizeTexture();
        RenderTexture.active = null;
    }

    private void ApplyTextureToSprite()
    {
        Sprite newSprite = Sprite.Create(_outputTexture, new Rect(0, 0, _textureSize.x, _textureSize.y), new Vector2(0.5f, 0.5f));
        _spriteRenderer.sprite = newSprite;
    }

    private Texture2D FArrToTexture2D(float[,] map, Vector2Int textureSize)
    {
        Texture2D output = new Texture2D(textureSize.x, textureSize.y, TextureFormat.ARGB32, false);
        for (int y = 0; y < textureSize.y; y++)
        {
            for (int x = 0; x < textureSize.x; x++)
            {
                Color mapColor = new Color(map[x, y], map[x, y], map[x, y]);
                output.SetPixel(x, y, mapColor);
            }
        }
        output.Apply();
        return output;
    }

    private void OnDestroy()
    {
        if (_noiseTexture != null)
        {
            _noiseTexture.Release();
        }
    }

    private void DebugInfo()
    {
        float minValue = float.MaxValue;
        float maxValue = float.MinValue;
        float avgValue = 0;

        for (int y = 0; y < _textureSize.y; y++)
        {
            for(int x = 0; x < _textureSize.x; x++)
            {
                float pixelValue = _outputTexture.GetPixel(x, y).r;
                minValue = Mathf.Min(minValue, pixelValue);
                maxValue = Mathf.Max(maxValue, pixelValue);
                avgValue += pixelValue;
            }
        }
        avgValue = avgValue / (_textureSize.x * _textureSize.y);
        Debug.Log($"Noise min: {minValue}, max: {maxValue}, avg: {avgValue}");
    }
}

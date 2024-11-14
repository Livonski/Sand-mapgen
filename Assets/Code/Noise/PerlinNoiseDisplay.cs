using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PerlinNoiseDisplay : MonoBehaviour
{
    [SerializeField] private ComputeShader _perlinNoiseShader;
    [SerializeField] private NoiseParameters _noiseParameters;
    [SerializeField] private Vector2Int _textureSize;

    [SerializeField] private SpriteRenderer _spriteRenderer;
    private RenderTexture _noiseTexture;
    private Texture2D _outputTexture;

    private int kernelID;

    private void Start()
    {
        kernelID = _perlinNoiseShader.FindKernel("CSMain");
        InitializeTexture();
        UpdateShaderParameters();

        int threadGroupsX = Mathf.CeilToInt(_textureSize.x / 8.0f);
        int threadGroupsY = Mathf.CeilToInt(_textureSize.y / 8.0f);

        _perlinNoiseShader.Dispatch(kernelID, threadGroupsY, threadGroupsX, 1);

        CopyRenderTextureToTexture2D();

        ApplyTextureToSprite();
        DebugInfo();
    }

    private void Update()
    {
        UpdateShaderParameters();

        int threadGroupsX = Mathf.CeilToInt(_textureSize.x / 8.0f);
        int threadGroupsY = Mathf.CeilToInt(_textureSize.y / 8.0f);

        _perlinNoiseShader.Dispatch(kernelID, threadGroupsY, threadGroupsX, 1);

        CopyRenderTextureToTexture2D();
    }

    private void InitializeTexture()
    {
        //_noiseTexture = new RenderTexture(_textureSize.x, _textureSize.y, 0, RenderTextureFormat.ARGBFloat);
        _noiseTexture = new RenderTexture(_textureSize.x, _textureSize.y, 0, RenderTextureFormat.ARGB32);
        _noiseTexture.enableRandomWrite = true;
        _noiseTexture.Create();

        //_outputTexture = new Texture2D(_textureSize.x, _textureSize.y, TextureFormat.RGBAFloat, false);
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
        RenderTexture.active = null;
    }

    private void ApplyTextureToSprite()
    {
        Sprite newSprite = Sprite.Create(_outputTexture, new Rect(0, 0, _textureSize.x, _textureSize.y), new Vector2(0.5f, 0.5f));
        _spriteRenderer.sprite = newSprite;
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

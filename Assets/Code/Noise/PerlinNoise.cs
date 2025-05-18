using System;
using UnityEngine;

public class PerlinNoise : MonoBehaviour
{
    [SerializeField] private ComputeShader _perlinNoiseShader;

    private int _kernelID;

    public float[,] GenerateNoiseMap(int width, int height, NoiseParameters noiseParameters)
    {
        _kernelID = _perlinNoiseShader.FindKernel("CSMain");

        int count = width * height;
        float[] noise = new float[count];

        ComputeBuffer noiseBuffer = new ComputeBuffer(count, sizeof(float));
        noiseBuffer.SetData(noise);

        _perlinNoiseShader.SetBuffer(_kernelID, "Result", noiseBuffer);

        UpdateShaderParameters(width, height, noiseParameters);

        int threadGroupsX = Mathf.CeilToInt(width / 8);
        int threadGroupsY = Mathf.CeilToInt(height / 8);

        _perlinNoiseShader.Dispatch(_kernelID, threadGroupsX, threadGroupsY, 1);
        float[,] result = new float[width, height];
        float[] flat = new float[count];
        noiseBuffer.GetData(flat);
        noiseBuffer.Release();
        Buffer.BlockCopy(flat, 0, result, 0, count * sizeof(float));
        return result;
    }

    private void UpdateShaderParameters(int width, int height, NoiseParameters noiseParameters)
    {
        _perlinNoiseShader.SetInt("Width", width);
        _perlinNoiseShader.SetInt("Height", height);

        _perlinNoiseShader.SetFloat("Scale", noiseParameters.scale);
        _perlinNoiseShader.SetFloat("OffsetX", noiseParameters.offset.x);
        _perlinNoiseShader.SetFloat("OffsetY", noiseParameters.offset.y);

        _perlinNoiseShader.SetInt("Octaves", noiseParameters.octaves);

        _perlinNoiseShader.SetFloat("Persistance", noiseParameters.persistance);
        _perlinNoiseShader.SetFloat("Lacunarity", noiseParameters.lacunarity);

        _perlinNoiseShader.SetInt("Seed", noiseParameters.seed);
    }
}

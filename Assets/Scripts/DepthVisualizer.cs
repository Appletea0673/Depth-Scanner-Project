using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DepthVisualizer : MonoBehaviour
{
    public Material depthMaterial;
    public Renderer quadRenderer;

    // Update the quad's material to the depth material
    void Update()
    {
        // Get the _EnvironmentDepthTexture from global shader properties
        depthMaterial.SetTexture("_EnvironmentDepthTexture", Shader.GetGlobalTexture("_EnvironmentDepthTexture"));

        // Set the quad's material to the depth material
        quadRenderer.material = depthMaterial;
    }
}

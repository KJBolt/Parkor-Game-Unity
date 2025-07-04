using System.Collections.Generic;
using UnityEngine;

public class SkinnedMeshHighlighter : MonoBehaviour
{
    [SerializeField] List<SkinnedMeshRenderer> meshesToHighlight; // Use mesh renderer for objects also check the mesh renderer for the object to confirm
    [SerializeField] Material originalMaterial;
    [SerializeField] Material highlightMaterial;

    public void HighlightMesh(bool highlight)
    {
        foreach (var mesh in meshesToHighlight)
        {
            mesh.material = highlight ? highlightMaterial : originalMaterial;
        }
    }
}

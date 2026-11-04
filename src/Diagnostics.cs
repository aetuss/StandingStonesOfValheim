using System.Collections.Generic;
using UnityEngine;

namespace StandingStonesOfValheim
{
    // With DebugLog on: writes what an object is made of and whether its parts can be seen (the rune altar
    // was invisible in tests; this lets the log show why).
    static class Diagnostics
    {
        static readonly HashSet<string> told = new HashSet<string>();

        internal static void Describe(GameObject go, string label)
        {
            if (!Plugin.DebugLog.Value || go == null || !told.Add(label)) return;
            Transform root = go.transform;
            Plugin.Log.LogInfo(label + ": " + go.name + " active " + go.activeInHierarchy + " layer " + LayerMask.LayerToName(go.layer) +
                " at " + root.position.ToString("F1") + " scale " + root.lossyScale.ToString("F2"));
            foreach (Component component in go.GetComponentsInChildren<Component>(true))
            {
                if (component is Transform) continue;
                string line = "  " + Path(component.transform, root) + ": " + component.GetType().Name;
                Renderer renderer = component as Renderer;
                if (renderer != null)
                {
                    line += " enabled " + renderer.enabled + " active " + renderer.gameObject.activeInHierarchy + " layer " + LayerMask.LayerToName(renderer.gameObject.layer) +
                        " visible " + renderer.isVisible + " bounds " + (renderer.bounds.center - root.position).ToString("F2") + " size " + renderer.bounds.size.ToString("F2") +
                        " shadow " + renderer.shadowCastingMode + " property block " + renderer.HasPropertyBlock();
                    foreach (Material m in renderer.sharedMaterials)
                    {
                        if (m == null) { line += " | null material"; continue; }
                        line += " | " + m.name + " (" + (m.shader != null ? m.shader.name : "?") + ", queue " + m.renderQueue + ")";
                        if (m.shader == null) continue;
                        for (int i = 0; i < m.shader.GetPropertyCount() && i < 60; i++)
                        {
                            string name = m.shader.GetPropertyName(i);
                            UnityEngine.Rendering.ShaderPropertyType type = m.shader.GetPropertyType(i);
                            if (type == UnityEngine.Rendering.ShaderPropertyType.Float || type == UnityEngine.Rendering.ShaderPropertyType.Range)
                                line += " " + name + "=" + m.GetFloat(name).ToString("0.##");
                        }
                    }
                }
                MeshFilter filter = component as MeshFilter;
                if (filter != null) line += " mesh " + (filter.sharedMesh != null ? filter.sharedMesh.name + " " + filter.sharedMesh.bounds.size.ToString("F2") : "none");
                LODGroup lod = component as LODGroup;
                if (lod != null) line += " lods " + lod.lodCount + " size " + lod.size.ToString("F2") + " enabled " + lod.enabled;
                Collider collider = component as Collider;
                if (collider != null) line += " enabled " + collider.enabled + " layer " + LayerMask.LayerToName(collider.gameObject.layer);
                Plugin.Log.LogInfo(line);
            }
        }

        static string Path(Transform t, Transform root)
        {
            string path = t.name;
            for (Transform p = t.parent; p != null && t != root && p != root.parent; p = p.parent) path = p.name + "/" + path;
            return path;
        }
    }
}

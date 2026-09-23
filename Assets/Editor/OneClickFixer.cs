using UnityEditor;
using UnityEngine;

public class OneClickFixer
{
    [MenuItem("Tools/1-Click Fix/Turn ON Gizmos (Show Blue NavMesh)")]
    public static void TurnOnGizmos()
    {
        if (SceneView.lastActiveSceneView != null)
        {
            // Gizmos ko ON karta hai
            SceneView.lastActiveSceneView.drawGizmos = true;
            
            // Scene view ko refresh karta hai
            SceneView.RepaintAll();
            
            Debug.Log("✅ Gizmos ON ho gaye hain! Ab aapko zameen par Blue NavMesh dikhna chahiye.");
        }
        else
        {
            Debug.LogWarning("Pehle Scene window par click karein, phir ye button dabayein!");
        }
    }
}

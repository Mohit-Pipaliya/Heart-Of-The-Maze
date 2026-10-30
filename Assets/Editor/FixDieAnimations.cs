using UnityEngine;
using UnityEditor;

[InitializeOnLoad]
public class FixDieAnimations
{
    static FixDieAnimations()
    {
        EditorApplication.delayCall += CheckAndFixDieAnimations;
    }

    [MenuItem("Tools/1-Click Fix/Fix Gun Die Floating In Air")]
    public static void CheckAndFixDieAnimations()
    {
        bool gunFixed = FixClip("Assets/Animations/Character /Player/Gun/Gun Die.fbx");
        bool normalFixed = FixClip("Assets/Animations/Character /Player/Normal/Normal Die.fbx");

        if (gunFixed || normalFixed)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("<color=green><b>[FixDieAnimations]</b> Gun Die aur Normal Die animations successfully fix ho gaye hain! 'Bake Into Pose (Y)' enable kar diya gaya hai taaki player hawa me na ude.</color>");
        }
    }

    private static bool FixClip(string fbxPath)
    {
        ModelImporter importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
        if (importer == null)
        {
            Debug.LogWarning($"[FixDieAnimations] Could not find asset at path: {fbxPath}");
            return false;
        }

        ModelImporterClipAnimation[] clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0)
        {
            clips = importer.defaultClipAnimations;
        }

        if (clips == null || clips.Length == 0)
        {
            Debug.LogWarning($"[FixDieAnimations] No animation clips found in {fbxPath}");
            return false;
        }

        bool changed = false;
        foreach (var clip in clips)
        {
            // Root Transform Position (Y) - Bake Into Pose MUST be true for death animations
            // so the falling animation doesn't get extracted as ignored root motion!
            if (!clip.lockRootHeightY)
            {
                clip.lockRootHeightY = true;
                changed = true;
            }

            // Based Upon: Original
            if (!clip.keepOriginalPositionY)
            {
                clip.keepOriginalPositionY = true;
                changed = true;
            }

            // Death animation should not loop
            if (clip.loopTime)
            {
                clip.loopTime = false;
                changed = true;
            }
        }

        if (changed || importer.clipAnimations == null || importer.clipAnimations.Length == 0)
        {
            importer.clipAnimations = clips;
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
            Debug.Log($"[FixDieAnimations] Updated and reimported {fbxPath}");
            return true;
        }

        return false;
    }
}

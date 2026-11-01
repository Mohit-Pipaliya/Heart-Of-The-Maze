using UnityEngine;
using UnityEditor;

[InitializeOnLoad]
public class FixDieAnimations
{
    static FixDieAnimations()
    {
        EditorApplication.delayCall += CheckAndFixDieAnimations;
    }

    [MenuItem("Tools/1-Click Fix/Fix Die & Crawl Floating In Air")]
    public static void CheckAndFixDieAnimations()
    {
        bool gunFixed = FixClip("Assets/Animations/Character /Player/Gun/Gun Die.fbx", false);
        bool normalFixed = FixClip("Assets/Animations/Character /Player/Normal/Normal Die.fbx", false);
        bool swordFixed = FixClip("Assets/Animations/Character /Player/Sword/Sword Die.fbx", false);
        bool zombieDieFixed = FixClip("Assets/Animations/Zombie/Female/Zombie Death.fbx", false);
        bool zombieCrawlFixed = FixClip("Assets/Animations/Zombie/Female/Zombie Crawl.fbx", true);

        if (gunFixed || normalFixed || swordFixed || zombieDieFixed || zombieCrawlFixed)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("<color=green><b>[FixDieAnimations]</b> Die aur Crawl/Slide animations successfully fix ho gaye hain! 'Bake Into Pose (Y)' enable kar diya gaya hai taaki character ya zombie hawa me na ude.</color>");
        }
    }

    private static bool FixClip(string fbxPath, bool shouldLoop = false)
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
            // Root Transform Position (Y) - Bake Into Pose MUST be true for death & crawl animations
            // so the falling/crawling animation doesn't get extracted as ignored root motion!
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

            // Loop setting
            if (clip.loopTime != shouldLoop)
            {
                clip.loopTime = shouldLoop;
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

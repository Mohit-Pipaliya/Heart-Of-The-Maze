#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.AI;

[InitializeOnLoad]
public static class SpiderTerrainFixer
{
    static SpiderTerrainFixer()
    {
        EditorApplication.delayCall += CheckAndFixSpider;
    }

    [MenuItem("Tools/Spider/Fix Spider Position & Snap To Terrain %#k")]
    public static void FixSpiderNow()
    {
        CheckAndFixSpider();
    }

    public static void CheckAndFixSpider()
    {
        SpiderEnemy spider = Object.FindFirstObjectByType<SpiderEnemy>();
        if (spider == null)
        {
            GameObject spiderObj = GameObject.Find("SpiderSkin1_1");
            if (spiderObj != null) spider = spiderObj.GetComponent<SpiderEnemy>();
        }

        if (spider == null)
        {
            Debug.LogWarning("[SpiderTerrainFixer] No SpiderEnemy or SpiderSkin1_1 found in active scene.");
            return;
        }

        Transform st = spider.transform;
        Vector3 curPos = st.position;

        // Try to find ground using Raycast from sky downwards
        float skyY = 300f;
        Vector3 rayStart = new Vector3(curPos.x, skyY, curPos.z);
        float bestGroundY = float.MinValue;
        bool foundGround = false;

        RaycastHit[] hits = Physics.RaycastAll(rayStart, Vector3.down, 500f, ~LayerMask.GetMask("Enemy", "Player", "Ignore Raycast"), QueryTriggerInteraction.Ignore);
        foreach (var h in hits)
        {
            if (h.transform == st || h.transform.IsChildOf(st)) continue;
            // We want the solid ground surface (terrain or ground collider)
            if (h.point.y > bestGroundY)
            {
                bestGroundY = h.point.y;
                foundGround = true;
            }
        }

        // Also check Terrain.activeTerrain if available
        Terrain terrain = Terrain.activeTerrain;
        if (terrain != null)
        {
            float tHeight = terrain.SampleHeight(curPos) + terrain.transform.position.y;
            if (tHeight > bestGroundY)
            {
                bestGroundY = tHeight;
                foundGround = true;
            }
        }

        // Also check NavMesh directly above/below
        if (NavMesh.SamplePosition(new Vector3(curPos.x, foundGround ? bestGroundY : 50f, curPos.z), out NavMeshHit navHit, 60f, NavMesh.AllAreas))
        {
            bestGroundY = navHit.position.y;
            foundGround = true;
        }

        if (foundGround)
        {
            Vector3 targetPos = new Vector3(curPos.x, bestGroundY, curPos.z);
            float diff = Mathf.Abs(curPos.y - bestGroundY);

            Debug.Log($"<color=#00FF88><b>[SpiderTerrainFixer]</b></color> Spider current pos: {curPos} | Detected terrain height: {bestGroundY:F2} | Difference: {diff:F2}m");

            if (diff > 0.2f)
            {
                Undo.RecordObject(st, "Snap Spider to Terrain");
                st.position = targetPos;
                EditorUtility.SetDirty(st);

                NavMeshAgent agent = spider.GetComponent<NavMeshAgent>();
                if (agent != null && Application.isPlaying && agent.isOnNavMesh)
                {
                    agent.Warp(targetPos);
                }

                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(spider.gameObject.scene);
                Debug.Log($"<color=#00FF88><b>[SpiderTerrainFixer] Successfully snapped Spider to terrain surface at Y = {bestGroundY:F2}!</b></color>");
            }
            else
            {
                Debug.Log($"[SpiderTerrainFixer] Spider is already at correct terrain height ({curPos.y:F2}).");
            }
        }
        else
        {
            Debug.LogWarning($"[SpiderTerrainFixer] Could not find ground surface above or below spider at ({curPos.x}, {curPos.z}).");
        }
    }
}
#endif

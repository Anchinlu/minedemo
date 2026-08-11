using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

namespace MineDemo.World
{
    public readonly struct WorldGenDebugResult
    {
        public readonly bool found;
        public readonly int worldX;
        public readonly int worldZ;
        public readonly WorldColumn column;

        public WorldGenDebugResult(bool found, int worldX, int worldZ, WorldColumn column)
        {
            this.found = found;
            this.worldX = worldX;
            this.worldZ = worldZ;
            this.column = column;
        }
    }

    public class WorldGenDebugTools : MonoBehaviour
    {
        public static WorldGenDebugTools Instance { get; private set; }
        
        public bool teleportToSearchResult = true;

        private Coroutine activeSearch;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Update()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (UnityEngine.InputSystem.Keyboard.current != null)
            {
                if (UnityEngine.InputSystem.Keyboard.current.f8Key.wasPressedThisFrame)
                {
                    StartSearch(MountainZone.Peak);
                }
                else if (UnityEngine.InputSystem.Keyboard.current.f7Key.wasPressedThisFrame)
                {
                    StartSearch(MountainZone.Slope);
                }
                else if (UnityEngine.InputSystem.Keyboard.current.f6Key.wasPressedThisFrame)
                {
                    StartSearch(MountainZone.Meadow);
                }
            }
#endif
        }

        private void StartSearch(MountainZone targetZone)
        {
            if (activeSearch != null)
            {
                Debug.LogWarning("[WorldGenDebug] Search already in progress!");
                return;
            }

            Transform player = null;
            WorldManager wm = FindFirstObjectByType<WorldManager>();
            if (wm != null && wm.player != null)
            {
                player = wm.player;
            }
            else
            {
                var playerObj = GameObject.FindWithTag("Player");
                if (playerObj != null) player = playerObj.transform;
            }

            if (player == null)
            {
                Debug.LogError("[WorldGenDebug] Cannot find player to originate search.");
                return;
            }

            int originX = Mathf.FloorToInt(player.position.x);
            int originZ = Mathf.FloorToInt(player.position.z);
            
            Debug.Log($"[WorldGenDebug] Starting search for {targetZone} around ({originX}, {originZ})...");

            activeSearch = StartCoroutine(FindNearestTerrainZone(
                originX, originZ, targetZone, 2000, 16, 512, 
                (result) => 
                {
                    activeSearch = null;
                    if (result.found)
                    {
                        Debug.Log($"[WorldGenDebug] {targetZone} found | Seed:{TerrainGenerator.Seed} | X:{result.worldX} | Z:{result.worldZ} | SurfaceY:{result.column.surfaceY} | Slope:{result.column.slope:F1} | Ridge:{result.column.noise.ridges:F2} | Erosion:{result.column.noise.erosion:F2} | PeakPotential:{result.column.noise.peakPotential:F2} | Jaggedness:{result.column.noise.jaggedness:F2} | IsoPeak:{MountainZoneResolver.GetIsolatedPeakWeight(result.column):F2}");
                        
                        if (teleportToSearchResult && player != null)
                        {
                            var cc = player.GetComponent<CharacterController>();
                            if (cc != null) cc.enabled = false;
                            
                            player.position = new Vector3(result.worldX + 0.5f, result.column.surfaceY + 3f, result.worldZ + 0.5f);
                            
                            if (cc != null) cc.enabled = true;
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"[WorldGenDebug] Could not find {targetZone} within radius.");
                    }
                }
            ));
        }

        public IEnumerator FindNearestTerrainZone(
            int originX,
            int originZ,
            MountainZone targetZone,
            int searchRadius,
            int step,
            int samplesPerFrame,
            Action<WorldGenDebugResult> onComplete)
        {
            int maxRadiusSteps = searchRadius / step;
            int samplesThisFrame = 0;
            
            WorldGenDebugResult bestCandidate = new WorldGenDebugResult(false, 0, 0, default);
            float bestCandidateScore = -1f;

            // Spiral search pattern
            int x = 0, z = 0, dx = 0, dz = -1;
            int t = Mathf.Max(maxRadiusSteps * 2, 1);
            int maxI = t * t;

            for (int i = 0; i < maxI; i++)
            {
                if (-maxRadiusSteps <= x && x <= maxRadiusSteps && -maxRadiusSteps <= z && z <= maxRadiusSteps)
                {
                    int sampleX = originX + x * step;
                    int sampleZ = originZ + z * step;
                    
                    WorldColumn col = TerrainGenerator.GetWorldColumn(sampleX, sampleZ);
                    
                    bool exactMatch = false;
                    
                    if (targetZone == MountainZone.Peak)
                    {
                        if (col.mountainZone == MountainZone.Peak && col.surfaceY >= 160 && MountainZoneResolver.GetIsolatedPeakWeight(col) > 0.4f)
                        {
                            exactMatch = true;
                        }
                        else
                        {
                            // Track best candidate if exact match not found
                            float score = MountainZoneResolver.GetIsolatedPeakWeight(col);
                            if (score > bestCandidateScore)
                            {
                                bestCandidateScore = score;
                                bestCandidate = new WorldGenDebugResult(true, sampleX, sampleZ, col);
                            }
                        }
                    }
                    else
                    {
                        if (col.mountainZone == targetZone)
                        {
                            exactMatch = true;
                        }
                    }

                    if (exactMatch)
                    {
                        onComplete?.Invoke(new WorldGenDebugResult(true, sampleX, sampleZ, col));
                        yield break;
                    }
                    
                    samplesThisFrame++;
                    if (samplesThisFrame >= samplesPerFrame)
                    {
                        samplesThisFrame = 0;
                        yield return null; // Wait for next frame
                    }
                }

                if (x == z || (x < 0 && x == -z) || (x > 0 && x == 1 - z))
                {
                    t = dx;
                    dx = -dz;
                    dz = t;
                }
                x += dx;
                z += dz;
            }

            if (bestCandidate.found)
            {
                Debug.LogWarning("[WorldGenDebug] Exact criteria not fully met, returning best candidate.");
                onComplete?.Invoke(bestCandidate);
            }
            else
            {
                onComplete?.Invoke(new WorldGenDebugResult(false, 0, 0, default));
            }
        }
    }
}

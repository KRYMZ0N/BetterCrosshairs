using UnityEngine;
using UnityEngine.UI;
using System.Data;
using System.IO;
using System.Collections.Generic;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.PlayerScripts;
using System.Reflection;
using Il2CppFishNet;

using System.Runtime.InteropServices;
using MelonLoader;
using Il2Cpp;


[assembly: MelonInfo(typeof(BetterCrosshairs.Main), "Better Crosshairs", "0.0.1", "KRYMZ0N")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace BetterCrosshairs;
public class Main : MelonMod {

    [DllImport("CrosshairMath", CallingConvention = CallingConvention.Cdecl)]
    public static extern float UpdateCrosshairPhysics(float speedXZ, float speedY, [MarshalAs(UnmanagedType.I1)] bool isFiring, [MarshalAs(UnmanagedType.I1)] bool isADS, float deltaTime);

    private Texture2D? _whiteTexture;
    private float _dynamicSpread = 0f;
    private bool _shouldDraw = false;
    
    // Cached references to completely eliminate garbage collection and loop lag
    private Player? _cachedPlayer;
    private Vector3 _lastPlayerPosition;
    
    private const float CrosshairLength = 12f;
    private const float CrosshairThickness = 2f;
    private const float BaseGap = 6f;
    private List<GameObject> _suppressedHUD = new List<GameObject>();
private bool _hasScannedForHUD = false;

    private Texture2D ReticleTexture {
        get {
            if (_whiteTexture == null) {
                _whiteTexture = Texture2D.whiteTexture;
            }
            return _whiteTexture;
        }
    }
    // private void DisableDefaultHUD() {
    //     // Targets the primary structural UI container revealed by console dump
    //     GameObject defaultCrosshairContainer = GameObject.Find("Crosshair");
    //     if (defaultCrosshairContainer != null) {
    //         defaultCrosshairContainer.SetActive(false);
    //         _defaultCrosshairHidden = true;
    //         LoggerInstance.Msg("Successfully disabled default 'Crosshair' UI hierarchy.");
    //         return;
    //     }

    //     // Fail-safe redundancy targeting standalone reticle objects
    //     GameObject standaloneReticle = GameObject.Find("Reticle");
    //     if (standaloneReticle != null) {
    //         standaloneReticle.SetActive(false);
    //         _defaultCrosshairHidden = true;
    //         LoggerInstance.Msg("Successfully disabled standalone 'Reticle' UI asset.");
    //     }
    // }

    private void FindAndCacheHUD() {
        // FindObjectsOfTypeAll searches EVERYTHING, including hidden/inactive objects
        var allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
        
        foreach (var obj in allObjects) {
            // Target the exact names from your console dump
            if (obj.name == "Crosshair" || obj.name == "Reticle") {
                
                // hideFlags check ensures we only grab the actual in-scene UI, not the raw Prefab template
                if (obj.hideFlags == HideFlags.None) {
                    _suppressedHUD.Add(obj);
                    obj.SetActive(false);
                    LoggerInstance.Msg($"[SUPPRESSOR] Found and locked target: {obj.name}");
                }
            }
        }
        
        _hasScannedForHUD = true; // Lock the scan so it only runs once per map
    }
    public override void OnInitializeMelon() {
        ExtractNativeDll();

        LoggerInstance.Msg("Crosshair Intitialized!");
    }

    // OnUpdate runs exactly ONCE per frame loop. Perfect for physics calculations.
    public override void OnUpdate() {
        var playerInv = PlayerSingleton<PlayerInventory>.Instance;
        if (playerInv == null) {
            _shouldDraw = false;
            _cachedPlayer = null; 
            _hasScannedForHUD = false; 
        _suppressedHUD.Clear(); // Clear the list when leaving a match
            return;
        }

        _shouldDraw = true;

        if (_cachedPlayer == null) {
            var allPlayers = UnityEngine.Object.FindObjectsOfType<Player>();
            foreach (var player in allPlayers) {
                if (player.IsOwner) { 
                    _cachedPlayer = player;
                    _lastPlayerPosition = _cachedPlayer.transform.position;
                    break; 
                }
            }
        }
        
        // 1. Run the deep memory scan ONCE when you load in
        if (!_hasScannedForHUD && _cachedPlayer != null && _cachedPlayer.transform.position != Vector3.zero) {
            FindAndCacheHUD();
        }

        // 2. THE SUPPRESSOR: Constantly force the UI off if the game tries to turn it on during ADS
        foreach (var uiElement in _suppressedHUD) {
        if (uiElement != null) {
            // Target the CanvasRenderer to stop it from drawing to the screen
            var renderer = uiElement.GetComponent<CanvasRenderer>();
            if (renderer != null && renderer.cull == false) {
                renderer.cull = true; // Tells Unity's UI system to skip rendering this object
            }
        }
    }

        float speedXZ = 0.0f;
        float speedY = 0.0f;

        if (_cachedPlayer != null) {
            Vector3 currentPosition = _cachedPlayer.transform.position;
            
            if (Time.deltaTime > 0f) {
                // Isolate horizontal strafing/walking
                Vector3 currentXZ = new Vector3(currentPosition.x, 0, currentPosition.z);
                Vector3 lastXZ = new Vector3(_lastPlayerPosition.x, 0, _lastPlayerPosition.z);
                speedXZ = Vector3.Distance(currentXZ, lastXZ) / Time.deltaTime;

                // Isolate vertical jumping/falling
                speedY = Mathf.Abs(currentPosition.y - _lastPlayerPosition.y) / Time.deltaTime;
            }
            
            _lastPlayerPosition = currentPosition;
        }

        bool isPlayerFiring = Input.GetMouseButton(0); // Left Click
        bool isADS = Input.GetMouseButton(1);          // Right Click (ADS)
        
        // Feed the split directional data and ADS state into the physics simulation
        _dynamicSpread = UpdateCrosshairPhysics(speedXZ, speedY, isPlayerFiring, isADS, Time.deltaTime);
    }



    public override void OnGUI() {
        // Pure rendering pass. No physics calculations, no component hunting.
        if (!_shouldDraw) return;

        GUI.depth = 0;

        float width = Screen.width > 0 ? Screen.width : 1920f;
        float height = Screen.height > 0 ? Screen.height : 1080f;

        float centerX = width / 2f;
        float centerY = height / 2f;
        
        // Use the smooth value updated from the update loop
        float totalGap = BaseGap + _dynamicSpread;

        GUI.color = Color.green;

        // Draw reticle
        GUI.DrawTexture(new Rect(centerX - totalGap - CrosshairLength, centerY - (CrosshairThickness / 2f), CrosshairLength, CrosshairThickness), ReticleTexture);
        GUI.DrawTexture(new Rect(centerX + totalGap, centerY - (CrosshairThickness / 2f), CrosshairLength, CrosshairThickness), ReticleTexture);
        GUI.DrawTexture(new Rect(centerX - (CrosshairThickness / 2f), centerY - totalGap - CrosshairLength, CrosshairThickness, CrosshairLength), ReticleTexture);
        GUI.DrawTexture(new Rect(centerX - (CrosshairThickness / 2f), centerY + totalGap, CrosshairThickness, CrosshairLength), ReticleTexture);
        
        GUI.color = Color.white;
    }

        private void ExtractNativeDll() {
        string targetPath = Path.Combine(Directory.GetCurrentDirectory(), "CrosshairMath.dll");
        var assembly = Assembly.GetExecutingAssembly();
        string resourceName = "BetterCrosshairs.CrosshairMath.dll";

        try {
            using (Stream? stream = assembly.GetManifestResourceStream(resourceName)) {
                if (stream == null) {
                    LoggerInstance.Error($"Failed to find embedded resource: {resourceName}");
                    return;
                }

                using (FileStream fileStream = new FileStream(targetPath, FileMode.Create, FileAccess.Write)) {
                    stream.CopyTo(fileStream);
                }
                LoggerInstance.Msg("Successfully extracted updated CrosshairMath.dll.");
            }
        } 
        catch (System.IO.IOException ex) {
            LoggerInstance.Error($"DLL is locked by Windows! Close the game and manually delete CrosshairMath.dll. Error: {ex.Message}");
        }
    }
}


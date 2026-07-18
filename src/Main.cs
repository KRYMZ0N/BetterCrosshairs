using UnityEngine;
using UnityEngine.UI;
using System.IO;
using System.Collections.Generic;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.PlayerScripts;
using System.Reflection;
using Il2CppFishNet;
using System.Runtime.InteropServices;
using MelonLoader;
using Il2Cpp;
// Cleaner

[assembly: MelonInfo(typeof(BetterCrosshairs.Main), "Better Crosshairs", "0.1.0", "KRYMZ0N")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace BetterCrosshairs;
public class Main : MelonMod {

    [DllImport("CrosshairMath", CallingConvention = CallingConvention.Cdecl)]
    public static extern float UpdateCrosshairPhysics(float speedXZ, float speedY, [MarshalAs(UnmanagedType.I1)] bool isFiring, [MarshalAs(UnmanagedType.I1)] bool isADS, float deltaTime);

    // Global Configuration hooks accessed by CrosshairMenu
    public static MelonPreferences_Category CrosshairCategory = null!;
    public static MelonPreferences_Entry<float> ConfigBaseGap = null!;
    public static MelonPreferences_Entry<float> ConfigLength = null!;
    public static MelonPreferences_Entry<float> ConfigThickness = null!;
    public static MelonPreferences_Entry<string> ConfigColorHex = null!;

    // Instantiating our separate interface handler class
    private CrosshairMenu _menu = new CrosshairMenu();

    private Texture2D? _whiteTexture;
    public Color CurrentColor { get; private set; } = Color.green;
    private Color _cachedCrosshairColor = Color.green;
    private float _dynamicSpread = 0f;
    private float _playerSearchTimer = 0f;
    private bool _hasAttemptedADSSearch = false;
    private bool _shouldDraw = false;
    
    // Cached references to completely eliminate garbage collection and loop lag
    private Player? _cachedPlayer;
    private Vector3 _lastPlayerPosition;
    
    private List<GameObject> _suppressedHUD = new List<GameObject>();

    private Texture2D ReticleTexture {
        get {
            if (_whiteTexture == null) {
                _whiteTexture = Texture2D.whiteTexture;
            }
            return _whiteTexture;
        }
    }
    public static Main Instance { get; private set; } = null!;
    public override void OnInitializeMelon() {

        Instance = this;
        ExtractNativeDll();

        CrosshairCategory = MelonPreferences.CreateCategory("BetterCrosshairs", "Better Crosshairs Settings");
        ConfigBaseGap = CrosshairCategory.CreateEntry("BaseGap", 6f, "Base Crosshair Gap");
        ConfigLength = CrosshairCategory.CreateEntry("LineLength", 12f, "Crosshair Line Length");
        ConfigThickness = CrosshairCategory.CreateEntry("LineThickness", 2f, "Crosshair Line Thickness");
        ConfigColorHex = CrosshairCategory.CreateEntry("CrosshairColor", "#00FF00", "Crosshair Hex Color");
        
        // Cache the color immediately on startup so OnGUI doesn't have to parse strings
        if (ColorUtility.TryParseHtmlString(ConfigColorHex.Value, out Color savedColor)) {
            UpdateColorCache(savedColor);
        }

        MelonEvents.OnUpdate.Subscribe(GlobalInputCheck, 0);

        LoggerInstance.Msg("Crosshair Intitialized!");
    }

    // Ensure this method accepts Color, NOT string
    public void UpdateColorCache(Color color) {
        CurrentColor = color;
        _cachedCrosshairColor = color;
        // Sync with your config file
        ConfigColorHex.Value = "#" + ColorUtility.ToHtmlStringRGB(color);
    }

    // OnUpdate runs exactly ONCE per frame loop. Perfect for physics calculations.
    private void GlobalInputCheck() {
        // Only allow F10 to register if the player is actively loaded in the match
        if (_shouldDraw && Input.GetKeyDown(KeyCode.F10)) {
            _menu.ToggleMenu();
        }
    }

    public override void OnUpdate() {

        // 1. THE THROTTLED SEARCH
        // If we don't have the player, only scan the hierarchy ONCE per second, not 60x a second.
        if (_cachedPlayer == null) {
            _playerSearchTimer += Time.deltaTime;
            
            if (_playerSearchTimer > 1.0f) {
                // Move the expensive singleton check into the throttled loop
                var playerInv = PlayerSingleton<PlayerInventory>.Instance;
                if (playerInv == null) {
                    _shouldDraw = false;
                    _menu.ForceClose(); 
                    _hasAttemptedADSSearch = false; 
                    return;
                }

                var allPlayers = UnityEngine.Object.FindObjectsOfType<Player>();
                foreach (var player in allPlayers) {
                    if (player.IsOwner) { 
                        _cachedPlayer = player;
                        _lastPlayerPosition = _cachedPlayer.transform.position;
                        break; 
                    }
                }
                _playerSearchTimer = 0f;
            }
            
            if (_cachedPlayer == null) {
                _shouldDraw = false;
                return;
            }
        }

        _shouldDraw = true;

        // Get inputs early
        bool isPlayerFiring = Input.GetMouseButton(0); 
        bool isADS = Input.GetMouseButton(1); 

        // THE ON-DEMAND ADS SNIPER
        if (!_hasAttemptedADSSearch && isADS && _cachedPlayer != null && _cachedPlayer.transform.position != Vector3.zero) {
            CrosshairMenu.FindHudNetworkSafe(_suppressedHUD);
            _hasAttemptedADSSearch = true; 
        }

        // 2. DELEGATE-FREE CULLING
        // We only call RemoveAll if we actually detect a destroyed UI element in the standard loop
        bool needsCleanup = false;
        
        for (int i = 0; i < _suppressedHUD.Count; i++) {
            GameObject uiElement = _suppressedHUD[i];
            
            if (uiElement == null) {
                needsCleanup = true;
            } 
            else if (uiElement.transform.localScale != Vector3.zero) {
                uiElement.transform.localScale = Vector3.zero;
            }
        }

        // Only allocate the lambda delegate if absolutely necessary
        if (needsCleanup) {
            _suppressedHUD.RemoveAll(item => item == null);
        }

        float speedXZ = 0.0f;
        float speedY = 0.0f;

        if (_cachedPlayer != null) {
            Vector3 currentPosition = _cachedPlayer.transform.position;
            
            if (Time.deltaTime > 0f) {
                Vector3 currentXZ = new Vector3(currentPosition.x, 0, currentPosition.z);
                Vector3 lastXZ = new Vector3(_lastPlayerPosition.x, 0, _lastPlayerPosition.z);
                speedXZ = Vector3.Distance(currentXZ, lastXZ) / Time.deltaTime;
                speedY = Mathf.Abs(currentPosition.y - _lastPlayerPosition.y) / Time.deltaTime;
            }
            
            _lastPlayerPosition = currentPosition;
        }
        
        _dynamicSpread = UpdateCrosshairPhysics(speedXZ, speedY, isPlayerFiring, isADS, Time.deltaTime);
    }



    public override void OnGUI() {
        // 1. Delegate menu rendering duties immediately to our sub-class engine
        // (This uses the cached delegate we fixed in CrosshairMenu.cs)
        _menu.Draw();

        // 2. Bail out early if we don't have a valid player/session
        if (!_shouldDraw) return;

        GUI.depth = 0;

        float width = Screen.width > 0 ? Screen.width : 1920f;
        float height = Screen.height > 0 ? Screen.height : 1080f;

        float centerX = width / 2f;
        float centerY = height / 2f;
        
        // Dynamically scale parameters using the configuration variables updated by the menu sliders
        float totalGap = ConfigBaseGap.Value + _dynamicSpread;
        float currentLength = ConfigLength.Value;
        float currentThickness = ConfigThickness.Value;

        // 3. Apply the cached color (Zero allocations!)
        GUI.color = _cachedCrosshairColor;

        // 4. Draw reticle lines using configurable values
        GUI.DrawTexture(new Rect(centerX - totalGap - currentLength, centerY - (currentThickness / 2f), currentLength, currentThickness), ReticleTexture);
        GUI.DrawTexture(new Rect(centerX + totalGap, centerY - (currentThickness / 2f), currentLength, currentThickness), ReticleTexture);
        GUI.DrawTexture(new Rect(centerX - (currentThickness / 2f), centerY - totalGap - currentLength, currentThickness, currentLength), ReticleTexture);
        GUI.DrawTexture(new Rect(centerX - (currentThickness / 2f), centerY + totalGap, currentThickness, currentLength), ReticleTexture);
        
        // 5. Reset color to white to prevent tinting other UI elements
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


using UnityEngine;
using MelonLoader;
using System.Collections.Generic;
using System;
using Il2CppInterop.Runtime; // Required for DelegateSupport

namespace BetterCrosshairs;

public class CrosshairMenu {
    
    private bool _isOpen = false;
    private Rect _windowRect = new Rect(100, 100, 300, 350);
    private Action<int> _managedDrawAction = null!; 
    private GUI.WindowFunction? _drawMenuDelegate; // Make it nullable

    public CrosshairMenu() {
        _managedDrawAction = DrawMenuContents;
        
        // FIX: Compile the Il2Cpp unmanaged-to-managed thunk immediately on startup.
        // This takes the performance hit during the loading screen instead of when pressing F10.
        _drawMenuDelegate = DelegateSupport.ConvertDelegate<GUI.WindowFunction>(_managedDrawAction);
    }

    public void ToggleMenu() {
        _isOpen = !_isOpen;
    }

    public void ForceClose() {
        _isOpen = false;
    }

    public void Draw() {
        if (!_isOpen) return;   
        _windowRect = GUI.Window(0, _windowRect, _drawMenuDelegate, "Better Crosshairs Setup");
    }

    private void DrawMenuContents(int windowID) {
        GUILayout.BeginVertical();
        GUILayout.Space(10);

        GUILayout.Label($"Base Center Gap: {Main.ConfigBaseGap.Value:F1}");
        Main.ConfigBaseGap.Value = GUILayout.HorizontalSlider(Main.ConfigBaseGap.Value, 0f, 50f);

        GUILayout.Space(10);

        GUILayout.Label($"Line Length: {Main.ConfigLength.Value:F1}");
        Main.ConfigLength.Value = GUILayout.HorizontalSlider(Main.ConfigLength.Value, 1f, 100f);

        GUILayout.Space(10);

        GUILayout.Label($"Line Thickness: {Main.ConfigThickness.Value:F1}");
        Main.ConfigThickness.Value = GUILayout.HorizontalSlider(Main.ConfigThickness.Value, 1f, 20f);

        GUILayout.Space(15);

        GUILayout.Space(10);
    
        GUILayout.Label("Red");
        float r = GUILayout.HorizontalSlider(Main.Instance.CurrentColor.r, 0f, 1f);
        
        GUILayout.Label("Green");
        float g = GUILayout.HorizontalSlider(Main.Instance.CurrentColor.g, 0f, 1f);
        
        GUILayout.Label("Blue");
        float b = GUILayout.HorizontalSlider(Main.Instance.CurrentColor.b, 0f, 1f);

        Color newColor = new Color(r, g, b);
        
        // Update live so they see the change as they slide
        if (newColor != Main.Instance.CurrentColor) {
            Main.Instance.UpdateColorCache(newColor);
        }

        if (GUILayout.Button("Reset to Green")) {
            Color green = Color.green;
            Main.Instance.UpdateColorCache(green);
            
            // Convert the Color back to a Hex string to update the text field
            Main.ConfigColorHex.Value = "#" + ColorUtility.ToHtmlStringRGB(green);
            Main.CrosshairCategory.SaveToFile();
        }

        if (GUILayout.Button("Save Configurations")) {
            Main.CrosshairCategory.SaveToFile();
            MelonLogger.Msg("Saved settings changes safely to configuration file.");
            _isOpen = false;
        }

        GUILayout.EndVertical();
        GUI.DragWindow(new Rect(0, 0, 10000, 20));
    }

    public static void FindHudNetworkSafe(List<GameObject> targetList) {
        targetList.Clear();
        
        // FIX: Replaced Resources.FindObjectsOfTypeAll with UnityEngine.Object.FindObjectsOfType
        // Passing 'true' includes inactive objects without dumping the entire internal engine heap.
        var allObjects = UnityEngine.Object.FindObjectsOfType<GameObject>(true);
        
        foreach (var obj in allObjects) {
            if (obj.hideFlags != HideFlags.None) continue;
            
            string n = obj.name; 
            if (n == "Crosshair" || n == "Reticle") {
                targetList.Add(obj);
                obj.transform.localScale = Vector3.zero;
            }
        }
    }
}
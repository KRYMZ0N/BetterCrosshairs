using UnityEngine;
using MelonLoader;

namespace BetterCrosshairs;

public class CrosshairMenu
{
    private bool _isOpen = false;
    private Rect _windowRect = new Rect(100, 100, 300, 250); // X, Y, W, H

    public void ToggleMenu() {
        _isOpen = !_isOpen;
    }

    public void Draw() {
        if (!_isOpen) return;

        _windowRect = GUI.Window(0, _windowRect, (GUI.WindowFunction)DrawMenuContents, "Better Crosshairs Setup");
    }

    private void DrawMenuContents(int windowID) {
        GUILayout.BeginVertical();
        GUILayout.Space(10);

        // Gap Slider
        GUILayout.Label($"Base Center Gap: {Main.ConfigBaseGap.Value:F1}");
        Main.ConfigBaseGap.Value = GUILayout.HorizontalSlider(Main.ConfigBaseGap.Value, 0f, 50f);

        GUILayout.Space(10);

        // Length Slider
        GUILayout.Label($"Line Length: {Main.ConfigLength.Value:F1}");
        Main.ConfigLength.Value = GUILayout.HorizontalSlider(Main.ConfigLength.Value, 1f, 100f);

        GUILayout.Space(10);

        // Thickness Slider
        GUILayout.Label($"Line Thickness: {Main.ConfigThickness.Value:F1}");
        Main.ConfigThickness.Value = GUILayout.HorizontalSlider(Main.ConfigThickness.Value, 1f, 20f);

        GUILayout.Space(15);

        // Save Button
        if (GUILayout.Button("Save Configurations")) {
            Main.CrosshairCategory.SaveToFile();
            MelonLogger.Msg("Saved settings changes safely to configuration file.");
            _isOpen = false;
        }

        GUILayout.EndVertical();

        // Allows user to click and drag the top bar of window anywhere on screen
        GUI.DragWindow(new Rect(0, 0, 10000, 20));
    }
}
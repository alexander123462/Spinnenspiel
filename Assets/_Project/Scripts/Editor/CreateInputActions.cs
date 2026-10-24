using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Spinnenspiel.EditorTools
{
    /// <summary>B9: .inputactions-Asset "SpiderControls" via Setup-API; Unity generates the wrapper class at import.</summary>
    public static class CreateInputActions
    {
        private const string AssetPath = "Assets/_Project/Art/Input/SpiderControls.inputactions";

        public static void Build()
        {
            // Asset in memory via Setup-API, then serialize to JSON source file.
            InputActionAsset asset = ScriptableObject.CreateInstance<InputActionAsset>();
            InputActionMap map = InputActionSetupExtensions.AddActionMap(asset, "Default");

            // Move: WASD-2DVector + Gamepad-Stick (StickDeadzone on the stick bindings).
            InputAction move = EnsureAction(map, "Move", InputActionType.Value);
            InputActionSetupExtensions.AddCompositeBinding(move, "2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            move.AddBinding("<Gamepad>/leftStick/up", processors: "stickDeadzone(min=0.1)");
            move.AddBinding("<Gamepad>/leftStick/down", processors: "stickDeadzone(min=0.1)");
            move.AddBinding("<Gamepad>/leftStick/left", processors: "stickDeadzone(min=0.1)");
            move.AddBinding("<Gamepad>/leftStick/right", processors: "stickDeadzone(min=0.1)");

            // Look: Mouse-Delta + right stick with StickDeadzone.
            InputAction look = EnsureAction(map, "Look", InputActionType.Value);
            look.AddBinding("<Mouse>/delta");
            look.AddBinding("<Gamepad>/rightStick", processors: "stickDeadzone(min=0.1)");

            // Sprint
            InputAction sprint = EnsureAction(map, "Sprint", InputActionType.Button);
            sprint.AddBinding("<Keyboard>/leftShift");
            sprint.AddBinding("<Gamepad>/leftStickPress");

            // Zoom
            InputAction zoom = EnsureAction(map, "Zoom", InputActionType.Value);
            zoom.AddBinding("<Mouse>/scroll/y");

            // Silk
            InputAction silk = EnsureAction(map, "Silk", InputActionType.Button);
            silk.AddBinding("<Keyboard>/f");
            silk.AddBinding("<Mouse>/rightButton");
            silk.AddBinding("<Gamepad>/rightShoulder");

            // Release
            InputAction release = EnsureAction(map, "Release", InputActionType.Button);
            release.AddBinding("<Keyboard>/space");
            release.AddBinding("<Gamepad>/buttonSouth");

            // Serialize to the .inputactions source file (Unity importer reads it).
            string json = asset.ToJson();
            AssetUtil.EnsureFolder("Assets/_Project/Art/Input");
            if (System.IO.File.Exists(AssetPath))
            {
                Debug.Log("[Input] SpiderControls.inputactions already exists – rebuilding identical.");
            }
            System.IO.File.WriteAllText(AssetPath, json);
            AssetDatabase.ImportAsset(AssetPath, ImportAssetOptions.ForceUpdate);

            // Generate the wrapper class next to SpiderInput (Runtime asmdef).
            UnityEngine.InputSystem.Editor.InputActionCodeGenerator.Options options = default;
            options.className = "SpiderControls";
            options.namespaceName = "Spinnenspiel";
            options.sourceAssetPath = AssetPath;
            string wrapper = UnityEngine.InputSystem.Editor.InputActionCodeGenerator.GenerateWrapperCode(asset, options);
            File.WriteAllText("Assets/_Project/Scripts/Runtime/Input/SpiderControls.cs", wrapper);

            Debug.Log("[Input] SpiderControls.inputactions + generated class written.");
        }

        private static InputAction EnsureAction(InputActionMap map, string name, InputActionType type)
        {
            foreach (InputAction action in map.actions)
            {
                if (action.name == name)
                {
                    return action;
                }
            }
            return InputActionSetupExtensions.AddAction(map, name, type: type);
        }
    }
}

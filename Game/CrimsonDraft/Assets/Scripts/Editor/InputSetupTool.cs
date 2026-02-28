#nullable enable

using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CrimsonDraft.Editor
{
    public static class InputSetupTool
    {
        private const string InputFolder = "Assets/Input";
        private const string AssetPath = "Assets/Input/CrimsonDraftControls.inputactions";

        [MenuItem("CrimsonDraft/Input/Create Input Actions", priority = 30)]
        public static void CreateInputActions()
        {
            Directory.CreateDirectory(InputFolder);

            var asset = ScriptableObject.CreateInstance<InputActionAsset>();

            BuildGameplayMap(asset);
            BuildCombatMap(asset);
            BuildUIMap(asset);

            File.WriteAllText(AssetPath, asset.ToJson());
            Object.DestroyImmediate(asset);

            AssetDatabase.Refresh();

            var imported = AssetDatabase.LoadAssetAtPath<InputActionAsset>(AssetPath);
            EditorGUIUtility.PingObject(imported);
            Selection.activeObject = imported;

            Debug.Log($"[InputSetupTool] Created {AssetPath}. Assign it to GameLifetimeScope in the Inspector.");
        }

        private static void BuildGameplayMap(InputActionAsset asset)
        {
            var map = asset.AddActionMap("Gameplay");

            var move = map.AddAction("Move", InputActionType.Value, expectedControlLayout: "Vector2");
            AddWasdArrowsGamepadToMove(move);

            var interact = map.AddAction("Interact", InputActionType.Button);
            interact.AddBinding("<Keyboard>/f");
            interact.AddBinding("<Keyboard>/e");
            interact.AddBinding("<Gamepad>/buttonSouth");

            var inventory = map.AddAction("OpenInventory", InputActionType.Button);
            inventory.AddBinding("<Keyboard>/tab");
            inventory.AddBinding("<Gamepad>/select");

            var pause = map.AddAction("Pause", InputActionType.Button);
            pause.AddBinding("<Keyboard>/escape");
            pause.AddBinding("<Gamepad>/start");
        }

        private static void BuildCombatMap(InputActionAsset asset)
        {
            var map = asset.AddActionMap("Combat");

            var navigate = map.AddAction("Navigate", InputActionType.Value, expectedControlLayout: "Vector2");
            AddArrowsAndGamepadToNavigate(navigate);

            var confirm = map.AddAction("Confirm", InputActionType.Button);
            confirm.AddBinding("<Keyboard>/space");
            confirm.AddBinding("<Keyboard>/enter");
            confirm.AddBinding("<Gamepad>/buttonSouth");

            var cancel = map.AddAction("Cancel", InputActionType.Button);
            cancel.AddBinding("<Keyboard>/escape");
            cancel.AddBinding("<Keyboard>/backspace");
            cancel.AddBinding("<Gamepad>/buttonEast");

            var useItem = map.AddAction("UseItem", InputActionType.Button);
            useItem.AddBinding("<Keyboard>/q");
            useItem.AddBinding("<Gamepad>/buttonWest");
        }

        private static void BuildUIMap(InputActionAsset asset)
        {
            var map = asset.AddActionMap("UI");

            var navigate = map.AddAction("Navigate", InputActionType.Value, expectedControlLayout: "Vector2");
            AddArrowsAndGamepadToNavigate(navigate);

            var submit = map.AddAction("Submit", InputActionType.Button);
            submit.AddBinding("<Keyboard>/enter");
            submit.AddBinding("<Keyboard>/space");
            submit.AddBinding("<Gamepad>/buttonSouth");

            var cancel = map.AddAction("Cancel", InputActionType.Button);
            cancel.AddBinding("<Keyboard>/escape");
            cancel.AddBinding("<Gamepad>/buttonEast");
        }

        private static void AddWasdArrowsGamepadToMove(InputAction action)
        {
            action.AddCompositeBinding("2DVector")
                .With("Up",    "<Keyboard>/w")
                .With("Down",  "<Keyboard>/s")
                .With("Left",  "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");

            action.AddCompositeBinding("2DVector")
                .With("Up",    "<Keyboard>/upArrow")
                .With("Down",  "<Keyboard>/downArrow")
                .With("Left",  "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/rightArrow");

            action.AddBinding("<Gamepad>/leftStick");
            action.AddBinding("<Gamepad>/dpad");
        }

        private static void AddArrowsAndGamepadToNavigate(InputAction action)
        {
            action.AddCompositeBinding("2DVector")
                .With("Up",    "<Keyboard>/upArrow")
                .With("Down",  "<Keyboard>/downArrow")
                .With("Left",  "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/rightArrow");

            action.AddBinding("<Gamepad>/dpad");
            action.AddBinding("<Gamepad>/leftStick");
        }
    }
}

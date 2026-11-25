using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Backrooms.Player
{
    /// Every input action in one place, so the controls screen can rebind them. Movement lives in the project's
    /// input asset (Player map); the tools' actions are made here. Overrides are saved to bindings.json.
    public static class Controls
    {
        public static readonly InputAction Flashlight = Make("Flashlight", "<Keyboard>/f", "<Gamepad>/buttonNorth");
        public static readonly InputAction Camcorder = Make("Camcorder", "<Mouse>/rightButton", "<Gamepad>/leftTrigger");
        public static readonly InputAction Bag = Make("Bag", "<Keyboard>/tab", "<Gamepad>/select");
        public static readonly InputAction Interact = Make("Interact", "<Keyboard>/e", "<Gamepad>/buttonWest");
        public static readonly InputAction Logbook = Make("Logbook", "<Keyboard>/j", "<Gamepad>/dpad/up");
        public static readonly InputAction Chalk = Make("Chalk", "<Keyboard>/g", "<Gamepad>/dpad/down");   // Level 6: mark the wall
        public static readonly InputAction ThrowLight = Make("Throw glowstick", "<Keyboard>/q", "<Gamepad>/rightShoulder");
        public static readonly InputAction ThrowBottle = Make("Throw Exterminator", "<Keyboard>/x", "<Gamepad>/leftShoulder");
        public static readonly InputAction FirstAid = Make("Use first-aid kit", "<Keyboard>/h", "<Gamepad>/rightStickPress");
        public static readonly InputAction Repellent = Make("Spray moth repellent", "<Keyboard>/z", "<Gamepad>/dpad/right");
        public static readonly InputAction Flare = Make("Throw flare", "<Keyboard>/t");   // on a pad: the Exterminator button, when you have none of those
        public static readonly InputAction SetDown = Make("Set down almond water", "<Keyboard>/b", "<Gamepad>/dpad/left");
        public static readonly InputAction DrinkPad = Make("Drink", "<Gamepad>/buttonWest");
        public static readonly InputAction BatteryPad = Make("Battery", "<Gamepad>/buttonNorth");
        static readonly InputAction[] Tools = { Flashlight, Camcorder, Bag, Interact, Logbook, Chalk, ThrowLight, ThrowBottle, FirstAid, Repellent, SetDown, Flare };

        /// Turns every tool action on. The editor here enters Play mode without reloading scripts, and stopping Play
        /// switches code-made actions off, so they're switched back on at the start of every session.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void EnableAll()
        {
            foreach (var a in new[] { Flashlight, Camcorder, Bag, Interact, Logbook, Chalk, ThrowLight, ThrowBottle, FirstAid, Repellent, SetDown, Flare, DrinkPad, BatteryPad })
                if (!a.enabled) a.Enable();
        }

        static InputAction Make(string name, params string[] bindings)
        {
            var a = new InputAction(name, InputActionType.Button);
            foreach (var b in bindings) a.AddBinding(b);
            a.Enable();
            return a;
        }

        public struct Entry
        {
            public string Label;
            public InputAction Action;
            public int Binding;
            public string Shown => Action.GetBindingDisplayString(Binding);
        }

        /// The keyboard and mouse bindings a player can change. shortcut: gamepad bindings aren't rebindable yet.
        public static List<Entry> Rebindable()
        {
            var list = new List<Entry>();
            var map = InputSystem.actions.FindActionMap("Player", true);
            var move = map.FindAction("Move", true);
            foreach (var (part, label) in new[] { ("up", "Move forward"), ("down", "Move back"), ("left", "Move left"), ("right", "Move right") })
            {
                int i = move.bindings.IndexOf(b => b.isPartOfComposite && b.name == part && b.path.StartsWith("<Keyboard>"));
                if (i >= 0) list.Add(new Entry { Label = label, Action = move, Binding = i });
            }
            foreach (var name in new[] { "Jump", "Sprint", "Crouch" }) Add(list, map.FindAction(name, true), name);
            foreach (var a in Tools) Add(list, a, a.name);
            return list;
        }

        static void Add(List<Entry> list, InputAction a, string label)
        {
            int i = a.bindings.IndexOf(b => !b.isComposite && (b.path.StartsWith("<Keyboard>") || b.path.StartsWith("<Mouse>")));
            if (i >= 0) list.Add(new Entry { Label = label, Action = a, Binding = i });
        }

        /// Waits for the next key or button and binds it. Escape cancels.
        public static void Rebind(Entry e, Action done)
        {
            bool was = e.Action.enabled;
            e.Action.Disable();
            e.Action.PerformInteractiveRebinding(e.Binding)
                .WithControlsExcluding("<Mouse>/position").WithControlsExcluding("<Mouse>/delta").WithControlsExcluding("<Mouse>/scroll")
                .WithControlsExcluding("<Gamepad>")
                .WithCancelingThrough("<Keyboard>/escape")
                .OnComplete(op => { op.Dispose(); Finish(); })
                .OnCancel(op => { op.Dispose(); Finish(); })
                .Start();

            void Finish()
            {
                if (was) e.Action.Enable();
                Save();
                done?.Invoke();
            }
        }

        public static void ResetAll()
        {
            InputSystem.actions.RemoveAllBindingOverrides();
            foreach (var a in Tools) a.RemoveAllBindingOverrides();
            Save();
        }

        [Serializable]
        sealed class Saved { public string asset; public List<string> tools = new List<string>(); }

        static string PathFile => Path.Combine(Application.persistentDataPath, "bindings.json");

        public static void Save()
        {
            var s = new Saved { asset = InputSystem.actions.SaveBindingOverridesAsJson() };
            foreach (var a in Tools) s.tools.Add(a.SaveBindingOverridesAsJson());
            try { File.WriteAllText(PathFile, JsonUtility.ToJson(s, true)); }
            catch (Exception ex) { Debug.LogWarning($"Couldn't save key bindings: {ex.Message}"); }
        }

        public static void Load()
        {
            try
            {
                if (!File.Exists(PathFile)) return;
                var s = JsonUtility.FromJson<Saved>(File.ReadAllText(PathFile));
                if (!string.IsNullOrEmpty(s.asset)) InputSystem.actions.LoadBindingOverridesFromJson(s.asset);
                for (int i = 0; i < Tools.Length && i < s.tools.Count; i++)
                    if (!string.IsNullOrEmpty(s.tools[i])) Tools[i].LoadBindingOverridesFromJson(s.tools[i]);
            }
            catch (Exception ex) { Debug.LogWarning($"Key bindings unreadable: {ex.Message}"); }
        }
    }
}

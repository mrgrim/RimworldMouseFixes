using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;
using Verse;

// ReSharper disable once CheckNamespace
namespace MouseFixes;

using DragStateTuple = (bool buttonPressed, bool dragStarted, Vector2 startPos, Vector2 lastPos);

// Exists solely as a type for use in the Unity Player Loop
public class FixMouseDrag;

public class MouseFixes : Mod
{
    private static readonly Harmony harmony = new ("org.gr1m.mods.rimworld.MouseFixes.Harmony");
    private static double _dragThresholdSq;
    
    public MouseFixes(ModContentPack content) : base(content)
    {
        Harmony.DEBUG = true;

        // This is just a ploy to avoid reflection
        new ReversePatcher(harmony,
            typeof(MouseFixes).GetMethod(nameof(ReversePatchTarget)),
            new HarmonyMethod(QueueEvent)).Patch();
        
        harmony.PatchAll(Assembly.GetExecutingAssembly());

        // Lots of good info on hooking into the Unity Player Loop here:
        // https://giannisakritidis.com/blog/Early-And-Super-Late-Update-In-Unity/
        
        var currentLoop = PlayerLoop.GetCurrentPlayerLoop();
        var newLoop = new PlayerLoopSystem()
        {
            loopConditionFunction = currentLoop.loopConditionFunction,
            type = currentLoop.type,
            updateDelegate = currentLoop.updateDelegate,
            updateFunction = currentLoop.updateFunction
        };

        List<PlayerLoopSystem> newSubsystemList = [];
        foreach (var subsystem in currentLoop.subSystemList)
        {
            if (subsystem.type == typeof(PreUpdate))
            {
                var targetSubsystem = subsystem.subSystemList.FirstIndexOf(ss =>
                    ss.type == typeof(PreUpdate.IMGUISendQueuedEvents));
                
                var coalesceEvents = new PlayerLoopSystem
                {
                    subSystemList = null,
                    updateDelegate = DoFixMouseDrag,
                    type = typeof(FixMouseDrag)
                };

                var newSubsystem = subsystem;
                var newSubList = subsystem.subSystemList.ToList();
                
                newSubList.Insert(targetSubsystem, coalesceEvents);
                newSubsystem.subSystemList = newSubList.ToArray();
                newSubsystemList.Add(newSubsystem);
            }
            else
                newSubsystemList.Add(subsystem);
        }
        
        newLoop.subSystemList = newSubsystemList.ToArray();
        PlayerLoop.SetPlayerLoop(newLoop);

        var dpi = Screen.dpi == 0 ? 92 : Screen.dpi; // 24" 1080p is 92 dpi
        _dragThresholdSq = Math.Pow((dpi / 25.4) * 1.5, 2); // 1.5mm
    }

    private static readonly DragStateTuple[] DragState = new DragStateTuple[16];

    private static void DoFixMouseDrag()
    {
        List<Event> preEventQueue = [];
        var mousePos = GUIUtility.ScreenToGUIPoint(UI.MousePositionOnUIInverted * Prefs.UIScale);

        while (Event.GetEventCount() > 0)
        {
            var outEvent = new Event();
            Event.PopEvent(outEvent);

            // ReSharper disable once SwitchStatementMissingSomeEnumCasesNoDefault
            switch (outEvent.type)
            {
                case EventType.MouseDown when outEvent.button < 16:
                    DragState[outEvent.button] = (true, false, outEvent.mousePosition, outEvent.mousePosition);
                    break;
                case EventType.MouseUp when outEvent.button < 16:
                    CheckDrag(outEvent.button);
                    DragState[outEvent.button] = (false, false, Vector2.zero, Vector2.zero);
                    break;
            }
            
            if (outEvent.type != EventType.MouseDrag) preEventQueue.Add(outEvent);
        }

        for (var button = 0; button < DragState.Length; button++)
            if (DragState[button].buttonPressed)
                CheckDrag(button);
        
        preEventQueue.ForEach(QueueEvent);
        return;

        void CheckDrag(int button)
        {
            if (!DragState[button].dragStarted && !(Vector2.SqrMagnitude(mousePos - DragState[button].startPos) > _dragThresholdSq)) return;

            var delta = mousePos - DragState[button].lastPos;
            if (delta.sqrMagnitude < 1.0) return;
            
            var dragEvent = new Event { type = EventType.MouseDrag, mousePosition = mousePos, delta = delta, button = button };

            DragState[button].dragStarted = true;
            DragState[button].lastPos = mousePos;
            
            preEventQueue.Add(dragEvent);
        }
    }

    public static void ReversePatchTarget(Event _)
    {
        throw new NotImplementedException("Stub");
    }

    private static void QueueEvent(Event outEvent)
    {
        _ = Transpiler(null);
        return;

        // ReSharper disable once UnusedParameter.Local
        IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> _)
        {
            return new List<CodeInstruction>([
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Event), "QueueEvent")),
                new CodeInstruction(OpCodes.Ret)
            ]);
        }
    }

    [HarmonyPatch(typeof(UnityGUIBugsFixer), "FixSteamDeckMousePositionNeverUpdating")]
    internal static class UnityGUIBugsFixerPatch
    {
        internal static bool Prefix()
        {
            return false;
        }
    }

    [HarmonyPatch]
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    internal static class UnityEventMousePositionGetterOverridePatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.PropertyGetter(typeof(Event), nameof(Event.mousePosition));
        }

        // ReSharper disable once InconsistentNaming
        // ReSharper disable once RedundantAssignment
        private static bool Prefix(ref Vector2 __result)
        {
            __result = GUIUtility.ScreenToGUIPoint(UI.MousePositionOnUIInverted * Prefs.UIScale);
            return false;
        }
    }
}

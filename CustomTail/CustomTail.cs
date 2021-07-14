using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using UnityEngine;
using MonoMod.RuntimeDetour;
using System.IO;

namespace CustomTail
{
    public partial class CustomTail : Partiality.Modloader.PartialityMod
    {
        public CustomTail()
        {
            ModID = "Custom Tail";
            Version = "1.1";
            author = "Slime_Cubed";
        }

        public override void OnEnable()
        {
            new Hook(
                typeof(RainWorld).GetMethod("Update", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                typeof(CustomTail).GetMethod("RainWorld_Update")
            );
        }

        // Delay adding the hook so then it applies after everything else
        private static int _applyHooksDelay = 2;
        public static void RainWorld_Update(Action<RainWorld> orig, RainWorld self)
        {
            orig(self);
            if (_applyHooksDelay > 0)
            {
                _applyHooksDelay--;
                if (_applyHooksDelay == 0)
                {
                    DynamicHooks.AddDeepHook(
                        typeof(PlayerGraphics).GetMethod("ApplyPalette", BindingFlags.Public | BindingFlags.Instance),
                        typeof(CustomTail).GetMethod("PlayerGraphics_ApplyPalette")
                    );
                }
            }
        }
        
        public delegate void orig_ApplyPalette(PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, RoomPalette palette);
        private static bool _lockApplyPalette = false;
        private static bool _loggedMissingSprite = false;
        public static void PlayerGraphics_ApplyPalette(orig_ApplyPalette orig, PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, RoomPalette palette)
        {
            // Work-around to avoid calling this hook multiple times
            if (_lockApplyPalette)
            {
                orig(self, sLeaser, rCam, palette);
                return;
            }

            _lockApplyPalette = true;
            orig(self, sLeaser, rCam, palette);
            _lockApplyPalette = false;

            // Edit the tail sprite
            // Note that, while it is a mesh, meshes inherit from FSprite, so it's still a sprite :)
            TriangleMesh tail = null;
            for(int i = 0; i < sLeaser.sprites.Length; i++)
            {
                if(sLeaser.sprites[i] is TriangleMesh tm)
                {
                    tail = tm;
                    break;
                }
            }

            // Get tail settings
            TailConfig cfg = GetTailConfig((self.owner as Player).playerState.slugcatCharacter);

            if (tail != null)
            {
                // Set the tail's element to a custom sprite
                try
                {
                    tail.element = Futile.atlasManager.GetElementWithName(cfg.sprite);
                } catch(FutileException e)
                {
                    if (!_loggedMissingSprite)
                    {
                        _loggedMissingSprite = true;
                        Debug.LogError(new Exception($"Tail sprite \"{cfg.sprite}\" not found. Defaulting to \"Futile_White\". Further errors will not be logged.", e));
                    }
                    tail.element = Futile.atlasManager.GetElementWithName("Futile_White");
                }

                // Register that the tail must have custom colors
                if(tail.verticeColors == null || tail.verticeColors.Length != tail.vertices.Length)
                    tail.verticeColors = new Color[tail.vertices.Length];

                tail.customColor = false;
                Color baseColor = tail.color;
                tail.customColor = true;

                // Use the player's color when the given color is exactly black
                Color fromColor = cfg.baseTint;
                Color toColor = cfg.tipTint;
                if (fromColor == Color.black) fromColor = baseColor;
                if (toColor == Color.black) toColor = baseColor;

                // Calculate UVs and colors
                for (int i = tail.verticeColors.Length - 1; i >= 0; i--)
                {
                    float perc = i / 2 / (float)(tail.verticeColors.Length / 2);
                    tail.verticeColors[i] = Color.Lerp(fromColor, toColor, perc);
                    Vector2 uv;
                    if (i % 2 == 0)
                        uv = new Vector2(perc, 0f);
                    else if (i < tail.verticeColors.Length - 1)
                        uv = new Vector2(perc, 1f);
                    else
                        uv = new Vector2(1f, 0f);

                    // Map UV values to the element
                    uv.x = Mathf.Lerp(tail.element.uvBottomLeft.x, tail.element.uvTopRight.x, uv.x);
                    uv.y = Mathf.Lerp(tail.element.uvBottomLeft.y, tail.element.uvTopRight.y, uv.y);

                    tail.UVvertices[i] = uv;
                }
                tail.Refresh();
            }
        }
    }

    public static class DynamicHooks
    {
        // Define a bunch of assemblies that should never contain game code
        private static string[] _systemAssemblies = new string[]
        {
            "HOOKS-Assembly-CSharp",
            "Partiality",
            "MonoMod.RuntimeDetour.HookGen",
            "MonoMod",
            "BepInEx-Partiality-Wrapper",
            "GoKit",
            "UnityEngine.UI",
            "UnityEngine",
            "BepInEx.MonoMod.Loader",
            "Mono.Security",
            "HarmonySharedState",
            "BepInEx.Harmony",
            "0Harmony",
            "MonoMod.Utils",
            "MonoMod.RuntimeDetour",
            "System",
            "Mono.Cecil",
            "System.Core",
            "BepInEx",
            "BepInEx.Preloader",
            "mscorlib"
        };
        
        public static MethodInfo[] GetChildMethods(this MethodInfo baseMethod, BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, bool ignoreSystemAssemblies = true)
        {
            // First, get base type to search for
            Type baseType = baseMethod.DeclaringType;
            ParameterInfo[] baseParams = baseMethod.GetParameters();
            Type[] argTypes = new Type[baseParams.Length];
            for(int i = 0; i < argTypes.Length; i++)
                argTypes[i] = baseParams[i].ParameterType;

            // Search through all assemblies
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            LinkedList<MethodInfo> methods = new LinkedList<MethodInfo>();
            foreach (Assembly asmb in assemblies)
            {
                // Discard system assemblies to save time
                if (ignoreSystemAssemblies && _systemAssemblies.Contains(asmb.GetName().Name)) continue;

                try
                {
                    // Search through all types for ones that inherit from the given type
                    Type[] types = asmb.GetTypes();
                    for (int i = types.Length - 1; i >= 0; i--)
                    {
                        // This type is a child of the given type
                        // Get the method, if the current type overrides it
                        if (!baseType.IsAssignableFrom(types[i])) continue;
                        MethodInfo childMethod = types[i].GetMethod(baseMethod.Name, flags | BindingFlags.DeclaredOnly, null, argTypes, null);
                        if (childMethod != null)
                            methods.AddLast(childMethod);
                        break;
                    }
                }
                catch (Exception)
                {}
            }

            MethodInfo[] o = new MethodInfo[methods.Count];
            methods.CopyTo(o, 0);
            return o;
        }

        // Utility method to create hooks for an array of methods, rather than a single one
        public static Hook[] AddHooks(MethodInfo[] from, MethodInfo to, object target = null)
        {
            Hook[] hooks = new Hook[from.Length];
            for (int i = from.Length - 1; i >= 0; i--)
            {
                if (target != null)
                    hooks[i] = new Hook(from[i], to, target);
                else
                    hooks[i] = new Hook(from[i], to);
            }
            return hooks;
        }

        // Adds hooks to a method and all overrides of the method
        public static Hook[] AddDeepHook(MethodInfo from, MethodInfo to, object target = null)
        {
            return AddHooks(from.GetChildMethods(), to, target);
        }
    }
}
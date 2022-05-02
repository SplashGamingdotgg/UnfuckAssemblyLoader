// Author: Diametric
// Lets you use "// Reference:" in Oxide mods and load assemblies from the HarmonyMods folder.
// Caveats: Prefers HarmonyMods first, so if Blah.dll exists in both HarmonyMods and Managed folders, the HarmonyMods version will be used.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using Harmony;
using UnityEngine;

namespace UnfuckAssemblyLoader
{
    [HarmonyPatch]
    internal class UnfuckFirstAddReference : AbstractPatchyBoi
    {
        private static readonly CodeInstruction[] Search =
        {
            new CodeInstruction(OpCodes.Call, typeof(Oxide.Core.Interface).GetProperty("ExtensionDirectory")),
            new CodeInstruction(OpCodes.Callvirt, typeof(Oxide.Core.Interface).GetProperty("Oxide")),
            new CodeInstruction(OpCodes.Ldarg_2),
            new CodeInstruction(OpCodes.Ldstr, ".dll"),
            new CodeInstruction(OpCodes.Call, typeof(string).GetMethod("Concat", new[] { typeof(string), typeof(string) })),
            new CodeInstruction(OpCodes.Call, typeof(Path).GetMethod("Combine", new[] { typeof(string), typeof(string) })),
            new CodeInstruction(OpCodes.Call, typeof(File).GetMethod("Exists"))
        };

        private static readonly CodeInstruction[] Patch =
        {
            new CodeInstruction(OpCodes.Ldarg_2),
            new CodeInstruction(OpCodes.Call, typeof(UnfuckFirstAddReference).GetMethod("AssemblyExists"))
        };

        public static bool AssemblyExists(string assemblyName)
        {
            return File.Exists(Path.Combine(Oxide.Core.Interface.Oxide.ExtensionDirectory, assemblyName + ".dll")) ||
                   File.Exists(Path.Combine(Oxide.Core.Interface.Oxide.RootDirectory, "HarmonyMods", assemblyName + ".dll"));
        }        
        
        static MethodInfo TargetMethod()
        {
            // Needed to do this, the internal class couldn't be found even with a fully qualified string to Type.GetType (i dunno why)
            var assembly = Assembly.GetAssembly(typeof(Oxide.Plugins.Timer));
            var type = assembly.GetType("Oxide.Plugins.Compilation");
            var method = type.GetMethod(
                "AddReference",
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new Type[] {typeof(Oxide.Plugins.CompilablePlugin), typeof(string)},
                null);
            
            return method;
        }

        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return ApplyPatch(instructions, Search, Patch);
        }
    }

    [HarmonyPatch]
    internal class UnfuckSecondAddReference : AbstractPatchyBoi
    {
        private static readonly CodeInstruction[] Search =
        {
            new CodeInstruction(OpCodes.Call, typeof(Oxide.Core.Interface).GetProperty("ExtensionDirectory")),
            new CodeInstruction(OpCodes.Callvirt, typeof(Oxide.Core.Interface).GetProperty("Oxide"))
        };

        private static readonly CodeInstruction[] Patch =
        {
            new CodeInstruction(OpCodes.Ldloc_0),
            new CodeInstruction(OpCodes.Call, typeof(UnfuckSecondAddReference).GetMethod("GetAssemblyDirectory"))
        };

        // Prefer HarmonyMods first. This doesn't really handle the case well if the assembly with the same name exists
        // in both the extensions folder and harmonymods.
        public static string GetAssemblyDirectory(string assemblyName)
        {
            if (File.Exists(Path.Combine(Oxide.Core.Interface.Oxide.RootDirectory, "HarmonyMods", assemblyName)))
            {
                return Path.Combine(Oxide.Core.Interface.Oxide.RootDirectory, "HarmonyMods");
            }
            
            return Oxide.Core.Interface.Oxide.ExtensionDirectory;
        }

        static MethodInfo TargetMethod()
        {
            // Needed to do this, the internal class couldn't be found even with a fully qualified string to Type.GetType (i dunno why)
            var assembly = Assembly.GetAssembly(typeof(Oxide.Plugins.Timer));
            var type = assembly.GetType("Oxide.Plugins.Compilation");
            var method = type.GetMethod(
                "AddReference",
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new Type[] {typeof(Oxide.Plugins.CompilablePlugin), typeof(AssemblyName)},
                null);
            
            return method;
        }    
        
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return ApplyPatch(instructions, Search, Patch);
        }
    }
    
    public abstract class AbstractPatchyBoi
    {
        static bool CodeInstructionMatch(CodeInstruction a, CodeInstruction b)
        {
            if (a.opcode.Name != b.opcode.Name) return false;
            if (a.operand == b.operand) return true;
            if (a.operand is MethodBase aMethod && b.operand is MethodBase bMethod)
                return aMethod.Equals((bMethod));
            
            return true;
        }
        
        protected static IEnumerable<CodeInstruction> ApplyPatch(IEnumerable<CodeInstruction> instructions, CodeInstruction[] search, CodeInstruction[] patch)
        {
            var newInstructions = new List<CodeInstruction>(instructions);

            var found = false;
            var idx = 0;
                
            for (idx = 0; idx < newInstructions.Count - search.Length; idx++)
            {
                for (var x = 0; x < search.Length; x++)
                {
                    if (!CodeInstructionMatch(newInstructions[idx + x] , search[x]))
                    {
                        goto outer;
                    }
                }

                // Break out we found it bois
                found = true;
                break;
                
                outer: ;
            }
            
            if (found)
            {
                Debug.Log($"[UnfuckAssemblyLoader] Found injection offset at {idx}");
                newInstructions.RemoveRange(idx, search.Length);
                newInstructions.InsertRange(idx, patch);
            }
            else
            {
                Debug.Log("[UnfuckAssemblyLoader] Unable to locate injection offset.");
            }
            return newInstructions;
        }            
    }
}
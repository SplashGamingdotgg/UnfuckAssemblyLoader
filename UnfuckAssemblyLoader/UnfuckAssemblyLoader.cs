// Author: Diametric
// Copyright (c) Splash Gaming
// Lets you use "// Reference:" in Oxide mods and uses the assembly loaded by Harmony.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Harmony;
using Oxide.Plugins;
using UnityEngine;

namespace UnfuckAssemblyLoader
{
    [HarmonyPatch]
    internal class UnfuckAddReference
    {
        // Internal class Type for the Compilation class. The irony of making a class harder to mod in a framework
        // built to mod games.
        private static Type _oxideCompilationClass = null;
        private static Type _oxideCompilerFile = null;
        private static MethodInfo _internalAddReferenceMethod = null;
        private static Dictionary<string, Assembly> _harmonyLoaderAssemblies = null;
        
        private static Assembly GetHarmonyAssembly(string name)
        {
            foreach (var assembly in _harmonyLoaderAssemblies)
            {
                Debug.Log($"[UnfuckAssemblyLoader] Checking {assembly.Key} for {name}");
            }
            
            Assembly asm;
            return _harmonyLoaderAssemblies.TryGetValue(name, out asm) ? asm : null;
        }

        private static string GetJukedAssemblyName(string name)
        {
            int pos = name.LastIndexOf('_');
            return pos == -1 ? name : name.Substring(0, pos);
        }
        
        private static void AddHarmonyReference(CompilablePlugin plugin, AssemblyName reference, object __instance)
        {
            string harmonyModsDir = Path.Combine(Oxide.Core.Interface.Oxide.RootDirectory, "HarmonyMods");
            if (!Directory.Exists(harmonyModsDir))
            {
                Debug.LogWarning($"[UnfuckAssemblyLoader] Could not find HarmonyMods directory at {harmonyModsDir}.");
                return;
            }

            string assemblyName = GetJukedAssemblyName(reference.Name);
            string filename = assemblyName + ".dll";
            // This is such a pain but we have to do it this way because the CompilerFile class is internal.
            object references = AccessTools.Field(_oxideCompilationClass, "references")?.GetValue(__instance);
            if (references == null)
            {
                Debug.LogWarning($"[UnfuckAssemblyLoader] Failed to get references field for {plugin.Name}. Compilation may fail.");
            }
            else
            {
                Type[] genericAddArgs = { typeof(string), _oxideCompilerFile };
                var genericDict = typeof(Hash<,>).MakeGenericType(genericAddArgs);
                var dictAdd = genericDict.GetMethod("Add", new [] { typeof(string), _oxideCompilerFile });
                
                if (dictAdd == null)
                {
                    Debug.LogWarning($"[UnfuckAssemblyLoader] Failed to get Add method for {plugin.Name} references dictionary. Compilation may fail. This is a bug.");
                }
                else
                {
                    ConstructorInfo compilerFileConstructor = AccessTools.Constructor(_oxideCompilerFile, new []{ typeof(string), typeof(string) });
                    object compilerFile = compilerFileConstructor.Invoke(new object[] { harmonyModsDir, filename });
                    dictAdd.Invoke(references, new[] { filename, compilerFile });
                }
            }
            
            Debug.Log($"[UnfuckAssemblyLoader] Added reference to {filename} for {plugin.Name}.");
            plugin.References.Add(assemblyName);
        }
        
        public static bool Prefix(CompilablePlugin plugin, string assemblyName, object __instance)
        {
            // TODO: Track all this adding of dependencies and references so we can hook harmony.load/unload.
            try
            {
                var assembly = GetHarmonyAssembly(assemblyName);
                if (assembly == null)
                {
                    return true;
                }
                
                AddHarmonyReference(plugin, assembly.GetName(), __instance);
                
                foreach (AssemblyName reference in assembly.GetReferencedAssemblies())
                {
                    // Janky hardcoded hack copied directly from Oxide :)
                    if (reference.Name.StartsWith("Newtonsoft.Json") || reference.Name.StartsWith("Rust.Workshop"))
                    {
                        continue;
                    }
                    
                    var dep = GetHarmonyAssembly(reference.Name);
                    if (dep != null)
                    {
                        AddHarmonyReference(plugin, reference, __instance);
                    }
                    else
                    {
                        _internalAddReferenceMethod.Invoke(__instance, new object[] { plugin, reference });
                    }
                }

                return false;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Exception while trying to AddReference to Oxide plugin: {e}");
                Debug.LogException(e);
            }

            return true;
        }

        // Finds our patch method, but also finds and caches all the various internal/private types we'll need later.
        public static MethodInfo TargetMethod()
        {
            _harmonyLoaderAssemblies = (Dictionary<string, Assembly>)AccessTools.Field(typeof(HarmonyLoader), "assemblyNames")?.GetValue(null);
            if (_harmonyLoaderAssemblies == null)
            {
                Debug.LogWarning("[UnfuckAssemblyLoader] Failed to get HarmonyLoader.assemblyNames");
                return null;
            }

            _oxideCompilationClass = AccessTools.TypeByName("Oxide.Plugins.Compilation");
            if (_oxideCompilationClass == null)
            {
                Debug.LogWarning("[UnfuckAssemblyLoader] Unable to get Oxide.Plugins.Compilation.  Is Oxide installed?");
                return null;
            }
            
            _oxideCompilerFile = AccessTools.TypeByName("ObjectStream.Data.CompilerFile");
            if (_oxideCompilerFile == null)
            {
                Debug.LogWarning("[UnfuckAssemblyLoader] Unable to get ObjectStream.Data.CompilerFile.  Is Oxide installed?");
                return null;
            }
            
            _internalAddReferenceMethod = AccessTools.Method(_oxideCompilationClass, "AddReference", new []{ typeof(CompilablePlugin), typeof(AssemblyName) });
            if (_internalAddReferenceMethod == null)
            {
                Debug.LogWarning("[UnfuckAssemblyLoader] Unable to get internal Oxide.Plugins.Compilation.AddReference(CompilablePlugin, AssemblyName).  Is Oxide installed?");
                return null;
            }
            
            var patchMethod = AccessTools.Method(_oxideCompilationClass, "AddReference", new [] { typeof(CompilablePlugin), typeof(string) });
            if (patchMethod != null) return patchMethod;
            
            Debug.LogWarning("[UnfuckAssemblyLoader] Unable to get Oxide.Plugins.Compilation.AddReference(CompilablePlugin, string).  Oxide may have changed.");
            return null;
        }
    }
}

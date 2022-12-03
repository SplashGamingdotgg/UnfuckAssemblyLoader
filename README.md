# UnfuckAssemblyLoader

~~This should really be called Unfuck**Oxide**AssemblyLoader.~~  

> tl;dr - Dec 2022 patch of Rust broke the ability for Oxide to reference any Harmony DLLs entirely.
> This fixes that.

I stand by the original name now.   As of December 1st 2022's patch, Rust's Harmony loader fully 
supports loading and unloading at runtime.  However, the caveat to this was assembly namespaces 
needed to be juked. This causes an issue where referencing assemblies of the same name, from 
anywhere, including the HarmonyMod's folder loads them into a different namespace.  
Unfortunately .NET's CLR isn't clever enough to allow casting between identical assemblies in 
different namespaces. So it leaves all the code in a harmony mod assembly untouchable without 
very complicated, and frustrating reflection.

In light of these changes, I've updated the Harmony mod to attempt to load the already loaded 
assembly from the HarmonyLoader class in Rust.  This is not without caveats.  As this time I 
won't have written any dependency reload checking. So if you unload a Harmony DLL that is being 
utilized by a plugin, expect bad things. **Unload the plugin first, then unload/reload your 
Harmony DLL.**


# FAQ

- [How do I use it?](#how-do-i-use-it)
- [Why not just submit a PR for native Oxide support?](#no-pr-for-oxide)
- [Why would you even want this functionality?](#why-use-it)

## How do I use it?

Download the latest release from the releases page, and place it in your `HarmonyMods` folder.
Use `// Reference: MyHarmonyMod` in your plugin to reference the harmony mod assembly.

## Why not just submit a PR for native Oxide support?

The code we're modifying here is the `Oxide.CSharp` module.  This is a game agnostic module,
so it complicates trying to support a Rust specific feature/fix. 

## Why would you even want this functionality?

There are a lot of reasons you'd want to load DLLs into Oxide plugins. In my specific case I 
wrote a Harmony mod that tracks various PVP statistics.  I wanted to use it on a non-oxide, 
vanilla only server for external support (such as posting stats to discord, websites, etc.)

I also wanted to use it on a modded server and integrate it with a killfeed. I created some
simply callback hooks that would allow me to hook into the stats mod to get the data needed
to display the killfeed using an Oxide plugin and CUI.  There are countless other use cases
for this functionality, and I hope this helps someone else out there.
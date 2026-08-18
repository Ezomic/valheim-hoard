using System;
using System.IO;
using System.Runtime.CompilerServices;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using Ezomic.Core;
using HarmonyLib;

namespace Yoke
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    // Soft, not hard. Yoke installs and runs on its own; a hard dependency
    // that is absent does not degrade, the plugin simply never loads. Soft still buys
    // the load-order guarantee when Core is present, which is what registering needs.
    [BepInDependency(CoreGuid, BepInDependency.DependencyFlags.SoftDependency)]
    // Also soft. Utangard decides what "earned" means when it is installed - see
    // Progression.EarnedThroughUtangard - but Yoke is a stack mod, not a companion piece,
    // and must load on its own. Soft still buys the load-order guarantee that the check
    // needs: Chainloader.PluginInfos is only complete for plugins that loaded first.
    [BepInDependency(UtangardGuid, BepInDependency.DependencyFlags.SoftDependency)]
    // No BepInProcess. It is a whitelist, and a dedicated server runs valheim_server.exe.
    // This mod already patches CopyOtherDB precisely because a client rebuilds its item
    // database from the server's copy - so a server without it hands back vanilla stack
    // sizes and undoes the mod on every join.
    public class YokePlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "ezomic.valheim.yoke";
        public const string PluginName = "Yoke";
        // Pre-1.0 on purpose: 1.0.0 is reserved for the first version that has been played
        // and published. See CHANGELOG.md.
        public const string PluginVersion = "0.13.0";
        public const string PluginAuthor = "Robbin Thijssen";

        /// <summary>Core's plugin GUID. Optional - see TryRegisterWithCore.</summary>
        private const string CoreGuid = "ezomic.valheim.core";

        /// <summary>Utangard's plugin GUID. Optional - see Progression.</summary>
        private const string UtangardGuid = "ezomic.valheim.utangard";

        internal static ManualLogSource Log;

        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;
            AdoptOldConfig();
            YokeConfig.Bind(Config);
            TryRegisterWithCore();

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(typeof(ObjectDbPatches));

            Log.LogInfo(PluginName + " " + PluginVersion + " by " + PluginAuthor + " - ready.");
        }

        /// <summary>
        /// Carries Hoard's settings over on the first run under the new name.
        ///
        /// A BepInEx config file is named after the plugin GUID, so a rename hands everyone
        /// defaults, and it does it silently. That is worse here than it looks: BiomeOverrides
        /// runs to about a hundred entries, and losing it drops the mod back to derived-only
        /// placement with nothing in the log to say why the ore stopped stacking.
        ///
        /// Before Bind, and that ordering is the whole trick. BaseUnityPlugin builds Config
        /// with saveOnInit false, so nothing is written to disk until something binds a
        /// setting - which leaves a window where dropping the old file into place means the
        /// new name reads the old answers.
        ///
        /// Only when the new file does not exist. After the first run this is a no-op, so a
        /// later edit is never overwritten by a stale Hoard cfg left lying beside it.
        /// </summary>
        private void AdoptOldConfig()
        {
            try
            {
                string path = Config.ConfigFilePath;
                if (File.Exists(path)) return;

                string folder = Path.GetDirectoryName(path);
                if (string.IsNullOrEmpty(folder)) return;

                string old = Path.Combine(folder, "ezomic.valheim.hoard.cfg");
                if (!File.Exists(old)) return;

                File.Copy(old, path);
                Log.LogInfo("Adopted Hoard's config, so its settings carry over unchanged.");
            }
            catch (Exception e)
            {
                // Defaults are a worse outcome than a warning, but neither is worth failing
                // the load over.
                Log.LogWarning("Could not adopt Hoard's config: " + e.Message);
            }
        }

        /// <summary>
        /// Joins Core's version gate when Core is installed, and does nothing when it is not.
        ///
        /// Yoke is worth installing on its own, and a hard dependency that is absent does
        /// not degrade gracefully - the plugin never loads at all. So the reference is
        /// compile-time only and the call is made behind a check.
        ///
        /// What is given up standing alone is the gate, not the mod.
        /// Item data that differs between two ends desyncs inventories, and nothing without Core
        /// reports that. The CopyOtherDB patch still keeps a client on the server's numbers,
        /// which covers the common case on its own.
        /// </summary>
        private void TryRegisterWithCore()
        {
            if (!Chainloader.PluginInfos.ContainsKey(CoreGuid))
            {
                Log.LogInfo("Core not installed - running standalone, without the version gate.");
                return;
            }

            RegisterWithCore();
        }

        /// <summary>
        /// Kept separate and never inlined on purpose. The JIT resolves the assemblies a method
        /// needs when it first compiles that method, so a Suite call sitting directly in Awake
        /// would drag Ezomic.Core in before the check above could prevent it - and the
        /// missing-assembly exception would land during plugin load, which is the failure this
        /// whole arrangement exists to avoid. Isolating it means the type is only ever resolved
        /// on a machine that has Core.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void RegisterWithCore()
        {
            // Everyone, not HostOnly. Both ends have to agree about this mod, and the
            // disagreement is silent when they do not: a client that cannot resolve a prefab
            // hash discards the ZDO rather than erroring - destroying what is already standing
            // in the world - and item data that differs desyncs inventories.
            Suite.Register(PluginGuid, PluginName, PluginVersion, Config);
        }


        private void OnDestroy()
        {
            if (_harmony != null) _harmony.UnpatchSelf();
        }
    }

    internal static class ObjectDbPatches
    {
        /// <summary>
        /// Both entry points are hooked because both really happen: Awake builds the
        /// database once at startup, and CopyOtherDB rebuilds it from the server's copy
        /// when a world loads. Patching only Awake means the server's untouched values
        /// quietly replace yours the moment you join a game.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(ObjectDB), "Awake")]
        private static void TuneOnAwake()
        {
            StackTuner.Rebuild();
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.CopyOtherDB))]
        private static void TuneOnCopy()
        {
            StackTuner.Rebuild();
        }

        /// <summary>
        /// Every way a global key can arrive funnels through here: the bulk list the server
        /// sends on connect via RPC_GlobalKeys, and a boss dying while you are stood there.
        /// Patching SetGlobalKey instead would miss the first, and a client that joined a
        /// world where the bosses are already down is the common case, not the edge.
        ///
        /// It fires for every key in the game, most of which are world modifiers rather than
        /// progress, so the work of deciding whether anything moved lives behind
        /// ApplyIfProgressionChanged rather than here.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(ZoneSystem), "GlobalKeyAdd", new[] { typeof(string), typeof(bool) })]
        private static void TuneOnGlobalKey()
        {
            StackTuner.ApplyIfProgressionChanged();
        }

        /// <summary>
        /// The last of the three tables the biome index needs to turn up.
        ///
        /// ZoneSystem is up early enough that the world's keys arrive through it, and
        /// ZNetScene with the world - but SpawnSystem appears later, and a pass that ran
        /// before it has creature drops missing from every biome. There is no ordering to
        /// rely on here, so the index is simply rebuilt when the straggler arrives.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(SpawnSystem), "Awake")]
        private static void TuneWhenSpawnersExist()
        {
            // Once. There is more than one SpawnSystem in a loaded world, and without this
            // every one of them rebuilds the index and walks the whole item database again.
            if (BiomeIndex.Complete) return;

            StackTuner.Rebuild();
        }
    }
}

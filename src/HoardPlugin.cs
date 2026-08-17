using System.Runtime.CompilerServices;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using Ezomic.Core;
using HarmonyLib;

namespace Hoard
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    // Soft, not hard. Hoard installs and runs on its own; a hard dependency
    // that is absent does not degrade, the plugin simply never loads. Soft still buys
    // the load-order guarantee when Core is present, which is what registering needs.
    [BepInDependency(CoreGuid, BepInDependency.DependencyFlags.SoftDependency)]
    // Also soft. Utangard decides what "earned" means when it is installed - see
    // Progression.EarnedThroughUtangard - but Hoard is a stack mod, not a companion piece,
    // and must load on its own. Soft still buys the load-order guarantee that the check
    // needs: Chainloader.PluginInfos is only complete for plugins that loaded first.
    [BepInDependency(UtangardGuid, BepInDependency.DependencyFlags.SoftDependency)]
    // No BepInProcess. It is a whitelist, and a dedicated server runs valheim_server.exe.
    // This mod already patches CopyOtherDB precisely because a client rebuilds its item
    // database from the server's copy - so a server without it hands back vanilla stack
    // sizes and undoes the mod on every join.
    public class HoardPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "ezomic.valheim.hoard";
        public const string PluginName = "Hoard";
        // Pre-1.0 on purpose: 1.0.0 is reserved for the first version that has been played
        // and published. See CHANGELOG.md.
        public const string PluginVersion = "0.10.0";
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
            HoardConfig.Bind(Config);
            TryRegisterWithCore();

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(typeof(ObjectDbPatches));

            Log.LogInfo(PluginName + " " + PluginVersion + " by " + PluginAuthor + " - ready.");
        }

        /// <summary>
        /// Joins Core's version gate when Core is installed, and does nothing when it is not.
        ///
        /// Hoard is worth installing on its own, and a hard dependency that is absent does
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
    }
}

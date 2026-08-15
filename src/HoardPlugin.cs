using BepInEx;
using BepInEx.Logging;
using Ezomic.Core;
using HarmonyLib;

namespace Hoard
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("ezomic.valheim.core", BepInDependency.DependencyFlags.HardDependency)]
    // No BepInProcess. It is a whitelist, and a dedicated server runs valheim_server.exe.
    // This mod already patches CopyOtherDB precisely because a client rebuilds its item
    // database from the server's copy - so a server without it hands back vanilla stack
    // sizes and undoes the mod on every join.
    public class HoardPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "ezomic.valheim.hoard";
        public const string PluginName = "Hoard";
        public const string PluginVersion = "1.0.0";
        public const string PluginAuthor = "Robbin Thijssen";

        internal static ManualLogSource Log;

        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;
            HoardConfig.Bind(Config);
            // Everyone, not HostOnly. Both ends have to agree about this mod, and the
            // disagreement is silent when they do not: a client that cannot resolve a prefab
            // hash discards the ZDO rather than erroring - destroying what is already standing
            // in the world - and item data that differs desyncs inventories.
            Suite.Register(PluginGuid, PluginName, PluginVersion, Config);

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(typeof(ObjectDbPatches));

            Log.LogInfo(PluginName + " " + PluginVersion + " by " + PluginAuthor + " - ready.");
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
            StackTuner.Apply();
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.CopyOtherDB))]
        private static void TuneOnCopy()
        {
            StackTuner.Apply();
        }
    }
}

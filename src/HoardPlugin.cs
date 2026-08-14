using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace Hoard
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInProcess("valheim.exe")]
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

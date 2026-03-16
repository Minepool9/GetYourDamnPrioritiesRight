using BepInEx;
using Configgy;
using GetYourDamnPrioritiesRight;
using HarmonyLib;
using UnityEngine;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    private ConfigBuilder config;

    [Configgable(displayName: "Enabled")]
    public static ConfigToggle modenabled = new ConfigToggle(true);

    [Configgable(displayName: "Auto aim for hooks?")]
    public static ConfigToggle hooks = new ConfigToggle(true);

    void Awake()
    {
        new Harmony(MyPluginInfo.PLUGIN_GUID).PatchAll();
        config = new ConfigBuilder(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME);
        config.BuildAll();
    }
}

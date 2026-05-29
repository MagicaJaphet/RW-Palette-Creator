using BepInEx;
using MagicaHookingLibrary.Helpers;
using System.Security.Permissions;
using MagicaHookingLibrary;
using BepInEx.Logging;

// Allows access to private members
#pragma warning disable CS0618
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
#pragma warning restore CS0618

namespace PaletteEditor;

[BepInDependency("rwmodding.coreorg.rk", BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency("magica.hookinglibrary", BepInDependency.DependencyFlags.HardDependency)]
[BepInPlugin("magica.paletteeditor", "Palette Editor", "1.0.0")]
sealed class Plugin : PluginTemplate
{
	public static new ManualLogSource Logger;

	public override void OnEnable()
	{
		Logger = base.Logger;

		base.OnEnable();
	}

    public override void PreModsInit(RainWorld self)
    {
        HookHelpers.ApplyHooks(HookHelpers.HookType.Pre, Logger);
    }

    public override void OnModsInit(RainWorld self)
    {
		RegionKitWrapper.RegionKitEnabled = MiscHelpers.IsModActive("regionkit");
        HookHelpers.ApplyHooks(HookHelpers.HookType.On, Logger);
    }

    public override void PostModsInit(RainWorld self)
    {
        HookHelpers.ApplyHooks(HookHelpers.HookType.Post, Logger);
    }

	// TODO: Remix menu options
	// Custom undo stack + image scale
}
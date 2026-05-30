using BepInEx;
using MagicaHookingLibrary.Helpers;
using System.Security.Permissions;
using MagicaHookingLibrary;
using BepInEx.Logging;
using Menu;
using Menu.Remix.MixedUI;
using UnityEngine;
using System.IO;
using Menu.Remix.MixedUI.ValueTypes;

// Allows access to private members
#pragma warning disable CS0618
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
#pragma warning restore CS0618

namespace PaletteEditor;

[BepInDependency("rwmodding.coreorg.rk", BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency("magica.hookinglibrary", BepInDependency.DependencyFlags.HardDependency)]
[BepInPlugin(_MOD_ID, "Palette Editor", "1.0.0")]
sealed class Plugin : PluginTemplate
{
	public const string _MOD_ID = "magica.paletteeditor";

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

		RemixOptions.RegisterOI();
        HookHelpers.ApplyHooks(HookHelpers.HookType.On, Logger);
    }

    public override void PostModsInit(RainWorld self)
    {
        HookHelpers.ApplyHooks(HookHelpers.HookType.Post, Logger);
    }
}

internal class RemixOptions : OptionInterface
{
	public static RemixOptions Instance { get; } = new();
	public static void RegisterOI()
	{
		if (MachineConnector.GetRegisteredOI(Plugin._MOD_ID) != Instance)
		{
			MachineConnector.SetRegisteredOI(Plugin._MOD_ID, Instance);
		}
	}

	public static float Margin { get; } = 10f;

	public static Configurable<int> UndoStack { get; } = Instance.config.Bind(nameof(UndoStack), 30, new ConfigurableInfo(
		"The number of undos avaliable per loaded palette.",
		null, "", null));

	public static Configurable<float> PaletteImageScale { get; } = Instance.config.Bind(nameof(PaletteImageScale), 10f, new ConfigurableInfo(
		"The size of the editting palette.",
		null, "", null));
	public static Configurable<bool> LoadSavedPalettes { get; } = Instance.config.Bind(nameof(LoadSavedPalettes), true, new ConfigurableInfo(
		"Whether the game should prioritize any saved palettes when loading a palette file.",
		null, "", null));

	public OpTab ModOptionsTab { get; private set; }

	public override void Initialize()
	{
		base.Initialize();

		ModOptionsTab = new OpTab(this, "Mod Options");

		Tabs = [ModOptionsTab];

		OpDragger drag = new(UndoStack, new(Margin, ModOptionsTab.CanvasSize.y - (Margin * 3f)))
		{
			max = 100,
			min = 0,
			description = UndoStack.info.description
		};
		OpLabel dragLabel = new(drag.pos.x + drag.size.x + Margin, drag.pos.y, "Undo Stack");

		OpCheckBox check = new(LoadSavedPalettes, drag.pos + new Vector2(0f, -(drag.size.y + Margin)))
		{
			description = LoadSavedPalettes.info.description
		};
		OpLabel checkLabel = new(check.pos.x + check.size.x + Margin, check.pos.y, "Prioritize Saved Palettes");

		PreviewPalette imageSize = new(PaletteImageScale, check.pos + new Vector2(0f, -(check.size.y + Margin)), ModOptionsTab._container.GetPosition() + new Vector2(ModOptionsTab.CanvasSize.x - Margin, Margin), 100, 0)
		{
			max = 15f,
			min = 8f,
			_increment = 50,
			description = PaletteImageScale.info.description
		};
		OpLabel previewLabel = new(imageSize.pos.x + imageSize.size.x + Margin, imageSize.pos.y, "Image Scale");

		ModOptionsTab.AddItems(drag, dragLabel, check, checkLabel, imageSize, previewLabel);
	}

	internal class PreviewPalette : OpFloatSlider
	{
		private FTexture _paletteImage;
		private static Texture2D _outskirtsPalette;

		public PreviewPalette(Configurable<float> config, Vector2 pos, Vector2 imagePos, int length, byte decimalNum = 1, bool vertical = false) : base(config, pos, length, decimalNum, vertical)
		{
			if (_outskirtsPalette == null)
			{
				_outskirtsPalette = new Texture2D(32, 16, TextureFormat.ARGB32, false);
				try
				{
					AssetManager.SafeWWWLoadTexture(ref _outskirtsPalette, "file:///" + AssetManager.ResolveFilePath(Path.Combine("Palettes", "palette0.png")), false, true);
				}
				catch (FileLoadException) { }
			}
			_paletteImage = new(_outskirtsPalette) { anchorX = 1f, anchorY = 0f, scale = float.Parse(PaletteImageScale.defaultValue) };
			Futile.stage.AddChild(_paletteImage);
			_paletteImage.SetPosition(imagePos);
		}

		public override void Change()
		{
			base.Change();

			if (_paletteImage != null)
			{
				_paletteImage.scale = this.GetValueFloat();
			}
		}

		public override void Deactivate()
		{
			if (_paletteImage != null)
				_paletteImage.isVisible = false;
			base.Deactivate();
		}

		public override void Reactivate()
		{
			if (_paletteImage != null)
				_paletteImage.isVisible = true;
			base.Reactivate();
		}
	}
}
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
using RWCustom;

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
	private Vector2 _nextItemPos;

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

	public static Configurable<bool> LoadSavedPalettes { get; } = Instance.config.Bind(nameof(LoadSavedPalettes), true, new ConfigurableInfo(
		"Whether the game should prioritize any saved palettes when loading a palette file.",
		null, "", null));

	public static Configurable<float> PaletteImageScale { get; } = Instance.config.Bind(nameof(PaletteImageScale), 10f, new ConfigurableInfo(
		"The size of the editting palette.",
		null, "", null));

	public static Configurable<bool> ShowKeyLines { get; } = Instance.config.Bind(nameof(ShowKeyLines), true, new ConfigurableInfo(
		"Whether the palette key will have the main key lines ontop for distinction.",
		null, "", null));

	public static Configurable<bool> ShowKeyToolTip { get; } = Instance.config.Bind(nameof(ShowKeyToolTip), true, new ConfigurableInfo(
		"Whether to show what value is being currently hovered.",
		null, "", null));

	public static Configurable<bool> ShowUnusedKeyXs { get; } = Instance.config.Bind(nameof(ShowUnusedKeyXs), true, new ConfigurableInfo(
		"Whether to show what keys are not in use.",
		null, "", null));

	public OpTab ModOptionsTab { get; private set; }

	public override void Initialize()
	{
		base.Initialize();

		ModOptionsTab = new OpTab(this, "Mod Options");

		Tabs = [ModOptionsTab];

		_nextItemPos = new(Margin, ModOptionsTab.CanvasSize.y - (Margin * 3f));

		AddItem(new OpDragger(UndoStack, _nextItemPos) { max = 100, min = 0 }, "Undo Stack");
		AddItem(new OpCheckBox(LoadSavedPalettes, _nextItemPos), "Prioritize Saved Palettes");
		AddItem(new PaletteScaleSlider(PaletteImageScale, _nextItemPos, ModOptionsTab._container.GetPosition() + new Vector2(ModOptionsTab.CanvasSize.x - Margin, Margin), 100, 0)
		{
			max = 15f,
			min = 8f,
			_increment = 50,
		}, "Image Scale");
		AddItem(new KeyLineCheckBox(ShowKeyLines, _nextItemPos), "Show Key Lines");
		AddItem(new KeyToolTipButton(ShowKeyToolTip, _nextItemPos), "Show Key ToolTip");
		AddItem(new UnusedKeysButton(ShowUnusedKeyXs, _nextItemPos), "Use Xs for Unused Keys");
	}

	internal void AddItem(UIconfig item, string text)
	{
		_nextItemPos = item.pos + new Vector2(0f, -(item.size.y + Margin));

		item.description = item.cfgEntry.info.description;
		OpLabel label = new(item.pos.x + item.size.x + Margin, item.pos.y, text);

		ModOptionsTab.AddItems(item, label);
	}

	internal class UnusedKeysButton : OpCheckBox
	{
		public UnusedKeysButton(Configurable<bool> config, Vector2 pos) : base(config, pos)
		{
		}

		public override void Change()
		{
			base.Change();

			if (PaletteScaleSlider._xLines != null)
			{
				foreach (var x in PaletteScaleSlider._xLines)
				{
					x.forceHide = !this.GetValueBool();
					x.Show(this.GetValueBool());
				}
			}
		}
	}

	internal class KeyToolTipButton : OpCheckBox
	{
		public KeyToolTipButton(Configurable<bool> config, Vector2 pos) : base(config, pos)
		{
		}

		public override void Change()
		{
			base.Change();
			
			if (PaletteScaleSlider._hoverKey != null)
			{
				PaletteScaleSlider._hoverKey.forceHide = !this.GetValueBool();
			}
		}
	}


	internal class KeyLineCheckBox : OpCheckBox
	{
		public KeyLineCheckBox(Configurable<bool> config, Vector2 pos) : base(config, pos)
		{
		}

		public override void Change()
		{
			base.Change();
			if (PaletteScaleSlider._keyLines != null)
			{
				for (int i = 0; i < PaletteScaleSlider._keyLines.Length - 4; i++)
				{
					PaletteScaleSlider._keyLines[i].isVisible = this.GetValueBool();
				}
			}
		}
	}

	internal class PaletteScaleSlider : OpFloatSlider
	{
		private FTexture _paletteImage;
		private static Texture2D _outskirtsPalette;
		internal static PaletteEditor.KeyLine[] _keyLines;
		private IntVector2 _exactHoveredPixel;
		internal static PaletteEditor.HoverToolTip _hoverKey;
		internal static PaletteEditor.UnusedKeySprites[] _xLines;

		public PaletteScaleSlider(Configurable<float> config, Vector2 pos, Vector2 imagePos, int length, byte decimalNum = 1, bool vertical = false) : base(config, pos, length, decimalNum, vertical)
		{
			if (_outskirtsPalette == null)
			{
				_outskirtsPalette = new Texture2D(PaletteEditor.PalPixelSize.x, PaletteEditor.PalPixelSize.y, TextureFormat.ARGB32, false);
				try
				{
					AssetManager.SafeWWWLoadTexture(ref _outskirtsPalette, "file:///" + AssetManager.ResolveFilePath(Path.Combine("Palettes", "palette0.png")), false, true);
				}
				catch (FileLoadException) { }
			}
			_paletteImage = new(_outskirtsPalette) { anchorX = 1f, anchorY = 0f, scale = float.Parse(PaletteImageScale.defaultValue) };
			Futile.stage.AddChild(_paletteImage);
			_paletteImage.SetPosition(imagePos);

			_xLines = PaletteEditor.UnusedKeySprites.GetUnusedKeySprites(null, true);
			foreach (var x in _xLines)
			{
				x.UpdateColor(_outskirtsPalette);
			}

			_keyLines = PaletteEditor.KeyLine.GetKeyLines(true);
			foreach (var k in _keyLines)
			{
				Futile.stage.AddChild(k);
				k.SetPos(_paletteImage.GetPosition(), float.Parse(PaletteImageScale.defaultValue));
			}

			_hoverKey = new PaletteEditor.HoverToolTip(null, null, Margin);
		}

		public override void Update()
		{
			base.Update();

			if (_paletteImage != null)
			{
				Vector2 m = Futile.mousePosition;
				Vector2 p = _paletteImage.GetPosition();
				Vector2 pS = new(_paletteImage.width, _paletteImage.height);
				float value = this.GetValueFloat();

				_exactHoveredPixel = ClampIntVector((m - p + new Vector2(pS.x, 0f) - (new Vector2(_paletteImage.scale, _paletteImage.scale) / 2f)) / new Vector2((float)PaletteEditor.PalPixelSize.x * value, (float)PaletteEditor.PalPixelSize.y * value));
				_hoverKey?.Update();
				bool getStringKey = PaletteEditor.PaletteKeys[_exactHoveredPixel.x, (PaletteEditor.PalPixelSize.y - 1) - _exactHoveredPixel.y].TryGet(out string key);
				_hoverKey?.Show(m.x < p.x && m.x > p.x - pS.x && m.y > p.y && m.y < p.y + pS.y && getStringKey && !Input.anyKey);
				_hoverKey?.SetText(key);
			}
		}

		private IntVector2 ClampIntVector(Vector2 mouseLerp)
		{
			return new(ClampTilePositon(mouseLerp.x, PaletteEditor.PalPixelSize.x), ClampTilePositon(mouseLerp.y, PaletteEditor.PalPixelSize.y));
		}

		private int ClampTilePositon(float mouseLerp, int limit)
		{
			return (int)Mathf.Max(0f, Mathf.Min(Mathf.Round(mouseLerp * limit), limit - 1));
		}

		public override void Change()
		{
			base.Change();

			float value = this.GetValueFloat();
			if (_paletteImage != null)
			{
				_paletteImage.scale = value;
			}
			foreach (var k in _keyLines)
			{
				k?.SetScale(value);
				k?.SetPos(_paletteImage.GetPosition(), value);
			}
			foreach (var x in _xLines)
			{
				x?.SetScale(value);
				x?.SetPos(_paletteImage.GetPosition(), value);
			}
		}

		public override void Deactivate()
		{
			if (_paletteImage != null)
				_paletteImage.isVisible = false;
			foreach (var k in _keyLines)
			{
				if (k != null)
				{
					k.isVisible = false;
				}
			}
			foreach (var x in _xLines)
			{
				x.Show(false);
			}
			base.Deactivate();
		}

		public override void Reactivate()
		{
			if (_paletteImage != null)
				_paletteImage.isVisible = true;
			foreach (var k in _keyLines)
			{
				if (k != null)
				{
					k.isVisible = true;
				}
			}
			foreach (var x in _xLines)
			{
				x.Show(true);
			}
			base.Reactivate();
		}
	}
}
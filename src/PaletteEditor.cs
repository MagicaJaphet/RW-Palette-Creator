using DevInterface;
using MagicaHookingLibrary.Helpers;
using Menu;
using Menu.Remix;
using Menu.Remix.MixedUI;
using RWCustom;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using UnityEngine;
using static PaletteEditor.PaletteEditor.PalettePage.PalettePreviewer;

namespace PaletteEditor;
internal class PaletteEditor
{
	internal class UndoablePalette
	{
		internal Texture2D Texture
		{
			get => _texture;
			set
			{
				_texture = value;
				Clear();
			}
		}
		private Texture2D _texture;

		private int _index;
		private static int UndoStack { get => RemixOptions.UndoStack.Value; }
		private readonly List<Color[]> _stack = [];
		private Color[] _init;
		internal static readonly int _colorHistory = 8;
		internal Color[] _history = new Color[_colorHistory];
		internal static FSprite[] _historySprites = new FSprite[_colorHistory];

		internal bool CanUndo
		{
			get
			{
				return _index + 1 < _stack.Count;
			}
		}

		internal bool CanRedo
		{
			get
			{
				return _index - 1 >= 0;
			}
		}

		public int PalIndex { get; }

		internal UndoablePalette(int index) => PalIndex = index;

		private void UpdateHistory(int index, float alpha, Color color)
		{
			_history[index] = color;
			_history[index].a = alpha;
			if (_historySprites.Length > index && _historySprites[index] != null)
			{
				_historySprites[index].color = color;
				_historySprites[index].alpha = alpha;
			}
		}

		internal void Paint(int x, int y, bool blend = true)
		{
			if (Texture == null) return;
			ColorOperator a = new(Texture.GetPixel(x, y)); // base layer
			ColorOperator b = new(PaintColor.Value); // top layer

			// Blend mode calculations
			if (blend)
			{
				Color equal = b.Color;
				equal.a = Alpha;
				if (_history[0] != equal)
				{
					for (int i = _history.Length - 1; i >= 1; i--)
					{
						var lastState = _history[i - 1];
						UpdateHistory(i, lastState.a, lastState);
					}

					UpdateHistory(0, Alpha, b.Color);
				}

				switch (CurrentBlendMode)
				{
					case BlendMode.Normal:
						break;

					case BlendMode.Multiply:
						b *= a;
						break;

					case BlendMode.Screen:
						b = (a.Inverted * b.Inverted).Inverted;
						break;

					case BlendMode.Overlay:
						if (a < 0.5f)
						{
							b = 2f * a * b;
						}
						else
						{
							b = (2f * a.Inverted * b.Inverted).Inverted;
						}
						break;

					case BlendMode.HardLight:
						if (b < 0.5f)
						{
							b = 2f * a * b;
						}
						else
						{
							b = (2f * a.Inverted * b.Inverted).Inverted;
						}
						break;

					case BlendMode.SoftLight:
						if (b <= 0.5f)
						{
							b = a - ((2f * b).Inverted * a * a.Inverted);
						}
						else
						{
							ColorOperator g = a <= 0.25f ?
								((((16 * a) - 12f) * a) + 4f) * a
								: ColorOperator.Sqrt(a);

							b = a + (((2f * b) - 1f) * (g - a));
						}
						break;

					case BlendMode.ColorDodge:
						b = a / b.Inverted;
						break;

					case BlendMode.Burn:
						b = (b.Inverted / a).Inverted;
						break;

					case BlendMode.Divide:
						b = a / b;
						break;

					case BlendMode.Add:
						b += a;
						break;

					case BlendMode.Darken:
						b = ColorOperator.Min(a, b);
						break;

					case BlendMode.Lighten:
						b = ColorOperator.Max(a, b);
						break;
				}
			}

			Texture.SetPixel(x, y, Color.Lerp(a.Color, b.Color, Alpha));
		}

		internal void Paint(Color[] cols)
		{
			if (Texture == null || cols.Length != Texture.GetPixels().Length) return;
			Texture.SetPixels(cols);
		}

		internal void PickColor(int x, int y)
		{
			if (Texture == null) return;
			PaintColor.Value = Texture.GetPixel(x, y);
		}

		internal void Apply(RoomCamera rCam)
		{
			if (Texture == null) return;
			Texture.Apply();
			PalettePage.Preview?.SetTexture(Texture);
			rCam?.ApplyFade();
		}

		internal void Reset(RoomCamera rCam)
		{
			if (Texture == null || _init == null) return;
			Texture.SetPixels(_init);
			Apply(rCam);
			Clear();
			AddToStack();
		}

		internal void Init()
		{
			if (Texture == null || _stack.Count > 0) return;
			_init = Texture.GetPixels();
			AddToStack();
		}

		internal void AddToStack()
		{
			if (Texture == null) return;

			if (_index != 0)
			{
				_stack.RemoveRange(0, Math.Min(_index, _stack.Count - 1));
				_index = 0;
			}
			_stack.Insert(0, Texture.GetPixels());
			if (_stack.Count > UndoStack)
			{
				_stack.RemoveRange(UndoStack, _stack.Count - UndoStack);
			}
		}

		internal void Undo(RoomCamera rCam)
		{
			if (CanUndo)
			{
				Paint(GetStack(ClampIndex(++_index)));
				Apply(rCam);
			}
		}

		internal void Redo(RoomCamera rCam)
		{
			if (CanRedo)
			{
				Paint(GetStack(ClampIndex(--_index)));
				Apply(rCam);
			}
		}

		private void Clear()
		{
			_stack.Clear();
			_index = 0;
		}

		private Color[] GetStack(int index)
		{
			if (index >= 0 && index < _stack.Count)
				return _stack[index];
			return null;
		}

		private int ClampIndex(int index)
		{
			return Math.Max(0, Math.Min(index, UndoStack - 1));
		}

		internal void CopySunToRain(RoomCamera rCam)
		{
			if (Texture == null) return;
			for (int x = 0; x < PalPixelSize.x; x++)
			{
				for (int y = 15; y >= 8; y--) // Texture2D index from the bottom left, so start at the top
				{
					Texture.SetPixel(x, y - 8, Texture.GetPixel(x, y));
				}
			}
			AddToStack();
			Apply(rCam);
		}
	}

	internal static UndoablePalette MainPalette { get; set; } = new(0);
	internal static UndoablePalette FadePalette { get; set; } = new(1);
	internal static List<UndoablePalette> MoreFadePalettes { get; set; } = [];

	internal static Configurable<Color> PaintColor = new(null, "_colorPicker", Color.red, null);
	internal static float Alpha { get; set; } = 1f;

	internal readonly static IntVector2 PalPixelSize = new(32, 16);

	internal static void IterateThroughPixels(Action<int, int> action)
	{
		for (int x = 0; x < PalPixelSize.x; x++)
			for (int y = 0; y < PalPixelSize.y; y++) 
				action(x, y);
	}

	internal struct PaletteKey
	{
		private string _keyName;

		internal PaletteKey(string key) => _keyName = key;

		internal bool TryGet(out string key)
		{
			key = _keyName;
			return !string.IsNullOrEmpty(_keyName);
		}
	}

	private static PaletteKey[,] Init()
	{
		var palKeys = new PaletteKey[PalPixelSize.x, PalPixelSize.y];

		string[,] keys = new string[PalPixelSize.x, PalPixelSize.y / 2];

		keys[0, 0] = "Sky";
		keys[1, 0] = "Fog";
		keys[2, 0] = "Black";
		keys[3, 0] = "Item";
		keys[4, 0] = "Deep Water Top";
		keys[5, 0] = "Deep Water Bottom";
		keys[6, 0] = "Water Surface Close";
		keys[7, 0] = "Water Surface Far";
		keys[8, 0] = "Water Surface Highlight";
		keys[9, 0] = "Fog Intensity";
		keys[10, 0] = "Shortcut Dot";
		keys[11, 0] = "Shortcut Dot Blink";
		keys[12, 0] = "Shortcut Dot Travel";
		keys[13, 0] = "Shortcut Symbol";
		keys[30, 0] = "Darkness";

		for (int x = 0; x < PalPixelSize.x; x++)
		{
			keys[x, 1] = "Grime";
			if (x < 30)
			{
				keys[x, 2] = $"Sun Highlight [{x}]";
				keys[x, 3] = $"Sun Middle [{x}]";
				keys[x, 4] = $"Sun Shadow [{x}]";
				keys[x, 5] = $"Shade Highlight [{x}]";
				keys[x, 6] = $"Shade Middle [{x}]";
				keys[x, 7] = $"Shade Shadow [{x}]";
			}
			else
			{
				keys[x, 2] = "Effect Color (Not Saved)";
				keys[x, 3] = "Effect Color (Not Saved)";
				keys[x, 4] = "Effect Color (Not Saved)";
				keys[x, 5] = "Effect Color (Not Saved)";
			}
		}

		for (int x = 0; x < PalPixelSize.x; x++)
		{
			for (int y = 0; y < 8; y++)
			{
				palKeys[x, y] = new($"{keys[x, y]}");
				if (!string.IsNullOrEmpty(keys[x, y]))
					palKeys[x, y + 8] = new($"(Rain) {keys[x, y]}");
				else
				{
					UnusedKeys[x, y] = true;
					UnusedKeys[x, y + 8] = true;
				}
			}
		}

		return palKeys;
	}

	public static bool[,] UnusedKeys { get; private set; } = new bool[PalPixelSize.x, PalPixelSize.y];

	internal static PaletteKey[,] PaletteKeys { get; } = Init();

	internal class UnusedKeySprites
	{
		internal bool forceHide;
		private bool _anchorRight;
		private IntVector2 _key;
		private FSprite[] _xLines;

		internal UnusedKeySprites(IntVector2 key, DevUINode owner, bool anchorRight)
		{
			forceHide = !RemixOptions.ShowUnusedKeyXs.Value;
			_anchorRight = anchorRight;
			_key = key;
			_xLines = [
				new("pixel") { rotation = 45f },
				new("pixel") { rotation = 135f }
				];

			SetScale(PaletteScale);

			foreach (var x in _xLines)
			{
				Futile.stage.AddChild(x);
				owner?.fSprites.Add(x);
			}
		}

		internal static UnusedKeySprites[] GetUnusedKeySprites(DevUINode owner, bool anchorRight)
		{
			List<UnusedKeySprites> u = [];
			IterateThroughPixels((x, y) =>
			{
				if (UnusedKeys[x, y]) u.Add(new(new(x,y), owner, anchorRight));
			});
			return [.. u];
		}

		internal void SetScale(float scale)
		{
			foreach (var x in  _xLines)
			{
				x.scaleX = Mathf.Sqrt(2) * scale;
			}
		}

		internal void SetPos(Vector2 pos, float scale)
		{
			Vector2 offset = new((_key.x * scale) + (scale / 2f), ((15 - _key.y) * scale) + 0.5f + (scale / 2f));
			foreach (var x in _xLines)
			{
				x.SetPosition(pos + offset + new Vector2(_anchorRight ? -((float)PalPixelSize.x * scale) : 0f, 0f));
			}
		}

		internal void UpdateColor(Texture2D tex)
		{
			if (tex == null) return;

			Color inverted = new ColorOperator(tex.GetPixel(_key.x, (PalPixelSize.y - 1) - _key.y)).Inverted.Color;
			foreach (var x in _xLines)
			{
				x.color = inverted;
			}
		}

		internal void Show(bool show)
		{
			if (forceHide) show = false;

			foreach (var x in _xLines)
			{
				x.isVisible = show;
			}
		}
	}

	internal class KeyLine : FSprite
	{
		internal IntVector2 _initialPos; // Based on the bottom left of the pixel it resides on
		internal int _initialSize;
		internal bool _vertical;
		private bool _anchorRight;

		internal KeyLine(IntVector2 initialPos, int initialSize, bool vertical, bool anchorRight) : base("pixel")
		{
			_initialPos = initialPos;
			_initialSize = initialSize;
			_vertical = vertical;
			_anchorRight = anchorRight;

			SetScale(PaletteScale);
		}

		internal void SetScale(float scale)
		{
			if (_vertical)
			{
				scaleY = (_initialSize * scale);
				anchorY = 0f;
			}
			else
			{
				scaleX = _initialSize * scale;
				anchorX = 0f;
			}
		}

		internal void SetPos(Vector2 pos, float scale)
		{
			Vector2 offset = new(_initialPos.x * scale, ((15 - _initialPos.y) * scale) + 0.5f);
			SetPosition(pos + offset + new Vector2(_anchorRight ? -((float)PalPixelSize.x * scale) : 0f, 0f));
		}

		internal static KeyLine[] GetKeyLines(bool anchorRight)
		{
			List<KeyLine> k = [];

			// Initial ones from the sun palette
			k = [
				// Top row
				new(new(2, 0), 1, true, anchorRight),
				new(new(4, 0), 1, true, anchorRight),
				new(new(9, 0), 1, true, anchorRight),
				new(new(10, 0), 1, true, anchorRight),	
				new(new(13, 0), 1, true, anchorRight),
				new(new(30, 0), 1, true, anchorRight),
				new(new(31, 0), 1, true, anchorRight),

				// Grime dividers
				new(new(0, 0), PalPixelSize.x, false, anchorRight),
				new(new(0, 1), PalPixelSize.x, false, anchorRight),

				// Sun / shade divider
				new(new(0, 4), PalPixelSize.x, false, anchorRight),

				// Sublayer dividers
				new(new(10, 7), 6, true, anchorRight),
				new(new(20, 7), 6, true, anchorRight),
			];

			// Then duplicate them
			int count = k.Count;
			for (int i = 0; i < count; i++)
			{
				k.Add(new(k[i]._initialPos + new IntVector2(0, 8), k[i]._initialSize, k[i]._vertical, anchorRight));
			}

			k.Add(new(new(0, 7), PalPixelSize.x, false, anchorRight)); // Divider

			foreach (var key in k)
			{
				key.isVisible = RemixOptions.ShowKeyLines.Value;
			}

			k.AddRange([ // Surrounding boxes
				new(new(0, -1), PalPixelSize.x, false, anchorRight),
				new(new(0, 15), PalPixelSize.y, true, anchorRight),
				new(new(0, 15), PalPixelSize.x, false, anchorRight),
				new(new(PalPixelSize.x, 15), PalPixelSize.y, true, anchorRight),
				]);

			return [.. k];
		}
	}

	internal class HoverToolTip
	{
		internal bool forceHide;
		private float _margin;
		private FLabel _label;
		private FSprite _box;
		private FSprite[] _boxLines;
		private float _lastValidMouseX;

		internal HoverToolTip(Panel parentNode, DevUINode owner, float margin)
		{
			forceHide = !RemixOptions.ShowKeyToolTip.Value;
			_margin = margin;
			_label = new FLabel(Custom.GetFont(), "") { anchorX = 0f, anchorY = 0f };
			_box = new FSprite("pixel")
			{
				anchorX = 0f,
				anchorY = 0f,
				color = parentNode != null ? parentNode.fSprites[0].color : MenuColorEffect.rgbDarkGrey,
				alpha = parentNode != null ? parentNode.fSprites[0].alpha : 0.5f,
				scaleX = margin * 2f,
				scaleY = _label.FontLineHeight + (margin * 2f)
			};
			_boxLines = [
				new("pixel") { scaleX = _box.scaleX, anchorX = 0f },
						new("pixel") { scaleY = _box.scaleY + 1.2f, anchorY = 0f },
						new("pixel") { scaleX = _box.scaleX + 1f, anchorX = 0f },
						new("pixel") { scaleY = _box.scaleY, anchorY = 0f },
						];

			Futile.stage.AddChild(_box);
			owner?.fSprites.Add(_box);
			Futile.stage.AddChild(_label);
			owner?.fLabels.Add(_label);

			foreach (var h in _boxLines)
			{
				Futile.stage.AddChild(h);
				owner?.fSprites.Add(h);
			}
		}

		internal void Update()
		{
			if (Futile.mousePosition.x < Custom.rainWorld.options.ScreenSize.x - _box.scaleX)
			{
				_lastValidMouseX = Futile.mousePosition.x + _box.scaleX;
			}
			_box.SetPosition(new(Mathf.Min(Futile.mousePosition.x, _lastValidMouseX - _box.scaleX), Mathf.Max(Futile.mousePosition.y, 0f)));
			_label.SetPosition(_box.GetPosition() + new Vector2(_margin + 0.001f, _margin));
			for (int i = 0; i < _boxLines.Length; i++)
			{
				var line = _boxLines[i];
				line.SetPosition(_box.GetPosition() + i switch
				{
					1 => new Vector2(0f, -0.5f),
					2 => new Vector2(0f, _box.scaleY),
					3 => new Vector2(_box.scaleX + 0.5f, 0f),
					_ => new Vector2()
				});
			}
		}

		internal void SetText(string text)
		{
			if (string.IsNullOrEmpty(text)) return;
			_label.text = text;
			_box.scaleX = _label.textRect.width + (_margin * 2f);

			_boxLines[0].scaleX = _box.scaleX;
			_boxLines[2].scaleX = _box.scaleX + 1f;
		}

		internal void Show(bool show)
		{
			if (forceHide) show = false;

			_label.isVisible = show;
			_box.isVisible = show;
			foreach (var h in _boxLines)
			{
				h.isVisible = show;
			}
		}
	}

	internal enum BlendMode
	{
		Normal,
		Multiply,
		Screen,
		Overlay,
		HardLight,
		SoftLight,
		ColorDodge,
		Burn,
		Divide,
		Add,
		Darken,
		Lighten
	}
	internal static BlendMode CurrentBlendMode { get; set; } = BlendMode.Normal;

	/// <summary>
	/// The scale of the palette preview.
	/// </summary>
	internal static float PaletteScale { get => RemixOptions.PaletteImageScale.Value; }
	internal class PalettePage : DevInterface.Page
	{
		internal static string Name { get; } = "Palette Editor";
		internal static float Margin { get; } = 5f;
		internal static Vector2 Padding { get; } = new(Margin * 2f, Margin * 2f);
		internal static float GenericElementHeight { get; } = 20f;
		internal static FTexture Preview { get; set; }
		internal static Vector2 PaletteImageSize { get => new Vector2(PalPixelSize.x, PalPixelSize.y) * PaletteScale; }

		private static Vector2 _palettePanelSize { get => PaletteImageSize + new Vector2(0f, (GenericElementHeight * 2f) + Margin); }
		private SmallElements.BlendModeButton _blendModeButton;

		/// <summary>
		/// Sets up the UI for the palette editor.
		/// </summary>
		internal PalettePage(DevUI owner) : base(owner, "Palette_Editor_Page", null, Name)
		{
			MainPalette.Init();
			FadePalette.Init();

			if (RegionKitWrapper.RegionKitEnabled)
			{
				foreach (var fade in MoreFadePalettes)
				{
					fade?.Init();
				}
			}

			Panel palettePreview = new(owner, "Palette_Image", this, new(Custom.rainWorld.options.ScreenSize.x - _palettePanelSize.x - Margin - Padding.x, Margin), _palettePanelSize + Padding, "Palette Image");
			subNodes.Add(palettePreview);
			palettePreview.subNodes.Add(new PalettePreviewer(owner, palettePreview));
			palettePreview.subNodes.Add(new SmallElements.ReloadButton(owner, palettePreview));
			palettePreview.subNodes.Add(new SmallElements.SaveButton(owner, palettePreview));
			for (int i = 0; i < 2 + (RegionKitWrapper.RegionKitEnabled ? MoreFadePalettes.Count : 0); i++)
			{
				palettePreview.subNodes.Add(new SmallElements.PaletteButton(owner, palettePreview, i));
			}
			palettePreview.subNodes.Add(new SmallElements.CopySunToRainButton(owner, palettePreview));	

			Panel colorPicker = new(owner, "Color_Picker", this, palettePreview.absPos - new Vector2(200f, 0f), new(190f, 205f), "Color Picker");
			subNodes.Add(colorPicker);
			colorPicker.subNodes.Add(new ColorPicker(owner, colorPicker));
			colorPicker.subNodes.Add(new SmallElements.AlphaSlider(owner, colorPicker));
			_blendModeButton = new SmallElements.BlendModeButton(owner, colorPicker);
			colorPicker.subNodes.Add(_blendModeButton);
			for (int i = 0; i < UndoablePalette._colorHistory; i++)
			{
				colorPicker.subNodes.Add(new SmallElements.ColorHistory(owner, colorPicker, i));
			}

			Panel _tempSettingsPanel = new Panel(owner, "Temp_Settings", this, new(20f, 20f), new(200f, 70f), "Preview Settings");
			subNodes.Add(_tempSettingsPanel);
			_tempSettingsPanel.subNodes.Add(new SmallElements.RainSlider(owner, _tempSettingsPanel));
			_tempSettingsPanel.subNodes.Add(new SmallElements.WaterButton(owner, _tempSettingsPanel));
			_tempSettingsPanel.subNodes.Add(new SmallElements.WaterSlider(owner, _tempSettingsPanel));
		}

		public override void Signal(DevUISignalType type, DevUINode sender, string message)
		{
			if (sender == _blendModeButton)
			{
				CurrentBlendMode = (BlendMode)Enum.Parse(typeof(BlendMode), message);
				_blendModeButton.Text = message;
			}
		}

		internal class SmallElements
		{
			internal class CopySunToRainButton : Button
			{
				public CopySunToRainButton(DevUI owner, Panel parentNode) : base(owner, "Sun_To_Rain", parentNode, new(Margin, Margin), 100f, "Copy Sun to Rain")
				{
				}

				public override void Clicked()
				{
					base.Clicked();

					SelectedPalette.CopySunToRain(owner.game.cameras[0]);
				}
			}

			internal class ColorHistory : RectangularDevUINode
			{
				private int _index;
				private FSprite[] _boxLines;

				internal ColorHistory(DevUI owner, Panel parentNode, int index) : base(owner, "Color_History", parentNode, new(160f, 205f - (GenericElementHeight + Margin + ((GenericElementHeight + Margin) * index))), new(25f, GenericElementHeight))
				{
					_index = index;

					UndoablePalette._historySprites[index] = new("pixel")
					{
						width = size.x,
						height = size.y,
						anchorX = 0,
						anchorY = 0,
					};
					Futile.stage.AddChild(UndoablePalette._historySprites[index]);
					fSprites.Add(UndoablePalette._historySprites[index]);

					if (SelectedPalette != null && SelectedPalette._history[index] != null)
					{
						Color col = SelectedPalette._history[index];
						UndoablePalette._historySprites[index].color = col;
						UndoablePalette._historySprites[index].alpha = col.a;
					}

					_boxLines = [
						new("pixel") { scaleX = size.x, anchorX = 0 },
						new("pixel") { scaleY = size.y + 0.5f, anchorY = 0 },
						new("pixel") { scaleX = size.x, anchorX = 0 },
						new("pixel") { scaleY = size.y, anchorY = 0 }
						];
					foreach (var b in _boxLines)
					{
						Futile.stage.AddChild(b);
						fSprites.Add(b);
					}
				}

				public override void Update()
				{
					base.Update();
					if (MouseOver && !WasClicked && Input.GetMouseButtonDown(0))
					{
						PaintColor.Value = UndoablePalette._historySprites[_index].color;
						Alpha = UndoablePalette._historySprites[_index].alpha;
					}
				}

				public override void Refresh()
				{
					base.Refresh();
					MoveSprite(0, absPos);
					for (int i = 0; i < _boxLines.Length; i++)
					{
						MoveSprite(i + 1, absPos +
							i switch
							{
								1 => new Vector2(0f, -0.5f),
								2 => new Vector2(0f, size.y),
								3 => new Vector2(size.x, 0f),
								_ => new Vector2()
							});
					}
				}
			}

			internal static void DestroyTheFUCKINGWater(DevUI owner)
			{
				owner.room.water = false;
				owner.room.waterObject.fWaterLevel = -100f;
				owner.room.waterObject.lastFWaterLevel = -100f;
				owner.game.cameras[0].waterLight?.CleanOut();
				owner.game.cameras[0].waterLight = null;
				owner.room.waterObject.Destroy();
				owner.room.drawableObjects.Remove(owner.room.waterObject);
				owner.room.waterObject = null;
			}

			internal class WaterButton : Button
			{
				public WaterButton(DevUI owner, Panel parentNode) : base(owner, "Add_Water", parentNode, new(5f, 30f), 190f, owner.room.waterObject != null ? "Remove Water" : "Add Water") { }

				public override void Clicked()
				{
					if (owner.room.waterObject == null)
					{
						owner.room.defaultWaterLevel = (int)Mathf.Lerp(0f, owner.room.TileHeight, WaterSlider.waterHeight);
						owner.room.AddWater();
					}
					else
					{
						DestroyTheFUCKINGWater(owner);
					}
					Text = owner.room.waterObject != null ? "Remove Water" : "Add Water";
					Refresh();
					base.Clicked();
				}
			}

			internal class WaterSlider : DevInterface.Slider
			{
				private FSprite _waterLinePreview;

				internal static float waterHeight = 0.1f;
				private int _origDefaultWaterLevel = -1;
				private float _lastHeight;

				public WaterSlider(DevUI owner, Panel parentNode) : base(owner, "Water_Height", parentNode, new(5f, 50f), "Water Level", false, 60f)
				{
					if (owner.room.waterObject != null)
					{
						waterHeight = owner.room.defaultWaterLevel / owner.room.TileHeight;
						_origDefaultWaterLevel = owner.room.defaultWaterLevel;
					}
					_waterLinePreview = new FSprite("pixel") { scaleX = Custom.rainWorld.options.ScreenSize.x, scaleY = 2f, color = Color.blue, anchorX = 0f };
					Futile.stage.AddChild(_waterLinePreview);
					fSprites.Add(_waterLinePreview);
				}

				public override void NubDragged(float nubPos)
				{
					if (_lastHeight != nubPos)
					{
						_lastHeight = nubPos;

						waterHeight = RoundNub(nubPos);
						parentNode.Refresh();
						Refresh();

						if (owner.room.waterObject != null)
						{
							DestroyTheFUCKINGWater(owner);

							owner.room.defaultWaterLevel = (int)Mathf.Lerp(0f, owner.room.TileHeight, waterHeight);
							owner.room.AddWater();
						}
					}
				}

				private float RoundNub(float nubPos)
				{
					float mul = 20f * (1f / (float)owner.room.TileHeight) * 100f;
					return Mathf.Round(nubPos * mul) / mul;
				}

				public override void Refresh()
				{
					base.Refresh();
					NumberText = ((int)Mathf.Lerp(0f, owner.room.TileHeight, waterHeight)).ToString();
					RefreshNubPos(RoundNub(waterHeight));
					_waterLinePreview.SetPosition(new(0f, (Mathf.Lerp(0f, owner.room.TileHeight, waterHeight) * 20f) - 20f - owner.game.cameras[0].CamPos(0).y));
					_waterLinePreview.MoveToBack();

				}

				public override void ClearSprites()
				{
					base.ClearSprites();

					if (owner.room.waterObject != null)
					{
						owner.room.waterObject.fWaterLevel = -100f;
						owner.room.waterObject.lastFWaterLevel = -100f;
						owner.room.waterObject.Destroy();
						owner.room.waterObject = null;

						if (_origDefaultWaterLevel != -1)
						{
							owner.room.defaultWaterLevel = _origDefaultWaterLevel;
							owner.room.AddWater();
						}
					}
				}
			}

			internal class RainSlider : DevInterface.Slider
			{
				internal static float rainLerp;
				private float _prevClds;
				public RainSlider(DevUI owner, Panel parentNode) : base(owner, "Rain_Lerp", parentNode, new(5f, 5f), "Rain Blend", false, 60f)
				{
					rainLerp = 0f;
					_prevClds = owner.room.roomSettings.Clouds;
				}

				public override void NubDragged(float nubPos)
				{
					rainLerp = nubPos;
					owner.room.roomSettings.Clouds = Mathf.Max(_prevClds, rainLerp);
					parentNode.Refresh();
					Refresh();
				}

				public override void Refresh()
				{
					base.Refresh();
					NumberText = $"{Mathf.Round(rainLerp * 100f)}%";
					RefreshNubPos(rainLerp);
				}

				public override void ClearSprites()
				{
					base.ClearSprites();
					owner.room.roomSettings.Clouds = _prevClds;
					rainLerp = 0f;
				}
			}

			internal class AlphaSlider : DevInterface.Slider
			{
				private float _lastAlpha;

				internal AlphaSlider(DevUI owner, Panel parentNode) : base(owner, "Color_Alpha", parentNode, new(Margin, (Margin * 2f) + GenericElementHeight), "Alpha", false, 20f) { }

				public override void NubDragged(float nubPos)
				{
					Alpha = nubPos;
					parentNode.Refresh();
					Refresh();
				}

				public override void Update()
				{
					base.Update();

					if (_lastAlpha != Alpha)
					{
						_lastAlpha = Alpha;
						RefreshNubPos(Alpha);
					}
				}

				public override void Refresh()
				{
					base.Refresh();
					NumberText = "";
					RefreshNubPos(Alpha);
				}
			}

			internal class BlendModeButton : ButtonWithSelectPanel
			{
				internal static SelectPanel MakePanel(ButtonWithSelectPanel button)
				{
					string[] modes = Enum.GetNames(typeof(BlendMode));
					return new SelectPanel(button.owner, "Blend_Mode_Select", button, new(button.pos.x - 150f, Margin), new(155f, (Margin * 2f) + (GenericElementHeight * modes.Length)), "Blend Modes", modes);
				}

				internal BlendModeButton(DevUI owner, Panel parentNode) : base(owner, "Blend_Mode", parentNode, new(Margin, Margin), 150f, Enum.GetName(typeof(BlendMode), BlendMode.Normal), new MakeSelectPanel(MakePanel)) { }
			}

			internal class PaletteButton : Button
			{
				private int _index;
				private static float normalWidth = 30f;
				private static float shortWidth = 15f;

				public PaletteButton(DevUI owner, Panel parentNode, int index) : 
					base(owner, $"Palette_Selector{index}", parentNode, 
						new(Margin + ((RegionKitWrapper.RegionKitEnabled && MoreFadePalettes.Count > 0 && index > 1 ? ((shortWidth + Margin) * (index - 1)) + (normalWidth + Margin) : (normalWidth + Margin) * index)), (Margin * 2f) + GenericElementHeight), 
						RegionKitWrapper.RegionKitEnabled && MoreFadePalettes.Count > 0 && index > 0 ? shortWidth : normalWidth, 
						index == 0 ? "Main" : RegionKitWrapper.RegionKitEnabled && MoreFadePalettes.Count > 0 ? $"F{index}" : "Fade")
				{
					_index = index;
				}

				public override void Clicked()
				{
					Refresh();
					base.Clicked();
					SelectedPalette = _index switch
					{
						0 => MainPalette,
						1 => FadePalette,
						_ => MoreFadePalettes.FirstOrDefault(x => x.PalIndex == _index) ?? MainPalette // TODO Replace with regionkits fade palettes
					};
					for (int i = 0; i < UndoablePalette._colorHistory; i++)
					{
						UndoablePalette._historySprites[i].color = SelectedPalette._history[i];
						UndoablePalette._historySprites[i].alpha = SelectedPalette._history[i].a;
					}
				}
			}

			internal class ReloadButton(DevUI owner, Panel parentNode) : Button(owner, $"Reload_Palette", parentNode, new(parentNode.size.x - Margin - width, Margin), width, "Reload")
			{
				internal static float width = 45f;

				public override void Clicked()
				{
					base.Clicked();
					SelectedPalette?.Reset(owner.game.cameras[0]);
				}
			}

			internal class SaveButton : Button
			{
				private bool _saving;
				internal static float width = 65f;

				internal static string SavePath { get; } = Path.Combine(Application.streamingAssetsPath, "savedpalettes");

				public SaveButton(DevUI owner, Panel parentNode) : base(owner, $"Save_Palette", parentNode, new(parentNode.size.x - (Margin * 2f) - ReloadButton.width - width, Margin), width, "Save Image")
				{
				}

				public override void Clicked()
				{
					base.Clicked();

					if (_saving) return;
					_saving = true;

					try
					{
						if (!Directory.Exists(SavePath) && Directory.Exists(Application.streamingAssetsPath))
						{
							Directory.CreateDirectory(SavePath);
						}

						if (SelectedPalette?.Texture != null)
						{
							Texture2D cloneWithoutEffectCols = SelectedPalette.Texture.Clone();
							for (int x = 30; x < PalPixelSize.x; x++)
							{
								for (int y = 0; y < 14; y++)
								{
									if (y == 6 || y == 7) continue;
									cloneWithoutEffectCols.SetPixel(x, y, Color.white);
								}
							}
							cloneWithoutEffectCols.Apply();
							File.WriteAllBytes(Path.Combine(SavePath, $"palette{(SelectedPalette.PalIndex switch
							{
								0 => owner.room.roomSettings.pal ?? -1,
								1 => owner.room.roomSettings.fadePalette.palette,
								_ => RegionKitWrapper.RegionKitEnabled ? RegionKitWrapper.GetPalNumber(owner.room.roomSettings, SelectedPalette.PalIndex) : -1
							})}.png"), cloneWithoutEffectCols.EncodeToPNG());
						}
					}
					catch (Exception ex)
					{
						Plugin.Logger.LogError(ex);
					}

					_saving = false;
				}
			}
		}

		internal class PalettePreviewer : RectangularDevUINode
		{
			internal static bool WasClicked { get; private set; }
			internal static UndoablePalette SelectedPalette {
				get => _selectedPalette;
				set
				{
					_selectedPalette = value;
					Preview?.SetTexture(_selectedPalette.Texture);
				}
			}
			private static UndoablePalette _selectedPalette;

			private FSprite[] _hoverLines;
			private KeyLine[] _keyLines = [];
			private bool _notUndo;
			private bool _notRedo;
			private bool[,] _clickedThisFrame;
			private IntVector2 _exactHoveredPixel;
			private IntVector2 _hoveredPixel;
			private int _brushSize = 1;
			private int _maxBrushSize = 6;

			private HoverToolTip _hoverTip;
			private UnusedKeySprites[] _xKeys;

			internal PalettePreviewer(DevUI owner, Panel parentNode) : base(owner, "Palette_Image_Preview", parentNode, new(Margin, (GenericElementHeight * 2f) + (Margin * 2f)), PaletteImageSize)
			{
				_selectedPalette = MainPalette;
				Preview = new(_selectedPalette.Texture, "palettePreview")
				{
					scale = PaletteScale,
					anchorX = 0f,
					anchorY = 0f,
				};
				Futile.stage.AddChild(Preview);
				fSprites.Add(Preview);

				_hoverLines = [
					new("pixel") { scaleX = PaletteScale + 1f, anchorX = 0f },
					new("pixel") { scaleY = PaletteScale + 1f, anchorY = 0f },
					new("pixel") { scaleX = PaletteScale + 1f, anchorX = 0f },
					new("pixel") { scaleY = PaletteScale, anchorY = 0f },
				];

				foreach (var f in _hoverLines)
				{
					Futile.stage.AddChild(f);
					fSprites.Add(f);
				}
				ResizeHoverLines();

				_xKeys = UnusedKeySprites.GetUnusedKeySprites(parentNode, false);

				_keyLines = KeyLine.GetKeyLines(false);

				foreach (var k in _keyLines)
				{
					Futile.stage.AddChild(k);
					fSprites.Add(k);
				}

				_hoverTip = new(parentNode, this, Margin);

				ResetClicked();
			}

			public override void Refresh()
			{
				base.Refresh();
				MoveSprite(0, absPos);
				foreach (var k in _keyLines)
				{
					k.SetPos(absPos, PaletteScale);
				}
				foreach (var x in _xKeys)
				{
					x.SetPos(absPos, PaletteScale);
				}
			}

			public override void Update()
			{
				base.Update();

				if (MouseOver)
				{
					List<IntVector2> hoveredPixels = [];
					for (int x = 0; x < _brushSize; x++) 
					{
						for (int y = 0; y < _brushSize; y++)
						{
							hoveredPixels.Add(_hoveredPixel + new IntVector2(x, y));
						}
					}
					if (Input.GetMouseButton(0))
					{
						WasClicked = true;
						foreach (var i in hoveredPixels)
						{
							if (!_clickedThisFrame[i.x, i.y])
							{
								_clickedThisFrame[i.x, i.y] = true;
								SelectedPalette.Paint(i.x, i.y);
							}
						}
						SelectedPalette.Apply(owner.game.cameras[0]);
					}
					else if (!Input.GetMouseButton(0))
					{
						ResetClicked();
						if (WasClicked)
						{
							WasClicked = false;
							SelectedPalette.AddToStack();
						}
					}

					if (!Input.GetMouseButton(0) && Input.GetMouseButtonDown(1))
					{
						IntVector2 hovered = _exactHoveredPixel;
						SelectedPalette.PickColor(hovered.x, hovered.y);
					}

					if (Input.mouseScrollDelta.y != 0f)
					{
						if (Input.mouseScrollDelta.y > 0f)
						{
							_brushSize = Math.Min(_maxBrushSize, ++_brushSize);
						}
						else
						{
							_brushSize = Math.Max(1, --_brushSize);
						}
						ResizeHoverLines();
					}
				}

				if (MiscHelpers.CheckForSingleInput(ref _notUndo, KeyCode.LeftControl, KeyCode.Z))
				{
					SelectedPalette?.Undo(owner.game.cameras[0]);
				}

				if (MiscHelpers.CheckForSingleInput(ref _notRedo, KeyCode.LeftControl, KeyCode.X))
				{
					SelectedPalette?.Redo(owner.game.cameras[0]);
				}

				Vector2 spritePos = Preview?.GetPosition() ?? absPos;
				float mouseOffset = (PaletteScale * _brushSize) / 2f;

				_exactHoveredPixel = ClampIntVector(((Vector2)Futile.mousePosition - spritePos - (new Vector2(PaletteScale, PaletteScale) / 2f)) / PaletteImageSize, false);
				_hoveredPixel = ClampIntVector(((Vector2)Futile.mousePosition - new Vector2(mouseOffset, mouseOffset) - spritePos) / PaletteImageSize, true);
				Color inverted = new ColorOperator(SelectedPalette.Texture.GetPixel(_hoveredPixel.x, _hoveredPixel.y)).Inverted.Color;

				for (int i = 0; i < 4; i++)
				{
					MoveSprite(i + 1, GetHoverPos(new(Mathf.Lerp(spritePos.x, spritePos.x + PaletteImageSize.x, _hoveredPixel.x / (float)PalPixelSize.x), Mathf.Lerp(spritePos.y, spritePos.y + PaletteImageSize.y, _hoveredPixel.y / (float)PalPixelSize.y)), i));
					if (_hoverLines != null && i < _hoverLines.Length)
					{
						_hoverLines[i].isVisible = MouseOver;
						if (SelectedPalette?.Texture != null)
						{
							_hoverLines[i].color = inverted;
						}
					}
				}

				foreach (var x in _xKeys)
				{
					x.UpdateColor(SelectedPalette.Texture);
				}

				_hoverTip?.Update();
				bool getStringKey = PaletteKeys[_exactHoveredPixel.x, 15 - _exactHoveredPixel.y].TryGet(out string key);
				_hoverTip?.Show(MouseOver && !Input.anyKey && getStringKey);
				_hoverTip?.SetText(key);
			}

			private IntVector2 ClampIntVector(Vector2 mouseLerp, bool brush)
			{
				return new(ClampTilePositon(mouseLerp.x, PalPixelSize.x, brush), ClampTilePositon(mouseLerp.y, PalPixelSize.y, brush));
			}

			private int ClampTilePositon(float mouseLerp, int limit, bool brush)
			{
				return (int)Mathf.Max(0f, Mathf.Min(Mathf.Round(mouseLerp * limit), limit - (brush ? _brushSize : 1)));
			}

			private void ResizeHoverLines()
			{
				if (_hoverLines == null) return;

				_hoverLines[0].scaleX = (PaletteScale * _brushSize) + 1f;
				_hoverLines[1].scaleY = (PaletteScale * _brushSize) + 1f;
				_hoverLines[2].scaleX = (PaletteScale * _brushSize) + 1f;
				_hoverLines[3].scaleY = PaletteScale * _brushSize;
			}

			private void ResetClicked()
			{
				_clickedThisFrame = new bool[PalPixelSize.x, PalPixelSize.y];
			}

			private Vector2 GetHoverPos(Vector2 pos, int i)
			{
				return pos + i switch
				{
					1 => new Vector2(0f, -0.5f),
					2 => new Vector2(0f, PaletteScale * _brushSize),
					3 => new Vector2(PaletteScale * _brushSize + 0.5f, 0f),
					_ => new Vector2()
				};
			}
		}

		internal class ColorPicker : DevUINode
		{
			internal class PaletteEditorMenuWrapper : Menu.Menu
			{
				internal PaletteEditorMenuWrapper() : base(Custom.rainWorld.processManager, null) => pages = [new(this, null, "Page", 0)];
			}

			private PaletteEditorMenuWrapper _menu = new();
			private MenuTabWrapper _tab;
			private UIelementWrapper _wrapper;
			private OpColorPicker _colorPicker;
			private Color _lastColorValue;
			private static Vector2 _offset = new (Margin, (GenericElementHeight * 2f) + (Margin * 2f));

			internal ColorPicker(DevUI owner, Panel parentNode) : base(owner, "Color_Picker_Element", parentNode)
			{
				_tab = new(_menu, _menu.pages[0]);
				_colorPicker = new OpColorPicker(PaintColor, parentNode.absPos + _offset);
				_wrapper = new(_tab, _colorPicker);
			}

			public override void Update()
			{
				base.Update();

				if (!WasClicked)
				{
					if (_lastColorValue != PaintColor.Value)
					{
						_lastColorValue = PaintColor.Value;
						_colorPicker.valueColor = _lastColorValue;
					}
					_menu?.Update();
					_tab?.Update();
					_wrapper?.Update();
					_colorPicker?.Update();
					if (_colorPicker != null)
						PaintColor.Value = _colorPicker.valueColor;
				}
			}

			public override void Refresh()
			{
				base.Refresh();

				_colorPicker?.SetPos((parentNode as Panel).absPos + _offset);
				_colorPicker?.GrafUpdate(Custom.rainWorld.processManager.currentMainLoop.myTimeStacker);
				if (_colorPicker != null)
					_colorPicker._cdis0.alpha = Alpha;
			}

			public override void ClearSprites()
			{
				base.ClearSprites();
				_colorPicker?.Unload();
			}
		}
	}
}

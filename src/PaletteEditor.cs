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
using System.Runtime.InteropServices;
using UnityEngine;

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
		private static readonly int _undoStack = 30;
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

		internal void Paint(int x, int y)
		{
			if (Texture == null) return;
			ColorOperator a = new(Texture.GetPixel(x, y)); // base layer
			ColorOperator b = new(PaintColor.Value); // top layer

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

			// Blend mode calculations
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
					b = ((2f * b).Inverted * (a * a)) + (2f * b * a);
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
				_stack.RemoveRange(0, _index);
				_index = 0;
			}
			_stack.Insert(0, Texture.GetPixels());
			if (_stack.Count > _undoStack)
			{
				_stack.RemoveRange(_undoStack, _stack.Count - _undoStack);
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
		}

		private Color[] GetStack(int index)
		{
			if (index >= 0 && index < _stack.Count)
				return _stack[index];
			return null;
		}

		private int ClampIndex(int index)
		{
			return Math.Max(0, Math.Min(index, _undoStack - 1));
		}
	}

	internal static UndoablePalette MainPalette { get; set; } = new(0);
	internal static UndoablePalette FadePalette { get; set; } = new(1);
	internal static List<UndoablePalette> MoreFadePalettes { get; set; } = [];

	internal static Configurable<Color> PaintColor = new(null, "_colorPicker", Color.red, null);
	internal static float Alpha { get; set; } = 1f;
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
	internal static float PaletteScale { get; } = 10f;

	internal class PalettePage : DevInterface.Page
	{
		internal static string Name { get; } = "Palette Editor";
		internal static float Margin { get; } = 5f;
		internal static Vector2 Padding { get; } = new(Margin * 2f, Margin * 2f);
		internal static float GenericElementHeight { get; } = 20f;
		internal static FTexture Preview { get; set; }
		internal static Vector2 PaletteImageSize { get; } = new Vector2(32f, 16f) * PaletteScale;

		private static Vector2 _palettePanelSize = PaletteImageSize + new Vector2(0f, GenericElementHeight);
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

					if (PalettePreviewer.SelectedPalette != null && PalettePreviewer.SelectedPalette._history[index] != null)
					{
						Color col = PalettePreviewer.SelectedPalette._history[index];
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
					if (MouseOver && !PalettePreviewer.WasClicked && Input.GetMouseButtonDown(0))
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
						owner.room.waterObject.fWaterLevel = -100f;
						owner.room.waterObject.lastFWaterLevel = -100f;
						owner.room.waterObject.Destroy();
						owner.room.waterObject = null;
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
							owner.room.waterObject.fWaterLevel = -100f;
							owner.room.waterObject.lastFWaterLevel = -100f;
							owner.room.waterObject.Destroy();
							owner.room.waterObject = null;

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
						new(Margin + ((RegionKitWrapper.RegionKitEnabled && MoreFadePalettes.Count > 0 && index > 1 ? ((shortWidth + Margin) * (index - 1)) + (normalWidth + Margin) : (normalWidth + Margin) * index)), 5f), 
						RegionKitWrapper.RegionKitEnabled && MoreFadePalettes.Count > 0 && index > 0 ? shortWidth : normalWidth, 
						index == 0 ? "Main" : RegionKitWrapper.RegionKitEnabled && MoreFadePalettes.Count > 0 ? $"F{index}" : "Fade")
				{
					_index = index;
				}

				public override void Clicked()
				{
					Refresh();
					base.Clicked();
					PalettePreviewer.SelectedPalette = _index switch
					{
						0 => MainPalette,
						1 => FadePalette,
						_ => MoreFadePalettes.FirstOrDefault(x => x.PalIndex == _index) ?? MainPalette // TODO Replace with regionkits fade palettes
					};
					for (int i = 0; i < UndoablePalette._colorHistory; i++)
					{
						UndoablePalette._historySprites[i].color = PalettePreviewer.SelectedPalette._history[i];
						UndoablePalette._historySprites[i].alpha = PalettePreviewer.SelectedPalette._history[i].a;
					}
				}
			}

			internal class ReloadButton(DevUI owner, Panel parentNode) : Button(owner, $"Reload_Palette", parentNode, new(parentNode.size.x - Margin - width, Margin), width, "Reload")
			{
				internal static float width = 45f;

				public override void Clicked()
				{
					base.Clicked();
					PalettePreviewer.SelectedPalette?.Reset(owner.game.cameras[0]);
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

						if (PalettePreviewer.SelectedPalette?.Texture != null)
						{
							Texture2D cloneWithoutEffectCols = PalettePreviewer.SelectedPalette.Texture.Clone();
							for (int x = 30; x < 32; x++)
							{
								for (int y = 0; y < 14; y++)
								{
									if (y == 6 || y == 7) continue;
									cloneWithoutEffectCols.SetPixel(x, y, Color.white);
								}
							}
							cloneWithoutEffectCols.Apply();
							File.WriteAllBytes(Path.Combine(SavePath, $"palette{(PalettePreviewer.SelectedPalette.PalIndex switch
							{
								0 => owner.room.roomSettings.pal ?? -1,
								1 => owner.room.roomSettings.fadePalette.palette,
								_ => RegionKitWrapper.RegionKitEnabled ? RegionKitWrapper.GetPalNumber(owner.room.roomSettings, PalettePreviewer.SelectedPalette.PalIndex) : -1
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
			private bool _notUndo;
			private bool _notRedo;
			private bool[,] _clickedThisFrame;
			private IntVector2 _hoveredPixel;
			private int _brushSize = 1;
			private int _maxBrushSize = 6;

			internal PalettePreviewer(DevUI owner, DevUINode parentNode) : base(owner, "Palette_Image_Preview", parentNode, new(Margin, GenericElementHeight + Margin), PaletteImageSize)
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

				ResetClicked();
			}

			public override void Refresh()
			{
				base.Refresh();
				MoveSprite(0, absPos);
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
						IntVector2 hovered = _hoveredPixel + new IntVector2(_brushSize / 2, _brushSize / 2);
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

				for (int i = 0; i < 4; i++)
				{
					Vector2 spritePos = Preview?.GetPosition() ?? absPos;
					Vector2 spriteSize = PaletteImageSize;
					float mouseOffset = (PaletteScale * _brushSize) / 2f;
					
					Vector2 mouseLerp = ((Vector2)Futile.mousePosition - new Vector2(mouseOffset, mouseOffset) - spritePos) / PaletteImageSize;

					_hoveredPixel = ClampIntVector(mouseLerp);

					MoveSprite(i + 1, GetHoverPos(new(Mathf.Lerp(spritePos.x, spritePos.x + spriteSize.x, _hoveredPixel.x / 32f), Mathf.Lerp(spritePos.y, spritePos.y + spriteSize.y, _hoveredPixel.y / 16f)), i));
					if (_hoverLines != null && i < _hoverLines.Length)
					{
						_hoverLines[i].isVisible = MouseOver;
						if (SelectedPalette?.Texture != null)
						{
							Color pixel = SelectedPalette.Texture.GetPixel(_hoveredPixel.x, _hoveredPixel.y);
							_hoverLines[i].color = new(1f - pixel.r, 1f - pixel.g, 1f - pixel.b);
						}
					}
				}
			}

			private IntVector2 ClampIntVector(Vector2 mouseLerp)
			{
				return new(ClampTilePositon(mouseLerp.x, 32), ClampTilePositon(mouseLerp.y, 16));
			}

			private int ClampTilePositon(float mouseLerp, int limit)
			{
				return (int)Mathf.Max(0f, Mathf.Min(Mathf.Round(mouseLerp * limit), limit - _brushSize));
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
				_clickedThisFrame = new bool[32, 16];
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

				if (!PalettePreviewer.WasClicked)
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

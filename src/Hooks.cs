using DevInterface;
using MagicaHookingLibrary.Interfaces;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using RWCustom;
using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace PaletteEditor;
internal class Hooks : IOwnHooks
{
	public void PreApply()
	{
	}

	public void OnApply()
	{
		if (RegionKitWrapper.RegionKitEnabled)
			RegionKitWrapper.Hooks();

		On.WaterLight.DrawUpdate += WaterLight_DrawUpdate;
		_ = new Hook(typeof(RoomCamera).GetProperty(nameof(RoomCamera.DarkPalette), System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetGetMethod(true), RoomCamera_DarkPalette);
		On.RainWorldGame.AllowRainCounterToTick += RainWorldGame_AllowRainCounterToTick;

		On.Menu.Remix.MixedUI.OpColorPicker.DisplayDescription += OpColorPicker_DisplayDescription;
		On.RoomCamera.ApplyEffectColorsToPaletteTexture += RoomCamera_ApplyEffectColorsToPaletteTexture;
		IL.RoomCamera.LoadPalette += RoomCamera_LoadPalette;

		On.DevInterface.DevUI.ctor += DevUI_ctor;
		On.DevInterface.Page.SwitchPageButtonPos += Page_SwitchPageButtonPos;
		On.DevInterface.DevUI.SwitchPage += DevUI_SwitchPage;
	}

	private void WaterLight_DrawUpdate(On.WaterLight.orig_DrawUpdate orig, WaterLight self, Vector2 camPos)
	{
		if (self.waterObject != null)
		{
			orig(self, camPos);
		}
	}

	internal static float RoomCamera_DarkPalette(Func<RoomCamera, float> orig, RoomCamera self)
	{
		if (self.game?.devToolsActive ?? false && self.game?.devUI.activePage is PaletteEditor.PalettePage)
		{
			return PaletteEditor.PalettePage.SmallElements.RainSlider.rainLerp;
		}
		return orig(self);
	}

	private bool RainWorldGame_AllowRainCounterToTick(On.RainWorldGame.orig_AllowRainCounterToTick orig, RainWorldGame self)
	{
		return orig(self) && (!self.devToolsActive || self.devUI?.activePage is not PaletteEditor.PalettePage);
	}

	private string OpColorPicker_DisplayDescription(On.Menu.Remix.MixedUI.OpColorPicker.orig_DisplayDescription orig, Menu.Remix.MixedUI.OpColorPicker self)
	{
		if (Custom.rainWorld.processManager.currentMainLoop is RainWorldGame) return "";
		return orig(self);
	}

	private void RoomCamera_ApplyEffectColorsToPaletteTexture(On.RoomCamera.orig_ApplyEffectColorsToPaletteTexture orig, RoomCamera self, ref Texture2D texture, int color1, int color2)
	{
		orig(self, ref texture, color1, color2);

		texture.Apply();
	}

	private void RoomCamera_LoadPalette(ILContext il)
	{
		ILCursor c = new(il);

		static string PrioritizeSavedPalettes(string text, int pal)
		{
			if (RemixOptions.LoadSavedPalettes.Value)
			{
				string newPath = Path.Combine(PaletteEditor.PalettePage.SmallElements.SaveButton.SavePath, $"palette{pal}.png");
				if (File.Exists(newPath))
				{
					return newPath;
				}
			}
			return text;
		}

		c.GotoNext(x => x.MatchStloc(0));
		c.Emit(OpCodes.Ldarg_1);
		c.EmitDelegate(PrioritizeSavedPalettes);

		static void AddPaletteImageToAtlasManager(RoomCamera self, int pal, ref Texture2D texture, string path)
		{
			if (texture == self.fadeTexA)
			{
				PaletteEditor.MainPalette.Texture = texture;
			}
			if (texture == self.fadeTexB)
			{
				PaletteEditor.FadePalette.Texture = texture;
			}
		}

		c.GotoNext(
			x => x.MatchLdarg(0),
			x => x.MatchCallOrCallvirt(typeof(RoomCamera).GetProperty(nameof(RoomCamera.room)).GetGetMethod())
			);

		c.MoveAfterLabels();
		c.Emit(OpCodes.Ldarg_0);
		c.Emit(OpCodes.Ldarg_1);
		c.Emit(OpCodes.Ldarg_2);
		c.Emit(OpCodes.Ldloc_0);
		c.EmitDelegate(AddPaletteImageToAtlasManager);
	}

	private void DevUI_ctor(On.DevInterface.DevUI.orig_ctor orig, DevUI self, RainWorldGame game)
	{
		orig(self, game);

		if (!self.pages.Contains(PaletteEditor.PalettePage.Name))
			self.pages = [.. self.pages, PaletteEditor.PalettePage.Name];
	}

	private Vector2 Page_SwitchPageButtonPos(On.DevInterface.Page.orig_SwitchPageButtonPos orig, Page self, int i, string name)
	{
		if (name == PaletteEditor.PalettePage.Name)
		{
			return new Vector2(100f, 705f);
		}
		return orig(self, i, name);
	}

	private void DevUI_SwitchPage(On.DevInterface.DevUI.orig_SwitchPage orig, DevUI self, int newPage)
	{

		if (!self.pages.Contains(PaletteEditor.PalettePage.Name))
			self.pages = [.. self.pages, PaletteEditor.PalettePage.Name];
	
		if (newPage == self.pages.IndexOf(PaletteEditor.PalettePage.Name))
		{
			self.ClearSprites();
			self.activePage = new PaletteEditor.PalettePage(self);
			return;
		}

		orig(self, newPage);
	}

	public void PostApply()
	{
	}
}

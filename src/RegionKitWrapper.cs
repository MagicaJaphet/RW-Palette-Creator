using DevInterface;
using MonoMod.RuntimeDetour;
using RegionKit.Modules.Misc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using static RoomSettings;

namespace PaletteEditor;
internal class RegionKitWrapper
{
	internal static bool RegionKitEnabled { get; set; }

	internal static int GetPalNumber(RoomSettings roomSettings, int index)
	{
		if (roomSettings.GetMoreFade(index - 2) is FadePalette pal)
		{
			return pal.palette;
		}
		return -1;
	}

	internal static void Hooks()
	{
		_ = new Hook(typeof(MoreFadePalettes).GetMethod(nameof(MoreFadePalettes.ChangeMoreFade), System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static), MoreFadePalettes_ChangeMoreFade);
		_ = new Hook(typeof(MoreFadePalettes).GetMethod(nameof(MoreFadePalettes.DeleteMoreFade), System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static), MoreFadePalettes_DeleteMoreFade);
	}

	internal static void MoreFadePalettes_ChangeMoreFade(Action<RoomCamera, FadePalette[]> orig, RoomCamera self, FadePalette[] newFades)
	{
		orig(self, newFades);

		for (int i = 0; i < newFades.Length; i++)
		{
			var fade = newFades[i];
			if (self.MoreFadeTextures().TryGetValue(fade, out Texture2D tex))
			{
				if (PaletteEditor.MoreFadePalettes.Count <= i) PaletteEditor.MoreFadePalettes.Add(new(i + 2));
				PaletteEditor.MoreFadePalettes[i].Texture = tex;
			}
		}
	}

	internal static void MoreFadePalettes_DeleteMoreFade(Action<RoomSettings, int> orig, RoomSettings rs, int index)
	{
		orig(rs, index);

		if (PaletteEditor.MoreFadePalettes.Count > index && index >= 0)
		{
			PaletteEditor.MoreFadePalettes.RemoveAt(index);
		}
	}
}

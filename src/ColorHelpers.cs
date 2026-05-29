using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace PaletteEditor;

/// <summary>
/// Struct to perform <see cref="Color"/> operations that would otherwise be unavaliable.
/// </summary>
internal struct ColorOperator
{
	internal Color Color
	{
		get => clr;
		set => clr = value;
	}

	internal ColorOperator Inverted { get => 1f - new ColorOperator(Color); }

	private Color clr;

	internal ColorOperator(float r, float g, float b) => clr = new Color(r, g, b);

	internal ColorOperator(Color color) => clr = color;

	public static ColorOperator operator +(float a, ColorOperator b)
	{
		return new(a + b.clr.r, a + b.clr.g, a + b.clr.b);
	}

	public static bool operator <(float a, ColorOperator b)
	{
		return a < b.clr.r && a < b.clr.g && a < b.clr.b;
	}
	public static bool operator >(float a, ColorOperator b)
	{
		return a > b.clr.r && a > b.clr.g && a > b.clr.b;
	}
	public static bool operator <(ColorOperator b, float a)
	{
		return a < b.clr.r && a < b.clr.g && a < b.clr.b;
	}
	public static bool operator >(ColorOperator b, float a)
	{
		return a < b.clr.r && a < b.clr.g && a < b.clr.b;
	}

	public static ColorOperator operator *(float a, ColorOperator b)
	{
		return new(a * b.Color);
	}
	public static ColorOperator operator /(float a, ColorOperator b)
	{
		return new(a / b.clr.r, a / b.clr.g, a / b.clr.b);
	}

	public static ColorOperator operator -(float a, ColorOperator b)
	{
		return new(a - b.clr.r, a - b.clr.g, a - b.clr.b);
	}

	public static ColorOperator operator +(ColorOperator a, ColorOperator b)
	{
		return new(a.clr + b.clr);
	}

	public static ColorOperator operator -(ColorOperator a, ColorOperator b)
	{
		return new(a.clr - b.clr);
	}

	public static ColorOperator operator *(ColorOperator a, ColorOperator b)
	{
		return new(a.clr * b.clr);
	}

	public static ColorOperator operator /(ColorOperator a, ColorOperator b)
	{
		return new(a.clr.r / b.clr.r, a.clr.g / b.clr.g, a.clr.b / b.clr.b);
	}

	public static bool operator ==(ColorOperator a, Color b)
	{
		return a.clr == b;
	}
	public static bool operator !=(ColorOperator a, Color b)
	{
		return a.clr != b;
	}

	internal static ColorOperator Min(ColorOperator a, ColorOperator b)
	{
		return new(Mathf.Min(a.clr.r, b.clr.r), Mathf.Min(a.clr.g, b.clr.g), Mathf.Min(a.clr.b, b.clr.b));
	}

	internal static ColorOperator Max(ColorOperator a, ColorOperator b)
	{
		return new(Mathf.Max(a.clr.r, b.clr.r), Mathf.Max(a.clr.g, b.clr.g), Mathf.Max(a.clr.b, b.clr.b));
	}

	public override bool Equals(object obj)
	{
		return obj is ColorOperator op &&
			   clr.Equals(op.clr);
	}

	public override int GetHashCode()
	{
		return 241020152 + clr.GetHashCode();
	}
}

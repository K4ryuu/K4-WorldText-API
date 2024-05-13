using System.Drawing;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace K4WorldTextSharedAPI
{
	public enum TextPlacement
	{
		Wall,
		Floor,
	}

	public class TextLine
	{
		public required string Text;
		public Color Color = Color.White;
		public int FontSize = 20;
		public bool FullBright = true;
		public float Scale = 0.4f;
	}

	public interface IK4WorldTextSharedAPI
	{
		public int AddWorldText(TextPlacement placement, TextLine textLine, Vector position, QAngle angle, bool saveConfig = false);
		public int AddWorldText(TextPlacement placement, List<TextLine> textLines, Vector position, QAngle angle, bool saveConfig = false);
		public int AddWorldTextAtPlayer(CCSPlayerController player, TextPlacement placement, TextLine textLine, bool saveConfig = false);
		public int AddWorldTextAtPlayer(CCSPlayerController player, TextPlacement placement, List<TextLine> textLines, bool saveConfig = false);
		public void UpdateWorldText(int id, TextLine? textLine = null);
		public void UpdateWorldText(int id, List<TextLine>? textLines = null);
		public void RemoveWorldText(int id);
	}
}

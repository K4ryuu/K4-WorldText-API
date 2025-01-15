using System.Drawing;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace K4WorldTextSharedAPI;

public enum TextPlacement
{
    Wall,
    Floor
}

public class TextLine
{
    public Color Color = Color.White;
    public int FontSize = 20;
    public bool FullBright = true;

    public PointWorldTextJustifyHorizontal_t JustifyHorizontal =
        PointWorldTextJustifyHorizontal_t.POINT_WORLD_TEXT_JUSTIFY_HORIZONTAL_CENTER;

    public PointWorldTextJustifyVertical_t JustifyVertical =
        PointWorldTextJustifyVertical_t.POINT_WORLD_TEXT_JUSTIFY_VERTICAL_CENTER;

    public PointWorldTextReorientMode_t ReorientMode = PointWorldTextReorientMode_t.POINT_WORLD_TEXT_REORIENT_NONE;
    public float Scale = 0.4f;
    public required string Text;
}

public interface IK4WorldTextSharedAPI
{
    public int AddWorldText(TextPlacement placement, TextLine textLine, Vector position, QAngle angle,
        bool saveConfig = false);

    public int AddWorldText(TextPlacement placement, List<TextLine> textLines, Vector position, QAngle angle,
        bool saveConfig = false);

    public int AddWorldTextAtPlayer(CCSPlayerController player, TextPlacement placement, TextLine textLine,
        bool saveConfig = false);

    public int AddWorldTextAtPlayer(CCSPlayerController player, TextPlacement placement, List<TextLine> textLines,
        bool saveConfig = false);

    public void UpdateWorldText(int id, TextLine? textLine = null);
    public void UpdateWorldText(int id, List<TextLine>? textLines = null);
    public void RemoveWorldText(int id, bool removeFromConfig = true);
    public List<CPointWorldText>? GetWorldTextLineEntities(int id);
    public void TeleportWorldText(int id, Vector position, QAngle angle, bool modifyConfig = false);
    public void RemoveAllTemporary();
}
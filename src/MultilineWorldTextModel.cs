using CounterStrikeSharp.API.Modules.Utils;
using K4ryuuCS2WorldTextAPI;
using K4WorldTextSharedAPI;
using Microsoft.Extensions.Logging;
using static K4ryuuCS2WorldTextAPI.Plugin;

public class MultilineWorldText : IDisposable
{
    private static int nextId = 1;

    private bool disposed;
    public TextPlacement placement;
    private readonly Plugin Plugin;

    public Vector? SpawnOrigin;
    public QAngle? SpawnRotation;

    public MultilineWorldText(Plugin plugin, List<TextLine> lines, bool save = false, bool fromConfig = false)
    {
        Plugin = plugin;

        Id = nextId++;
        Lines = lines;
        SaveToConfig = save ;
    }

    public int Id { get; }
    public List<TextLine> Lines { get; private set; } = new();
    public List<WorldText> Texts { get; } = new();
    public bool SaveToConfig { get; set; }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public void Teleport(Vector absOrigin, QAngle absRotation, bool modifyConfig = false)
    {
        Remove();

        if (modifyConfig && SaveToConfig)
        {
            var config = Plugin.loadedConfigs?.FirstOrDefault(c =>
                c.Lines == Lines && c.AbsOrigin == Texts[0].AbsOrigin.ToString() &&
                c.AbsRotation == Texts[0].AbsRotation.ToString());
            if (config != null)
            {
                config.AbsOrigin = absOrigin.ToString();
                config.AbsRotation = absRotation.ToString();
                Plugin.SaveConfig();
            }
        }

        Spawn(absOrigin, absRotation, placement);
    }

    public void Spawn(Vector absOrigin, QAngle absRotation, TextPlacement placement)
    {
        this.placement = placement;

        WorldText? lastSpanedText = null;

        var currentHeight = 0f;
        foreach (var line in Lines)
        {
            switch (placement)
            {
                case TextPlacement.Wall:
                    Texts.Add(new WorldText(Plugin, absOrigin.With(z: absOrigin.Z - currentHeight), absRotation, line));
                    break;
                case TextPlacement.Floor:
                    if (lastSpanedText?.Entity != null)
                    {
                        var direction = Plugin.EntityFaceToDirection(lastSpanedText.Entity.AbsRotation!.Y - 270);
                        var offset = Plugin.GetDirectionOffset(direction, currentHeight);

                        lastSpanedText = new WorldText(Plugin, absOrigin - offset, absRotation, line);
                    }
                    else
                    {
                        lastSpanedText = new WorldText(Plugin, absOrigin, absRotation, line);
                    }

                    Texts.Add(lastSpanedText);
                    break;
            }

            currentHeight += Math.Max(10, line.FontSize / 3);
        }

        SpawnOrigin = Texts[0].AbsOrigin;
        SpawnRotation = Texts[0].AbsRotation;

        if (SaveToConfig)
            SaveConfig();
    }

    public void Update(List<TextLine>? lines = null)
    {
        Remove();

        if (lines != null)
            Lines = lines;

        if (SpawnOrigin != null && SpawnRotation != null)
            Spawn(SpawnOrigin, SpawnRotation, placement);

        if (SaveToConfig)
            SaveConfig();
    }

    private void SaveConfig()
    {
        var config = new WorldTextConfig
        {
            Placement = placement,
            Lines = Lines,
            AbsOrigin = Texts[0].AbsOrigin.ToString(),
            AbsRotation = Texts[0].AbsRotation.ToString()
        };

        var existingConfig = Plugin.loadedConfigs?.FirstOrDefault(c =>
            c.Lines == Lines && c.AbsOrigin == Texts[0].AbsOrigin.ToString() &&
            c.AbsRotation == Texts[0].AbsRotation.ToString());
        if (existingConfig != null)
        {
            existingConfig.Lines = config.Lines;
            existingConfig.AbsOrigin = config.AbsOrigin;
            existingConfig.AbsRotation = config.AbsRotation;
        }
        else
        {
            Plugin.loadedConfigs?.Add(config);
        }

        Plugin.SaveConfig();
    }

    public void Remove()
    {
        if (Texts.Count > 0)
            Texts.ForEach(text => text.Remove());
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposed)
        {
            if (disposing)
                Remove();

            disposed = true;
        }
    }
}
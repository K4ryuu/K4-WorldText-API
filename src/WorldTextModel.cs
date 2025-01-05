using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using K4ryuuCS2WorldTextAPI;
using K4WorldTextSharedAPI;

public class WorldText : IDisposable
{
    private bool disposed;
    public Plugin Plugin;

    public WorldText(Plugin plugin, Vector absOrigin, QAngle absRotation, TextLine data)
    {
        Plugin = plugin;
        AbsOrigin = absOrigin;
        AbsRotation = absRotation;
        Data = data;

        Spawn();
    }

    public CPointWorldText? Entity { get; private set; }

    public TextLine Data { get; set; }
    public Vector AbsOrigin { get; set; }
    public QAngle AbsRotation { get; set; }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public void Spawn()
    {
        Entity = Utilities.CreateEntityByName<CPointWorldText>("point_worldtext");
        if (Entity is null)
            throw new Exception("Failed to create point_worldtext Entity.");

        Entity.MessageText = Data.Text;
        Entity.Enabled = true;
        Entity.FontSize = Data.FontSize;
        Entity.Color = Data.Color;
        Entity.Fullbright = Data.FullBright;
        Entity.WorldUnitsPerPx = Data.Scale;
        Entity.DepthOffset = 0.0f;
        Entity.JustifyHorizontal = Data.JustifyHorizontal;
        Entity.JustifyVertical = Data.JustifyVertical;
        Entity.ReorientMode = Data.ReorientMode;

        Entity.Teleport(AbsOrigin, AbsRotation);
        Entity.DispatchSpawn();
    }

    public void Update(TextLine? data = null)
    {
        if (Entity?.IsValid == true)
            Entity.Remove();

        if (data != null)
            Data = data;

        Spawn();
    }

    public void Remove()
    {
        if (Entity?.IsValid == true)
            Entity.Remove();

        Entity = null;
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
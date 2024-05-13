
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using K4ryuuCS2WorldTextAPI;
using K4WorldTextSharedAPI;

public class WorldText : IDisposable
{
	public Plugin Plugin;

	public CPointWorldText? Entity { get; private set; } = null;

	public TextLine Data { get; set; }
	public Vector AbsOrigin { get; set; }
	public QAngle AbsRotation { get; set; }

	private bool disposed = false;

	public WorldText(Plugin plugin, Vector absOrigin, QAngle absRotation, TextLine data)
	{
		this.Plugin = plugin;
		this.AbsOrigin = absOrigin;
		this.AbsRotation = absRotation;
		this.Data = data;

		this.Spawn();
	}

	public void Spawn()
	{
		this.Entity = Utilities.CreateEntityByName<CPointWorldText>("point_worldtext");
		if (this.Entity is null)
			throw new Exception("Failed to create point_worldtext Entity.");

		this.Entity.MessageText = this.Data.Text;
		this.Entity.Enabled = true;
		this.Entity.FontSize = this.Data.FontSize;
		this.Entity.Color = this.Data.Color;
		this.Entity.Fullbright = this.Data.FullBright;
		this.Entity.WorldUnitsPerPx = this.Data.Scale;
		this.Entity.DepthOffset = 0.0f;
		this.Entity.JustifyHorizontal = PointWorldTextJustifyHorizontal_t.POINT_WORLD_TEXT_JUSTIFY_HORIZONTAL_CENTER;
		this.Entity.JustifyVertical = PointWorldTextJustifyVertical_t.POINT_WORLD_TEXT_JUSTIFY_VERTICAL_CENTER;
		this.Entity.ReorientMode = PointWorldTextReorientMode_t.POINT_WORLD_TEXT_REORIENT_NONE;

		this.Entity.Teleport(this.AbsOrigin, this.AbsRotation);
		this.Entity.DispatchSpawn();
	}

	public void Update(TextLine? data = null)
	{
		if (this.Entity?.IsValid == true)
			this.Entity.Remove();

		if (data != null)
			this.Data = data;

		this.Spawn();
	}

	public void Remove()
	{
		if (this.Entity?.IsValid == true)
			this.Entity.Remove();

		this.Entity = null;
	}

	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
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

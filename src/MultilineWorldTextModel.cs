using CounterStrikeSharp.API.Modules.Utils;
using K4ryuuCS2WorldTextAPI;
using K4WorldTextSharedAPI;
using Newtonsoft.Json;
using static K4ryuuCS2WorldTextAPI.Plugin;

public class MultilineWorldText : IDisposable
{
	private static int nextId = 1;
	private Plugin Plugin;

	public int Id { get; }
	public TextPlacement placement;
	public List<TextLine> Lines { get; private set; } = new();
	public List<WorldText> Texts { get; private set; } = new();
	public bool SaveToConfig { get; set; }

	private bool disposed = false;

	public MultilineWorldText(Plugin plugin, List<TextLine> lines, bool save = false)
	{
		this.Plugin = plugin;

		this.Id = nextId++;
		this.Lines = lines;
		this.SaveToConfig = save;
	}

	public void Spawn(Vector absOrigin, QAngle absRotation, TextPlacement placement)
	{
		this.placement = placement;

		WorldText? lastSpanedText = null;

		float currentHeight = 0f;
		foreach (TextLine line in this.Lines)
		{
			switch (placement)
			{
				case TextPlacement.Wall:
					this.Texts.Add(new WorldText(this.Plugin, absOrigin.With(z: absOrigin.Z + currentHeight), absRotation, line));
					break;
				case TextPlacement.Floor:
					if (lastSpanedText?.Entity != null)
					{
						string direction = Plugin.EntityFaceToDirection(lastSpanedText.Entity.AbsRotation!.Y - 270);
						Vector offset = Plugin.GetDirectionOffset(direction, currentHeight);

						lastSpanedText = new WorldText(this.Plugin, absOrigin - offset, absRotation, line);
					}
					else
						lastSpanedText = new WorldText(this.Plugin, absOrigin, absRotation, line);

					this.Texts.Add(lastSpanedText);
					break;
			}
			currentHeight += Math.Max(10, line.FontSize / 3);
		}

		if (this.SaveToConfig)
			this.SaveConfig();
	}

	public void Update(List<TextLine>? lines = null)
	{
		this.Remove();

		if (lines != null)
			this.Lines = lines;

		this.Spawn(this.Texts[0].AbsOrigin, this.Texts[0].AbsRotation, this.placement);

		if (this.SaveToConfig)
			this.SaveConfig();
	}

	private void SaveConfig()
	{
		WorldTextConfig config = new WorldTextConfig
		{
			Placement = this.placement,
			Lines = this.Lines,
			AbsOrigin = this.Texts[0].AbsOrigin.ToString(),
			AbsRotation = this.Texts[0].AbsRotation.ToString()
		};

		WorldTextConfig? existingConfig = Plugin.loadedConfigs?.FirstOrDefault(c => c.Lines.SequenceEqual(config.Lines) && c.Placement == config.Placement);
		if (existingConfig != null)
		{
			existingConfig.Lines = config.Lines;
			existingConfig.AbsOrigin = config.AbsOrigin;
			existingConfig.AbsRotation = config.AbsRotation;
		}
		else
			Plugin.loadedConfigs?.Add(config);

		Plugin.SaveConfig();
	}

	public void Remove()
	{
		if (this.Texts.Count > 0)
			this.Texts.ForEach(text => text.Remove());
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
				this.Remove();

			disposed = true;
		}
	}
}
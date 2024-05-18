using System.Drawing;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using K4WorldTextSharedAPI;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace K4ryuuCS2WorldTextAPI
{
	[MinimumApiVersion(227)]
	public class Plugin : BasePlugin
	{
		public override string ModuleName => "CS2 WorldText API";
		public override string ModuleVersion => "1.2.1";
		public override string ModuleAuthor => "K4ryuu";

		public static PluginCapability<IK4WorldTextSharedAPI> Capability_SharedAPI { get; } = new("k4-worldtext:sharedapi");

		public string configFilePath = string.Empty;
		public List<WorldTextConfig>? loadedConfigs = null;
		public List<MultilineWorldText> multilineWorldTexts = new List<MultilineWorldText>();

		public override void Load(bool hotReload)
		{
			Capabilities.RegisterPluginCapability(Capability_SharedAPI, () => new GameTextAPIHandler(this));

			RegisterEventHandler((EventRoundStart @event, GameEventInfo info) =>
			{
				if (loadedConfigs is null)
					LoadConfig(Server.MapName);

				multilineWorldTexts.ForEach(multilineWorldText => multilineWorldText.Update());
				return HookResult.Continue;
			});

			RegisterListener<Listeners.OnMapEnd>(() =>
			{
				loadedConfigs?.Clear();
				loadedConfigs = null;
			});

			if (hotReload)
				LoadConfig(Server.MapName);
		}

		public override void Unload(bool hotReload)
		{
			if (loadedConfigs is not null)
				SaveConfig();

			multilineWorldTexts.ForEach(multilineWorldText => multilineWorldText.Dispose());
		}

		public class GameTextAPIHandler : IK4WorldTextSharedAPI
		{
			public Plugin plugin;
			public GameTextAPIHandler(Plugin plugin)
			{
				this.plugin = plugin;
			}

			public int AddWorldText(TextPlacement placement, TextLine textLine, Vector position, QAngle angle, bool saveConfig = false)
			{
				return this.AddWorldText(placement, new List<TextLine> { textLine }, position, angle);
			}

			public int AddWorldText(TextPlacement placement, List<TextLine> textLines, Vector position, QAngle angle, bool saveConfig = false)
			{
				MultilineWorldText multilineWorldText = new MultilineWorldText(plugin, textLines, saveConfig);
				multilineWorldText.Spawn(position, angle, placement);

				plugin.multilineWorldTexts.Add(multilineWorldText);
				return multilineWorldText.Id;
			}

			public int AddWorldTextAtPlayer(CCSPlayerController player, TextPlacement placement, TextLine textLine, bool saveConfig = false)
			{
				return this.AddWorldTextAtPlayer(player, placement, new List<TextLine> { textLine });
			}

			public int AddWorldTextAtPlayer(CCSPlayerController player, TextPlacement placement, List<TextLine> textLines, bool saveConfig = false)
			{
				return plugin.SpawnMultipleLines(player, placement, textLines, saveConfig);
			}

			public void UpdateWorldText(int id, TextLine? textLine = null)
			{
				this.UpdateWorldText(id, textLine is null ? null : new List<TextLine> { textLine });
			}

			public void UpdateWorldText(int id, List<TextLine>? textLines = null)
			{
				MultilineWorldText? target = plugin.multilineWorldTexts.Find(wt => wt.Id == id);
				if (target is null)
					throw new Exception($"WorldText with ID {id} not found.");

				target.Update(textLines);
			}

			public void RemoveWorldText(int id, bool removeFromConfig = true)
			{
				MultilineWorldText? target = plugin.multilineWorldTexts.Find(wt => wt.Id == id);
				if (target is null)
					throw new Exception($"WorldText with ID {id} not found.");

				target.Dispose();
				plugin.multilineWorldTexts.Remove(target);

				if (removeFromConfig)
				{
					plugin.loadedConfigs?.RemoveAll(config => config.Lines == target.Lines && config.AbsOrigin == target.Texts[0].AbsOrigin.ToString() && config.AbsRotation == target.Texts[0].AbsRotation.ToString());
					plugin.SaveConfig();
				}
			}

			public List<CPointWorldText>? GetWorldTextLineEntities(int id)
			{
				MultilineWorldText? target = plugin.multilineWorldTexts.Find(wt => wt.Id == id);
				if (target is null)
					throw new Exception($"WorldText with ID {id} not found.");

				return target.Texts.Where(t => t.Entity != null).Select(t => t.Entity).Cast<CPointWorldText>().ToList();
			}

			public void TeleportWorldText(int id, Vector position, QAngle angle, bool modifyConfig = false)
			{
				MultilineWorldText? target = plugin.multilineWorldTexts.Find(wt => wt.Id == id);
				if (target is null)
					throw new Exception($"WorldText with ID {id} not found.");

				target.Teleport(position, angle, modifyConfig);
			}

			public void RemoveAllTemporary()
			{
				plugin.multilineWorldTexts.Where(wt => !wt.SaveToConfig).ToList().ForEach(multilineWorldText => multilineWorldText.Dispose());
			}
		}

		public void LoadConfig(string mapName)
		{
			configFilePath = Path.Combine(ModuleDirectory, $"worldtext_{mapName}.json");

			if (!File.Exists(configFilePath))
				return;

			try
			{
				string json = File.ReadAllText(configFilePath);
				loadedConfigs = JsonConvert.DeserializeObject<List<WorldTextConfig>>(json);

				if (loadedConfigs == null)
				{
					Logger.LogWarning($"Failed to deserialize configuration file: {configFilePath}");
					loadedConfigs = new List<WorldTextConfig>();
				}

				foreach (var config in loadedConfigs)
				{
					Vector vector = ParseVector(config.AbsOrigin);
					QAngle qAngle = ParseQAngle(config.AbsRotation);

					MultilineWorldText multilineWorldText = new MultilineWorldText(this, config.Lines, fromConfig: true);
					multilineWorldText.Spawn(vector, qAngle, placement: config.Placement);

					multilineWorldTexts.Add(multilineWorldText);
				}
			}
			catch (Exception ex)
			{
				Logger.LogError($"Error while loading configuration file: {ex.Message}");
			}
		}

		public void SaveConfig()
		{
			string updatedJson = JsonConvert.SerializeObject(loadedConfigs, Formatting.Indented);
			File.WriteAllText(configFilePath, updatedJson);
		}

		[ConsoleCommand("css_wt", "Spawns a world text")]
		[ConsoleCommand("css_worldtext", "Spawns a world text")]
		[RequiresPermissions("@css/root")]
		public void OnWorldTextSpawn(CCSPlayerController player, CommandInfo command)
		{
			if (command.ArgCount < 2)
			{
				command.ReplyToCommand($" {ChatColors.Silver}[ {ChatColors.Lime}K4-WorldText {ChatColors.Silver}] {ChatColors.Red}Usage: {ChatColors.Yellow}!css_worldtext <floor|wall>");
				return;
			}

			if (player.PlayerPawn.Value?.Health <= 0)
			{
				command.ReplyToCommand($" {ChatColors.Silver}[ {ChatColors.Lime}K4-WorldText {ChatColors.Silver}] {ChatColors.Red}You must be alive to use this command.");
				return;
			}

			string placementName = command.GetCommandString.Split(' ')[1];

			TextPlacement? placement;
			switch (placementName.ToLower())
			{
				case "floor":
					placement = TextPlacement.Floor;
					break;
				case "wall":
					placement = TextPlacement.Wall;
					break;
				default:
					command.ReplyToCommand($" {ChatColors.Silver}[ {ChatColors.Lime}K4-WorldText {ChatColors.Silver}] {ChatColors.Red}Invalid placement. {ChatColors.Yellow}Use 'floor' or 'wall'.");
					return;
			}

			List<TextLine> startLines = new List<TextLine>
			{
				new TextLine
				{
					Text = "Welcome to the CS2 WorldText API!",
					Color = Color.Yellow,
					FontSize = 24
				},
				new TextLine
				{
					Text = "You can edit this line in the config file.",
					Color = Color.Cyan,
					FontSize = 18
				},
				new TextLine
				{
					Text = new Random().Next().ToString(),
					Color = Color.Red,
					FontSize = 20
				}
			};

			SpawnMultipleLines(player, (TextPlacement)placement, startLines, true);
			command.ReplyToCommand($" {ChatColors.Silver}[ {ChatColors.Lime}K4-WorldText {ChatColors.Silver}] {ChatColors.Green}WorldText spawned! You can edit the text in the config file.");
		}

		[ConsoleCommand("css_rwt", "Removes a world text")]
		[ConsoleCommand("css_removeworldtext", "Removes a world text")]
		[RequiresPermissions("@css/root")]
		public void OnRemoveWorldTextSpawn(CCSPlayerController player, CommandInfo command)
		{
			if (player.PlayerPawn.Value?.Health <= 0)
			{
				command.ReplyToCommand($" {ChatColors.Silver}[ {ChatColors.Lime}K4-WorldText {ChatColors.Silver}] {ChatColors.Red}You must be alive to use this command.");
				return;
			}

			var GameRules = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").FirstOrDefault()?.GameRules;

			if (GameRules is null)
				return;

			MultilineWorldText? target = multilineWorldTexts
				.Where(multilineWorldText => DistanceTo(multilineWorldText.Texts[0].AbsOrigin, player.PlayerPawn.Value!.AbsOrigin!) < 100)
				.OrderBy(multilineWorldText => DistanceTo(multilineWorldText.Texts[0].AbsOrigin, player.PlayerPawn.Value!.AbsOrigin!))
				.FirstOrDefault();

			if (target is null)
			{
				command.ReplyToCommand($" {ChatColors.Silver}[ {ChatColors.Lime}K4-WorldText {ChatColors.Silver}] {ChatColors.Red}Move closer to the WorldText that you want to remove.");
				return;
			}

			target.Dispose();
			multilineWorldTexts.Remove(target);

			loadedConfigs?.RemoveAll(config => config.Lines == target.Lines && config.AbsOrigin == target.Texts[0].AbsOrigin.ToString() && config.AbsRotation == target.Texts[0].AbsRotation.ToString());
			SaveConfig();

			command.ReplyToCommand($" {ChatColors.Silver}[ {ChatColors.Lime}K4-WorldText {ChatColors.Silver}] {ChatColors.Green}WorldText removed!");
		}

		[ConsoleCommand("css_wt_reload", "Reloads the world text config")]
		[RequiresPermissions("@css/root")]
		public void OnWorldTextReload(CCSPlayerController player, CommandInfo command)
		{
			multilineWorldTexts.ForEach(multilineWorldText => multilineWorldText.Dispose());
			multilineWorldTexts.Clear();

			loadedConfigs?.Clear();
			LoadConfig(Server.MapName);

			command.ReplyToCommand($" {ChatColors.Silver}[ {ChatColors.Lime}K4-WorldText {ChatColors.Silver}] {ChatColors.Green}WorldText config has been reloaded!");
		}

		[ConsoleCommand("css_wti", "Shows informations about nearest world text")]
		[RequiresPermissions("@css/root")]
		public void OnWorldTextInfo(CCSPlayerController player, CommandInfo command)
		{
			if (player.PlayerPawn.Value?.Health <= 0)
			{
				command.ReplyToCommand($" {ChatColors.Silver}[ {ChatColors.Lime}K4-WorldText {ChatColors.Silver}] {ChatColors.Red}You must be alive to use this command.");
				return;
			}

			MultilineWorldText? target = multilineWorldTexts
				.Where(multilineWorldText => DistanceTo(multilineWorldText.Texts[0].AbsOrigin, player.PlayerPawn.Value!.AbsOrigin!) < 100)
				.OrderBy(multilineWorldText => DistanceTo(multilineWorldText.Texts[0].AbsOrigin, player.PlayerPawn.Value!.AbsOrigin!))
				.FirstOrDefault();

			if (target is null)
			{
				command.ReplyToCommand($" {ChatColors.Silver}[ {ChatColors.Lime}K4-WorldText {ChatColors.Silver}] {ChatColors.Red}Move closer to the WorldText that you want to get information about.");
				return;
			}

			player.PrintToChat($" {ChatColors.Silver}[ {ChatColors.Lime}K4-WorldText {ChatColors.Silver}] {ChatColors.Green}WorldText Informations");
			player.PrintToChat($" {ChatColors.Silver}Placement: {ChatColors.Yellow}{target.placement switch { TextPlacement.Floor => "Floor", TextPlacement.Wall => "Wall", _ => "Unknown" }}");
			player.PrintToChat($" {ChatColors.Silver}Lines: {ChatColors.Yellow}{target.Texts.Count}");
			player.PrintToChat($" {ChatColors.Silver}Location: {ChatColors.Yellow}{target.Texts[0].AbsOrigin}");
			player.PrintToChat($" {ChatColors.Silver}Rotation: {ChatColors.Yellow}{target.Texts[0].AbsRotation}");
			player.PrintToChat($" {ChatColors.Silver}Saved in config: {ChatColors.Yellow}{(loadedConfigs?.Any(config => config.Lines == target.Lines && config.AbsOrigin == target.Texts[0].AbsOrigin.ToString() && config.AbsRotation == target.Texts[0].AbsRotation.ToString()) ?? false ? "Yes" : "No")}");
		}

		public int SpawnMultipleLines(CCSPlayerController player, TextPlacement placement, List<TextLine> lines, bool saveConfig = false)
		{
			Vector AbsOrigin = Vector.Zero;
			QAngle AbsRotation = QAngle.Zero;
			QAngle tempRotation = GetNormalizedAngles(player);
			switch (placement)
			{
				case TextPlacement.Wall:
					AbsOrigin = GetEyePosition(player, lines);
					AbsRotation = new QAngle(tempRotation.X, tempRotation.Y + 270, tempRotation.Z + 90);
					break;
				case TextPlacement.Floor:
					AbsOrigin = player.PlayerPawn.Value!.AbsOrigin!.With(z: player.PlayerPawn.Value!.AbsOrigin!.Z + 1);
					AbsRotation = new QAngle(tempRotation.X, tempRotation.Y + 270, tempRotation.Z);
					break;
			}

			string direction = EntityFaceToDirection(player.PlayerPawn.Value!.AbsRotation!.Y);
			Vector offset = GetDirectionOffset(direction, 15);

			MultilineWorldText multilineWorldText = new MultilineWorldText(this, lines, saveConfig);
			multilineWorldText.Spawn(AbsOrigin + offset, AbsRotation, placement);

			multilineWorldTexts.Add(multilineWorldText);
			return multilineWorldText.Id;
		}

		public static QAngle GetNormalizedAngles(CCSPlayerController player)
		{
			QAngle AbsRotation = player.PlayerPawn.Value!.AbsRotation!;
			return new QAngle(
				AbsRotation.X,
				(float)Math.Round(AbsRotation.Y / 10.0) * 10,
				AbsRotation.Z
			);
		}

		public static Vector GetEyePosition(CCSPlayerController player, List<TextLine> lines)
		{
			Vector absorigin = player.PlayerPawn.Value!.AbsOrigin!;
			CPlayer_CameraServices camera = player.PlayerPawn.Value!.CameraServices!;

			float totalHeight = lines.Sum(line => line.FontSize / 5);
			return new Vector(absorigin.X, absorigin.Y, absorigin.Z + camera.OldPlayerViewOffsetZ + totalHeight);
		}

		public string EntityFaceToDirection(float yaw)
		{
			if (yaw >= -22.5 && yaw < 22.5)
				return "X";
			else if (yaw >= 22.5 && yaw < 67.5)
				return "XY";
			else if (yaw >= 67.5 && yaw < 112.5)
				return "Y";
			else if (yaw >= 112.5 && yaw < 157.5)
				return "-XY";
			else if (yaw >= 157.5 || yaw < -157.5)
				return "-X";
			else if (yaw >= -157.5 && yaw < -112.5)
				return "-X-Y";
			else if (yaw >= -112.5 && yaw < -67.5)
				return "-Y";
			else
				return "X-Y";
		}

		public Vector GetDirectionOffset(string direction, float offsetValue)
		{
			return direction switch
			{
				"X" => new Vector(offsetValue, 0, 0),
				"-X" => new Vector(-offsetValue, 0, 0),
				"Y" => new Vector(0, offsetValue, 0),
				"-Y" => new Vector(0, -offsetValue, 0),
				"XY" => new Vector(offsetValue, offsetValue, 0),
				"-XY" => new Vector(-offsetValue, offsetValue, 0),
				"X-Y" => new Vector(offsetValue, -offsetValue, 0),
				"-X-Y" => new Vector(-offsetValue, -offsetValue, 0),
				_ => Vector.Zero
			};
		}

		public static Vector ParseVector(string vectorString)
		{
			string[] components = vectorString.Split(' ');
			if (components.Length != 3)
				throw new ArgumentException("Invalid vector string format.");

			float x = float.Parse(components[0]);
			float y = float.Parse(components[1]);
			float z = float.Parse(components[2]);

			return new Vector(x, y, z);
		}

		public static QAngle ParseQAngle(string qangleString)
		{
			string[] components = qangleString.Split(' ');
			if (components.Length != 3)
				throw new ArgumentException("Invalid QAngle string format.");

			float x = float.Parse(components[0]);
			float y = float.Parse(components[1]);
			float z = float.Parse(components[2]);

			return new QAngle(x, y, z);
		}

		private float DistanceTo(Vector a, Vector b)
		{
			return (float)Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y) + (a.Z - b.Z) * (a.Z - b.Z));
		}

		public class WorldTextConfig
		{
			public TextPlacement Placement { get; set; }
			public required List<TextLine> Lines { get; set; }
			public required string AbsOrigin { get; set; }
			public required string AbsRotation { get; set; }
		}
	}
}

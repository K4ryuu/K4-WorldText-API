﻿using System.Drawing;
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

namespace K4ryuuCS2WorldTextAPI;

[MinimumApiVersion(227)]
public class Plugin : BasePlugin
{
    public string configFilePath = string.Empty;
    public List<WorldTextConfig> loadedConfigs = new();
    public List<MultilineWorldText> multilineWorldTexts = new();
    public override string ModuleName => "CS2 WorldText API";
    public override string ModuleVersion => "1.2.3";
    public override string ModuleAuthor => "K4ryuu";

    public static PluginCapability<IK4WorldTextSharedAPI> Capability_SharedAPI { get; } = new("k4-worldtext:sharedapi");

    public override void Load(bool hotReload)
    {
        Capabilities.RegisterPluginCapability(Capability_SharedAPI, () => new GameTextAPIHandler(this));
        RegisterListener<Listeners.OnMapStart>(LoadConfig);
        RegisterEventHandler((EventRoundStart @event, GameEventInfo info) =>
        {
            // if (loadedConfigs is null)
            //     LoadConfig(Server.MapName);

            LoadAssets();
            multilineWorldTexts.ForEach(multilineWorldText => multilineWorldText.Update());
            return HookResult.Continue;
        });

        if (hotReload)
        {
            LoadConfig(Server.MapName);
            LoadAssets();
        }
    }

    public override void Unload(bool hotReload)
    {
        ClearData();
    }

    public void ClearData()
    {
        multilineWorldTexts.ForEach(multilineWorldText => multilineWorldText.Dispose());
        multilineWorldTexts.Clear();
        loadedConfigs.Clear();
    }

    public void LoadConfig(string mapName)
    {
        ClearData();
        //if(loadedConfigs is not null) loadedConfigs.Clear();
        configFilePath = Path.Combine(ModuleDirectory, $"worldtext_{mapName}.json");

        if (!File.Exists(configFilePath))
            return;

        try
        {
            var json = File.ReadAllText(configFilePath);
            loadedConfigs = JsonConvert.DeserializeObject<List<WorldTextConfig>>(json);

            if (loadedConfigs is null)
            {
                Logger.LogWarning($"Failed to deserialize configuration file: {configFilePath}");
                loadedConfigs = new List<WorldTextConfig>();
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error while loading configuration file: {ex.Message}");
        }
    }

    public void LoadAssets()
    {
        //if (loadedConfigs is null) return;

        foreach (var config in loadedConfigs)
        {
            var vector = ParseVector(config.AbsOrigin);
            var qAngle = ParseQAngle(config.AbsRotation);

            var multilineWorldText = new MultilineWorldText(this, config.Lines, fromConfig: true);
            multilineWorldText.Spawn(vector, qAngle, config.Placement);

            multilineWorldTexts.Add(multilineWorldText);
        }
    }

    public void SaveConfig()
    {
        if (loadedConfigs.Count == 0) return;

        var updatedJson = JsonConvert.SerializeObject(loadedConfigs, Formatting.Indented);
        File.WriteAllText(configFilePath, updatedJson);
    }

    [ConsoleCommand("css_wt", "Spawns a world text")]
    [ConsoleCommand("css_worldtext", "Spawns a world text")]
    [RequiresPermissions("@css/root")]
    public void OnWorldTextSpawn(CCSPlayerController player, CommandInfo command)
    {
        if (command.ArgCount < 2)
        {
            command.ReplyToCommand(
                $" {ChatColors.Silver}[ {ChatColors.Lime}K4-WorldText {ChatColors.Silver}] {ChatColors.Red}Usage: {ChatColors.Yellow}!css_worldtext <floor|wall>");
            return;
        }

        if (!player.PawnIsAlive)
        {
            command.ReplyToCommand(
                $" {ChatColors.Silver}[ {ChatColors.Lime}K4-WorldText {ChatColors.Silver}] {ChatColors.Red}You must be alive to use this command.");
            return;
        }

        var placementName = command.GetCommandString.Split(' ')[1];

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
                command.ReplyToCommand(
                    $" {ChatColors.Silver}[ {ChatColors.Lime}K4-WorldText {ChatColors.Silver}] {ChatColors.Red}Invalid placement. {ChatColors.Yellow}Use 'floor' or 'wall'.");
                return;
        }

        var startLines = new List<TextLine>
        {
            new()
            {
                Text = "Welcome to the CS2 WorldText API!",
                Color = Color.Yellow,
                FontSize = 24
            },
            new()
            {
                Text = "You can edit this line in the config file.",
                Color = Color.Cyan,
                FontSize = 18
            },
            new()
            {
                Text = new Random().Next().ToString(),
                Color = Color.Red,
                FontSize = 20
            }
        };

        SpawnMultipleLines(player, (TextPlacement)placement, startLines, true);
        command.ReplyToCommand(
            $" {ChatColors.Silver}[ {ChatColors.Lime}K4-WorldText {ChatColors.Silver}] {ChatColors.Green}WorldText spawned! You can edit the text in the config file.");
    }

    [ConsoleCommand("css_rwt", "Removes a world text")]
    [ConsoleCommand("css_removeworldtext", "Removes a world text")]
    [RequiresPermissions("@css/root")]
    public void OnRemoveWorldTextSpawn(CCSPlayerController player, CommandInfo command)
    {
        if (player.PlayerPawn.Value?.Health <= 0)
        {
            command.ReplyToCommand(
                $" {ChatColors.Silver}[ {ChatColors.Lime}K4-WorldText {ChatColors.Silver}] {ChatColors.Red}You must be alive to use this command.");
            return;
        }

        var GameRules = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").FirstOrDefault()
            ?.GameRules;

        if (GameRules is null)
            return;

        var target = multilineWorldTexts
            .Where(multilineWorldText =>
                DistanceTo(multilineWorldText.Texts[0].AbsOrigin, player.PlayerPawn.Value!.AbsOrigin!) < 100)
            .OrderBy(multilineWorldText =>
                DistanceTo(multilineWorldText.Texts[0].AbsOrigin, player.PlayerPawn.Value!.AbsOrigin!))
            .FirstOrDefault();

        if (target is null)
        {
            command.ReplyToCommand(
                $" {ChatColors.Silver}[ {ChatColors.Lime}K4-WorldText {ChatColors.Silver}] {ChatColors.Red}Move closer to the WorldText that you want to remove.");
            return;
        }

        target.Dispose();
        multilineWorldTexts.Remove(target);

        loadedConfigs?.RemoveAll(config =>
            config.Lines == target.Lines && config.AbsOrigin == target.Texts[0].AbsOrigin.ToString() &&
            config.AbsRotation == target.Texts[0].AbsRotation.ToString());
        SaveConfig();

        command.ReplyToCommand(
            $" {ChatColors.Silver}[ {ChatColors.Lime}K4-WorldText {ChatColors.Silver}] {ChatColors.Green}WorldText removed!");
    }

    [ConsoleCommand("css_wt_reload", "Reloads the world text config")]
    [RequiresPermissions("@css/root")]
    public void OnWorldTextReload(CCSPlayerController player, CommandInfo command)
    {
        multilineWorldTexts.ForEach(multilineWorldText => multilineWorldText.Dispose());
        multilineWorldTexts.Clear();

        loadedConfigs?.Clear();
        LoadConfig(Server.MapName);
        LoadAssets();

        command.ReplyToCommand(
            $" {ChatColors.Silver}[ {ChatColors.Lime}K4-WorldText {ChatColors.Silver}] {ChatColors.Green}WorldText config has been reloaded!");
    }

    [ConsoleCommand("css_wti", "Shows informations about nearest world text")]
    [RequiresPermissions("@css/root")]
    public void OnWorldTextInfo(CCSPlayerController player, CommandInfo command)
    {
        if (player.PlayerPawn.Value?.Health <= 0)
        {
            command.ReplyToCommand(
                $" {ChatColors.Silver}[ {ChatColors.Lime}K4-WorldText {ChatColors.Silver}] {ChatColors.Red}You must be alive to use this command.");
            return;
        }

        var target = multilineWorldTexts
            .Where(multilineWorldText =>
                DistanceTo(multilineWorldText.Texts[0].AbsOrigin, player.PlayerPawn.Value!.AbsOrigin!) < 100)
            .OrderBy(multilineWorldText =>
                DistanceTo(multilineWorldText.Texts[0].AbsOrigin, player.PlayerPawn.Value!.AbsOrigin!))
            .FirstOrDefault();

        if (target is null)
        {
            command.ReplyToCommand(
                $" {ChatColors.Silver}[ {ChatColors.Lime}K4-WorldText {ChatColors.Silver}] {ChatColors.Red}Move closer to the WorldText that you want to get information about.");
            return;
        }

        player.PrintToChat(
            $" {ChatColors.Silver}[ {ChatColors.Lime}K4-WorldText {ChatColors.Silver}] {ChatColors.Green}WorldText Informations");
        player.PrintToChat(
            $" {ChatColors.Silver}Placement: {ChatColors.Yellow}{target.placement switch { TextPlacement.Floor => "Floor", TextPlacement.Wall => "Wall", _ => "Unknown" }}");
        player.PrintToChat($" {ChatColors.Silver}Lines: {ChatColors.Yellow}{target.Texts.Count}");
        player.PrintToChat($" {ChatColors.Silver}Location: {ChatColors.Yellow}{target.Texts[0].AbsOrigin}");
        player.PrintToChat($" {ChatColors.Silver}Rotation: {ChatColors.Yellow}{target.Texts[0].AbsRotation}");
        player.PrintToChat(
            $" {ChatColors.Silver}Saved in config: {ChatColors.Yellow}{(loadedConfigs?.Any(config => config.Lines == target.Lines && config.AbsOrigin == target.Texts[0].AbsOrigin.ToString() && config.AbsRotation == target.Texts[0].AbsRotation.ToString()) ?? false ? "Yes" : "No")}");
    }

    public int SpawnMultipleLines(CCSPlayerController player, TextPlacement placement, List<TextLine> lines,
        bool saveConfig = false)
    {
        var AbsOrigin = Vector.Zero;
        var AbsRotation = QAngle.Zero;
        var tempRotation = GetNormalizedAngles(player);
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

        var direction = EntityFaceToDirection(player.PlayerPawn.Value!.AbsRotation!.Y);
        var offset = GetDirectionOffset(direction, 15);

        var multilineWorldText = new MultilineWorldText(this, lines, saveConfig);
        multilineWorldText.Spawn(AbsOrigin + offset, AbsRotation, placement);

        multilineWorldTexts.Add(multilineWorldText);
        return multilineWorldText.Id;
    }

    public static QAngle GetNormalizedAngles(CCSPlayerController player)
    {
        var AbsRotation = player.PlayerPawn.Value!.AbsRotation!;
        return new QAngle(
            AbsRotation.X,
            (float)Math.Round(AbsRotation.Y / 10.0) * 10,
            AbsRotation.Z
        );
    }

    public static Vector GetEyePosition(CCSPlayerController player, List<TextLine> lines)
    {
        var absorigin = player.PlayerPawn.Value!.AbsOrigin!;
        var camera = player.PlayerPawn.Value!.CameraServices!;

        float totalHeight = lines.Sum(line => line.FontSize / 5);
        return new Vector(absorigin.X, absorigin.Y, absorigin.Z + camera.OldPlayerViewOffsetZ + totalHeight);
    }

    public string EntityFaceToDirection(float yaw)
    {
        if (yaw >= -22.5 && yaw < 22.5)
            return "X";
        if (yaw >= 22.5 && yaw < 67.5)
            return "XY";
        if (yaw >= 67.5 && yaw < 112.5)
            return "Y";
        if (yaw >= 112.5 && yaw < 157.5)
            return "-XY";
        if (yaw >= 157.5 || yaw < -157.5)
            return "-X";
        if (yaw >= -157.5 && yaw < -112.5)
            return "-X-Y";
        if (yaw >= -112.5 && yaw < -67.5)
            return "-Y";
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
        var components = vectorString.Split(' ');
        if (components.Length != 3)
            throw new ArgumentException("Invalid vector string format.");

        var x = float.Parse(components[0]);
        var y = float.Parse(components[1]);
        var z = float.Parse(components[2]);

        return new Vector(x, y, z);
    }

    public static QAngle ParseQAngle(string qangleString)
    {
        var components = qangleString.Split(' ');
        if (components.Length != 3)
            throw new ArgumentException("Invalid QAngle string format.");

        var x = float.Parse(components[0]);
        var y = float.Parse(components[1]);
        var z = float.Parse(components[2]);

        return new QAngle(x, y, z);
    }

    private float DistanceTo(Vector a, Vector b)
    {
        return (float)Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y) + (a.Z - b.Z) * (a.Z - b.Z));
    }

    public class GameTextAPIHandler : IK4WorldTextSharedAPI
    {
        public Plugin plugin;

        public GameTextAPIHandler(Plugin plugin)
        {
            this.plugin = plugin;
        }

        public int AddWorldText(TextPlacement placement, TextLine textLine, Vector position, QAngle angle,
            bool saveConfig = false)
        {
            return AddWorldText(placement, new List<TextLine> { textLine }, position, angle);
        }

        public int AddWorldText(TextPlacement placement, List<TextLine> textLines, Vector position, QAngle angle,
            bool saveConfig = false)
        {
            var multilineWorldText = new MultilineWorldText(plugin, textLines, saveConfig);
            multilineWorldText.Spawn(position, angle, placement);

            plugin.multilineWorldTexts.Add(multilineWorldText);
            return multilineWorldText.Id;
        }

        public int AddWorldTextAtPlayer(CCSPlayerController player, TextPlacement placement, TextLine textLine,
            bool saveConfig = false)
        {
            return AddWorldTextAtPlayer(player, placement, new List<TextLine> { textLine });
        }

        public int AddWorldTextAtPlayer(CCSPlayerController player, TextPlacement placement, List<TextLine> textLines,
            bool saveConfig = false)
        {
            return plugin.SpawnMultipleLines(player, placement, textLines, saveConfig);
        }

        public void UpdateWorldText(int id, TextLine? textLine = null)
        {
            UpdateWorldText(id, textLine is null ? null : new List<TextLine> { textLine });
        }

        public void UpdateWorldText(int id, List<TextLine>? textLines = null)
        {
            var target = plugin.multilineWorldTexts.Find(wt => wt.Id == id);
            if (target is null)
                throw new Exception($"WorldText with ID {id} not found.");

            target.Update(textLines);
        }

        public void RemoveWorldText(int id, bool removeFromConfig = true)
        {
            var target = plugin.multilineWorldTexts.Find(wt => wt.Id == id);
            if (target is null)
                throw new Exception($"WorldText with ID {id} not found.");

            target.Dispose();
            plugin.multilineWorldTexts.Remove(target);

            if (removeFromConfig)
            {
                plugin.loadedConfigs?.RemoveAll(config =>
                    config.Lines == target.Lines && config.AbsOrigin == target.Texts[0].AbsOrigin.ToString() &&
                    config.AbsRotation == target.Texts[0].AbsRotation.ToString());
                plugin.SaveConfig();
            }
        }

        public List<CPointWorldText>? GetWorldTextLineEntities(int id)
        {
            var target = plugin.multilineWorldTexts.Find(wt => wt.Id == id);
            if (target is null)
                throw new Exception($"WorldText with ID {id} not found.");

            return target.Texts.Where(t => t.Entity != null).Select(t => t.Entity).Cast<CPointWorldText>().ToList();
        }

        public void TeleportWorldText(int id, Vector position, QAngle angle, bool modifyConfig = false)
        {
            var target = plugin.multilineWorldTexts.Find(wt => wt.Id == id);
            if (target is null)
                throw new Exception($"WorldText with ID {id} not found.");

            target.Teleport(position, angle, modifyConfig);
        }

        public void RemoveAllTemporary()
        {
            plugin.multilineWorldTexts.Where(wt => !wt.SaveToConfig).ToList()
                .ForEach(multilineWorldText => multilineWorldText.Dispose());
        }
    }

    public class WorldTextConfig
    {
        public TextPlacement Placement { get; set; }
        public required List<TextLine> Lines { get; set; }
        public required string AbsOrigin { get; set; }
        public required string AbsRotation { get; set; }
    }
}
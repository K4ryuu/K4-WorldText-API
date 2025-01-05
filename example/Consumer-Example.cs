using System.Drawing;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Utils;
using K4WorldTextSharedAPI;
using Timer = CounterStrikeSharp.API.Modules.Timers.Timer;

namespace K4WorldTextConsumerExample;

[MinimumApiVersion(205)]
public class PluginK4WorldTextConsumerExample : BasePlugin
{
    public List<int> spanwedMessages = new();
    public Dictionary<int, Timer> textUpdaters = new();
    public override string ModuleName => "YourPluginNameHere";
    public override string ModuleAuthor => "YourNameHere";
    public override string ModuleVersion => "1.0.0";

    public static PluginCapability<IK4WorldTextSharedAPI> Capability_SharedAPI { get; } = new("k4-worldtext:sharedapi");

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        SpawnRegularText(TextPlacement.Wall); // Wall text
        SpawnRegularText(TextPlacement.Floor); // Floor text

        // Dynamic can be updated with a timer for example and you can display realtime data
        // Its just an example but you can update anytime
        SpawnDynamicText(TextPlacement.Wall); // Dynamic wall text
        SpawnDynamicText(TextPlacement.Floor); // Dynamic floor text
    }

    public void SpawnDynamicText(TextPlacement placement)
    {
        var checkAPI = Capability_SharedAPI.Get();

        if (checkAPI != null)
        {
            var messageID = checkAPI.AddWorldText(placement, new List<TextLine>
            {
                new()
                {
                    Text = new Random().Next().ToString(),
                    Color = Color.Yellow,
                    FontSize = 24,
                    FullBright = true,
                    Scale = 0.5f
                }
            }, new Vector(0, 0, 0), new QAngle(0, 0, 0), true);

            spanwedMessages.Add(messageID);

            var updateTimer = AddTimer(1, () =>
            {
                checkAPI.UpdateWorldText(messageID, new List<TextLine>
                {
                    new()
                    {
                        Text = new Random().Next().ToString(),
                        Color = Color.Yellow,
                        FontSize = 24,
                        FullBright = true,
                        Scale = 0.5f
                    }
                });
            });

            textUpdaters.Add(messageID, updateTimer); // I save it only to free them later
        }
        else
        {
            throw new Exception("Failed to get shared API capability for K4-WorldText");
        }
    }

    public void SpawnRegularText(TextPlacement placement)
    {
        var checkAPI = Capability_SharedAPI.Get();

        if (checkAPI != null)
        {
            var messageID = checkAPI.AddWorldText(placement, new List<TextLine>
            {
                new()
                {
                    Text = "Welcome to the CS2 WorldText API!",
                    Color = Color.Yellow,
                    FontSize = 24,
                    FullBright = true,
                    Scale = 0.5f
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
            }, new Vector(0, 0, 0), new QAngle(0, 0, 0), true);

            spanwedMessages.Add(messageID);
        }
        else
        {
            throw new Exception("Failed to get shared API capability for K4-WorldText");
        }
    }

    public override void Unload(bool hotReload)
    {
        var checkAPI = Capability_SharedAPI.Get();

        if (checkAPI != null)
            spanwedMessages.ForEach(messageID =>
            {
                checkAPI.RemoveWorldText(messageID);

                if (textUpdaters.TryGetValue(messageID, out var timer))
                {
                    timer?.Kill();
                    textUpdaters.Remove(messageID);
                }
            });
        else
            throw new Exception("Failed to get shared API capability for K4-Arenas.");
    }
}
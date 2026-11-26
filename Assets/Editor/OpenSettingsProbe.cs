using System.Reflection;
using UnityEngine;

public static class OpenSettingsProbe
{
    public static string OpenSettings()
    {
        var game = Object.FindFirstObjectByType<Game.Factory.FactoryGame>();
        if (game == null) return "no game";
        var hudField = typeof(Game.Factory.FactoryGame).GetField("_hud", BindingFlags.Instance | BindingFlags.NonPublic);
        var hud = hudField != null ? hudField.GetValue(game) : null;
        if (hud == null) return "no hud";
        var open = hud.GetType().GetMethod("OpenSettings", BindingFlags.Instance | BindingFlags.NonPublic);
        if (open == null) return "no method";
        open.Invoke(hud, null);
        return "opened";
    }
}

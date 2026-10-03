using UnityEditor;
using UnityEngine;

public static class CardDefenseDebugMenu
{
    private const string AddCoinsMenu = "Tools/Add 10000 Coins";
    private const string SkipWaveMenu = "Tools/Skip Current Wave";
    private const string Wave15Menu = "Tools/Go To Wave 15";
    private const string InvincibleMenu = "Tools/Toggle Invincibility";

    [MenuItem(AddCoinsMenu)]
    private static void AddCoins()
    {
        CardDefenseGame game = Object.FindAnyObjectByType<CardDefenseGame>();
        if (game == null) return;
        game.GrantDebugCoins(10000);
    }

    [MenuItem(AddCoinsMenu, true)]
    private static bool CanAddCoins()
    {
        return EditorApplication.isPlaying &&
            Object.FindAnyObjectByType<CardDefenseGame>() != null;
    }

    [MenuItem(SkipWaveMenu)]
    private static void SkipWave()
    {
        CardDefenseGame game = Object.FindAnyObjectByType<CardDefenseGame>();
        if (game != null) game.SkipDebugWave();
    }

    [MenuItem(SkipWaveMenu, true)]
    private static bool CanSkipWave()
    {
        return EditorApplication.isPlaying &&
            Object.FindAnyObjectByType<CardDefenseGame>() != null;
    }

    [MenuItem(Wave15Menu)]
    private static void GoToWave15()
    {
        CardDefenseGame game = Object.FindAnyObjectByType<CardDefenseGame>();
        if (game != null) game.JumpToDebugWave(15);
    }

    [MenuItem(Wave15Menu, true)]
    private static bool CanGoToWave15()
    {
        return EditorApplication.isPlaying &&
            Object.FindAnyObjectByType<CardDefenseGame>() != null;
    }

    [MenuItem(InvincibleMenu)]
    private static void ToggleInvincibility()
    {
        CardDefenseGame game = Object.FindAnyObjectByType<CardDefenseGame>();
        if (game == null) return;
        game.ToggleDebugInvincibility();
        Menu.SetChecked(InvincibleMenu, game.DebugInvincible);
    }

    [MenuItem(InvincibleMenu, true)]
    private static bool CanToggleInvincibility()
    {
        CardDefenseGame game = EditorApplication.isPlaying
            ? Object.FindAnyObjectByType<CardDefenseGame>() : null;
        Menu.SetChecked(InvincibleMenu, game != null && game.DebugInvincible);
        return game != null;
    }
}

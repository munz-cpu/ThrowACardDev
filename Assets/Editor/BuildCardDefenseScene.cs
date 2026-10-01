using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class BuildCardDefenseScene
{
    private static readonly string[] SuitNames = { "HEART", "CLUB", "SPADE", "DIAMOND" };

    [MenuItem("Tools/Card Defense/Build Editable UI")]
    public static void Build()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/main.unity")
        {
            Debug.LogError("Open Assets/Scenes/main.unity before building the card defense UI.");
            return;
        }

        GameObject canvasObject = GameObject.Find("Card Canvas");
        if (canvasObject == null || !canvasObject.TryGetComponent(out CardDefenseGame game) ||
            !canvasObject.TryGetComponent(out CanvasCardThrow card))
        {
            Debug.LogError("Card Canvas with both gameplay components is required.");
            return;
        }

        var root = canvasObject.GetComponent<RectTransform>();
        Font font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Font/SeoulAlrim-Medium.otf");
        if (font == null)
        {
            Debug.LogError("Assets/Font/SeoulAlrim-Medium.otf is required.");
            return;
        }
        var sprites = new Sprite[4];
        var rankSprites = new Sprite[48];
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath("Assets/Resources/CardDeck.png"))
        {
            if (asset is not Sprite sprite) continue;
            int split = sprite.name.LastIndexOf('_');
            if (split < 0 || !int.TryParse(sprite.name.Substring(split + 1), out int index)) continue;
            if (index >= 0 && index < rankSprites.Length) rankSprites[index] = sprite;
            if (index == 0) sprites[0] = sprite;
            if (index == 12) sprites[1] = sprite;
            if (index == 24) sprites[2] = sprite;
            if (index == 36) sprites[3] = sprite;
        }

        RectTransform background = Rect(root, "Arena Background", Vector2.zero, Vector2.zero);
        Stretch(background);
        Image backgroundImage = Graphic(background, new Color(0.045f, 0.075f, 0.12f), false);
        background.SetAsFirstSibling();

        RectTransform enemies = Rect(root, "Enemies", Vector2.zero, Vector2.zero);
        Stretch(enemies);
        enemies.SetSiblingIndex(1);

        RectTransform core = Rect(root, "Core", new Vector2(112f, 112f), Vector2.zero);
        core.SetSiblingIndex(2);
        Image coreImage = Graphic(core, new Color(0.16f, 0.35f, 0.56f, 0.95f), false);
        Text coreText = Label(core, "Core Text", "CORE\n20 / 20", 22, Color.white,
            TextAnchor.MiddleCenter, font);
        Stretch(coreText.rectTransform);

        RectTransform effects = Rect(root, "Hit Effects", Vector2.zero, Vector2.zero);
        Stretch(effects);
        effects.SetAsLastSibling();

        RectTransform hud = Rect(root, "HUD", Vector2.zero, Vector2.zero);
        Stretch(hud);
        hud.SetAsLastSibling();
        Text status = Label(hud, "Score Text", "SCORE  0", 24, Color.white,
            TextAnchor.MiddleLeft, font);
        Anchor(status.rectTransform, new Vector2(0f, 1f), new Vector2(20f, -18f),
            new Vector2(590f, 42f));
        Text wave = Label(hud, "Wave Text", "WAVE  1", 24, Color.white,
            TextAnchor.MiddleRight, font);
        Anchor(wave.rectTransform, new Vector2(1f, 1f), new Vector2(-20f, -18f),
            new Vector2(500f, 42f));
        Text combo = Label(hud, "Combo Text", "", 32, Color.white,
            TextAnchor.MiddleCenter, font);
        Anchor(combo.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -80f),
            new Vector2(360f, 55f));
        Transform help = hud.Find("Help Text");
        if (help != null) Object.DestroyImmediate(help.gameObject);

        RectTransform selector = Rect(hud, "Suit Selector", new Vector2(440f, 180f), Vector2.zero);
        Anchor(selector, new Vector2(0.5f, 0f), Vector2.zero,
            new Vector2(440f, 180f));
        var buttons = new Button[4];
        for (int i = 0; i < 4; i++)
        {
            RectTransform slot = Rect(selector, SuitNames[i] + " Button",
                new Vector2(88f, 120f), new Vector2((i - 1.5f) * 106f, -80f));
            slot.anchorMin = slot.anchorMax = new Vector2(0.5f, 0f);
            Image image = Graphic(slot, Color.white, false);
            image.sprite = sprites[i];
            image.preserveAspect = true;
            Button button = slot.GetComponent<Button>();
            if (button == null) button = slot.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            button.interactable = false;
            Transform oldLabel = slot.Find("Label");
            if (oldLabel != null) Object.DestroyImmediate(oldLabel.gameObject);
            buttons[i] = button;
        }

        RectTransform gameOver = Rect(root, "Game Over Panel",
            new Vector2(440f, 250f), Vector2.zero);
        Graphic(gameOver, new Color(0.03f, 0.07f, 0.13f, 0.97f), true);
        Text title = Label(gameOver, "Title", "CORE LOST", 42, Color.white,
            TextAnchor.MiddleCenter, font);
        Anchor(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 55f),
            new Vector2(420f, 70f));
        RectTransform restart = Rect(gameOver, "Restart Button",
            new Vector2(220f, 58f), new Vector2(0f, -55f));
        Image restartImage = Graphic(restart, new Color(0.43f, 0.60f, 1f), true);
        Button restartButton = restart.GetComponent<Button>();
        if (restartButton == null) restartButton = restart.gameObject.AddComponent<Button>();
        restartButton.targetGraphic = restartImage;
        Text restartLabel = Label(restart, "Label", "PLAY AGAIN", 24, Color.white,
            TextAnchor.MiddleCenter, font);
        Stretch(restartLabel.rectTransform);
        gameOver.gameObject.SetActive(false);
        gameOver.SetAsLastSibling();

        var serialized = new SerializedObject(game);
        Set(serialized, "enemyLayer", enemies);
        Set(serialized, "effectLayer", effects);
        Set(serialized, "coreRect", core);
        Set(serialized, "coreImage", coreImage);
        Set(serialized, "coreText", coreText);
        Set(serialized, "statusText", status);
        Set(serialized, "waveText", wave);
        Set(serialized, "comboText", combo);
        Set(serialized, "uiFont", font);
        Set(serialized, "gameOverPanel", gameOver.gameObject);
        Set(serialized, "restartButton", restartButton);
        Set(serialized, "suitSelector", selector);
        SerializedProperty buttonArray = serialized.FindProperty("suitButtons");
        SerializedProperty spriteArray = serialized.FindProperty("suitSprites");
        SerializedProperty rankArray = serialized.FindProperty("rankSprites");
        buttonArray.arraySize = spriteArray.arraySize = 4;
        rankArray.arraySize = rankSprites.Length;
        for (int i = 0; i < 4; i++)
        {
            buttonArray.GetArrayElementAtIndex(i).objectReferenceValue = buttons[i];
            spriteArray.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
        }
        for (int i = 0; i < rankSprites.Length; i++)
            rankArray.GetArrayElementAtIndex(i).objectReferenceValue = rankSprites[i];
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(game);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Editable card defense UI saved in the scene.");
    }

    private static void Set(SerializedObject target, string name, Object value)
    {
        target.FindProperty(name).objectReferenceValue = value;
    }

    private static RectTransform Rect(RectTransform parent, string name, Vector2 size, Vector2 position)
    {
        Transform existing = parent.Find(name);
        RectTransform rect;
        if (existing != null) rect = existing.GetComponent<RectTransform>();
        else
        {
            var obj = new GameObject(name, typeof(RectTransform));
            rect = obj.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
        }
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        return rect;
    }

    private static Image Graphic(RectTransform rect, Color color, bool raycast)
    {
        Image image = rect.GetComponent<Image>();
        if (image == null) image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = raycast;
        return image;
    }

    private static Text Label(RectTransform parent, string name, string content, int size,
        Color color, TextAnchor alignment, Font font)
    {
        RectTransform rect = Rect(parent, name, new Vector2(220f, 45f), Vector2.zero);
        Text label = rect.GetComponent<Text>();
        if (label == null) label = rect.gameObject.AddComponent<Text>();
        label.font = font;
        label.text = content;
        label.fontSize = size;
        label.color = color;
        label.alignment = alignment;
        label.raycastTarget = false;
        return label;
    }

    private static void Stretch(RectTransform rect, float inset = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }

    private static void Anchor(RectTransform rect, Vector2 anchor, Vector2 inset, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = inset;
        rect.sizeDelta = size;
    }
}

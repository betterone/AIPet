using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Momo.Core;

namespace Momo
{
    // A dedicated phone-sized canvas keeps Boutique independent of the room HUD scale.
    public sealed class BoutiqueHud : MonoBehaviour
    {
        MomoApp app;
        TMP_FontAsset font;
        RectTransform safe, overview, detail, overviewTop, overviewBottom, detailTop, detailBottom, profileShade, profileCard;
        TMP_Text title, breed, traits, description, feedback, count;
        TMP_Text profileNumber, profileName, profileBreed, profileAppearance, profileSummary;
        readonly TMP_Text[] profileLevels = new TMP_Text[4];
        TMP_InputField nameInput;
        Button home, adopt;
        readonly RectTransform[] badges = new RectTransform[9];
        readonly string[] names = new string[9];
        int selected = -1;
        public bool ProfileOpen => profileShade != null && profileShade.gameObject.activeInHierarchy;
        public int ProfileIndex { get; private set; } = -1;
        bool confirmChange;
        float feedbackUntil;
        static readonly Color Ink = GameHudStyle.Ink, Muted = GameHudStyle.Muted, Paper = GameHudStyle.Paper, Mist = GameHudStyle.Mist, Accent = GameHudStyle.Accent;
        static readonly string[] BreedNames = { "橘色虎斑", "英短蓝猫", "暹罗重点色", "黑白燕尾服", "雪白猫咪", "孟买黑猫", "三花", "布偶双色", "狸花" };
        static readonly string[] Traits = { "活泼 · 大胆", "温和 · 安静", "好奇 · 爱探索", "活泼 · 爱玩闹", "从容 · 独立", "安静 · 亲人", "有主见 · 亲切", "温柔 · 爱陪伴", "敏捷 · 警觉" };
        static readonly string[] Descriptions = {
            "橘色条纹，奶白爪尖。试着伸出手，和它打个招呼。",
            "蓝灰毛色，圆圆的脸颊。留一点时间，看看它的慢眨眼。",
            "奶油毛色、深色面罩和蓝眼睛。移动手指，让它看看你。",
            "黑白毛色，白色小爪。先摸摸它，再一起伸个懒腰。",
            "雪白毛色，圆圆的琥珀色眼睛。慢慢靠近认识它。",
            "深黑毛色，金色眼睛。安静待一会儿，也是一种陪伴。",
            "奶白、橘色与深色斑块。伸出手，看看它的回应。",
            "奶油与白色相间，蓝眼睛和蓬松尾巴。轻轻打个招呼。",
            "棕色虎斑，黄绿色眼睛。让它跟着你的手指转转头。"
        };
        static readonly string[] AppearanceFacts = {
            "暖橘色虎斑纹，奶白色胸口和爪尖。",
            "蓝灰色短毛，圆润的脸颊和琥珀色眼睛。",
            "奶油色被毛，深色面罩、耳朵和尾巴；蓝色眼睛。",
            "经典黑白配色，白色胸口与爪尖。",
            "雪白被毛，圆润脸颊和琥珀色眼睛。",
            "深黑色被毛，明亮的金色眼睛。",
            "奶白底色上有橘色与深色斑块。",
            "奶油与白色相间，蓝眼睛和蓬松尾巴。",
            "棕色虎斑纹，黄绿色眼睛。"
        };
        static readonly string[] PersonalityLabels = { "玩心", "好奇心", "亲近人", "独立性" };
        static Color Hex(string value) { ColorUtility.TryParseHtmlString(value, out var color); return color; }

        public void Build(MomoApp app, TMP_FontAsset sharedFont)
        {
            this.app = app;
            font = sharedFont;
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 10;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(390, 740);
            // Expand keeps a minimum logical height on short phones and tablets.
            // Width-only scaling can leave less than 50 points for the cat on iPad.
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            gameObject.AddComponent<GraphicRaycaster>();
            safe = Box("Safe area", transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            overview = Box("Meet the cats", safe, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            detail = Box("Meet one cat", safe, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            overviewTop = Panel("Overview header", overview, 138, true);
            Label("Eyebrow", overviewTop, "MOMO · 萌宠小店", 11, Muted, 22, 16, -44, 20);
            Label("Title", overviewTop, ModelAvailability.WebShowcase?"今日新伙伴":"遇见你的猫", 28, Ink, 22, 38, -44, 56).fontStyle = FontStyles.Bold;
            Label("Intro", overviewTop, ModelAvailability.WebShowcase?"先认识今天新增的两只猫咪，其他伙伴正在待选中。":"先打个招呼，再决定和谁一起回家。", 14, Muted, 22, 100, -44, 32);
            overviewBottom = Panel("Overview footer", overview, 104, false);
            Label("Hint", overviewBottom, ModelAvailability.WebShowcase?"轻点今日新伙伴，靠近认识它":"轻点猫咪，靠近认识它", 14, Muted, 20, 6, -40, 24).alignment = TextAlignmentOptions.Center;
            home = Action("返回我的房间", overviewBottom, 20, 40, -40, 46, () => app.CloseBoutique());
            for (int i = 0; i < badges.Length; i++)
            {
                int index = i;
                var button = Action(BreedArchetypes.Get(i).defaultName, overview, 0, 0, 90, 38, () => Select(index));
                badges[i] = (RectTransform)button.transform;
                badges[i].anchorMin = badges[i].anchorMax = Vector2.zero;
                badges[i].pivot = new Vector2(.5f, .5f);
                var label=button.GetComponentInChildren<TMP_Text>();label.fontSize = 12;
                if(!ModelAvailability.IsVisibleInBoutique(i))button.gameObject.SetActive(false);
                else if(ModelAvailability.IsPendingInBoutique(i))
                {label.text="待选";button.interactable=false;}
            }

            detailTop = Panel("Detail header", detail, 150, true);
            Action("‹  所有猫咪", detailTop, 16, 6, 116, 44, () => { RememberName(); app.boutique.ReturnToGrid(); });
            count = Label("Position", detailTop, "", 12, Muted, 20, 12, -40, 28); count.alignment = TextAlignmentOptions.Right;
            title = Label("Name", detailTop, "", 28, Ink, 22, 44, -44, 54); title.fontStyle = FontStyles.Bold;
            breed = Label("Coat", detailTop, "", 15, Muted, 23, 99, -46, 24);
            traits = Label("Temperament", detailTop, "", 13, Accent, 23, 126, -140, 20);
            var profileButton = Action("档案", detailTop, 0, 0, 76, 40, OpenProfile);
            var profileButtonRect = (RectTransform)profileButton.transform;
            profileButtonRect.anchorMin = profileButtonRect.anchorMax = Vector2.one;
            profileButtonRect.anchoredPosition = new Vector2(-96, -106);

            detailBottom = Panel("Getting acquainted", detail, 318, false);
            description = Label("Appearance", detailBottom, "", 14, Ink, 22, 12, -44, 42);
            feedback = Label("Response", detailBottom, "轻触猫咪，或试试下面的互动。", 12, Muted, 22, 57, -44, 22);
            var actions = Box("Interactions", detailBottom, new Vector2(0, 1), Vector2.one, new Vector2(20, -87), new Vector2(-40, 44));
            ThirdAction("摸摸它", actions, 0, () => { app.boutique.PetSelected(); React("它眯起眼睛，轻轻抬起背。 "); });
            ThirdAction("看这里", actions, 1, () => { app.boutique.TeaseSelected(); React("它转过头，看看你的方向。 "); });
            ThirdAction("伸懒腰", actions, 2, () => { app.boutique.StretchSelected(); React("前爪伸出去，舒舒服服伸个懒腰。 "); });
            Label("Name prompt", detailBottom, "你想怎么称呼它？", 12, Muted, 22, 144, -44, 20);
            var inputBox = Surface("Cat name", detailBottom, 20, 171, -40, 44, Color.white);
            nameInput = inputBox.gameObject.AddComponent<TMP_InputField>();
            nameInput.targetGraphic = inputBox.GetComponent<Image>();
            var inputText = Label("Input text", inputBox, "", 17, Ink, 12, 0, -24, 44);
            inputText.alignment = TextAlignmentOptions.Left;
            nameInput.textViewport = inputBox; nameInput.textComponent = inputText; nameInput.characterLimit = 24;
            nameInput.lineType = TMP_InputField.LineType.SingleLine;
            nameInput.onValueChanged.AddListener(_ => ResetConfirmation());
            adopt = Action("带它回家", detailBottom, 20, 229, -40, 48, Commit);
            adopt.targetGraphic.color = Accent; adopt.GetComponentInChildren<TMP_Text>().color = Color.white;
            Label("Footnote", detailBottom, "相处，才是了解彼此的开始。", 11, Muted, 20, 286, -40, 20).alignment = TextAlignmentOptions.Center;
            // Navigation is outside the cat's viewport and remains thumb-reachable.
            var previous = Action("‹", detailTop, 0, 51, 52, 44, () => Select(app.boutique.NextSelectable(selected,-1)));
            ((RectTransform)previous.transform).anchorMin = ((RectTransform)previous.transform).anchorMax = Vector2.one;
            ((RectTransform)previous.transform).anchoredPosition = new Vector2(-136, -51);
            var next = Action("›", detailTop, 0, 51, 52, 44, () => Select(app.boutique.NextSelectable(selected,1)));
            ((RectTransform)next.transform).anchorMin = ((RectTransform)next.transform).anchorMax = Vector2.one;
            ((RectTransform)next.transform).anchoredPosition = new Vector2(-74, -51);
            BuildProfile();
            gameObject.SetActive(false);
        }

        void BuildProfile()
        {
            profileShade = Box("Cat profile shade", detail, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            profileShade.gameObject.AddComponent<Image>().color = GameHudStyle.Scrim;
            profileCard = Box("Cat profile card", profileShade, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                new Vector2(-175, 270), new Vector2(350, 540));
            profileCard.gameObject.AddComponent<Image>().color = Paper;
            GameHudStyle.Surface(profileCard.GetComponent<Image>(),false,true);
            profileNumber = Label("Profile number", profileCard, "", 11, Accent, 20, 16, -110, 20);
            var close = Action("关闭", profileCard, 0, 0, 72, 38, CloseProfile);
            var closeRect = (RectTransform)close.transform;
            closeRect.anchorMin = closeRect.anchorMax = Vector2.one;
            closeRect.anchoredPosition = new Vector2(-92, -14);
            profileName = Label("Profile name", profileCard, "", 26, Ink, 20, 50, -40, 40);
            profileName.fontStyle = FontStyles.Bold;
            profileBreed = Label("Profile breed", profileCard, "", 14, Muted, 20, 94, -40, 28);
            Surface("Profile divider", profileCard, 20, 130, -40, 1, Mist);
            Label("Appearance heading", profileCard, "外观特征", 12, Accent, 20, 147, -40, 22);
            profileAppearance = Label("Profile appearance", profileCard, "", 15, Ink, 20, 174, -40, 54);
            Label("Personality heading", profileCard, "初始性格倾向", 12, Accent, 20, 239, -40, 22);
            profileSummary = Label("Profile summary", profileCard, "", 16, Ink, 20, 262, -40, 24);
            for (int i = 0; i < profileLevels.Length; i++)
            {
                var row = Surface("Personality " + PersonalityLabels[i], profileCard, 20, 296 + i * 41, -40, 35, Mist);
                Label("Trait name", row, PersonalityLabels[i], 14, Ink, 12, 5, 120, 25);
                profileLevels[i] = Label("Trait level", row, "", 14, Accent, 0, 5, 88, 25);
                var levelRect = profileLevels[i].rectTransform;
                levelRect.anchorMin = levelRect.anchorMax = Vector2.one;
                levelRect.anchoredPosition = new Vector2(-100, -5);
                profileLevels[i].alignment = TextAlignmentOptions.Right;
            }
            Label("Profile note", profileCard, "这是领养前的初始倾向；相处习惯会慢慢形成。", 12, Muted, 20, 461, -40, 30);
            Action("继续认识它", profileCard, 20, 493, -40, 38, CloseProfile);
            profileShade.gameObject.SetActive(false);
        }

        static string Level(float value)
        { return value >= .8f ? "很强" : value >= .6f ? "较强" : value >= .4f ? "适中" : "较弱"; }

        public void OpenProfile()
        {
            if (app == null || !app.boutique.IsZoomed) return;
            int index = app.boutique.SelectedIndex;
            var cat = BreedArchetypes.Get(index);
            ProfileIndex = index;
            profileNumber.text = "猫咪档案 · " + (index + 1).ToString("00");
            profileName.text = cat.defaultName;
            profileBreed.text = BreedNames[index] + "  ·  " + cat.breedName;
            profileAppearance.text = AppearanceFacts[index];
            profileSummary.text = Traits[index];
            profileLevels[0].text = Level(cat.personality.playfulness);
            profileLevels[1].text = Level(cat.personality.curiosity);
            profileLevels[2].text = Level(cat.personality.sociability);
            profileLevels[3].text = Level(cat.personality.independence);
            profileShade.gameObject.SetActive(true);
        }

        public void CloseProfile()
        {
            ProfileIndex = -1;
            if (profileShade != null) profileShade.gameObject.SetActive(false);
        }

        void RememberName() { if (selected >= 0) names[selected] = nameInput.text; }
        void Select(int index) { RememberName(); app.boutique.SelectCat(index); }
        void ResetConfirmation()
        {
            confirmChange = false;
            if (adopt != null) adopt.GetComponentInChildren<TMP_Text>().text = app.CatCount>=WorldSimulation.Capacity ? "家里已有五只猫" : "带它回家";
        }
        void Commit()
        {
            if(app.simulation.State.adopted&&app.CatCount>=WorldSimulation.Capacity){feedback.text="家里已有五只猫，先陪陪它们吧。";return;}
            app.boutique.AdoptSelected(nameInput.text);
        }
        void React(string message) { ResetConfirmation(); feedback.text = message; feedbackUntil = Time.unscaledTime + 3.5f; }

        public void Refresh(bool visible)
        {
            if (gameObject.activeSelf != visible) gameObject.SetActive(visible);
            if (!visible) { selected = -1; CloseProfile(); return; }
            var area = Screen.safeArea;
            safe.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
            safe.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
            bool focused = app.boutique.IsZoomed;
            overview.gameObject.SetActive(!focused); detail.gameObject.SetActive(focused);
            if (!focused) CloseProfile();
            home.gameObject.SetActive(app.simulation.State.adopted);
            if (focused && selected != app.boutique.SelectedIndex)
            {
                CloseProfile();
                RememberName(); selected = app.boutique.SelectedIndex; var profile = BreedArchetypes.Get(selected);
                title.text = profile.defaultName; breed.text = BreedNames[selected];
                traits.text = "初识印象  ·  " + Traits[selected]; count.text = (selected + 1).ToString("00") + " / 09";
                description.text = Descriptions[selected];
                nameInput.text = names[selected] ?? profile.defaultName;
                feedback.text = "轻触猫咪，或试试下面的互动。"; ResetConfirmation();
            }
            if (!focused && selected >= 0) { RememberName(); selected = -1; }
            if (!confirmChange && Time.unscaledTime > feedbackUntil) feedback.text = "轻触猫咪，或试试下面的互动。";
            Canvas.ForceUpdateCanvases();
            float profileWidth = Mathf.Min(350, safe.rect.width - 24);
            profileCard.sizeDelta = new Vector2(profileWidth, 540);
            profileCard.anchoredPosition = new Vector2(-profileWidth / 2, 270);
            float scale = GetComponent<Canvas>().scaleFactor;
            float top = (focused ? 154 : 140) * scale;
            float bottom = (focused ? 322 : 106) * scale;
            app.boutique.SetViewport(new Rect(area.x / Screen.width, (area.y + bottom) / Screen.height,
                area.width / Screen.width, Mathf.Max(80, area.height - top - bottom) / Screen.height));
        }

        void LateUpdate()
        {
            if (app.boutique.IsZoomed) return;
            // Labels follow the actual camera projection, never a guessed 1440-wide grid.
            for (int i = 0; i < badges.Length; i++)
            {
                if(!badges[i].gameObject.activeSelf)continue;
                Vector3 position = app.boutique.GetCubbyScreenPosition(i);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(overview, position, null, out var local);
                badges[i].localPosition = local;
                badges[i].sizeDelta = new Vector2(Mathf.Min(100, safe.rect.width / 3 - 10), 30);
            }
        }

        RectTransform Box(string name, Transform parent, Vector2 min, Vector2 max, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.anchorMin = min; rect.anchorMax = max;
            rect.pivot = new Vector2(0, 1); rect.anchoredPosition = position; rect.sizeDelta = size; return rect;
        }
        RectTransform Panel(string name, Transform parent, float height, bool top)
        {
            var rect = Box(name, parent, new Vector2(0, top ? 1 : 0), new Vector2(1, top ? 1 : 0), Vector2.zero, new Vector2(0, height));
            rect.pivot = new Vector2(0, top ? 1 : 0); rect.gameObject.AddComponent<Image>().color = Paper; GameHudStyle.Surface(rect.GetComponent<Image>(),false,true); return rect;
        }
        RectTransform Surface(string name, Transform parent, float x, float y, float width, float height, Color color)
        {
            var rect = Box(name, parent, new Vector2(0, 1), new Vector2(width <= 0 ? 1 : 0, 1), new Vector2(x, -y), new Vector2(width, height));
            rect.gameObject.AddComponent<Image>().color = color; GameHudStyle.Surface(rect.GetComponent<Image>()); return rect;
        }
        TMP_Text Label(string name, Transform parent, string value, int size, Color color, float x, float y, float width, float height)
        {
            var rect = Box(name, parent, new Vector2(0, 1), new Vector2(width <= 0 ? 1 : 0, 1), new Vector2(x, -y), new Vector2(width, height));
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>(); text.font = font; text.fontSize = size; text.color = color; text.text = value;
            text.raycastTarget = false; text.textWrappingMode = TextWrappingModes.Normal; text.overflowMode = TextOverflowModes.Truncate;
            return text;
        }
        Button Action(string label, Transform parent, float x, float y, float width, float height, UnityEngine.Events.UnityAction action)
        {
            var rect = Surface(label, parent, x, y, width, height, Mist);
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = rect.GetComponent<Image>(); button.onClick.AddListener(action);
            var text = Label("Label", rect, label, 15, Ink, 4, 0, -8, height); text.alignment = TextAlignmentOptions.Center;
            var colors = button.colors; colors.highlightedColor = Hex("#D6E5DA"); colors.pressedColor = Hex("#B5D2C1"); colors.fadeDuration=.1f;button.colors = colors;
            return button;
        }
        void ThirdAction(string label, RectTransform parent, int column, UnityEngine.Events.UnityAction action)
        {
            var button = Action(label, parent, 0, 0, 0, 44, action);
            var rect = (RectTransform)button.transform;
            rect.anchorMin = new Vector2(column / 3f, 1); rect.anchorMax = new Vector2((column + 1) / 3f, 1);
            rect.offsetMin = new Vector2(column == 0 ? 0 : 4, -44); rect.offsetMax = new Vector2(column == 2 ? 0 : -4, 0);
        }
    }
}

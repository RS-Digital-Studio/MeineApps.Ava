#nullable enable
using System.Threading;
using ArcaneKingdom.Core.Services;
using ArcaneKingdom.Domain.Player;
using ArcaneKingdom.Game.Artwork;
using ArcaneKingdom.UI.Foundation;
using Cysharp.Threading.Tasks;
using UnityEngine.UIElements;

namespace ArcaneKingdom.UI.Guild
{
    /// <summary>
    /// Guild-Screen mit drei Tabs (Mitglieder, Tech-Tree, Chat). Wenn der Spieler in
    /// keiner Gilde ist, wird ein Empty-State mit "Gilde suchen" / "Gilde gründen"
    /// gezeigt.
    /// </summary>
    public sealed class GuildScreen : ScreenBase
    {
        private readonly ScreenManager _screenManager;
        private readonly ISaveService<PlayerSave> _save;
        private readonly ToastService _toast;

        private Button _backBtn = null!;
        private Label _name = null!;
        private Label _pointsLabel = null!;

        private Button _tabMembers = null!;
        private Button _tabTech = null!;
        private Button _tabChat = null!;

        private VisualElement _emptyState = null!;
        private VisualElement _membersTab = null!;
        private VisualElement _techTab = null!;
        private VisualElement _chatTab = null!;

        private Button _findBtn = null!;
        private Button _createBtn = null!;
        private Button _donateBtn = null!;
        private TextField _chatInput = null!;
        private Button _chatSend = null!;
        private VisualElement _chatMessages = null!;

        public override string Id => ScreenId.Guild;
        protected override string UxmlPath => "UI/GuildScreen";

        private readonly UIAssetService _uiAssets;

        public GuildScreen(ScreenManager screenManager, ISaveService<PlayerSave> save,
                           ToastService toast, UIAssetService uiAssets)
        {
            _screenManager = screenManager;
            _save = save;
            _toast = toast;
            _uiAssets = uiAssets;
        }

        protected override void BindElements(VisualElement root)
        {
            _uiAssets.ApplyUIBackground(root, "gilde");
            _backBtn      = Q<Button>("guild-back-button");
            _name         = Q<Label>("guild-name");
            _pointsLabel  = Q<Label>("guild-points-label");

            _tabMembers   = Q<Button>("guild-tab-members");
            _tabTech      = Q<Button>("guild-tab-tech");
            _tabChat      = Q<Button>("guild-tab-chat");

            _emptyState   = Q<VisualElement>("guild-empty");
            _membersTab   = Q<VisualElement>("guild-members-tab");
            _techTab      = Q<VisualElement>("guild-tech-tab");
            _chatTab      = Q<VisualElement>("guild-chat-tab");

            _findBtn      = Q<Button>("guild-find");
            _createBtn    = Q<Button>("guild-create");
            _donateBtn    = Q<Button>("guild-donate");
            _chatInput    = Q<TextField>("guild-chat-input");
            _chatSend     = Q<Button>("guild-chat-send");
            _chatMessages = Q<VisualElement>("guild-chat-messages");

            _backBtn.clicked += () => _screenManager.PopAsync().Forget();
            _tabMembers.clicked += () => SwitchTab("members");
            _tabTech.clicked    += () => SwitchTab("tech");
            _tabChat.clicked    += () => SwitchTab("chat");

            _findBtn.clicked   += () => _toast.Show("Gilden-Suche kommt mit Backend.", ToastKind.Info);
            _createBtn.clicked += () => _toast.Show("Gilden-Gruendung erfordert Stufe 25.", ToastKind.Warning);
            _donateBtn.clicked += () => _toast.Show("+10 GP gespendet (Mock).", ToastKind.Success);
            _chatSend.clicked  += OnSendChat;
        }

        public override async UniTask OnEnterAsync(CancellationToken ct)
        {
            var result = await _save.LoadAsync(ct);
            if (!result.IsSuccess || result.Value == null) return;
            var save = result.Value;

            _pointsLabel.text = $"{save.Currencies.GuildPoints} GP";

            var inGuild = !string.IsNullOrEmpty(save.Profile.GuildId);
            if (inGuild)
            {
                _name.text = $"Gilde {save.Profile.GuildId}";
                _emptyState.AddToClassList("ak-hidden");
                SwitchTab("members");
                MockPopulateMembers(save);
            }
            else
            {
                _name.text = "Gilde";
                _emptyState.RemoveFromClassList("ak-hidden");
                _membersTab.AddToClassList("ak-hidden");
                _techTab.AddToClassList("ak-hidden");
                _chatTab.AddToClassList("ak-hidden");
            }
        }

        private void SwitchTab(string id)
        {
            // Empty-State ausblenden wenn ein Tab gewaehlt wird
            _emptyState.AddToClassList("ak-hidden");

            SetVisible(_membersTab, id == "members");
            SetVisible(_techTab,    id == "tech");
            SetVisible(_chatTab,    id == "chat");

            UpdateTabStyle(_tabMembers, id == "members");
            UpdateTabStyle(_tabTech,    id == "tech");
            UpdateTabStyle(_tabChat,    id == "chat");
        }

        private static void SetVisible(VisualElement el, bool visible)
        {
            if (visible) el.RemoveFromClassList("ak-hidden");
            else el.AddToClassList("ak-hidden");
        }

        private static void UpdateTabStyle(Button btn, bool active)
        {
            btn.RemoveFromClassList("ak-btn--primary");
            btn.RemoveFromClassList("ak-btn--ghost");
            btn.AddToClassList(active ? "ak-btn--primary" : "ak-btn--ghost");
        }

        private void MockPopulateMembers(PlayerSave save)
        {
            var list = _membersTab.Q<ScrollView>()?.contentContainer
                       ?? _membersTab.Q<VisualElement>();
            list?.Clear();

            // Wir haben keinen echten Members-Service — Mock 3 Einträge
            for (var i = 0; i < 3; i++)
            {
                var row = new VisualElement();
                row.AddToClassList("ak-surface");
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                row.style.marginBottom = 4;

                var name = new Label(i == 0 ? save.Profile.DisplayName : $"Member_{i}");
                name.AddToClassList("ak-body");
                name.style.flexGrow = 1;
                row.Add(name);

                var level = new Label($"Lv {save.Profile.Level + i}");
                level.AddToClassList("ak-caption");
                row.Add(level);

                list?.Add(row);
            }
        }

        private void OnSendChat()
        {
            var msg = (_chatInput.value ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(msg)) return;

            var row = new Label($"Du: {msg}");
            row.AddToClassList("ak-body");
            row.style.marginBottom = 4;
            _chatMessages.Add(row);
            _chatInput.SetValueWithoutNotify(string.Empty);

            _toast.Show("Mock-Chat — kein Server-Backend angebunden.", ToastKind.Info);
        }
    }
}

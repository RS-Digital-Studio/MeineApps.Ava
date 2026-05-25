#nullable enable
using ArcaneKingdom.Core.Services;
using ArcaneKingdom.UI.Foundation;
using Cysharp.Threading.Tasks;
using UnityEngine.UIElements;

namespace ArcaneKingdom.UI.Chat
{
    /// <summary>
    /// Chat-Overlay (Spielplan v5 Kap. 14 + Impl_KOMPLETT Kap. 13).
    /// Halb-transparentes Slide-Up-Overlay mit 4 Tabs: Alle | Welt | Privat | Gilde.
    /// Backend lokal mit Mock-Daten, Firebase-Realtime-DB folgt.
    /// </summary>
    public sealed class ChatOverlay : ScreenBase
    {
        private readonly ScreenManager _screenManager;
        private readonly ToastService _toast;

        private VisualElement _tabBar = null!;
        private VisualElement _messages = null!;
        private TextField _input = null!;
        private Button _sendBtn = null!;
        private Button _closeBtn = null!;
        private string _activeChannel = "world";

        public override string Id => ScreenId.ChatOverlay;
        public override bool IsOverlay => true;
        protected override string UxmlPath => "UI/ChatOverlay";

        public ChatOverlay(ScreenManager screenManager, ToastService toast)
        {
            _screenManager = screenManager;
            _toast = toast;
        }

        protected override void BindElements(VisualElement root)
        {
            _tabBar   = Q<VisualElement>("chat-tabs");
            _messages = Q<VisualElement>("chat-messages");
            _input    = Q<TextField>("chat-input");
            _sendBtn  = Q<Button>("chat-send");
            _closeBtn = Q<Button>("chat-close");

            _closeBtn.clicked += () => _screenManager.PopAsync().Forget();
            _sendBtn.clicked += OnSendClicked;
            BuildTabs();
            RenderMessages();

            var backdrop = QOptional<VisualElement>("chat-backdrop");
            if (backdrop != null)
                backdrop.RegisterCallback<ClickEvent>(evt => { if (evt.target == backdrop) _screenManager.PopAsync().Forget(); });
        }

        private void BuildTabs()
        {
            _tabBar.Clear();
            string[] channels = { "all", "world", "private", "guild" };
            string[] labels = { "Alle", "Welt", "Privat", "Gilde" };
            for (var i = 0; i < channels.Length; i++)
            {
                var id = channels[i];
                var btn = new Button(() => { _activeChannel = id; BuildTabs(); RenderMessages(); }) { text = labels[i] };
                btn.style.flexGrow = 1; btn.style.height = 36; btn.style.marginRight = 4;
                if (id == _activeChannel)
                {
                    btn.style.backgroundColor = new StyleColor(new UnityEngine.Color(0.96f, 0.78f, 0.26f));
                    btn.style.color = new StyleColor(new UnityEngine.Color(0.07f, 0.07f, 0.13f));
                }
                _tabBar.Add(btn);
            }
        }

        private void RenderMessages()
        {
            _messages.Clear();
            // Mock-Nachrichten
            (string player, string msg, bool system)[] msgs = _activeChannel switch
            {
                "world" => new[]
                {
                    ("[KINGZ] Sperber", "Hat jemand eine Maxima-Karte zu tauschen?", false),
                    ("[NEXUS] Aria", "Sucht jemand fuer ein Klan-Match um 19:50?", false),
                    ("System", "Mysterioeser Dieb wurde entdeckt!", true)
                },
                "guild" => new[]
                {
                    ("[KINGZ] Drachenfaust", "Tech-Spenden bitte fuer Drachenhort Tier 3!", false),
                    ("System", "Gilden-Level 12 erreicht!", true)
                },
                "private" => new[]
                {
                    ("Sturmreiterin", "Hey, willst du gemeinsam Welt 7 angreifen?", false)
                },
                _ => new[]
                {
                    ("[KINGZ] Sperber", "Hat jemand eine Maxima-Karte zu tauschen?", false),
                    ("[KINGZ] Drachenfaust", "Tech-Spenden bitte!", false),
                    ("System", "Mysterioeser Dieb wurde entdeckt!", true)
                }
            };

            foreach (var (player, msg, system) in msgs)
            {
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.FlexStart;
                row.style.marginBottom = 6;

                var playerLbl = new Label(player);
                playerLbl.style.fontSize = 12;
                playerLbl.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Bold;
                playerLbl.style.color = system
                    ? new StyleColor(new UnityEngine.Color(0.96f, 0.78f, 0.26f))
                    : new StyleColor(new UnityEngine.Color(0.41f, 0.55f, 1.0f));
                playerLbl.style.minWidth = 130;
                row.Add(playerLbl);

                var msgLbl = new Label(msg);
                msgLbl.style.fontSize = 12;
                msgLbl.style.color = new StyleColor(new UnityEngine.Color(0.88f, 0.88f, 0.92f));
                msgLbl.style.flexGrow = 1;
                msgLbl.style.whiteSpace = WhiteSpace.Normal;
                row.Add(msgLbl);

                _messages.Add(row);
            }
        }

        private void OnSendClicked()
        {
            if (string.IsNullOrWhiteSpace(_input.value)) return;
            _toast.Show($"Nachricht gesendet ({_activeChannel}): {_input.value}", ToastKind.Info, 2f);
            _input.value = string.Empty;
            RenderMessages();
        }
    }
}

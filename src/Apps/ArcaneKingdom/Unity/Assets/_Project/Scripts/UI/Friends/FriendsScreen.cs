#nullable enable
using System.Threading;
using ArcaneKingdom.Core.Services;
using ArcaneKingdom.Domain.Player;
using ArcaneKingdom.UI.Foundation;
using Cysharp.Threading.Tasks;
using UnityEngine.UIElements;

namespace ArcaneKingdom.UI.Friends
{
    /// <summary>
    /// Freundes-Liste mit zwei Tabs (Liste / Anfragen). Daten kommen aus
    /// PlayerSave.FriendsSlice.
    /// </summary>
    public sealed class FriendsScreen : ScreenBase
    {
        private readonly ScreenManager _screenManager;
        private readonly ISaveService<PlayerSave> _save;
        private readonly ToastService _toast;

        private Button _backBtn = null!;
        private Button _addBtn = null!;
        private Button _tabList = null!;
        private Button _tabRequests = null!;

        private VisualElement _listTab = null!;
        private VisualElement _requestsTab = null!;
        private VisualElement _list = null!;
        private VisualElement _requestsList = null!;
        private Label _listEmpty = null!;
        private Label _requestsEmpty = null!;

        public override string Id => ScreenId.Friends;
        protected override string UxmlPath => "UI/FriendsScreen";

        public FriendsScreen(ScreenManager screenManager,
                             ISaveService<PlayerSave> save, ToastService toast)
        {
            _screenManager = screenManager;
            _save = save;
            _toast = toast;
        }

        protected override void BindElements(VisualElement root)
        {
            _backBtn       = Q<Button>("friends-back-button");
            _addBtn        = Q<Button>("friends-add-button");
            _tabList       = Q<Button>("friends-tab-list");
            _tabRequests   = Q<Button>("friends-tab-requests");
            _listTab       = Q<VisualElement>("friends-list-tab");
            _requestsTab   = Q<VisualElement>("friends-requests-tab");
            _list          = Q<VisualElement>("friends-list");
            _requestsList  = Q<VisualElement>("friends-requests-list");
            _listEmpty     = Q<Label>("friends-list-empty");
            _requestsEmpty = Q<Label>("friends-requests-empty");

            _backBtn.clicked += () => _screenManager.PopAsync().Forget();
            _addBtn.clicked  += () =>
                _toast.Show("Friend-Code-System folgt mit Backend-Integration.", ToastKind.Info);
            _tabList.clicked     += () => SwitchTab(showList: true);
            _tabRequests.clicked += () => SwitchTab(showList: false);
        }

        public override async UniTask OnEnterAsync(CancellationToken ct)
        {
            var result = await _save.LoadAsync(ct);
            if (!result.IsSuccess || result.Value == null) return;
            var slice = result.Value.FriendsSlice;

            BuildFriendList(slice);
            BuildRequestsList(slice);
            SwitchTab(showList: true);
        }

        private void SwitchTab(bool showList)
        {
            if (showList)
            {
                _listTab.RemoveFromClassList("ak-hidden");
                _requestsTab.AddToClassList("ak-hidden");
            }
            else
            {
                _listTab.AddToClassList("ak-hidden");
                _requestsTab.RemoveFromClassList("ak-hidden");
            }
            UpdateBtn(_tabList, showList);
            UpdateBtn(_tabRequests, !showList);
        }

        private static void UpdateBtn(Button b, bool active)
        {
            b.RemoveFromClassList("ak-btn--primary");
            b.RemoveFromClassList("ak-btn--ghost");
            b.AddToClassList(active ? "ak-btn--primary" : "ak-btn--ghost");
        }

        private void BuildFriendList(FriendsSaveSlice slice)
        {
            _list.Clear();
            if (slice.Friends.Count == 0)
            {
                _listEmpty.RemoveFromClassList("ak-hidden");
                return;
            }
            _listEmpty.AddToClassList("ak-hidden");
            foreach (var f in slice.Friends)
                _list.Add(BuildFriendRow(f.DisplayName, f.Status.ToString()));
        }

        private void BuildRequestsList(FriendsSaveSlice slice)
        {
            _requestsList.Clear();
            if (slice.IncomingRequests.Count == 0)
            {
                _requestsEmpty.RemoveFromClassList("ak-hidden");
                return;
            }
            _requestsEmpty.AddToClassList("ak-hidden");
            foreach (var req in slice.IncomingRequests)
                _requestsList.Add(BuildRequestRow(req.FromDisplayName));
        }

        private static VisualElement BuildFriendRow(string name, string status)
        {
            var row = new VisualElement();
            row.AddToClassList("ak-surface");
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 4;

            var nameLabel = new Label(name);
            nameLabel.AddToClassList("ak-body");
            nameLabel.style.flexGrow = 1;
            row.Add(nameLabel);

            var statusLabel = new Label(status);
            statusLabel.AddToClassList("ak-caption");
            statusLabel.AddToClassList(status is "Online" or "InBattle" or "InArena"
                ? "ak-text--success" : "ak-text--muted");
            row.Add(statusLabel);

            return row;
        }

        private VisualElement BuildRequestRow(string name)
        {
            var row = new VisualElement();
            row.AddToClassList("ak-surface");
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 4;

            var nameLabel = new Label(name);
            nameLabel.AddToClassList("ak-body");
            nameLabel.style.flexGrow = 1;
            row.Add(nameLabel);

            var accept = new Button(() => _toast.Show("Annehmen — Backend folgt.", ToastKind.Info))
                { text = "✓" };
            accept.AddToClassList("ak-btn"); accept.AddToClassList("ak-btn--sm");
            accept.AddToClassList("ak-btn--primary"); accept.AddToClassList("ak-btn--icon");
            row.Add(accept);

            var reject = new Button(() => _toast.Show("Ablehnen — Backend folgt.", ToastKind.Info))
                { text = "✕" };
            reject.AddToClassList("ak-btn"); reject.AddToClassList("ak-btn--sm");
            reject.AddToClassList("ak-btn--ghost"); reject.AddToClassList("ak-btn--icon");
            row.Add(reject);

            return row;
        }
    }
}

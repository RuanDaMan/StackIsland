using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace StackIsland
{
    /// <summary>
    /// Minimal UI in the genre's style: just a cash counter (top right) while playing, short toasts, a start
    /// menu and an Esc menu.
    /// </summary>
    public class Hud : MonoBehaviour
    {
        Game _g;
        RectTransform _root;
        TextMeshProUGUI _cash, _mgrText;
        Image _mgrBox;
        int _mgrShown = -1;
        RectTransform _cashBox;
        double _shown;
        float _pulse;
        RectTransform _toasts;
        readonly List<(RectTransform rt, CanvasGroup cg, float t)> _toastList = new List<(RectTransform, CanvasGroup, float)>();
        GameObject _menu, _pause, _hudLayer;
        Button _continueBtn;
        Toggle _windowed, _managers;

        public bool IsMenuOpen => (_menu != null && _menu.activeSelf) || (_pause != null && _pause.activeSelf) || (_welcome != null && _welcome.activeSelf);

        public void Init(Game g)
        {
            _g = g;
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<InputSystemUIInputModule>();
            }
            var cgo = new GameObject("Canvas", typeof(RectTransform));
            cgo.transform.SetParent(transform, false);
            var canvas = cgo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = cgo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            cgo.AddComponent<GraphicRaycaster>();
            _root = (RectTransform)cgo.transform;

            _hudLayer = UIKit.Rect("hud", _root).gameObject;
            UIKit.Stretch((RectTransform)_hudLayer.transform);
            BuildCash();
            BuildToasts();
            BuildInfo();
            BuildPause();
            BuildMenu();
            _shown = g.Cash;
        }

        // ------------------------------------------------------------------ cash counter

        void BuildCash()
        {
            var box = UIKit.Panel(_hudLayer.transform, new Color(0.08f, 0.1f, 0.14f, 0.55f), "cash");
            _cashBox = box.rectTransform;
            UIKit.Place(_cashBox, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-28, -24), new Vector2(330, 92));
            // a green bill icon
            var bill = UIKit.Panel(_cashBox, new Color(0.33f, 0.78f, 0.36f), "bill");
            UIKit.Place(bill.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(18, 0), new Vector2(78, 50));
            bill.rectTransform.localRotation = Quaternion.Euler(0, 0, 12);
            var band = UIKit.Panel(bill.rectTransform, new Color(0.95f, 0.97f, 0.9f), "band");
            UIKit.Place(band.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(14, 50));
            _cash = UIKit.Text(_cashBox, "0", 56, TextAlignmentOptions.MidlineRight, Color.white, "value");
            _cash.fontStyle = FontStyles.Bold;
            _cash.outlineWidth = 0.18f;
            _cash.outlineColor = new Color32(0, 0, 0, 160);
            UIKit.Place(_cash.rectTransform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-20, 0), new Vector2(220, 80));

            // manager auto-buy status under the cash (only once a manager is hired)
            _mgrBox = UIKit.Panel(_hudLayer.transform, new Color(0.08f, 0.1f, 0.14f, 0.55f), "managers");
            _mgrBox.raycastTarget = false;
            UIKit.Place(_mgrBox.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-28, -124), new Vector2(330, 44));
            _mgrText = UIKit.Text(_mgrBox.rectTransform, "", 22, TextAlignmentOptions.Center, Color.white, "label");
            UIKit.Stretch(_mgrText.rectTransform, 6);
            _mgrBox.gameObject.SetActive(false);
        }

        void UpdateManagers()
        {
            if (!_g.HasManagers) { if (_mgrShown != -1) { _mgrBox.gameObject.SetActive(false); _mgrShown = -1; } return; }
            int state = _g.Save.managersPaused ? 0 : 1;
            if (state == _mgrShown) return;
            _mgrShown = state;
            _mgrBox.gameObject.SetActive(true);
            _mgrText.text = state == 1
                ? "Manager auto-buy <color=#9CFF9A><b>ON</b></color>  ·  <b>M</b> to pause"
                : "Manager auto-buy <color=#FF9A8A><b>OFF</b></color>  ·  <b>M</b> to resume";
        }

        // ------------------------------------------------------------------ toasts

        void BuildToasts()
        {
            _toasts = UIKit.Rect("toasts", _hudLayer.transform);
            UIKit.Place(_toasts, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -40), new Vector2(800, 200));
            var v = UIKit.VLayout(_toasts.gameObject, 6, 0);
            v.childAlignment = TextAnchor.UpperCenter;
            v.childForceExpandWidth = false;
        }

        public void Toast(string msg)
        {
            var bg = UIKit.Panel(_toasts, new Color(0.08f, 0.1f, 0.14f, 0.75f), "toast");
            // the layout sizes the box from the text when it rebuilds; measuring here could return 0 before TMP is ready,
            // which squeezed the toast into a one-letter-wide column
            var h = bg.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(25, 25, 0, 0);
            h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = h.childForceExpandHeight = false;
            var t = UIKit.Text(bg.rectTransform, msg, 28, TextAlignmentOptions.Center);
            t.fontStyle = FontStyles.Bold;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Ellipsis;
            var le = bg.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 52;
            var cg = bg.gameObject.AddComponent<CanvasGroup>();
            _toastList.Add((bg.rectTransform, cg, 0f));
            while (_toastList.Count > 3) { Destroy(_toastList[0].rt.gameObject); _toastList.RemoveAt(0); }
        }

        // ------------------------------------------------------------------ info (hover / standing on a tile)

        Image _tipBg, _padBg;
        TextMeshProUGUI _tip, _padText;

        void BuildInfo()
        {
            _tipBg = UIKit.Panel(_hudLayer.transform, new Color(0.06f, 0.09f, 0.13f, 0.92f), "tooltip");
            _tipBg.raycastTarget = false;
            _tipBg.rectTransform.pivot = new Vector2(0, 1);
            _tipBg.rectTransform.anchorMin = _tipBg.rectTransform.anchorMax = Vector2.zero;
            _tip = UIKit.Text(_tipBg.rectTransform, "", 24);
            UIKit.Stretch(_tip.rectTransform, 14);
            _tipBg.gameObject.SetActive(false);

            _padBg = UIKit.Panel(_hudLayer.transform, new Color(0.06f, 0.09f, 0.13f, 0.88f), "padInfo");
            _padBg.raycastTarget = false;
            UIKit.Place(_padBg.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(720, 150));
            _padText = UIKit.Text(_padBg.rectTransform, "", 26, TextAlignmentOptions.Center);
            UIKit.Stretch(_padText.rectTransform, 14);
            _padBg.gameObject.SetActive(false);
        }

        void UpdateInfo()
        {
            if (IsMenuOpen || _g.Player == null) { _tipBg.gameObject.SetActive(false); _padBg.gameObject.SetActive(false); return; }

            // standing on a buy tile: say what it does at the bottom of the screen
            string padInfo = null;
            foreach (var p in BuyPad.All)
                if (p.Shown && p.PlayerOn(_g.Player.transform.position)) { padInfo = p.Info(); break; }
            _padBg.gameObject.SetActive(padInfo != null);
            if (padInfo != null && _padText.text != padInfo) _padText.text = padInfo;

            // mouse hover: stats for grills, the pen, the counter and tiles
            string tip = null;
            var cam = Camera.main;
            if (cam != null && !_g.PointerOverUI)
            {
                var ray = cam.ScreenPointToRay(GameInput.MousePosition);
                if (new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float enter)) tip = _g.InfoAt(ray.GetPoint(enter));
            }
            bool show = tip != null && tip != padInfo;
            if (_tipBg.gameObject.activeSelf != show) _tipBg.gameObject.SetActive(show);
            if (!show) return;
            _tip.text = tip;
            var size = _tip.GetPreferredValues(tip, 560, 1000);
            var rt = _tipBg.rectTransform;
            rt.sizeDelta = new Vector2(Mathf.Min(600, size.x + 30), size.y + 30);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, GameInput.MousePosition, null, out var local);
            var pos = local + _root.rect.size * 0.5f + new Vector2(24, -24);
            pos.x = Mathf.Min(pos.x, _root.rect.width - rt.sizeDelta.x - 8);
            pos.y = Mathf.Max(pos.y, rt.sizeDelta.y + 8);
            rt.anchoredPosition = pos;
        }

        // ------------------------------------------------------------------ menus

        GameObject Overlay(string name, float alpha)
        {
            var o = UIKit.Rect(name, _root);
            UIKit.Stretch(o);
            o.gameObject.AddComponent<Image>().color = new Color(0.03f, 0.08f, 0.14f, alpha);
            return o.gameObject;
        }

        void BuildMenu()
        {
            _menu = Overlay("menu", 0.35f);
            var title = UIKit.Text(_menu.transform, "STACK ISLAND", 120, TextAlignmentOptions.Center, Color.white, "title");
            title.fontStyle = FontStyles.Bold;
            title.outlineWidth = 0.25f;
            title.outlineColor = new Color32(20, 40, 70, 255);
            UIKit.Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 250), new Vector2(1400, 160));
            var sub = UIKit.Text(_menu.transform, "Catch it. Cook it. Stack it high.", 36, TextAlignmentOptions.Center, new Color(1f, 0.92f, 0.7f), "sub");
            sub.fontStyle = FontStyles.Italic;
            UIKit.Place(sub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 150), new Vector2(1200, 60));
            var col = UIKit.Rect("buttons", _menu.transform);
            UIKit.Place(col, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0, 60), new Vector2(420, 320));
            UIKit.VLayout(col.gameObject, 16, 0);
            _continueBtn = BigButton(col, "Continue", new Color(0.3f, 0.75f, 0.35f), () => CloseMenus());
            BigButton(col, "New Game", new Color(0.95f, 0.55f, 0.25f), () => _g.NewGame());
            BigButton(col, "Quit", new Color(0.35f, 0.38f, 0.45f), () => { _g.SaveNow(); Application.Quit(); });
            _menu.SetActive(false);
        }

        void BuildPause()
        {
            _pause = Overlay("pause", 0.55f);
            // one rounded card in the middle: title, Resume, settings, then leave options
            var card = UIKit.Panel(_pause.transform, new Color(0.1f, 0.14f, 0.2f, 0.96f), "card");
            UIKit.Place(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(560, 690));
            var col = UIKit.Rect("content", card.rectTransform);
            UIKit.Stretch(col, 36);
            var v = UIKit.VLayout(col.gameObject, 16, 0);
            v.childAlignment = TextAnchor.UpperCenter;
            var title = UIKit.Text(col, "Paused", 60, TextAlignmentOptions.Center, Color.white, "title");
            title.fontStyle = FontStyles.Bold;
            UIKit.Size(title, -1, 80);
            BigButton(col, "Resume", new Color(0.3f, 0.75f, 0.35f), () => CloseMenus());

            var settings = UIKit.Panel(col, new Color(1f, 1f, 1f, 0.06f), "settings");
            UIKit.Size(settings, -1, 180);
            var sv = UIKit.VLayout(settings.gameObject, 10, 16);
            sv.childAlignment = TextAnchor.MiddleLeft;
            var s = _g.Settings;
            var vol = UIKit.Slider(settings.transform, "Volume", s.volume, x => { s.volume = x; if (Sfx.I != null) Sfx.I.Volume = x; SaveSystem.SaveSettings(s); });
            ((RectTransform)vol.transform).GetComponent<LayoutElement>().preferredWidth = 220;
            _windowed = UIKit.Toggle(settings.transform, "Windowed", s.windowed, x => { s.windowed = x; _g.ApplyWindowMode(); SaveSystem.SaveSettings(s); });
            _managers = UIKit.Toggle(settings.transform, "Managers auto-buy upgrades (M)", !_g.Save.managersPaused, x => _g.SetManagersPaused(!x));

            BigButton(col, "Main menu", new Color(0.95f, 0.55f, 0.25f), () => { _g.SaveNow(); ShowMainMenu(); });
            BigButton(col, "Quit", new Color(0.35f, 0.38f, 0.45f), () => { _g.SaveNow(); Application.Quit(); });
            _pause.SetActive(false);
        }

        static Button BigButton(Transform parent, string label, Color c, UnityEngine.Events.UnityAction a)
        {
            var b = UIKit.Button(parent, label, a, 420, 76, c, 34);
            return b;
        }

        public void ShowMainMenu()
        {
            _pause.SetActive(false);
            _menu.SetActive(true);
            _menu.transform.SetAsLastSibling();
            _hudLayer.SetActive(false);
            _continueBtn.gameObject.SetActive(_g.Save.totalEarned > 0 || SaveSystem.HasSave());
            UIKit.SetLabel(_continueBtn, _g.Save.totalEarned > 0 ? "Continue" : "Play");
            _continueBtn.gameObject.SetActive(true);
            _g.Cam.Orbit = true;
            Time.timeScale = 0f;
        }

        void CloseMenus()
        {
            _menu.SetActive(false);
            _pause.SetActive(false);
            if (_welcome != null) _welcome.SetActive(false);
            _hudLayer.SetActive(true);
            _g.Cam.Orbit = false;
            _g.Cam.Yaw = 32f;
            Time.timeScale = 1f;
            if (_welcomeText != null)
            {
                // offline earnings waiting: show them now that the player is back on the island
                ShowWelcome(_welcomeText);
                _welcomeText = null;
            }
        }

        // ------------------------------------------------------------------ welcome back (offline earnings)

        GameObject _welcome;
        string _welcomeText;

        public void QueueWelcome(double earned, double away, double counted, double efficiency)
        {
            string time(double s) => s >= 3600 ? $"{(int)(s / 3600)}h {(int)(s % 3600 / 60)}m" : $"{(int)(s / 60)}m";
            _welcomeText = $"You were away for <b>{time(away)}</b>.\nYour helpers earned\n<size=150%><color=#9CFF9A><b>{NumberFormatter.Format(earned)}</b></color></size>\n" +
                           $"<size=75%><color=#B8C8D4>Counted {time(counted)} at {efficiency * 100:0}% (Night Shift raises both)</color></size>";
            // shown when the start menu closes (it always opens first when there's a save to continue)
        }

        void ShowWelcome(string text)
        {
            if (_welcome == null)
            {
                _welcome = Overlay("welcome", 0.45f);
                var card = UIKit.Panel(_welcome.transform, new Color(0.1f, 0.14f, 0.2f, 0.96f), "card");
                UIKit.Place(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(640, 440));
                var title = UIKit.Text(card.rectTransform, "Welcome back!", 52, TextAlignmentOptions.Center, UIKit.Accent, "title");
                title.fontStyle = FontStyles.Bold;
                UIKit.Place(title.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -26), new Vector2(600, 70));
                var body = UIKit.Text(card.rectTransform, "", 30, TextAlignmentOptions.Center, Color.white, "body");
                UIKit.Place(body.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(580, 220));
                var ok = UIKit.Button(card.rectTransform, "Collect", () => { _welcome.SetActive(false); Time.timeScale = 1f; Sfx.I?.Play(Sfx.I.Buy, 0.7f, 1.1f); }, 300, 70, new Color(0.3f, 0.75f, 0.35f), 32);
                UIKit.Place((RectTransform)ok.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 30), new Vector2(300, 70));
            }
            _welcome.transform.Find("card/body").GetComponent<TextMeshProUGUI>().text = text;
            _welcome.SetActive(true);
            _welcome.transform.SetAsLastSibling();
            Time.timeScale = 0f;
        }

        void Update()
        {
            if (_g == null) return;
            if (GameInput.Pause.WasPressedThisFrame() && !_menu.activeSelf)
            {
                if (_pause.activeSelf) CloseMenus();
                else
                {
                    _pause.SetActive(true);
                    _pause.transform.SetAsLastSibling();
                    _windowed.SetIsOnWithoutNotify(_g.Settings.windowed);
                    _managers.SetIsOnWithoutNotify(!_g.Save.managersPaused);
                    Time.timeScale = 0f;
                }
            }

            if (GameInput.ToggleManagers.WasPressedThisFrame() && !_menu.activeSelf && !_pause.activeSelf && _g.HasManagers)
                _g.SetManagersPaused(!_g.Save.managersPaused);
            UpdateManagers();
            UpdateInfo();

            // count up/down smoothly, and pop when money comes in
            double target = _g.Cash;
            if (target > _shown + 0.5) _pulse = 1f;
            _shown = Mathf.Abs((float)(target - _shown)) < 1 ? target : _shown + (target - _shown) * (1 - Mathf.Exp(-12f * Time.unscaledDeltaTime));
            _cash.text = NumberFormatter.Format(_shown);
            _pulse = Mathf.MoveTowards(_pulse, 0, Time.unscaledDeltaTime * 4f);
            _cashBox.localScale = Vector3.one * (1f + _pulse * 0.08f);

            for (int i = _toastList.Count - 1; i >= 0; i--)
            {
                var (rt, cg, t) = _toastList[i];
                t += Time.unscaledDeltaTime;
                cg.alpha = t < 0.15f ? t / 0.15f : t > 2.2f ? Mathf.Clamp01(1 - (t - 2.2f) / 0.4f) : 1f;
                if (t > 2.6f) { Destroy(rt.gameObject); _toastList.RemoveAt(i); continue; }
                _toastList[i] = (rt, cg, t);
            }
        }
    }
}

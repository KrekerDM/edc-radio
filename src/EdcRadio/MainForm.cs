using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using static EdcRadio.Text;

namespace EdcRadio
{
    internal sealed class MainForm : Form
    {
        private const int FullRows = 208;
        private const int MiniRows = 64;
        private const int LayeredStyle = 0x80000;
        private const int PassStyle = 0x20;
        private const int TopStyle = 0x8;
        private const int HotkeyMessage = 0x0312;
        private const int DisplayChangeMessage = 0x007E;
        private const long CoverBytes = 8 << 20;
        private const string Home = "https://edc-radio-site.vercel.app/";
        private static readonly Rectangle DialArea = new Rectangle(12, 60, 296, 24);
        private static readonly int[] SleepSteps = { 0, 15, 30, 60 };
        private static readonly string[] Pictures = { ".jpg", ".jpeg", ".png", ".bmp" };
        private static readonly (int Id, Keys Key)[] Hotkeys =
        {
            (1, Keys.Space), (2, Keys.Right), (3, Keys.Left), (4, Keys.Up), (5, Keys.Down), (6, Keys.O), (7, Keys.M), (8, Keys.H),
        };

        private enum Sheet
        {
            None,
            Help,
            Options,
        }

        private sealed class Hit
        {
            public string Id;
            public Rectangle Bounds;
            public Action Click;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IconInfo
        {
            public bool IsIcon;
            public int HotX;
            public int HotY;
            public IntPtr Mask;
            public IntPtr Color;
        }

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr CreateIconIndirect(ref IconInfo info);

        [DllImport("user32.dll")]
        private static extern bool GetIconInfo(IntPtr icon, out IconInfo info);

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr icon);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr handle);

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint key);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hwnd, int id);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);

        private readonly Settings _settings;
        private readonly Art _art;
        private readonly Canvas _canvas;
        private readonly Scene _scene = new Scene();
        private readonly Timer _timer = new Timer { Interval = 40 };
        private readonly Stopwatch _watch = Stopwatch.StartNew();
        private readonly Random _rnd = new Random();
        private readonly List<Hit> _hits = new List<Hit>();
        private readonly Dictionary<string, Bitmap> _covers = new Dictionary<string, Bitmap>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<int, Cursor[]> _cursors = new Dictionary<int, Cursor[]>();
        private readonly Glow _glow = new Glow();
        private readonly Layer _layer = new Layer();
        private readonly ContextMenuStrip _menu = new ContextMenuStrip();
        private readonly NotifyIcon _tray = new NotifyIcon();
        private readonly System.Threading.SynchronizationContext _ui;
        private readonly Discord _discord = new Discord(Discord.ApplicationId);
        private Bitmap _plain;
        private Radio _radio;
        private List<Track> _custom = new List<Track>();
        private int _loadId;
        private bool _loading;
        private bool _loaded;
        private double _loadedAt;
        private int _errors;
        private int _layerFailures;
        private string _presence = "";
        private long _presenceStart;
        private int _scale;
        private Point _mouse = new Point(-100, -100);
        private string _pressed;
        private bool _dragging;
        private Sheet _sheet;
        private int _helpPage;
        private bool _layered = true;
        private bool _scan;
        private float _scanLeft;
        private float _fade = 1f;
        private double _last;
        private double _now;
        private double _ticker;
        private float _noticeLeft;
        private string _notice = "";
        private string _station;
        private int _sleepStep;
        private int _wheel;
        private string _title = "";
        private bool _overlayKey;
        private bool _showKey;
        private bool _topmost;

        public MainForm(Settings settings, Art art)
        {
            _settings = settings;
            _art = art;
            _canvas = new Canvas(art);
            _scene.GameArt = art.FromGame;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.None;
            MaximizeBox = false;
            BackColor = Color.Black;
            Text = "radio.exe";
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Opaque, true);
            try
            {
                Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch (ArgumentException)
            {
            }
            _ui = System.Threading.SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
            _radio = new Radio(new List<Track>(), new List<Track>(), settings.Frequency, settings.Volume);
            LoadLibrary(false);
            Rectangle work = TargetScreen().WorkingArea;
            _scale = PickScale(settings.Scale, work);
            ClientSize = new Size(Canvas.W * _scale, Rows * _scale);
            Location = PickLocation(work);
            _topmost = _settings.Overlay || _settings.TopMost;
            _menu.Opening += (s, e) =>
            {
                FillMenu();
                e.Cancel = false;
            };
            _tray.Icon = Icon;
            _tray.Text = "radio.exe";
            _tray.ContextMenuStrip = _menu;
            _tray.MouseClick += (s, e) =>
            {
                if (e.Button == MouseButtons.Left) ToggleVisible();
            };
            _tray.Visible = _settings.Tray;
            _timer.Tick += (s, e) => Frame();
            _timer.Start();
        }

        private bool Empty => _radio.AlbumCount == 0;

        private bool Mini => _settings.Overlay ? _settings.OverlayMini : _settings.Mini;

        private int Rows => Mini ? MiniRows : FullRows;

        private bool PassClicks => _layered && _settings.Overlay && _settings.OverlayClickThrough && (_overlayKey || _settings.Tray);

        private float Background
        {
            get
            {
                if (!_settings.Overlay) return 1f;
                float b = Math.Max(0, Math.Min(100, _settings.OverlayBackground)) / 100f;
                return PassClicks ? b : Math.Max(1f / 255f, b);
            }
        }

        protected override bool ShowWithoutActivation => _settings != null && _settings.Overlay;

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                if (_layered) cp.ExStyle |= LayeredStyle;
                if (_settings != null && PassClicks) cp.ExStyle |= PassStyle;
                if (_topmost) cp.ExStyle |= TopStyle;
                return cp;
            }
        }

        private void LoadLibrary(bool quiet)
        {
            int id = ++_loadId;
            _loading = true;
            string folder = _settings.Folder;
            List<Track> custom = quiet ? _custom : null;
            System.Threading.SynchronizationContext ui = _ui;
            var worker = new System.Threading.Thread(() =>
            {
                var found = ReadLibrary(folder, custom);
                try
                {
                    ui.Post(_ => Loaded(id, quiet, found), null);
                }
                catch (Exception e) when (e is InvalidOperationException || e is ObjectDisposedException)
                {
                }
            })
            {
                IsBackground = true,
                Name = "library",
            };
            worker.SetApartmentState(System.Threading.ApartmentState.STA);
            worker.Start();
        }

        private static (List<Track> Album, List<Track> Custom) ReadLibrary(string folder, List<Track> custom)
        {
            List<Track> album = Library.Load(Library.Sources(folder));
            if (custom == null)
            {
                string customFolder = Library.GameCustomRadio();
                custom = customFolder != null ? Library.Load(new[] { customFolder }, false) : new List<Track>();
            }
            return (album, custom);
        }

        private void Loaded(int id, bool quiet, (List<Track> Album, List<Track> Custom) found)
        {
            if (IsDisposed || id != _loadId) return;
            _loading = false;
            _loadedAt = _watch.Elapsed.TotalSeconds;
            if (quiet && found.Album.Count == 0) return;
            Reload(found);
        }

        private void Reload((List<Track> Album, List<Track> Custom) found)
        {
            bool first = !_loaded;
            float frequency = _radio.Frequency;
            float volume = _radio.Volume;
            bool paused = _radio.Paused;
            float sleep = _radio.SleepLeft;
            bool tape = first ? _settings.Tape : _radio.Tape;
            string path = first ? _settings.TapePath : _radio.TapeTrack?.Path;
            int index = first ? _settings.TapeTrack : _radio.TapeIndex;
            _radio.Dispose();
            _radio = new Radio(found.Album, found.Custom, frequency, volume, paused);
            _custom = found.Custom;
            _loaded = true;
            _errors = 0;
            if (sleep > 0f) _radio.Sleep(sleep / 60f);
            int at = found.Album.FindIndex(t => string.Equals(t.Path, path, StringComparison.OrdinalIgnoreCase));
            if (at >= 0) index = at;
            _radio.PlaceTape(index);
            if (tape && _radio.AlbumCount > 0) _radio.StartTape(index);
            foreach (Bitmap b in _covers.Values) b?.Dispose();
            _covers.Clear();
            _scan = false;
        }

        private void Frame()
        {
            _now = _watch.Elapsed.TotalSeconds;
            float dt = (float)Math.Min(0.1, _now - _last);
            _last = _now;
            _radio.Tick(dt);
            if (_radio.SleepLeft < 0f) _sleepStep = 0;
            if (_radio.Errors != _errors)
            {
                _errors = _radio.Errors;
                if (_radio.Broken != null)
                {
                    Notice(string.Format(T("cannot play"), Path.GetFileName(_radio.Broken.Path)));
                    _noticeLeft = 4f;
                }
            }
            Publish();
            if (_scan)
            {
                if (_radio.Tape || Empty)
                {
                    _scan = false;
                }
                else
                {
                    _scanLeft -= dt;
                    if (_scanLeft <= 0f)
                    {
                        _radio.Seek(1);
                        _scanLeft = 6f;
                    }
                }
            }
            string station = _radio.Tape ? "tape" : _radio.Tuned?.Id;
            if (station != _station)
            {
                if (station != null) _scene.Jump();
                _station = station;
                _ticker = 0f;
            }
            _ticker += dt;
            if (_noticeLeft > 0f) _noticeLeft -= dt;
            _scene.Update(dt, _radio.Sounding, _radio.Tape ? 20f : _radio.Tuned?.Tempo ?? 14f);
            string title = "radio.exe · " + (_radio.Tape ? T("tape") : Freq(_radio.Frequency) + " " + StationName());
            if (title != _title)
            {
                _title = title;
                Text = title;
            }
            string tip = _radio.Now != null && _radio.Heard > 0f ? title + " · " + _radio.Now.Title : title;
            if (tip.Length > 120) tip = tip.Substring(0, 120);
            if (_tray.Text != tip) _tray.Text = tip;
            bool hover = Bounds.Contains(Cursor.Position);
            float target = _settings.Overlay && !hover ? 0.8f : 1f;
            _fade += (target - _fade) * Math.Min(1f, dt * 6f);
            if (!Visible || WindowState == FormWindowState.Minimized) return;
            Render();
            Present();
        }

        private void Publish()
        {
            Track t = _radio.Now;
            Station s = _radio.Tuned;
            bool on = _settings.Discord && t != null && _radio.Heard > 0f && !_radio.Paused && (_radio.Tape || s != null);
            if (on && !_radio.Ready) return;
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            long start = on ? now - (long)_radio.Position.TotalMilliseconds : 0;
            string where = !on ? ""
                : _radio.Tape ? T("tape") + ", " + Place(t)
                : Freq(s.Frequency) + " FM " + T(s.Id);
            string key = on ? where + "|" + t.Path : "";
            if (key == _presence && Math.Abs(start - _presenceStart) < 3000) return;
            _presence = key;
            _presenceStart = start;
            if (!on)
            {
                _discord.Show(null);
                return;
            }
            if (!_settings.DiscordShown)
            {
                _settings.DiscordShown = true;
                Notice(T("discord on"));
                _noticeLeft = 5f;
                SaveState();
            }
            long length = (long)_radio.Length.TotalMilliseconds;
            string page = EdcRadio.Text.Russian ? Home : Home + "en/";
            string artist = (t.Artist ?? "").Trim(' ', ',');
            _discord.Show(new Presence
            {
                Details = t.Title,
                State = artist == "" ? where : Discord.Fit(artist, Math.Max(8, 125 - where.Length)) + " · " + where,
                Start = start,
                End = length > 0 ? start + length : 0,
                LargeImage = Home + "img/rpc/radio.png",
                LargeText = "EDC Radio " + Program.Version,
                LargeUrl = page,
                SmallImage = Home + (_radio.Tape ? "img/rpc/tape.png" : "img/rpc/tower.png"),
                SmallText = where,
                Button = T("discord button"),
                ButtonUrl = page,
            });
        }

        private void Present()
        {
            int rows = Rows;
            int w = Canvas.W * _scale, h = rows * _scale;
            if (ClientSize.Width != w || ClientSize.Height != h) ClientSize = new Size(w, h);
            if (_layered && IsHandleCreated)
            {
                if (_layer.Ensure(w, h))
                {
                    float dim = _settings.Overlay ? Math.Max(0f, Math.Min(1f, _fade)) : 1f;
                    _glow.Compose(_canvas.Bitmap, _scale, rows, _settings.Glow, Background, dim, _layer.Bits, _layer.Stride);
                    if (_layer.Push(Handle, 255))
                    {
                        _layerFailures = 0;
                        return;
                    }
                }
                if (++_layerFailures < 25) return;
                _layered = false;
                UpdateStyles();
            }
            Invalidate();
        }

        private void Render()
        {
            Canvas c = _canvas;
            c.Clear();
            _hits.Clear();
            if (Mini)
            {
                _sheet = Sheet.None;
                RenderMini();
                return;
            }
            c.Box(0, 0, 39, 24, top: true);
            if (_sheet == Sheet.None)
            {
                Header();
                if (Empty)
                {
                    EmptyState();
                }
                else
                {
                    Dial();
                    NowPlaying();
                    Buttons();
                }
                c.Box(8, 136, 37, 6);
                _scene.Draw(c, Night(), _radio.Tape ? 0f : _radio.Heard);
            }
            Chrome();
            if (_sheet == Sheet.Help) Help();
            else if (_sheet == Sheet.Options) Options();
        }

        private void Chrome()
        {
            Canvas c = _canvas;
            c.Text("radio.exe", 16, -3);
            TitleButtons("-");
            int x = 16;
            x += Button("folder", T("folder"), x, 189, PickFolder) + 16;
            x += Button("options", T("settings"), x, 189, () => Open(Sheet.Options)) + 16;
            Button("help", "[?]", x, 192, () => Open(Sheet.Help), mono: true);
            c.Text(Program.Version, 296, 189, Align.Right);
        }

        private void RenderMini()
        {
            Canvas c = _canvas;
            c.Box(0, 0, 39, 7, top: true);
            c.Text("radio.exe", 16, -3);
            TitleButtons("+");
            Frequency();
            if (!Empty) _hits.Add(new Hit { Id = "mini freq", Bounds = new Rectangle(12, 8, 112, 26), Click = Next });
            bool tape = _radio.Tape;
            c.Text(c.Fit(!Empty ? StationName() : _loaded ? T("no music status") : T("loading"), 280 - 128), 128, 10);
            if (!Empty) c.Text(c.Fit(Status(), 280 - 128), 128, 23);
            MiniTower(288, 14);
            Track t = _radio.Now;
            if (t != null && _radio.Heard > 0f)
            {
                string time = Clock(_radio.Position) + " / " + Clock(_radio.Length);
                int tw = c.MeasureMono(time);
                c.Text(c.Fit(t.Title, 280 - tw), 16, 34);
                c.Mono(time, 304, 37, Align.Right);
                Progress(51);
            }
            else if (Empty)
            {
                if (_loaded)
                {
                    int w1 = Button("pick", T("pick folder"), 24, 34, PickFolder);
                    Button("steam", T("steam"), 24 + w1 + 24, 34, OpenSteam);
                }
            }
            else
            {
                string line = Ticker();
                if (line != null)
                {
                    int w = c.Measure(line);
                    int x = 304 - (int)(_ticker * 28f) % (288 + w + 48);
                    c.G.SetClip(new Rectangle(16, 34, 289, 14));
                    c.Text(line, x, 34, clear: true);
                    c.G.ResetClip();
                }
            }
            if (!tape && !Empty) Static(new Rectangle(16, 34, 289, 20));
        }

        private void TitleButtons(string mini)
        {
            Canvas c = _canvas;
            if (PassClicks)
            {
                string hint = _overlayKey ? "Ctrl+Alt+O" : T("tray hint");
                int w = c.Measure(hint);
                c.Fill(306 - w, 0, w + 6, 8, white: false);
                c.Text(hint, 310, -3, Align.Right);
                return;
            }
            c.Fill(266, 0, 46, 8, white: false);
            Button("mini", mini, 272, 0, ToggleMini, mono: true, marker: false);
            Button("min", "_", 288, 0, () => WindowState = FormWindowState.Minimized, mono: true, marker: false);
            Button("close", "x", 304, 0, Close, mono: true, marker: false);
        }

        private void MiniTower(int x, int y)
        {
            Canvas c = _canvas;
            bool live = !_radio.Tape && _radio.Heard > 0f && !_radio.Paused;
            if (_art.FromGame)
            {
                c.Sprite(_art.Ui, Art.Tower, x, y);
                int phase = live ? (int)(_now * 2.6) % 3 : 0;
                if (phase < 2)
                {
                    c.Fill(x + 3, y + 1, 1, 4, white: false);
                    c.Fill(x + 11, y + 1, 1, 4, white: false);
                }
                if (phase < 1)
                {
                    c.Fill(x + 5, y + 2, 1, 2, white: false);
                    c.Fill(x + 9, y + 2, 1, 2, white: false);
                }
            }
            else
            {
                c.Sprite(Sprites.Pack, Sprites.Radio, x, y + 4);
                if (live && _now % 1.0 < 0.5) c.Sprite(Sprites.Pack, Sprites.Blink, x + 10, y);
            }
        }

        private void Frequency()
        {
            Canvas c = _canvas;
            Track now = _radio.Now;
            if (_radio.Tape)
            {
                c.Mono(now == null ? "--" : Side(now) + "-" + now.Number.ToString("00", CultureInfo.InvariantCulture), 96, 14, Align.Right, 2);
                return;
            }
            c.Mono(Freq(_radio.Frequency), 96, 14, Align.Right, 2);
            c.Mono("FM", 100, 14);
            if (Empty) return;
            Station s = _radio.Tuned;
            float signal = s != null && s.OnAir ? _radio.Signal : 0f;
            int bars = (int)Math.Ceiling(signal * 4f - 0.01f);
            for (int i = 0; i < 4; i++)
            {
                int h = 2 + i * 2;
                if (i < bars) c.Fill(100 + i * 4, 31 - h, 3, h);
                else c.Dot(101 + i * 4, 30);
            }
        }

        private void Header()
        {
            Canvas c = _canvas;
            bool tape = _radio.Tape;
            Frequency();
            if (Empty) return;
            Bitmap cover = Cover();
            int right = tape || cover != null ? 250 : 304;
            string subtitle = Subtitle();
            int statusY = subtitle == "" ? 23 : 36;
            c.Text(c.Fit(StationName(), right - 128), 128, 10);
            if (subtitle != "") c.Text(c.Fit(subtitle, right - 128), 128, 23);
            bool live = !tape && !_radio.Paused && _radio.Heard > 0f && _noticeLeft <= 0f && !_scan;
            int sx = live ? 134 : 128;
            if (live)
            {
                c.Fill(128, statusY + 6, 3, 3);
                if (_now % 1.6 >= 1.1) c.Dot(129, statusY + 7, white: false);
            }
            c.Text(c.Fit(Status(), right - sx), sx, statusY);
            if (tape)
            {
                c.Box(256, 8, 5, 5);
                Cassette(261, 13);
            }
            else if (cover != null)
            {
                c.Box(256, 8, 5, 5);
                c.Sprite(cover, new Rectangle(0, 0, cover.Width, cover.Height), 261, 13);
            }
        }

        private void Cassette(int x, int y)
        {
            Canvas c = _canvas;
            int left = x + 1, top = y + 7, right = x + 36, bottom = y + 30;
            c.HLine(left + 1, right - 1, top);
            c.HLine(left + 1, right - 1, bottom);
            c.VLine(left, top + 1, bottom - 1);
            c.VLine(right, top + 1, bottom - 1);
            c.HLine(left + 4, right - 4, top + 3);
            c.VLine(left + 3, top + 4, top + 7);
            c.VLine(right - 3, top + 4, top + 7);
            c.HLine(left + 8, right - 8, top + 13);
            c.HLine(left + 8, right - 8, top + 17);
            c.HLine(left + 7, right - 7, bottom - 3);
            c.Dot(left + 6, bottom - 2);
            c.Dot(right - 6, bottom - 2);
            double spin = _radio.Sounding ? _now * 4.0 : 0.0;
            foreach (int cx in new[] { left + 9, right - 9 })
            {
                int cy = top + 15;
                for (int i = 0; i < 16; i++)
                {
                    double a = i * Math.PI / 8;
                    c.Dot(cx + (int)Math.Round(Math.Cos(a) * 4.4), cy + (int)Math.Round(Math.Sin(a) * 4.4));
                }
                for (int i = 0; i < 3; i++)
                {
                    double a = spin + i * Math.PI * 2 / 3;
                    c.Dot(cx + (int)Math.Round(Math.Cos(a) * 2), cy + (int)Math.Round(Math.Sin(a) * 2));
                }
                c.Dot(cx, cy);
            }
            if (_radio.Now != null) c.Text(Side(_radio.Now), x + 19, top + 1, Align.Center, clear: true);
        }

        private void Dial()
        {
            Canvas c = _canvas;
            c.Box(8, 56, 37, 3);
            foreach (Station s in _radio.Stations)
            {
                int x = DialX(s.Frequency);
                if (s.OnAir)
                {
                    c.HLine(x - 2, x + 2, 63);
                    c.HLine(x - 1, x + 1, 64);
                    c.Dot(x, 65);
                }
                else
                {
                    c.Dot(x - 2, 63);
                    c.Dot(x, 63);
                    c.Dot(x + 2, 63);
                }
            }
            c.HLine(DialX(88f), DialX(108f), 68);
            for (int m = 88; m <= 108; m++) c.VLine(DialX(m), 69, m % 2 == 0 ? 71 : 69);
            for (int m = 88; m <= 108; m += 4) c.Text(m.ToString(CultureInfo.InvariantCulture), DialX(m), 71, Align.Center, clear: true);
            if (!_radio.Tape && (!_scan || _now % 0.6 < 0.4))
            {
                int n = DialX(_radio.Frequency);
                c.VLine(n, 61, 72);
                c.Fill(n - 1, 61, 3, 2);
            }
        }

        private static int DialX(float f) => 30 + (int)Math.Round((f - 88f) * 13f);

        private void NowPlaying()
        {
            Canvas c = _canvas;
            Track t = _radio.Now;
            if (t != null && _radio.Heard > 0f)
            {
                c.Text(c.Fit(t.Title, 288), 16, 86);
                string time = Clock(_radio.Position) + " / " + Clock(_radio.Length);
                int tw = c.MeasureMono(time);
                c.Text(c.Fit(t.Artist ?? "", 280 - tw), 16, 99);
                c.Mono(time, 304, 102, Align.Right);
                Progress(115);
            }
            else
            {
                string line = Ticker();
                if (line != null)
                {
                    int w = c.Measure(line);
                    int span = 288 + w + 48;
                    int x = 304 - (int)(_ticker * 28f) % span;
                    c.G.SetClip(new Rectangle(16, 86, 289, 30));
                    c.Text(line, x, 93, clear: true);
                    c.G.ResetClip();
                }
            }
            if (!_radio.Tape) Static(new Rectangle(16, 86, 289, 31));
        }

        private void Progress(int y)
        {
            Canvas c = _canvas;
            double length = _radio.Length.TotalSeconds;
            int px = 16 + (length > 0 ? (int)(287 * Math.Min(1.0, _radio.Position.TotalSeconds / length)) : 0);
            c.HLine(16, px, y);
            c.Dotted(px + 2 + (px % 2), 304, y);
            c.Fill(px, y - 1, 2, 3);
        }

        private void Static(Rectangle area)
        {
            if (_radio.Paused) return;
            float noise = 1f - _radio.Heard;
            int dots = (int)(noise * noise * 70f * area.Height / 31f);
            for (int i = 0; i < dots; i++) _canvas.Dot(area.X + _rnd.Next(area.Width), area.Y + _rnd.Next(area.Height));
        }

        private string Ticker()
        {
            Station s = _radio.Tuned;
            if (_radio.Tape || s == null) return null;
            if (s.Id == "news") return T("news line");
            if (s.Id == "custom" && !s.OnAir) return T("custom line");
            if (s.OnAir) return null;
            bool anyAlbum = _radio.Stations.Any(x => x.Id != "custom" && x.OnAir);
            return _radio.AlbumCount > 0 && !anyAlbum ? T("foreign line") : T("missing line");
        }

        private void Buttons()
        {
            Canvas c = _canvas;
            const int y = 121;
            string pause = _radio.Paused ? T("play") : T("pause");
            int pw = Math.Max(c.Measure(T("play")), c.Measure(T("pause")));
            string tape = (_radio.Tape ? "[x] " : "[ ] ") + T("tape");
            string scan = (_scan ? "[x] " : "[ ] ") + T("scan");
            int tw = Math.Max(c.Measure("[x] " + T("tape")), c.Measure("[ ] " + T("tape")));
            int sw = Math.Max(c.Measure("[x] " + T("scan")), c.Measure("[ ] " + T("scan")));
            string sleep = T("sleep") + " [" + (_sleepStep == 0 ? T("off") : SleepSteps[_sleepStep] + " " + T("min")) + "]";
            bool hasTape = _radio.AlbumCount > 0;
            int total = c.MeasureMono("<<") + pw + c.MeasureMono(">>") + sw + c.Measure(sleep) + (hasTape ? tw : 0);
            int gaps = hasTape ? 5 : 4;
            int gap = Math.Max(9, Math.Min(16, (288 - total) / gaps));
            int x = 16;
            x += Button("back", "<<", x, y + 3, Previous, mono: true) + gap;
            Button("pause", pause, x, y, _radio.TogglePause);
            x += pw + gap;
            x += Button("next", ">>", x, y + 3, Next, mono: true) + gap;
            if (hasTape)
            {
                Button("tape", tape, x, y, ToggleTape);
                x += tw + gap;
            }
            Button("scan", scan, x, y, ToggleScan);
            x += sw + gap;
            Button("sleep", sleep, x, y, CycleSleep);
        }

        private void EmptyState()
        {
            Canvas c = _canvas;
            if (!_loaded)
            {
                if (_now > 0.6) c.Text(T("loading"), 160, 80, Align.Center);
                return;
            }
            c.Text(T("no music"), 160, 62, Align.Center);
            c.Text(T("no music 1"), 160, 80, Align.Center);
            c.Text(T("no music 2"), 160, 93, Align.Center);
            c.Text(T("no music 3"), 160, 106, Align.Center);
            int w1 = c.Measure(T("pick folder")), w2 = c.Measure(T("steam"));
            int x = 160 - (w1 + 28 + w2) / 2;
            Button("pick", T("pick folder"), x, 121, PickFolder);
            Button("steam", T("steam"), x + w1 + 28, 121, OpenSteam);
        }

        private void Help()
        {
            Canvas c = _canvas;
            c.Panel(32, 16, 31, 20, top: true);
            c.Text(T("help") + " · " + (_helpPage == 0 ? T("help window") : T("help anywhere")), 48, 13);
            Button("help x", "x", 32 + 30 * 8, 16, () => _sheet = Sheet.None, mono: true, marker: false);
            (string, string)[] rows = _helpPage == 0
                ? new[]
                {
                    ("← →", "help tune"),
                    ("Ctrl + ← →", "help seek"),
                    ("↑ ↓", "help volume"),
                    (T("key space"), "help pause"),
                    ("1–7", "help presets"),
                    ("T", "help tape"),
                    ("A", "help scan"),
                    ("S", "help sleep"),
                    ("G", "help glow"),
                    ("M", "help mini"),
                    ("F1", "help help"),
                }
                : new[]
                {
                    ("Ctrl+Alt + " + T("key space"), "help pause"),
                    ("Ctrl+Alt + ← →", "help seek"),
                    ("Ctrl+Alt + ↑ ↓", "help volume"),
                    ("Ctrl+Alt + O", "help overlay"),
                    ("Ctrl+Alt + M", "help mini"),
                    ("Ctrl+Alt + H", "help show"),
                    (T("help wheel"), "help wheel do"),
                };
            for (int i = 0; i < rows.Length; i++)
            {
                int y = 30 + i * 12;
                int kw = c.Text(rows[i].Item1, 48, y);
                string action = T(rows[i].Item2);
                int aw = c.Text(action, 272, y, Align.Right);
                c.Dotted(48 + kw + 3 + ((48 + kw + 3) % 2), 272 - aw - 4, y + 7);
            }
            c.Text((_helpPage + 1) + "/2", 160, 164, Align.Center);
            if (_helpPage > 0) Button("help prev", "<", 136, 164, () => _helpPage = 0, marker: false);
            if (_helpPage < 1) Button("help next", ">", 180, 164, () => _helpPage = 1, marker: false);
        }

        private void Options()
        {
            Canvas c = _canvas;
            c.Panel(24, 10, 33, 21, top: true);
            c.Text(T("settings"), 40, 7);
            Button("options x", "x", 24 + 32 * 8, 10, () => _sheet = Sheet.None, mono: true, marker: false);
            int y = 24;
            Toggle("options overlay", T("set overlay"), _settings.Overlay, y, ToggleOverlay);
            Choice("options background", T("set background"), _settings.OverlayBackground + " %", y += 14, NextBackground);
            Toggle("options overlay mini", T("set overlay mini"), _settings.OverlayMini, y += 14, () =>
            {
                _settings.OverlayMini = !_settings.OverlayMini;
                if (_settings.Overlay && _settings.OverlayMini) _sheet = Sheet.None;
                ApplyWindow();
            });
            Toggle("options click", T("set click"), _settings.OverlayClickThrough, y += 14, ToggleClicks);
            Toggle("options topmost", T("set topmost"), _settings.TopMost, y += 14, ToggleTopMost);
            Toggle("options mini", T("set mini"), _settings.Mini, y += 14, () =>
            {
                _settings.Mini = !_settings.Mini;
                _sheet = Sheet.None;
                ApplyWindow();
            });
            Toggle("options glow", T("set glow"), _settings.Glow, y += 14, ToggleGlow);
            Toggle("options tray", T("set tray"), _settings.Tray, y += 14, ToggleTray);
            Toggle("options discord", T("set discord"), _settings.Discord, y += 14, ToggleDiscord);
            Choice("options language", T("set language"), T("language name"), y += 14, ToggleLanguage);
            Choice("options scale", T("set scale"), "x" + _scale, y += 14, CycleScale);
        }

        private void Toggle(string id, string name, bool value, int y, Action click)
        {
            Canvas c = _canvas;
            var bounds = new Rectangle(36, y, 220, 13);
            _hits.Add(new Hit { Id = id, Bounds = bounds, Click = click });
            bool hover = bounds.Contains(_mouse);
            c.Text(hover ? ">" + name : name, 180, y, Align.Right);
            c.Mono(value ? "[X]" : "[ ]", 188, y + 3);
        }

        private void Choice(string id, string name, string value, int y, Action click)
        {
            Canvas c = _canvas;
            var bounds = new Rectangle(36, y, 220, 13);
            _hits.Add(new Hit { Id = id, Bounds = bounds, Click = click });
            bool hover = bounds.Contains(_mouse);
            c.Text(hover ? ">" + name : name, 180, y, Align.Right);
            c.Text("< " + value + " >", 188, y);
        }

        private int Button(string id, string label, int x, int y, Action click, bool mono = false, bool marker = true)
        {
            Canvas c = _canvas;
            int w = mono ? c.MeasureMono(label) : c.Measure(label);
            int h = mono ? 8 : 13;
            var bounds = new Rectangle(x - (marker ? 8 : 1), y - (mono ? 2 : 0), w + (marker ? 10 : 2), h + (mono ? 4 : 0));
            _hits.Add(new Hit { Id = id, Bounds = bounds, Click = click });
            bool hover = bounds.Contains(_mouse) && Allowed(id);
            int dy = hover && _pressed == id ? 1 : 0;
            if (hover && marker)
            {
                if (mono) c.Mono(">", x - 8, y + dy);
                else c.Text(">", x - 6, y + dy);
            }
            if (mono) c.Mono(label, x, y + dy);
            else c.Text(label, x, y + dy);
            return w;
        }

        private bool Allowed(string id) =>
            _sheet == Sheet.None
            || id == "close" || id == "min" || id == "mini"
            || (_sheet == Sheet.Help && id.StartsWith("help", StringComparison.Ordinal))
            || (_sheet == Sheet.Options && id.StartsWith("options", StringComparison.Ordinal));

        private Hit HitAt(Point p)
        {
            for (int i = _hits.Count - 1; i >= 0; i--)
            {
                Hit h = _hits[i];
                if (!Allowed(h.Id)) continue;
                if (h.Bounds.Contains(p)) return h;
            }
            return null;
        }

        private string StationName()
        {
            if (_radio.Tape) return T("tape");
            Station s = _radio.Tuned;
            return s == null ? T("between") : T(s.Id);
        }

        private string Subtitle()
        {
            Track t = _radio.Now;
            if (t != null && _radio.Heard > 0f) return t.Artist ?? "";
            return "";
        }

        private string Status()
        {
            if (_noticeLeft > 0f) return _notice;
            string s;
            if (_radio.Paused) s = T("paused");
            else if (_scan) s = T("scanning");
            else if (_radio.Tape) s = _radio.Now == null ? "" : Place(_radio.Now);
            else if (_radio.Heard > 0f) s = T("on air");
            else s = T("static");
            if (_radio.SleepLeft > 0f) s += " · " + string.Format(T("sleep in"), (int)Math.Ceiling(_radio.SleepLeft / 60f));
            return s;
        }

        private Bitmap Cover()
        {
            Track t = _radio.Now;
            if (_radio.Tape || t == null || _radio.Heard <= 0f) return null;
            string dir = Path.GetDirectoryName(t.Path);
            Station s = _radio.Tuned;
            string key = dir + "|" + (s == null ? "" : Freq(s.Frequency));
            if (_covers.TryGetValue(key, out Bitmap hit)) return hit;
            Bitmap result = null;
            string file = CoverFile(dir, s);
            if (file != null)
            {
                try
                {
                    if (new FileInfo(file).Length <= CoverBytes)
                    {
                        using (FileStream stream = File.OpenRead(file))
                        using (Image image = Image.FromStream(stream, false, false))
                        {
                            if ((long)image.Width * image.Height <= 16_000_000) result = Canvas.Dither(image, 38, 38);
                        }
                    }
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException || e is OutOfMemoryException || e is ExternalException)
                {
                    result = null;
                }
            }
            _covers[key] = result;
            return result;
        }

        private static string CoverFile(string dir, Station station)
        {
            string[] files;
            try
            {
                files = Directory.GetFiles(dir).Where(f => Pictures.Contains(Path.GetExtension(f).ToLowerInvariant())).OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToArray();
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                return null;
            }
            if (files.Length == 0) return null;
            string Name(string f) => Path.GetFileNameWithoutExtension(f).ToLowerInvariant();
            string freq = station == null ? null : Freq(station.Frequency);
            return (freq != null ? files.FirstOrDefault(f => Name(f).Contains(freq) && !Name(f).Contains("demo")) : null)
                   ?? files.FirstOrDefault(f => Name(f) == "cover" || Name(f) == "folder" || Name(f) == "front")
                   ?? files[0];
        }

        private static string Place(Track t) =>
            t.Number > 0 ? string.Format(T("side"), Side(t), t.Number) : string.Format(T("side only"), Side(t));

        private static string Side(Track t) => t.Disc == 1 ? "A" : t.Disc == 2 ? "B" : t.Disc.ToString(CultureInfo.InvariantCulture);

        private static string Freq(float f) => f.ToString("0.0", CultureInfo.InvariantCulture);

        private static string Clock(TimeSpan t) =>
            t.TotalHours >= 1
                ? string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}:{2:00}", (int)t.TotalHours, t.Minutes, t.Seconds)
                : string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}", (int)t.TotalMinutes, t.Seconds);

        private static bool Night()
        {
            int h = DateTime.Now.Hour;
            return h >= 18 || h < 7;
        }

        private void Notice(string text)
        {
            _notice = text;
            _noticeLeft = 1.6f;
        }

        private void Open(Sheet sheet)
        {
            if (Mini) LeaveMini();
            _sheet = _sheet == sheet ? Sheet.None : sheet;
            _helpPage = 0;
        }

        private void LeaveMini()
        {
            if (_settings.Overlay) _settings.OverlayMini = false;
            else _settings.Mini = false;
            ApplyWindow();
        }

        private void Step(int direction)
        {
            _scan = false;
            _radio.Tune(_radio.Frequency + direction * 0.1f);
        }

        private void Previous()
        {
            _scan = false;
            if (_radio.Tape) _radio.Skip(-1);
            else _radio.Seek(-1);
        }

        private void Next()
        {
            _scan = false;
            if (_radio.Tape) _radio.Skip(1);
            else _radio.Seek(1);
        }

        private void Preset(int number)
        {
            if (number < 1 || number > _radio.Stations.Count) return;
            _scan = false;
            _radio.Tune(_radio.Stations[number - 1].Frequency);
            _radio.Crackle();
        }

        private void ToggleScan()
        {
            if (Empty || _radio.Tape) return;
            _scan = !_scan;
            _scanLeft = 0.2f;
        }

        private void Louder(int steps)
        {
            _radio.Volume = (float)Math.Round(Math.Max(0f, Math.Min(1f, _radio.Volume + steps * 0.05f)), 2);
            Notice(string.Format(T("volume"), (int)Math.Round(_radio.Volume * 100)));
        }

        private void ToggleTape()
        {
            if (_radio.AlbumCount == 0) return;
            _scan = false;
            _radio.ToggleTape();
        }

        private void CycleSleep()
        {
            _sleepStep = (_sleepStep + 1) % SleepSteps.Length;
            _radio.Sleep(SleepSteps[_sleepStep]);
            Notice(_sleepStep == 0 ? T("sleep off") : string.Format(T("sleep in"), SleepSteps[_sleepStep]));
        }

        private void ToggleGlow()
        {
            _settings.Glow = !_settings.Glow;
            Notice(_settings.Glow ? T("glow on") : T("glow off"));
            SaveState();
        }

        private void ToggleLanguage()
        {
            EdcRadio.Text.Russian = !EdcRadio.Text.Russian;
            _settings.Language = EdcRadio.Text.Russian ? "ru" : "en";
            SaveState();
        }

        private void ToggleOverlay()
        {
            _settings.Overlay = !_settings.Overlay;
            if (_settings.Overlay) _sheet = Sheet.None;
            ApplyWindow();
            Notice(_settings.Overlay ? T("overlay on") : T("overlay off"));
            _noticeLeft = 3f;
        }

        private void ToggleMini()
        {
            if (_settings.Overlay) _settings.OverlayMini = !_settings.OverlayMini;
            else _settings.Mini = !_settings.Mini;
            _sheet = Sheet.None;
            ApplyWindow();
        }

        private void ToggleClicks()
        {
            _settings.OverlayClickThrough = !_settings.OverlayClickThrough;
            ApplyWindow();
        }

        private void ToggleTopMost()
        {
            _settings.TopMost = !_settings.TopMost;
            ApplyWindow();
        }

        private void ToggleDiscord()
        {
            _settings.Discord = !_settings.Discord;
            SaveState();
        }

        private void ToggleTray()
        {
            _settings.Tray = !_settings.Tray;
            _tray.Visible = _settings.Tray;
            ApplyWindow();
        }

        private void NextBackground()
        {
            int i = Array.IndexOf(Settings.Backgrounds, _settings.OverlayBackground);
            _settings.OverlayBackground = Settings.Backgrounds[(i + 1) % Settings.Backgrounds.Length];
            SaveState();
        }

        private void ApplyWindow()
        {
            if (!_layered)
            {
                _layered = true;
                _layerFailures = 0;
            }
            bool top = _settings.Overlay || _settings.TopMost;
            if (top != _topmost)
            {
                _topmost = top;
                if (IsHandleCreated) SetWindowPos(Handle, top ? (IntPtr)(-1) : (IntPtr)(-2), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010);
            }
            if (IsHandleCreated) UpdateStyles();
            SaveState();
            if (WindowState != FormWindowState.Normal) return;
            ClientSize = new Size(Canvas.W * _scale, Rows * _scale);
            KeepOnScreen();
        }

        private void FitScale()
        {
            if (WindowState != FormWindowState.Normal) return;
            int max = Limit(System.Windows.Forms.Screen.FromRectangle(Bounds).WorkingArea);
            if (_scale <= max) return;
            _scale = max;
            ClientSize = new Size(Canvas.W * _scale, Rows * _scale);
            Cursor = Cursors(0);
        }

        private void KeepOnScreen()
        {
            if (WindowState != FormWindowState.Normal) return;
            FitScale();
            Rectangle work = System.Windows.Forms.Screen.FromRectangle(Bounds).WorkingArea;
            int x = Math.Max(work.Left, Math.Min(Left, work.Right - Width));
            int y = Math.Max(work.Top, Math.Min(Top, work.Bottom - Height));
            if (x != Left || y != Top) Location = new Point(x, y);
        }

        private void ToggleVisible()
        {
            if (Visible && WindowState != FormWindowState.Minimized)
            {
                Hide();
                return;
            }
            Wake();
        }

        public void HideTray() => _tray.Visible = false;

        public void Wake()
        {
            Show();
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            if (!_settings.Overlay) Activate();
        }

        private void FillMenu()
        {
            _menu.Items.Clear();
            bool shown = Visible && WindowState != FormWindowState.Minimized;
            _menu.Items.Add(shown ? T("tray hide") : T("tray show"), null, (s, e) => ToggleVisible());
            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add(_radio.Paused ? T("play") : T("pause"), null, (s, e) => _radio.TogglePause());
            _menu.Items.Add(T("tray next"), null, (s, e) => Next());
            if (_radio.AlbumCount > 0) _menu.Items.Add(new ToolStripMenuItem(T("tape"), null, (s, e) => ToggleTape()) { Checked = _radio.Tape });
            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add(new ToolStripMenuItem(T("set overlay"), null, (s, e) => ToggleOverlay()) { Checked = _settings.Overlay });
            _menu.Items.Add(new ToolStripMenuItem(T("set click"), null, (s, e) => ToggleClicks()) { Checked = _settings.OverlayClickThrough });
            _menu.Items.Add(new ToolStripMenuItem(T("set topmost"), null, (s, e) => ToggleTopMost()) { Checked = _settings.TopMost });
            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add(T("tray quit"), null, (s, e) => Close());
        }

        private System.Windows.Forms.Screen TargetScreen()
        {
            if (_settings.X != int.MinValue && _settings.Y != int.MinValue)
                return System.Windows.Forms.Screen.FromPoint(new Point(_settings.X + 8, _settings.Y + 8));
            return System.Windows.Forms.Screen.PrimaryScreen;
        }

        private static int Limit(Rectangle work) => Math.Max(1, Math.Min(work.Width / Canvas.W, work.Height / FullRows));

        private static int PickScale(int requested, Rectangle work)
        {
            int max = Limit(work);
            if (requested >= 1) return Math.Min(requested, max);
            int auto = (int)Math.Min(work.Height * 0.62 / FullRows, work.Width * 0.6 / Canvas.W);
            return Math.Max(1, Math.Min(max, Math.Min(5, Math.Max(2, auto))));
        }

        private void CycleScale()
        {
            int max = Limit(System.Windows.Forms.Screen.FromRectangle(Bounds).WorkingArea);
            int next = _scale + 1 > max ? Math.Min(2, max) : _scale + 1;
            if (next == _scale) return;
            Point center = new Point(Left + Width / 2, Top + Height / 2);
            _scale = next;
            _settings.Scale = next;
            ClientSize = new Size(Canvas.W * _scale, Rows * _scale);
            Location = new Point(center.X - Width / 2, center.Y - Height / 2);
            KeepOnScreen();
            Cursor = Cursors(0);
            SaveState();
        }

        private Point PickLocation(Rectangle work)
        {
            var size = new Size(Canvas.W * _scale, Rows * _scale);
            if (_settings.X != int.MinValue && _settings.Y != int.MinValue)
            {
                var rect = new Rectangle(new Point(_settings.X, _settings.Y), size);
                if (System.Windows.Forms.Screen.AllScreens.Any(s =>
                    {
                        Rectangle r = Rectangle.Intersect(s.WorkingArea, rect);
                        return r.Width >= 160 && r.Height >= 40;
                    }))
                    return rect.Location;
            }
            return new Point(work.Left + (work.Width - size.Width) / 2, work.Top + (work.Height - size.Height) / 2);
        }

        private void PickFolder()
        {
            using (var dialog = new FolderBrowserDialog
            {
                Description = T("folder dialog"),
                UseDescriptionForTitle = true,
                ShowNewFolderButton = false,
            })
            {
                if (Directory.Exists(_settings.Folder)) dialog.SelectedPath = _settings.Folder;
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                _settings.Folder = dialog.SelectedPath;
                _settings.Save();
                LoadLibrary(false);
            }
        }

        private static void OpenSteam()
        {
            string url = Library.SteamRoot() != null ? "steam://store/" + Library.SoundtrackAppId : Library.StoreUrl;
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception e) when (e is System.ComponentModel.Win32Exception || e is InvalidOperationException)
            {
            }
        }

        private Cursor Cursors(int kind)
        {
            if (!_art.FromGame)
                return kind == 1 ? System.Windows.Forms.Cursors.Hand : kind >= 2 ? System.Windows.Forms.Cursors.SizeWE : System.Windows.Forms.Cursors.Default;
            if (!_cursors.TryGetValue(_scale, out Cursor[] set))
            {
                set = new[]
                {
                    MakeCursor(Art.Arrow, new Point(6, 6)),
                    MakeCursor(Art.Pointer, new Point(8, 7)),
                    MakeCursor(Art.HandOpen, new Point(8, 9)),
                    MakeCursor(Art.HandClosed, new Point(8, 9)),
                };
                _cursors[_scale] = set;
            }
            return set[kind] ?? System.Windows.Forms.Cursors.Default;
        }

        private Cursor MakeCursor(Rectangle source, Point hot)
        {
            int size = 16 * _scale;
            using (var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.Transparent);
                    g.InterpolationMode = InterpolationMode.NearestNeighbor;
                    g.PixelOffsetMode = PixelOffsetMode.Half;
                    g.DrawImage(_art.Ui, new Rectangle(0, 0, size, size), source, GraphicsUnit.Pixel);
                }
                IntPtr icon = bmp.GetHicon();
                if (!GetIconInfo(icon, out IconInfo info))
                {
                    DestroyIcon(icon);
                    return null;
                }
                info.IsIcon = false;
                info.HotX = hot.X * _scale;
                info.HotY = hot.Y * _scale;
                IntPtr cursor = CreateIconIndirect(ref info);
                DeleteObject(info.Mask);
                DeleteObject(info.Color);
                DestroyIcon(icon);
                return cursor == IntPtr.Zero ? null : new Cursor(cursor);
            }
        }

        private Point ToCanvas(Point p) => new Point(p.X / _scale, p.Y / _scale);

        private void TuneAt(int x)
        {
            _scan = false;
            _radio.Tune(88f + (x - 30) / 13f);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            var busy = new List<string>();
            foreach ((int id, Keys key) in Hotkeys)
            {
                bool ok = RegisterHotKey(Handle, id, 0x4003, (uint)key);
                if (id == 6) _overlayKey = ok;
                if (id == 8) _showKey = ok;
                if (!ok) busy.Add(key == Keys.Space ? T("key space") : key.ToString());
            }
            if (busy.Count > 0)
            {
                Notice(string.Format(T("hotkeys busy"), string.Join(", ", busy)));
                _noticeLeft = 6f;
            }
            UpdateStyles();
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            foreach ((int id, Keys _) in Hotkeys) UnregisterHotKey(Handle, id);
            base.OnHandleDestroyed(e);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == HotkeyMessage)
            {
                switch ((int)m.WParam)
                {
                    case 1:
                        _radio.TogglePause();
                        break;
                    case 2:
                        Next();
                        break;
                    case 3:
                        Previous();
                        break;
                    case 4:
                        Louder(1);
                        break;
                    case 5:
                        Louder(-1);
                        break;
                    case 6:
                        ToggleOverlay();
                        break;
                    case 7:
                        ToggleMini();
                        break;
                    case 8:
                        ToggleVisible();
                        break;
                }
                return;
            }
            base.WndProc(ref m);
            if (m.Msg == DisplayChangeMessage && IsHandleCreated) BeginInvoke(new Action(KeepOnScreen));
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Cursor = Cursors(0);
            Frame();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (WindowState == FormWindowState.Normal && IsHandleCreated) KeepOnScreen();
        }

        protected override void OnResizeEnd(EventArgs e)
        {
            base.OnResizeEnd(e);
            FitScale();
        }

        protected override void OnDpiChanged(DpiChangedEventArgs e)
        {
            e.Cancel = true;
            base.OnDpiChanged(e);
        }

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            if (!_loaded || !Empty || _loading || _watch.Elapsed.TotalSeconds - _loadedAt < 30.0) return;
            LoadLibrary(true);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            switch (keyData)
            {
                case Keys.Left:
                    Step(-1);
                    return true;
                case Keys.Right:
                    Step(1);
                    return true;
                case Keys.Control | Keys.Left:
                case Keys.MediaPreviousTrack:
                    Previous();
                    return true;
                case Keys.Control | Keys.Right:
                case Keys.MediaNextTrack:
                    Next();
                    return true;
                case Keys.Up:
                    Louder(1);
                    return true;
                case Keys.Down:
                    Louder(-1);
                    return true;
                case Keys.Space:
                case Keys.MediaPlayPause:
                    _radio.TogglePause();
                    return true;
                case Keys.MediaStop:
                    if (!_radio.Paused) _radio.TogglePause();
                    return true;
                case Keys.D1:
                case Keys.D2:
                case Keys.D3:
                case Keys.D4:
                case Keys.D5:
                case Keys.D6:
                case Keys.D7:
                    Preset(keyData - Keys.D0);
                    return true;
                case Keys.NumPad1:
                case Keys.NumPad2:
                case Keys.NumPad3:
                case Keys.NumPad4:
                case Keys.NumPad5:
                case Keys.NumPad6:
                case Keys.NumPad7:
                    Preset(keyData - Keys.NumPad0);
                    return true;
                case Keys.T:
                    ToggleTape();
                    return true;
                case Keys.A:
                    ToggleScan();
                    return true;
                case Keys.S:
                    CycleSleep();
                    return true;
                case Keys.G:
                    ToggleGlow();
                    return true;
                case Keys.M:
                    ToggleMini();
                    return true;
                case Keys.F1:
                    if (Mini) LeaveMini();
                    if (_sheet != Sheet.Help)
                    {
                        _sheet = Sheet.Help;
                        _helpPage = 0;
                    }
                    else if (_helpPage == 0)
                    {
                        _helpPage = 1;
                    }
                    else
                    {
                        _sheet = Sheet.None;
                    }
                    return true;
                case Keys.Escape:
                    if (_sheet == Sheet.None) break;
                    _sheet = Sheet.None;
                    return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            _mouse = ToCanvas(e.Location);
            Hit hit = HitAt(_mouse);
            if (hit != null)
            {
                _pressed = hit.Id;
                return;
            }
            if (_sheet != Sheet.None)
            {
                _sheet = Sheet.None;
                return;
            }
            if (!Mini && _scene.Cat.Contains(_mouse))
            {
                _scene.Pet();
                return;
            }
            if (!Mini && !Empty && DialArea.Contains(_mouse))
            {
                _dragging = true;
                Capture = true;
                TuneAt(_mouse.X);
                Cursor = Cursors(3);
                return;
            }
            ReleaseCapture();
            SendMessage(Handle, 0xA1, (IntPtr)2, IntPtr.Zero);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _mouse = ToCanvas(e.Location);
            if (_dragging)
            {
                _dragging = false;
                Capture = false;
            }
            string pressed = _pressed;
            _pressed = null;
            if (pressed == null) return;
            Hit hit = HitAt(_mouse);
            if (hit != null && hit.Id == pressed) hit.Click();
        }

        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            base.OnMouseCaptureChanged(e);
            _dragging = false;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            _mouse = ToCanvas(e.Location);
            if (_dragging && (e.Button & MouseButtons.Left) == 0) _dragging = false;
            if (_dragging) TuneAt(_mouse.X);
            int kind = _dragging ? 3
                : HitAt(_mouse) != null || (!Mini && _sheet == Sheet.None && _scene.Cat.Contains(_mouse)) ? 1
                : !Mini && !Empty && _sheet == Sheet.None && DialArea.Contains(_mouse) ? 2
                : 0;
            Cursor wanted = Cursors(kind);
            if (Cursor != wanted) Cursor = wanted;
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _mouse = new Point(-100, -100);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            _wheel += e.Delta;
            int notches = _wheel / 120;
            if (notches == 0) return;
            _wheel -= notches * 120;
            if (_sheet != Sheet.None) return;
            bool tune = Mini ? _mouse.X < 124 : DialArea.Contains(_mouse);
            if (!Empty && tune)
            {
                _scan = false;
                _radio.Tune(_radio.Frequency + notches * 0.1f);
            }
            else
            {
                Louder(notches);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (_layered) return;
            int rows = Rows, w = Canvas.W * _scale, h = rows * _scale;
            if (_plain == null || _plain.Width != w || _plain.Height != h)
            {
                _plain?.Dispose();
                _plain = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
            }
            BitmapData lk = _plain.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb);
            _glow.Compose(_canvas.Bitmap, _scale, rows, _settings.Glow, 1f, 1f, lk.Scan0, lk.Stride);
            _plain.UnlockBits(lk);
            Graphics g = e.Graphics;
            g.CompositingMode = CompositingMode.SourceCopy;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.DrawImage(_plain, new Rectangle(0, 0, w, h), 0, 0, w, h, GraphicsUnit.Pixel);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _timer.Stop();
            SaveState();
            base.OnFormClosed(e);
        }

        public void SaveState()
        {
            Point place = WindowState == FormWindowState.Normal ? Location : RestoreBounds.Location;
            _settings.X = place.X;
            _settings.Y = place.Y;
            _settings.Frequency = _radio.Frequency;
            _settings.Volume = _radio.Volume;
            if (_loaded && _radio.AlbumCount > 0)
            {
                _settings.Tape = _radio.Tape;
                _settings.TapeTrack = _radio.TapeIndex;
                _settings.TapePath = _radio.TapeTrack?.Path ?? "";
            }
            _settings.Save();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _timer.Dispose();
                _tray.Visible = false;
                _tray.Dispose();
                _menu.Dispose();
                _radio?.Dispose();
                _discord.Dispose();
                _canvas.Dispose();
                _layer.Dispose();
                _plain?.Dispose();
                foreach (Bitmap b in _covers.Values) b?.Dispose();
                foreach (Cursor[] set in _cursors.Values)
                    foreach (Cursor cursor in set)
                        cursor?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}

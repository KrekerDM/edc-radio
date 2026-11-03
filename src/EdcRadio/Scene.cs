using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace EdcRadio
{
    internal sealed class Scene
    {
        public const int X = 13;
        public const int Y = 141;
        public const int W = 294;
        public const int H = 46;
        private const int Ground = Y + 37;
        private const int TowerX = X + W - 40;
        private const int CatX = TowerX - 24;
        private const int TruckY = Ground - 14;
        private const int MoonX = X + 22;
        private const int MoonY = Y + 4;
        private static readonly float[] Weathers = { 0.25f, 0.6f, 1f, 1f, 1.5f };

        private sealed class Flake
        {
            public float X;
            public float Y;
            public float Fall;
            public float Phase;
            public bool Near;
        }

        private sealed class Mote
        {
            public float X;
            public float Y;
            public float Age;
            public float Life;
            public int Kind;
        }

        private sealed class Prop
        {
            public Rectangle Sprite;
            public int X;
            public int Y;
        }

        private readonly Random _rnd = new Random(11);
        private readonly List<Flake> _flakes = new List<Flake>();
        private readonly List<Mote> _notes = new List<Mote>();
        private readonly List<Mote> _puffs = new List<Mote>();
        private readonly List<Mote> _smoke = new List<Mote>();
        private readonly List<Prop> _props = new List<Prop>();
        private readonly Point[] _stars;
        private float _time;
        private float _truck = X + 40;
        private float _speed;
        private float _towerNote;
        private float _cabNote = 0.6f;
        private float _zz;
        private float _heart = 6f;
        private float _puff;
        private float _quiet = 10f;
        private float _jump = -1f;
        private float _beat;
        private float _snow = 1f;
        private float _snowTarget = 1f;
        private float _weatherLeft = 60f;

        public Scene()
        {
            for (int i = 0; i < 72; i++) _flakes.Add(NewFlake(Y + (float)_rnd.NextDouble() * H));
            var stars = new List<Point>();
            while (stars.Count < 16)
            {
                var p = new Point(X + 4 + _rnd.Next(W - 8), Y + 2 + _rnd.Next(18));
                if (p.X >= MoonX - 4 && p.X <= MoonX + 13 && p.Y <= MoonY + 12) continue;
                stars.Add(p);
            }
            _stars = stars.ToArray();
            for (int x = X + 3; x < CatX - 14; x += 9 + _rnd.Next(14))
                _props.Add(new Prop { Sprite = Sprites.PineS, X = x, Y = Ground - 8 - _rnd.Next(2) });
            for (int x = X + 10 + _rnd.Next(10); x < CatX - 16; x += 26 + _rnd.Next(30))
                _props.Add(new Prop { Sprite = Sprites.PineL, X = x, Y = Ground - 12 });
            _props.Add(new Prop { Sprite = Sprites.PineL, X = TowerX + 22, Y = Ground - 12 });
            _props.Add(new Prop { Sprite = Sprites.PineS, X = TowerX + 31, Y = Ground - 8 });
            for (int x = X + 6 + _rnd.Next(20); x < X + W - 10; x += 40 + _rnd.Next(40))
                _props.Add(new Prop { Sprite = Sprites.Drift, X = x, Y = Ground - 1 });
        }

        public bool GameArt { get; set; }

        public Rectangle Cat => new Rectangle(CatX, Ground - 15, 16, 16);

        public bool Sleeping => _quiet > 2f && _jump < 0f;

        public void Jump()
        {
            if (_jump < 0f) _jump = 0f;
        }

        public void Pet()
        {
            _jump = 0f;
            _quiet = Math.Min(_quiet, 0.5f);
            _puffs.Add(new Mote { X = CatX + 5, Y = Ground - 20, Life = 1.6f, Kind = 1 });
        }

        public void Update(float dt, bool music, float tempo)
        {
            _time += dt;
            _quiet = music ? 0f : _quiet + dt;
            float target = music ? tempo : 0f;
            _speed += (target - _speed) * Math.Min(1f, dt * 1.3f);
            if (!music && _speed < 0.05f) _speed = 0f;
            _truck += _speed * dt;
            if (_truck > X + W + 2) _truck = X - 18;
            _beat += dt * (music ? Math.Max(1.4f, Math.Min(2.6f, tempo / 9f)) : 0f);
            if (_jump >= 0f)
            {
                _jump += dt;
                if (_jump > 0.75f) _jump = -1f;
            }
            _weatherLeft -= dt;
            if (_weatherLeft <= 0f)
            {
                _snowTarget = Weathers[_rnd.Next(Weathers.Length)];
                _weatherLeft = 40f + (float)_rnd.NextDouble() * 80f;
            }
            _snow += (_snowTarget - _snow) * Math.Min(1f, dt * 0.1f);
            float wind = _snow > 1.2f ? 6f + (_snow - 1.2f) * 20f : 2f;
            foreach (Flake f in _flakes)
            {
                f.Y += f.Fall * (0.7f + 0.3f * _snow) * dt;
                f.X += (float)Math.Sin(_time * 0.9f + f.Phase) * (f.Near ? 5f : 3f) * dt - wind * dt;
                if (f.Y >= Y + H) Reset(f, Y - 2);
                if (f.X < X - 3) f.X += W + 6;
            }
            if (music)
            {
                _towerNote -= dt;
                if (_towerNote <= 0f)
                {
                    int top = GameArt ? Ground - 20 : Ground - 34;
                    _notes.Add(new Mote { X = TowerX + 8, Y = top, Life = 2.6f, Kind = _rnd.Next(3) });
                    _towerNote = 0.9f + (float)_rnd.NextDouble() * 0.8f;
                }
                _cabNote -= dt;
                if (_cabNote <= 0f)
                {
                    if (_truck > X && _truck < X + W - 16)
                        _notes.Add(new Mote { X = _truck + 8, Y = TruckY - 5, Life = 2f, Kind = _rnd.Next(3) });
                    _cabNote = 1.3f + (float)_rnd.NextDouble() * 1.2f;
                }
                _heart -= dt;
                if (_heart <= 0f)
                {
                    _puffs.Add(new Mote { X = CatX + 5, Y = Ground - 20, Life = 1.6f, Kind = 1 });
                    _heart = 9f + (float)_rnd.NextDouble() * 8f;
                }
            }
            if (Sleeping)
            {
                _zz -= dt;
                if (_zz <= 0f)
                {
                    _puffs.Add(new Mote { X = CatX + 11, Y = Ground - 12, Life = 2.2f, Kind = 0 });
                    _zz = 1.5f;
                }
            }
            if (_speed > 2f)
            {
                _puff -= dt;
                if (_puff <= 0f)
                {
                    _smoke.Add(new Mote { X = _truck - 1, Y = TruckY + 11, Life = 0.7f });
                    _puff = 0.2f;
                }
            }
            foreach (Mote n in _notes)
            {
                n.Age += dt;
                n.Y -= 8f * dt;
                n.X += ((float)Math.Sin(n.Age * 2.4f + n.Kind) * 4f - 3f) * dt;
            }
            foreach (Mote p in _puffs)
            {
                p.Age += dt;
                p.Y -= 6f * dt;
                p.X += (float)Math.Sin(p.Age * 3f) * 3f * dt;
            }
            foreach (Mote s in _smoke)
            {
                s.Age += dt;
                s.X -= 5f * dt;
                s.Y -= 3f * dt;
            }
            _notes.RemoveAll(n => n.Age > n.Life);
            _puffs.RemoveAll(p => p.Age > p.Life);
            _smoke.RemoveAll(s => s.Age > s.Life);
        }

        private Flake NewFlake(float y)
        {
            var f = new Flake();
            Reset(f, y);
            return f;
        }

        private void Reset(Flake f, float y)
        {
            f.Near = _rnd.NextDouble() < 0.18;
            f.Fall = f.Near ? 11f + (float)_rnd.NextDouble() * 5f : 5f + (float)_rnd.NextDouble() * 4f;
            f.Phase = (float)_rnd.NextDouble() * 6.28f;
            f.Y = y;
            f.X = X + (float)_rnd.NextDouble() * W;
        }

        public void Draw(Canvas s, bool night, float broadcast)
        {
            s.G.SetClip(new Rectangle(X, Y, W, H));
            if (night)
            {
                for (int i = 0; i < _stars.Length; i++)
                    if (Math.Sin(_time * 1.7 + i * 2.3) > -0.6) s.Dot(_stars[i].X, _stars[i].Y);
                s.Sprite(Sprites.Pack, Sprites.Moon, MoonX, MoonY);
            }
            foreach (Prop p in _props) s.Sprite(Sprites.Pack, p.Sprite, p.X, p.Y);
            s.HLine(X, X + W, Ground + 1);
            for (int row = 0; row < 3; row++)
                for (int x = X + row * 3; x < X + W; x += 5 + row * 2)
                    s.Dot(x, Ground + 3 + row * 2);
            DrawTower(s, broadcast);
            DrawCat(s);
            foreach (Mote m in _smoke)
                if (m.Age < m.Life * 0.8f) s.Dot((int)m.X, (int)m.Y);
            DrawTruck(s, night);
            foreach (Mote n in _notes)
            {
                if (n.Age > n.Life - 0.4f && (int)(n.Age * 10) % 2 == 0) continue;
                if (s.Art.Notes != null) s.Sprite(s.Art.Notes, new Rectangle(n.Kind * 8, 0, 8, 8), (int)n.X, (int)n.Y);
                else s.Sprite(Sprites.Pack, n.Kind == 1 ? Sprites.NoteB : Sprites.NoteA, (int)n.X, (int)n.Y);
            }
            foreach (Mote p in _puffs)
            {
                if (p.Age > p.Life - 0.4f && (int)(p.Age * 10) % 2 == 0) continue;
                s.Sprite(Sprites.Pack, p.Kind == 1 ? Sprites.Heart : Sprites.Zz, (int)p.X, (int)p.Y);
            }
            int active = (int)Math.Round(_flakes.Count * Math.Min(1f, _snow / 1.5f));
            for (int i = 0; i < active; i++)
            {
                Flake f = _flakes[i];
                if (f.Near) s.Sprite(Sprites.Pack, Sprites.Flake, (int)f.X - 1, (int)f.Y - 1);
                else s.Dot((int)f.X, (int)f.Y);
            }
            s.G.ResetClip();
        }

        private void DrawTruck(Canvas s, bool night)
        {
            int x = (int)Math.Round(_truck);
            bool moving = _speed > 1f;
            int bump = moving && (int)(_truck / 9f) % 5 == 0 ? -1 : 0;
            if (s.Art.FromGame)
            {
                if (night) Headlight(s, x + 15, TruckY + 8 + bump);
                s.Sprite(s.Art.Ui, Art.Truck, x, TruckY + bump);
            }
            else
            {
                int y = Ground - 12 + bump;
                if (night) Headlight(s, x + 21, y + 7);
                bool frame = moving && (int)(_truck / 4f) % 2 == 0;
                s.Sprite(Sprites.Pack, frame ? Sprites.TruckB : Sprites.TruckA, x, y);
            }
        }

        private static void Headlight(Canvas s, int x, int y)
        {
            s.HLine(x, x + 2, y);
            int fall = Math.Max(1, Ground - y);
            for (int i = 4; i <= 24; i += 2) s.Dot(x + i, y + fall * i / 24);
            s.HLine(x + 3, x + 34, Ground + 2);
            s.HLine(x + 7, x + 26, Ground + 3);
            s.HLine(x + 12, x + 19, Ground + 4);
        }

        private void DrawTower(Canvas s, float broadcast)
        {
            if (s.Art.FromGame)
            {
                int x = TowerX, y = Ground - 14;
                s.Sprite(s.Art.Ui, Art.Tower, x, y);
                int phase = broadcast > 0f ? (int)(_time * 2.6f) % 3 : 0;
                if (phase < 2)
                {
                    s.Fill(x + 3, y + 1, 1, 4, white: false);
                    s.Fill(x + 11, y + 1, 1, 4, white: false);
                }
                if (phase < 1)
                {
                    s.Fill(x + 5, y + 2, 1, 2, white: false);
                    s.Fill(x + 9, y + 2, 1, 2, white: false);
                }
            }
            else
            {
                int x = TowerX + 2, y = Ground - 26;
                s.Sprite(Sprites.Pack, Sprites.Tower, x, y);
                if (broadcast > 0f && (int)(_time * 2f) % 2 == 0) s.Sprite(Sprites.Pack, Sprites.Blink, x + 4, y - 2);
            }
        }

        private void DrawCat(Canvas s)
        {
            if (s.Art.Cats == null)
            {
                int hop = _jump >= 0f ? (int)Math.Round(Math.Sin(Math.Min(1f, _jump / 0.6f) * Math.PI) * 7) : 0;
                if (Sleeping) s.Sprite(Sprites.Pack, Sprites.KitSleep, CatX + 3, Ground - 7);
                else s.Sprite(Sprites.Pack, (int)_beat % 2 == 0 ? Sprites.CatA : Sprites.CatB, CatX + 2, Ground - 9 - hop);
                return;
            }
            int frame, lift = 0;
            if (_jump >= 0f)
            {
                frame = 0;
                if (_jump < 0.12f || (_jump > 0.55f && _jump < 0.68f)) frame = 2;
                else if (_jump <= 0.55f)
                {
                    float t = (_jump - 0.12f) / 0.43f;
                    frame = t < 0.5f ? 3 : 4;
                    lift = (int)Math.Round(Math.Sin(t * Math.PI) * 9);
                }
            }
            else if (Sleeping)
            {
                frame = 2;
            }
            else
            {
                frame = (int)_beat % 2 == 0 ? 0 : 1;
            }
            s.Sprite(s.Art.Cats, new Rectangle(frame * 16, 0, 16, 16), CatX, Ground - 15 - lift);
        }
    }
}

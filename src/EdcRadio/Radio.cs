using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Media;

namespace EdcRadio
{
    internal sealed class Radio : IDisposable
    {
        private readonly MediaPlayer _music = new MediaPlayer();
        private readonly MediaPlayer _noise = new MediaPlayer();
        private readonly List<Track> _album;
        private Station _onAir;
        private int _liveIndex;
        private TimeSpan? _seek;
        private int _tapeIndex;
        private float _sleepLeft = -1f;
        private float _clock;
        private float _retryAt;
        private float _crackle;
        private int _failures;

        public List<Station> Stations { get; }
        public float Frequency { get; private set; }
        public float Volume { get; set; }
        public bool Paused { get; private set; }
        public bool Tape { get; private set; }
        public Track Now { get; private set; }
        public int AlbumCount => _album.Count;
        public int TapeIndex => _tapeIndex;
        public float SleepLeft => _sleepLeft;
        public TimeSpan Position => Now == null ? TimeSpan.Zero : _music.Position;

        public TimeSpan Length => Now == null ? TimeSpan.Zero
            : Now.Length > TimeSpan.Zero ? Now.Length
            : _music.NaturalDuration.HasTimeSpan ? _music.NaturalDuration.TimeSpan : TimeSpan.Zero;

        public Station Tuned
        {
            get
            {
                Station s = EdcRadio.Stations.Nearest(Stations, Frequency);
                return EdcRadio.Stations.Signal(s, Frequency) > 0f ? s : null;
            }
        }

        public float Signal => Tape ? 1f : EdcRadio.Stations.Signal(Tuned, Frequency);

        public float Heard => Tape ? (Now != null ? 1f : 0f) : Tuned != null && Tuned.OnAir && Now != null ? Signal : 0f;

        public bool Sounding => !Paused && Heard > 0f;

        public Radio(List<Track> album, List<Track> custom, float frequency, float volume, bool paused = false)
        {
            _album = album;
            Stations = EdcRadio.Stations.Build(album, custom);
            Frequency = Clamp(frequency);
            Volume = Math.Max(0f, Math.Min(1f, volume));
            Paused = paused;
            _music.MediaOpened += (s, e) =>
            {
                _failures = 0;
                if (_seek.HasValue) _music.Position = _seek.Value;
                _seek = null;
            };
            _music.MediaEnded += (s, e) => Next();
            _music.MediaFailed += (s, e) => Failed();
            _noise.MediaEnded += (s, e) =>
            {
                _noise.Position = TimeSpan.Zero;
                _noise.Play();
            };
            string noise = NoiseFile();
            if (noise != null)
            {
                _noise.Volume = 0;
                _noise.Open(new Uri(noise));
                _noise.Play();
            }
        }

        public void Tune(float frequency)
        {
            if (Tape) LeaveTape();
            Frequency = Clamp(frequency);
        }

        public void Crackle() => _crackle = 0.35f;

        public void StartTape(int index)
        {
            if (_album.Count == 0) return;
            PlaceTape(index);
            Tape = true;
            _onAir = null;
            PlayTape();
        }

        public void PlaceTape(int index)
        {
            if (_album.Count > 0) _tapeIndex = (index % _album.Count + _album.Count) % _album.Count;
        }

        public void Seek(int direction)
        {
            List<Station> all = Stations.OrderBy(s => s.Frequency).ToList();
            Station target = direction > 0
                ? all.FirstOrDefault(s => s.Frequency > Frequency + 0.05f) ?? all[0]
                : all.LastOrDefault(s => s.Frequency < Frequency - 0.05f) ?? all[all.Count - 1];
            Tune(target.Frequency);
            Crackle();
        }

        public void TogglePause()
        {
            Paused = !Paused;
            if (Paused)
            {
                _music.Pause();
                return;
            }
            if (!Tape) _onAir = null;
            else if (Now != null) _music.Play();
            else PlayTape();
        }

        public void ToggleTape()
        {
            if (Tape)
            {
                LeaveTape();
                return;
            }
            if (_album.Count == 0) return;
            Tape = true;
            _onAir = null;
            PlayTape();
        }

        public void Skip(int direction)
        {
            if (!Tape || _album.Count == 0) return;
            _tapeIndex = ((_tapeIndex + direction) % _album.Count + _album.Count) % _album.Count;
            PlayTape();
        }

        public void Sleep(float minutes) => _sleepLeft = minutes <= 0 ? -1f : minutes * 60f;

        public void Tick(float dt)
        {
            _clock += dt;
            if (_sleepLeft > 0f)
            {
                _sleepLeft -= dt;
                if (_sleepLeft <= 0f)
                {
                    _sleepLeft = -1f;
                    if (!Paused) TogglePause();
                }
            }
            if (!Tape)
            {
                Station s = Tuned;
                if (s != null && s.OnAir)
                {
                    if (_onAir != s && !Paused && _clock >= _retryAt)
                    {
                        GoLive(s);
                    }
                    else if (_onAir != s && Now != null)
                    {
                        _music.Stop();
                        _onAir = null;
                        Now = null;
                    }
                }
                else if (_onAir != null || Now != null)
                {
                    _music.Stop();
                    _onAir = null;
                    Now = null;
                }
            }
            float fade = _sleepLeft > 0f && _sleepLeft < 10f ? _sleepLeft / 10f : 1f;
            float heard = Heard;
            float wobble = 1f - 0.35f * (1f - heard) * (0.5f + 0.5f * (float)Math.Sin(_clock * 3.1f));
            float level = Volume * Volume * fade;
            float crackle = 0f;
            if (_crackle > 0f)
            {
                _crackle -= dt;
                crackle = Math.Max(0f, _crackle / 0.35f);
            }
            _music.Volume = Paused ? 0 : level * heard * wobble * (1f - crackle);
            _noise.Volume = Paused || Tape ? 0 : level * Math.Min(1f, 0.35f * (1f - heard) + 0.6f * crackle);
        }

        private void LeaveTape()
        {
            Tape = false;
            _music.Stop();
            Now = null;
            _onAir = null;
        }

        private void PlayTape() => Play(_album[_tapeIndex % _album.Count], TimeSpan.Zero);

        private void GoLive(Station s)
        {
            _onAir = s;
            var (track, offset, index) = EdcRadio.Stations.LiveAt(s, DateTime.UtcNow);
            _liveIndex = index;
            Play(track, offset);
        }

        private void Next()
        {
            if (Tape)
            {
                Skip(1);
                return;
            }
            if (_onAir == null) return;
            var (track, offset, index) = EdcRadio.Stations.LiveAt(_onAir, DateTime.UtcNow);
            if (index == _liveIndex)
            {
                index = (index + 1) % _onAir.Tracks.Count;
                track = _onAir.Tracks[index];
                offset = TimeSpan.Zero;
            }
            _liveIndex = index;
            Play(track, offset);
        }

        private void Failed()
        {
            _failures++;
            Now = null;
            if (Tape)
            {
                if (_failures < _album.Count) Skip(1);
                else LeaveTape();
                return;
            }
            _onAir = null;
            _retryAt = _clock + 10f;
        }

        private void Play(Track track, TimeSpan offset)
        {
            Now = track;
            _seek = offset > TimeSpan.FromSeconds(1) ? offset : (TimeSpan?)null;
            _music.Open(new Uri(System.IO.Path.GetFullPath(track.Path)));
            if (!Paused) _music.Play();
        }

        private static float Clamp(float f) =>
            (float)Math.Round(Math.Min(EdcRadio.Stations.Max, Math.Max(EdcRadio.Stations.Min, f)), 1);

        private static string NoiseFile()
        {
            string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "edc-radio-static-2.wav");
            try
            {
                if (File.Exists(path)) return path;
                const int rate = 22050;
                int samples = rate * 4;
                var rnd = new Random(7);
                using (var w = new BinaryWriter(File.Create(path)))
                {
                    w.Write(Encoding.ASCII.GetBytes("RIFF"));
                    w.Write(36 + samples * 2);
                    w.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
                    w.Write(16);
                    w.Write((short)1);
                    w.Write((short)1);
                    w.Write(rate);
                    w.Write(rate * 2);
                    w.Write((short)2);
                    w.Write((short)16);
                    w.Write(Encoding.ASCII.GetBytes("data"));
                    w.Write(samples * 2);
                    double low = 0, band = 0;
                    for (int i = 0; i < samples; i++)
                    {
                        double white = rnd.NextDouble() * 2 - 1;
                        low += 0.08 * (white - low);
                        band = 0.6 * band + 0.4 * (white - low);
                        double crackle = rnd.NextDouble() < 0.002 ? (rnd.NextDouble() * 2 - 1) * 0.9 : 0;
                        double edge = Math.Min(1.0, Math.Min(i, samples - 1 - i) / 400.0);
                        double v = Math.Max(-1, Math.Min(1, (band * 0.7 + low * 0.5) * edge + crackle));
                        w.Write((short)(v * 12000));
                    }
                }
                return path;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                return null;
            }
        }

        public void Dispose()
        {
            _music.Close();
            _noise.Close();
        }
    }
}

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
        private const int NoiseRate = 22050;
        private const int NoiseSamples = NoiseRate * 4;

        private readonly MediaPlayer _noise = new MediaPlayer();
        private readonly List<Track> _album;
        private MediaPlayer _music;
        private Station _onAir;
        private int _liveIndex;
        private TimeSpan? _seek;
        private int _tapeIndex;
        private long _sleepAt;
        private double _clock;
        private double _retryAt;
        private float _crackle;
        private int _failures;
        private bool _noiseRetried;
        private bool _disposed;

        public List<Station> Stations { get; }
        public float Frequency { get; private set; }
        public float Volume { get; set; }
        public bool Paused { get; private set; }
        public bool Tape { get; private set; }
        public Track Now { get; private set; }
        public bool Ready { get; private set; }
        public Track Broken { get; private set; }
        public int Errors { get; private set; }
        public int AlbumCount => _album.Count;
        public int TapeIndex => _tapeIndex;
        public Track TapeTrack => _album.Count > 0 ? _album[_tapeIndex % _album.Count] : null;
        public float SleepLeft => _sleepAt == 0 ? -1f : Math.Max(0f, (_sleepAt - Environment.TickCount64) / 1000f);
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
            _music = NewPlayer();
            _noise.MediaEnded += (s, e) =>
            {
                if (_disposed) return;
                _noise.Position = TimeSpan.Zero;
                _noise.Play();
            };
            _noise.MediaFailed += (s, e) =>
            {
                if (_disposed || _noiseRetried) return;
                _noiseRetried = true;
                StartNoise(true);
            };
            StartNoise(false);
        }

        public void Tune(float frequency)
        {
            if (Tape) LeaveTape();
            Frequency = Clamp(frequency);
            _failures = 0;
            _retryAt = 0;
        }

        public void Crackle() => _crackle = 0.35f;

        public void StartTape(int index)
        {
            if (_album.Count == 0) return;
            PlaceTape(index);
            Tape = true;
            _onAir = null;
            _failures = 0;
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
            _failures = 0;
            _retryAt = 0;
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
            _failures = 0;
            PlayTape();
        }

        public void Skip(int direction)
        {
            if (!Tape || _album.Count == 0) return;
            _tapeIndex = ((_tapeIndex + direction) % _album.Count + _album.Count) % _album.Count;
            PlayTape();
        }

        public void Sleep(float minutes) => _sleepAt = minutes <= 0 ? 0 : Environment.TickCount64 + (long)(minutes * 60000f);

        public void Tick(float dt)
        {
            _clock += dt;
            if (_sleepAt != 0 && Environment.TickCount64 >= _sleepAt)
            {
                _sleepAt = 0;
                if (!Paused) TogglePause();
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
            float left = SleepLeft;
            float fade = left > 0f && left < 10f ? left / 10f : 1f;
            float heard = Heard;
            float wobble = 1f - 0.35f * (1f - heard) * (0.5f + 0.5f * (float)Math.Sin(_clock * 3.1));
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

        private MediaPlayer NewPlayer()
        {
            var player = new MediaPlayer { Volume = 0 };
            player.MediaOpened += (s, e) => Opened(player);
            player.MediaEnded += (s, e) =>
            {
                if (Current(player)) Next();
            };
            player.MediaFailed += (s, e) =>
            {
                if (Current(player)) Failed();
            };
            return player;
        }

        private bool Current(MediaPlayer player) => !_disposed && player == _music;

        private void Opened(MediaPlayer player)
        {
            if (!Current(player)) return;
            _failures = 0;
            if (_seek.HasValue)
            {
                TimeSpan seek = _seek.Value;
                if (player.NaturalDuration.HasTimeSpan)
                {
                    TimeSpan end = player.NaturalDuration.TimeSpan - TimeSpan.FromSeconds(1);
                    if (seek > end) seek = end > TimeSpan.Zero ? end : TimeSpan.Zero;
                }
                player.Position = seek;
            }
            _seek = null;
            Ready = true;
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
            Station s = _onAir;
            if (s == null) return;
            int count = s.Tracks.Count;
            var (track, offset, index) = EdcRadio.Stations.LiveAt(s, DateTime.UtcNow);
            int ahead = ((index - _liveIndex) % count + count) % count;
            if (ahead == 0 || ahead > count / 2)
            {
                index = (_liveIndex + 1) % count;
                track = s.Tracks[index];
                offset = TimeSpan.Zero;
            }
            _liveIndex = index;
            Play(track, offset);
        }

        private void Failed()
        {
            if (Now == null) return;
            _failures++;
            Errors++;
            Broken = Now;
            Now = null;
            if (Tape)
            {
                if (_failures < _album.Count) Skip(1);
                else LeaveTape();
                return;
            }
            Station s = _onAir;
            if (s != null && _failures < s.Tracks.Count)
            {
                _liveIndex = (_liveIndex + 1) % s.Tracks.Count;
                Play(s.Tracks[_liveIndex], TimeSpan.Zero);
                return;
            }
            _onAir = null;
            _retryAt = _clock + 10;
        }

        private void Play(Track track, TimeSpan offset)
        {
            Now = track;
            Ready = false;
            _seek = offset > TimeSpan.FromSeconds(1) ? offset : (TimeSpan?)null;
            MediaPlayer old = _music;
            _music = NewPlayer();
            _music.Volume = old.Volume;
            old.Close();
            _music.Open(new Uri(Path.GetFullPath(track.Path)));
            if (!Paused) _music.Play();
        }

        private void StartNoise(bool fresh)
        {
            string noise = NoiseFile(fresh);
            if (noise == null) return;
            _noise.Volume = 0;
            _noise.Open(new Uri(noise));
            _noise.Play();
        }

        private static float Clamp(float f) =>
            (float)Math.Round(Math.Min(EdcRadio.Stations.Max, Math.Max(EdcRadio.Stations.Min, f)), 1);

        private static string NoiseFile(bool fresh)
        {
            string path = Path.Combine(Path.GetTempPath(), "edc-radio-static-2.wav");
            string temp = path + "." + Environment.ProcessId + ".tmp";
            try
            {
                var existing = new FileInfo(path);
                if (!fresh && existing.Exists && existing.Length == 44 + NoiseSamples * 2) return path;
                var rnd = new Random(7);
                using (var w = new BinaryWriter(File.Create(temp)))
                {
                    w.Write(Encoding.ASCII.GetBytes("RIFF"));
                    w.Write(36 + NoiseSamples * 2);
                    w.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
                    w.Write(16);
                    w.Write((short)1);
                    w.Write((short)1);
                    w.Write(NoiseRate);
                    w.Write(NoiseRate * 2);
                    w.Write((short)2);
                    w.Write((short)16);
                    w.Write(Encoding.ASCII.GetBytes("data"));
                    w.Write(NoiseSamples * 2);
                    double low = 0, band = 0;
                    for (int i = 0; i < NoiseSamples; i++)
                    {
                        double white = rnd.NextDouble() * 2 - 1;
                        low += 0.08 * (white - low);
                        band = 0.6 * band + 0.4 * (white - low);
                        double crackle = rnd.NextDouble() < 0.002 ? (rnd.NextDouble() * 2 - 1) * 0.9 : 0;
                        double edge = Math.Min(1.0, Math.Min(i, NoiseSamples - 1 - i) / 400.0);
                        double v = Math.Max(-1, Math.Min(1, (band * 0.7 + low * 0.5) * edge + crackle));
                        w.Write((short)(v * 12000));
                    }
                }
                File.Move(temp, path, true);
                return path;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Forget(temp);
                return null;
            }
        }

        private static void Forget(string file)
        {
            try
            {
                File.Delete(file);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
            }
        }

        public void Dispose()
        {
            _disposed = true;
            _music.Close();
            _noise.Close();
        }
    }
}

using System;
using System.Buffers.Binary;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace EdcRadio
{
    internal sealed class Presence
    {
        public string Details;
        public string State;
        public long Start;
        public long End;
        public string LargeImage;
        public string LargeText;
        public string LargeUrl;
        public string SmallImage;
        public string SmallText;
        public string Button;
        public string ButtonUrl;
    }

    internal sealed class Discord : IDisposable
    {
        public const string ApplicationId = "1567356354182520916";

        private const int Handshake = 0;
        private const int Frame = 1;
        private const int Close = 2;
        private const int Ping = 3;
        private const int Pong = 4;
        private const int InvalidClientId = 4000;
        private const int Rejected = 600000;
        private const int Limit = 65536;
        private const int Wait = 5000;

        private readonly string _clientId;
        private readonly string _pipe;
        private readonly int _gap;
        private readonly int _retry;
        private readonly int _idle;
        private readonly object _gate = new object();
        private readonly AutoResetEvent _wake = new AutoResetEvent(false);
        private readonly CancellationTokenSource _stop = new CancellationTokenSource();
        private readonly Thread _worker;
        private string _wanted = "null";
        private string _lean = "null";
        private bool _closed;
        private volatile bool _stopping;
        private volatile bool _connected;
        private volatile int _sent;

        public Discord(string clientId, string pipe = "discord-ipc-", int gap = 5000, int retry = 20000, int idle = 20000)
        {
            _clientId = clientId;
            _pipe = pipe;
            _gap = gap;
            _retry = retry;
            _idle = idle;
            if (string.IsNullOrEmpty(clientId)) return;
            _worker = new Thread(Run) { IsBackground = true, Name = "discord" };
            _worker.Start();
        }

        public bool Connected => _connected;

        public int Sent => _sent;

        public void Show(Presence presence)
        {
            string full = presence == null ? "null" : Activity(presence);
            string lean = presence == null ? "null" : Activity(presence, true);
            lock (_gate)
            {
                if (_closed) return;
                _wanted = full;
                _lean = lean;
                _wake.Set();
            }
        }

        internal static string Activity(Presence p, bool lean = false)
        {
            using (var stream = new MemoryStream())
            {
                using (var w = new Utf8JsonWriter(stream))
                {
                    w.WriteStartObject();
                    w.WriteNumber("type", 2);
                    if (!lean) w.WriteNumber("status_display_type", 0);
                    w.WriteString("details", Fit(p.Details, 128));
                    if (!string.IsNullOrWhiteSpace(p.State)) w.WriteString("state", Fit(p.State, 128));
                    if (p.Start > 0)
                    {
                        w.WriteStartObject("timestamps");
                        w.WriteNumber("start", p.Start);
                        if (p.End > p.Start) w.WriteNumber("end", p.End);
                        w.WriteEndObject();
                    }
                    w.WriteStartObject("assets");
                    if (!string.IsNullOrEmpty(p.LargeImage)) w.WriteString("large_image", p.LargeImage);
                    if (!string.IsNullOrEmpty(p.LargeText)) w.WriteString("large_text", Fit(p.LargeText, 128));
                    if (!lean && !string.IsNullOrEmpty(p.LargeUrl)) w.WriteString("large_url", p.LargeUrl);
                    if (!string.IsNullOrEmpty(p.SmallImage)) w.WriteString("small_image", p.SmallImage);
                    if (!string.IsNullOrEmpty(p.SmallText)) w.WriteString("small_text", Fit(p.SmallText, 128));
                    w.WriteEndObject();
                    if (!lean && !string.IsNullOrEmpty(p.Button) && !string.IsNullOrEmpty(p.ButtonUrl))
                    {
                        w.WriteStartArray("buttons");
                        w.WriteStartObject();
                        w.WriteString("label", Cut(p.Button.Trim(), 32));
                        w.WriteString("url", p.ButtonUrl);
                        w.WriteEndObject();
                        w.WriteEndArray();
                    }
                    w.WriteEndObject();
                }
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        internal static string Command(string activity, int pid, string nonce)
        {
            using (var stream = new MemoryStream())
            {
                using (var w = new Utf8JsonWriter(stream))
                {
                    w.WriteStartObject();
                    w.WriteString("cmd", "SET_ACTIVITY");
                    w.WriteStartObject("args");
                    w.WriteNumber("pid", pid);
                    w.WritePropertyName("activity");
                    w.WriteRawValue(activity);
                    w.WriteEndObject();
                    w.WriteString("nonce", nonce);
                    w.WriteEndObject();
                }
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        internal static byte[] Encode(int op, string json)
        {
            byte[] body = Encoding.UTF8.GetBytes(json);
            var frame = new byte[8 + body.Length];
            BinaryPrimitives.WriteInt32LittleEndian(frame.AsSpan(0, 4), op);
            BinaryPrimitives.WriteInt32LittleEndian(frame.AsSpan(4, 4), body.Length);
            body.CopyTo(frame, 8);
            return frame;
        }

        internal static (int Op, string Json) Read(Stream stream, int timeout, CancellationToken stop = default)
        {
            var head = new byte[8];
            Exactly(stream, head, timeout, stop);
            int op = BinaryPrimitives.ReadInt32LittleEndian(head.AsSpan(0, 4));
            int length = BinaryPrimitives.ReadInt32LittleEndian(head.AsSpan(4, 4));
            if (length < 0 || length > Limit) throw new InvalidDataException("frame of " + length + " bytes");
            var body = new byte[length];
            Exactly(stream, body, timeout, stop);
            return (op, Encoding.UTF8.GetString(body));
        }

        internal static string Fit(string s, int max)
        {
            s = (s ?? "").Trim();
            if (s.Length > max) s = Cut(s, max - 1).TrimEnd() + "…";
            return s.Length < 2 ? s.PadRight(2, '.') : s;
        }

        private static string Cut(string s, int length)
        {
            if (s.Length <= length) return s;
            if (char.IsHighSurrogate(s[length - 1])) length--;
            return s.Substring(0, length);
        }

        private static void Exactly(Stream stream, byte[] buffer, int timeout, CancellationToken stop)
        {
            using (var cancel = CancellationTokenSource.CreateLinkedTokenSource(stop))
            {
                cancel.CancelAfter(timeout);
                stream.ReadExactlyAsync(buffer, cancel.Token).AsTask().GetAwaiter().GetResult();
            }
        }

        private static void Send(Stream stream, byte[] frame, CancellationToken stop)
        {
            using (var cancel = CancellationTokenSource.CreateLinkedTokenSource(stop))
            {
                cancel.CancelAfter(Wait);
                stream.WriteAsync(frame, cancel.Token).AsTask().GetAwaiter().GetResult();
                stream.FlushAsync(cancel.Token).GetAwaiter().GetResult();
            }
        }

        private static bool Lost(Exception e) =>
            e is IOException || e is InvalidDataException || e is ObjectDisposedException || e is UnauthorizedAccessException
            || e is TimeoutException || e is OperationCanceledException || e is InvalidOperationException || e is JsonException
            || e is ArgumentException || e is NotSupportedException;

        private void Run()
        {
            NamedPipeClientStream pipe = null;
            string sent = null;
            long lastSend = long.MinValue / 2;
            long blockedUntil = 0;
            int pid = Environment.ProcessId;
            CancellationToken stop = _stop.Token;
            bool proven = false;
            while (!_stopping)
            {
                try
                {
                    string wanted, lean;
                    lock (_gate)
                    {
                        wanted = _wanted;
                        lean = _lean;
                    }
                    if (pipe == null)
                    {
                        if (wanted == "null" || Environment.TickCount64 < blockedUntil)
                        {
                            _wake.WaitOne(wanted == "null" ? Timeout.Infinite : (int)Math.Max(1000, blockedUntil - Environment.TickCount64));
                            continue;
                        }
                        pipe = Connect();
                        if (pipe == null)
                        {
                            _wake.WaitOne(_retry);
                            continue;
                        }
                        if (!Greet(pipe, stop))
                        {
                            pipe.Dispose();
                            pipe = null;
                            blockedUntil = Environment.TickCount64 + Rejected;
                            continue;
                        }
                        _connected = true;
                        proven = false;
                        sent = null;
                    }
                    if (wanted == sent)
                    {
                        if (!_wake.WaitOne(_idle) && wanted != "null") sent = null;
                        continue;
                    }
                    long wait = lastSend + _gap - Environment.TickCount64;
                    if (wait > 0)
                    {
                        _wake.WaitOne((int)wait);
                        continue;
                    }
                    bool accepted = Set(pipe, wanted, pid, stop);
                    proven = true;
                    if (!accepted && lean != wanted) accepted = Set(pipe, lean, pid, stop);
                    sent = wanted;
                    lastSend = Environment.TickCount64;
                    if (accepted) _sent++;
                }
                catch (Exception e) when (Lost(e))
                {
                    bool again = proven;
                    proven = false;
                    pipe?.Dispose();
                    pipe = null;
                    _connected = false;
                    sent = null;
                    if (!_stopping && !again) _wake.WaitOne(_retry);
                }
            }
            pipe?.Dispose();
            _connected = false;
            lock (_gate)
            {
                _wake.Dispose();
                _stop.Dispose();
            }
        }

        private NamedPipeClientStream Connect()
        {
            for (int i = 0; i < 10 && !_stopping; i++)
            {
                var pipe = new NamedPipeClientStream(".", _pipe + i, PipeDirection.InOut, PipeOptions.Asynchronous);
                try
                {
                    pipe.Connect(100);
                    return pipe;
                }
                catch (Exception e) when (Lost(e))
                {
                    pipe.Dispose();
                }
            }
            return null;
        }

        private bool Greet(NamedPipeClientStream pipe, CancellationToken stop)
        {
            Send(pipe, Encode(Handshake, "{\"v\":1,\"client_id\":" + JsonSerializer.Serialize(_clientId) + "}"), stop);
            var (op, json) = Read(pipe, Wait, stop);
            if (op == Close)
            {
                using (JsonDocument doc = JsonDocument.Parse(json))
                {
                    bool invalid = doc.RootElement.ValueKind == JsonValueKind.Object
                        && doc.RootElement.TryGetProperty("code", out JsonElement code)
                        && code.ValueKind == JsonValueKind.Number && code.GetInt32() == InvalidClientId;
                    if (invalid) return false;
                }
                throw new IOException("Discord closed the handshake");
            }
            if (op != Frame) throw new InvalidDataException("unexpected opcode " + op);
            using (JsonDocument doc = JsonDocument.Parse(json))
            {
                if (doc.RootElement.ValueKind == JsonValueKind.Object
                    && doc.RootElement.TryGetProperty("evt", out JsonElement evt)
                    && evt.ValueKind == JsonValueKind.String && evt.GetString() == "READY")
                    return true;
            }
            throw new IOException("Discord did not send READY");
        }

        private bool Set(NamedPipeClientStream pipe, string activity, int pid, CancellationToken stop)
        {
            string nonce = Guid.NewGuid().ToString("N");
            Send(pipe, Encode(Frame, Command(activity, pid, nonce)), stop);
            var watch = Stopwatch.StartNew();
            while (watch.ElapsedMilliseconds < Wait)
            {
                var (op, json) = Read(pipe, Wait, stop);
                if (op == Ping)
                {
                    Send(pipe, Encode(Pong, json), stop);
                    continue;
                }
                if (op == Close) throw new IOException("Discord closed the connection");
                if (op != Frame) continue;
                using (JsonDocument doc = JsonDocument.Parse(json))
                {
                    JsonElement root = doc.RootElement;
                    if (root.ValueKind != JsonValueKind.Object) continue;
                    if (!root.TryGetProperty("nonce", out JsonElement n) || n.ValueKind != JsonValueKind.String || n.GetString() != nonce) continue;
                    return !(root.TryGetProperty("evt", out JsonElement evt) && evt.ValueKind == JsonValueKind.String && evt.GetString() == "ERROR");
                }
            }
            throw new TimeoutException("no answer to SET_ACTIVITY");
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_closed) return;
                _closed = true;
                _stopping = true;
                if (_worker == null)
                {
                    _wake.Dispose();
                    _stop.Dispose();
                    return;
                }
                _stop.Cancel();
                _wake.Set();
            }
            _worker.Join(1500);
        }
    }
}

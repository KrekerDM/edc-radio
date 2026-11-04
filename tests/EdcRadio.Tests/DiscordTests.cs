using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace EdcRadio.Tests
{
    public sealed class DiscordTests
    {
        private static Presence Song(string title = "Rainless") => new Presence
        {
            Details = title,
            State = "Sohaoying · 91.1 FM radio",
            Start = 1_700_000_000_000,
            End = 1_700_000_244_000,
            LargeImage = "https://edc-radio-site.vercel.app/img/rpc/radio.png",
            LargeText = "EDC Radio v1.1.0",
            LargeUrl = "https://edc-radio-site.vercel.app/en/",
            SmallImage = "https://edc-radio-site.vercel.app/img/rpc/tower.png",
            SmallText = "91.1 FM",
            Button = "Get EDC Radio",
            ButtonUrl = "https://edc-radio-site.vercel.app/en/",
        };

        private static string Prefix() => "edc-radio-test-" + Guid.NewGuid().ToString("N") + "-";

        private static NamedPipeServerStream Server(string prefix) =>
            new NamedPipeServerStream(prefix + "0", PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

        private static async Task Accept(NamedPipeServerStream server, int timeout = 10000)
        {
            using (var cancel = new CancellationTokenSource(timeout))
                await server.WaitForConnectionAsync(cancel.Token);
        }

        private static void Write(Stream server, int op, string json)
        {
            byte[] frame = Discord.Encode(op, json);
            server.Write(frame, 0, frame.Length);
            server.Flush();
        }

        private static void Ready(Stream server)
        {
            var (op, hello) = Discord.Read(server, 5000);
            Assert.Equal(0, op);
            Assert.Contains("\"client_id\":", hello);
            Write(server, 1, "{\"cmd\":\"DISPATCH\",\"evt\":\"READY\",\"data\":{}}");
        }

        private static JsonElement Activity(Stream server, bool error = false)
        {
            var (op, json) = Discord.Read(server, 5000);
            Assert.Equal(1, op);
            using (JsonDocument doc = JsonDocument.Parse(json))
            {
                string nonce = doc.RootElement.GetProperty("nonce").GetString();
                string evt = error ? "\"ERROR\"" : "null";
                Write(server, 1, "{\"cmd\":\"SET_ACTIVITY\",\"evt\":" + evt + ",\"data\":{},\"nonce\":\"" + nonce + "\"}");
                return doc.RootElement.GetProperty("args").GetProperty("activity").Clone();
            }
        }

        [Fact]
        public void ActivityListensAndKeepsMilliseconds()
        {
            using (JsonDocument doc = JsonDocument.Parse(Discord.Activity(Song())))
            {
                JsonElement a = doc.RootElement;
                Assert.Equal(2, a.GetProperty("type").GetInt32());
                Assert.Equal(0, a.GetProperty("status_display_type").GetInt32());
                Assert.Equal("Rainless", a.GetProperty("details").GetString());
                Assert.Equal(1_700_000_000_000, a.GetProperty("timestamps").GetProperty("start").GetInt64());
                Assert.Equal(1_700_000_244_000, a.GetProperty("timestamps").GetProperty("end").GetInt64());
                Assert.Equal("https://edc-radio-site.vercel.app/img/rpc/tower.png", a.GetProperty("assets").GetProperty("small_image").GetString());
                Assert.Equal("Get EDC Radio", a.GetProperty("buttons").EnumerateArray().Single().GetProperty("label").GetString());
            }
        }

        [Fact]
        public void LeanActivityDropsTheOptionalParts()
        {
            using (JsonDocument doc = JsonDocument.Parse(Discord.Activity(Song(), true)))
            {
                JsonElement a = doc.RootElement;
                Assert.False(a.TryGetProperty("status_display_type", out _));
                Assert.False(a.TryGetProperty("buttons", out _));
                Assert.False(a.GetProperty("assets").TryGetProperty("large_url", out _));
                Assert.Equal("Rainless", a.GetProperty("details").GetString());
            }
        }

        [Fact]
        public void ActivityFitsDiscordLimits()
        {
            var p = Song(new string('x', 300));
            p.State = "a";
            p.Button = new string('b', 50);
            using (JsonDocument doc = JsonDocument.Parse(Discord.Activity(p)))
            {
                JsonElement a = doc.RootElement;
                Assert.InRange(a.GetProperty("details").GetString().Length, 2, 128);
                Assert.InRange(a.GetProperty("state").GetString().Length, 2, 128);
                Assert.Equal(32, a.GetProperty("buttons")[0].GetProperty("label").GetString().Length);
            }
        }

        [Fact]
        public void FitNeverSplitsAnEmoji()
        {
            string fit = Discord.Fit(new string('a', 126) + "🎵🎵🎵", 128);
            Assert.True(fit.Length <= 128);
            for (int i = 0; i < fit.Length; i++)
                if (char.IsHighSurrogate(fit[i])) Assert.True(i + 1 < fit.Length && char.IsLowSurrogate(fit[i + 1]));
            Assert.EndsWith("…", fit);
        }

        [Fact]
        public void ActivityLeavesOutEmptyParts()
        {
            using (JsonDocument doc = JsonDocument.Parse(Discord.Activity(new Presence { Details = "Rest" })))
            {
                JsonElement a = doc.RootElement;
                Assert.False(a.TryGetProperty("timestamps", out _));
                Assert.False(a.TryGetProperty("buttons", out _));
                Assert.False(a.TryGetProperty("state", out _));
            }
        }

        [Fact]
        public void FramesRoundTrip()
        {
            string json = Discord.Command("null", 42, "n1");
            using (var stream = new MemoryStream(Discord.Encode(1, json)))
            {
                var (op, back) = Discord.Read(stream, 1000);
                Assert.Equal(1, op);
                Assert.Equal(json, back);
            }
            using (JsonDocument doc = JsonDocument.Parse(json))
            {
                Assert.Equal("SET_ACTIVITY", doc.RootElement.GetProperty("cmd").GetString());
                Assert.Equal(42, doc.RootElement.GetProperty("args").GetProperty("pid").GetInt32());
                Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("args").GetProperty("activity").ValueKind);
            }
        }

        [Fact]
        public void OversizedFramesAreRejected()
        {
            var frame = new byte[8];
            BitConverter.GetBytes(1).CopyTo(frame, 0);
            BitConverter.GetBytes(10_000_000).CopyTo(frame, 4);
            using (var stream = new MemoryStream(frame))
                Assert.Throws<InvalidDataException>(() => Discord.Read(stream, 1000));
        }

        [Fact]
        public async Task TalksToDiscordAndClearsOnExit()
        {
            string prefix = Prefix();
            using (NamedPipeServerStream server = Server(prefix))
            {
                var client = new Discord("123456789", prefix, gap: 50, retry: 200, idle: 60000);
                client.Show(Song());
                await Accept(server);
                Ready(server);
                Assert.Equal("Rainless", Activity(server).GetProperty("details").GetString());
                client.Show(null);
                Assert.Equal(JsonValueKind.Null, Activity(server).ValueKind);
                Assert.True(SpinWait.SpinUntil(() => client.Sent == 2, 5000));
                Assert.True(client.Connected);
                client.Dispose();
                Assert.False(client.Connected);
            }
        }

        [Fact]
        public async Task ErrorAnswerFallsBackToTheLeanActivity()
        {
            string prefix = Prefix();
            using (NamedPipeServerStream server = Server(prefix))
            {
                var client = new Discord("123456789", prefix, gap: 50, retry: 200, idle: 60000);
                client.Show(Song());
                await Accept(server);
                Ready(server);
                Assert.True(Activity(server, error: true).TryGetProperty("buttons", out _));
                JsonElement lean = Activity(server);
                Assert.False(lean.TryGetProperty("buttons", out _));
                Assert.True(SpinWait.SpinUntil(() => client.Sent == 1, 5000));
                client.Dispose();
            }
        }

        [Fact]
        public async Task IdleConnectionResendsAndReconnectsAtOnce()
        {
            string prefix = Prefix();
            var client = new Discord("123456789", prefix, gap: 50, retry: 20000, idle: 300);
            using (NamedPipeServerStream server = Server(prefix))
            {
                client.Show(Song());
                await Accept(server);
                Ready(server);
                Activity(server);
                Assert.Equal("Rainless", Activity(server).GetProperty("details").GetString());
            }
            var watch = Stopwatch.StartNew();
            using (NamedPipeServerStream again = Server(prefix))
            {
                await Accept(again, 5000);
                Assert.True(watch.ElapsedMilliseconds < 5000);
                Ready(again);
                Assert.Equal("Rainless", Activity(again).GetProperty("details").GetString());
            }
            client.Dispose();
        }

        [Fact]
        public async Task RejectedClientIdIsNotRetriedRightAway()
        {
            string prefix = Prefix();
            var client = new Discord("bad", prefix, gap: 50, retry: 100, idle: 60000);
            using (NamedPipeServerStream server = Server(prefix))
            {
                client.Show(Song());
                await Accept(server);
                Discord.Read(server, 5000);
                Write(server, 2, "{\"code\":4000,\"message\":\"Invalid Client ID\"}");
            }
            using (NamedPipeServerStream again = Server(prefix))
            using (var cancel = new CancellationTokenSource(1500))
            {
                client.Show(Song("Rest"));
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => again.WaitForConnectionAsync(cancel.Token));
            }
            Assert.Equal(0, client.Sent);
            client.Dispose();
        }

        [Fact]
        public async Task OtherHandshakeClosesAreRetriedSoon()
        {
            string prefix = Prefix();
            var client = new Discord("123456789", prefix, gap: 50, retry: 100, idle: 60000);
            using (NamedPipeServerStream server = Server(prefix))
            {
                client.Show(Song());
                await Accept(server);
                Discord.Read(server, 5000);
                Write(server, 2, "{\"code\":4002,\"message\":\"Rate limited\"}");
            }
            using (NamedPipeServerStream again = Server(prefix))
            {
                await Accept(again, 5000);
                Ready(again);
                Assert.Equal("Rainless", Activity(again).GetProperty("details").GetString());
            }
            client.Dispose();
        }

        [Fact]
        public void DisposingTwiceAndShowingAfterAreSafe()
        {
            var client = new Discord("123456789", Prefix(), gap: 50, retry: 100);
            client.Show(Song());
            Thread.Sleep(200);
            client.Dispose();
            client.Dispose();
            client.Show(Song("Rest"));
            client.Show(null);
            var idle = new Discord("");
            idle.Dispose();
            idle.Dispose();
            idle.Show(Song());
            Assert.False(client.Connected);
        }

        [Fact]
        public void EmptyClientIdStartsNothing()
        {
            var client = new Discord("");
            client.Show(Song());
            Thread.Sleep(100);
            Assert.False(client.Connected);
            Assert.Equal(0, client.Sent);
            client.Dispose();
        }
    }
}

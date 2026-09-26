using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using ADOFAI;

namespace JipperOverlayer.Overlayer;

public class PlayCount
{
    public static Dictionary<Hash, PlayData> Datas;
    private static string FilePath => Path.Combine(Loader.ModPath, "Plays.dat");
    private static readonly MD5 Md5 = MD5.Create();

    public static float Multiplier => GameRefs.SongPitch;

    public static void Load()
    {
        string path = FilePath;
        Datas = new Dictionary<Hash, PlayData>();
        if (File.Exists(path))
        {
            try { LoadFile(path); return; }
            catch (Exception e) { Loader.Warning($"Error loading play data: {e.Message}"); Datas.Clear(); }
        }
        path += ".bak";
        if (!File.Exists(path)) return;
        try { LoadFile(path); }
        catch (Exception e)
        {
            Loader.Warning($"Error loading backup: {e.Message}");
            Datas.Clear();
        }
    }

    private static void LoadFile(string path)
    {
        using FileStream fs = File.OpenRead(path);
        int version = fs.ReadByte();
        int count = fs.ReadInt();
        if (version < 0 || count < 0 || count > 1_000_000)
            throw new InvalidDataException($"Invalid play data header (version={version}, count={count})");
        for (int i = 0; i < count; i++)
        {
            Hash key = fs.ReadBytes(16);
            (Datas[key] = new PlayData()).Read(fs, version);
        }
    }

    public static void Dispose()
    {
        Save();
        Datas = null;
    }

    public static void AddAttempts(Hash hash, float progress) { var d = GetData(hash); if (d != null) d.AddAttempts(progress, Multiplier); }
    public static void RemoveAttempts(Hash hash, float progress) { var d = GetData(hash); if (d != null) d.RemoveAttempts(progress, Multiplier); }
    public static void SetBest(Hash hash, float start, float cur, float multiplier) { var d = GetData(hash); if (d != null) d.SetBest(start, cur, multiplier); }

    public static void Save()
    {
        try
        {
            string path = FilePath;
            if (Datas == null) { Loader.Warning("Save skipped: Datas is null"); return; }
            string tmpPath = path + ".tmp";
            using (FileStream fs = new(tmpPath, FileMode.Create))
            using (MemoryStream ms = new())
            {
                ms.WriteByte(1);
                int count = 0;
                foreach (var pair in Datas)
                    if (pair.Key.data != null && pair.Value != null) count++;
                ms.WriteInt(count);
                foreach (var pair in Datas)
                {
                    if (pair.Key.data == null || pair.Value == null) continue;
                    ms.Write(pair.Key.data, 0, pair.Key.data.Length);
                    pair.Value.Write(ms);
                }
                ms.WriteTo(fs);
            }
            if (File.Exists(path))
            {
                // 优先用同卷原子替换，避免「已删除旧文件、Move 失败」导致主文件消失。
                try
                {
                    File.Replace(tmpPath, path, path + ".bak", true);
                    return;
                }
                catch (PlatformNotSupportedException) { }
                catch (NotSupportedException) { }
                catch (IOException) { }
                string backup = path + ".bak";
                if (File.Exists(backup)) File.Delete(backup);
                File.Move(path, backup);
                try { File.Move(tmpPath, path); }
                catch
                {
                    if (!File.Exists(path) && File.Exists(backup)) File.Move(backup, path);
                    throw;
                }
            }
            else
            {
                File.Move(tmpPath, path);
            }
        }
        catch (Exception e) { Loader.Warning($"Error saving play data: {e.Message}"); }
    }

    public static PlayData GetData(Hash hash)
    {
        if (Datas == null || hash.data == null) return null;
        if (!Datas.ContainsKey(hash)) Datas[hash] = new PlayData();
        return Datas[hash];
    }

    public class PlayData
    {
        public int totalAttempts;
        public Dictionary<(float, float), int> attempts = new();
        public Dictionary<(float, float), float> best = new();

        public void AddAttempts(float progress, float multiplier)
        {
            var key = (progress, multiplier);
            if (attempts.ContainsKey(key)) attempts[key]++;
            else attempts[key] = 1;
            totalAttempts++;
        }

        public void RemoveAttempts(float progress, float multiplier)
        {
            if (!attempts.TryGetValue((progress, multiplier), out int value) || value <= 0) return;
            if (value == 1) attempts.Remove((progress, multiplier));
            else attempts[(progress, multiplier)] = value - 1;
            totalAttempts = Math.Max(0, totalAttempts - 1);
        }

        public void SetBest(float start, float cur, float multiplier)
        {
            (float, float) key = (start, multiplier);
            if (best.ContainsKey(key))
            {
                if (!(best[key] < cur)) return;
            }
            best[key] = cur;
        }

        public void Write(Stream stream)
        {
            stream.WriteInt(totalAttempts);
            stream.WriteInt(attempts.Count);
            foreach (var pair in attempts)
            {
                stream.WriteFloat(pair.Key.Item1);
                stream.WriteFloat(pair.Key.Item2);
                stream.WriteInt(pair.Value);
            }
            stream.WriteInt(best.Count);
            foreach (var pair in best)
            {
                stream.WriteFloat(pair.Key.Item1);
                stream.WriteFloat(pair.Key.Item2);
                stream.WriteFloat(pair.Value);
            }
        }

        public void Read(Stream stream, int version)
        {
            totalAttempts = stream.ReadInt();
            int size = stream.ReadInt();
            if (size < 0 || size > 1_000_000) throw new InvalidDataException($"Invalid attempts size: {size}");
            for (int i = 0; i < size; i++)
            {
                if (version == 0) stream.ReadByte();
                attempts[(stream.ReadFloat(), stream.ReadFloat())] = stream.ReadInt();
            }
            size = stream.ReadInt();
            if (size < 0 || size > 1_000_000) throw new InvalidDataException($"Invalid best size: {size}");
            for (int i = 0; i < size; i++)
            {
                if (version == 0) stream.ReadByte();
                best[(stream.ReadFloat(), stream.ReadFloat())] = stream.ReadFloat();
            }
        }

        public float GetBest(float start, float multiplier)
        {
            var key = (start, multiplier);
            return best.ContainsKey(key) ? best[key] : 0;
        }

        public int GetAttempts(float progress, float multiplier)
        {
            var key = (progress, multiplier);
            return attempts.ContainsKey(key) ? attempts[key] : 0;
        }

        public int GetAttempts(float progress) => GetAttempts(progress, Multiplier);
        public int GetAttempts()
        {
            int sum = 0;
            foreach (var v in attempts.Values) sum += v;
            return sum;
        }
        public static implicit operator int(PlayData data) => data.totalAttempts;
    }

    public static Hash GetMapHash()
    {
        lock (Md5)
        {
            byte[] source;
            if (ADOBase.isOfficialLevel)
            {
                string level = ADOBase.currentLevel;
                if (string.IsNullOrEmpty(level)) return new Hash(null);
                source = Encoding.UTF8.GetBytes(level);
            }
            else
            {
                if (GameRefs.LevelMaker == null) return new Hash(null);
                try { source = GetHash(); }
                catch (Exception e)
                {
                    Loader.Warning($"PlayCount: map hash unavailable ({e.Message})");
                    return new Hash(null);
                }
            }
            if (source == null || source.Length == 0) return new Hash(null);
            return new Hash(Md5.ComputeHash(source));
        }
    }

    private static byte[] GetHash()
    {
        using MemoryStream ms = new();
        scrLevelMaker lm = GameRefs.LevelMaker;
        if (lm == null) return Array.Empty<byte>();
        // 旧格式关卡只有 leveldata 字符串；customLevel.events 在旧关卡上可能为 null，
        // 继续遍历会在开局 Show 的 map-hash 路径抛 NRE。
        if (lm.isOldLevel)
        {
            ms.WriteUTF(lm.leveldata);
            return ms.ToArray();
        }
        ms.WriteObject(lm.floorAngles);
        var events = ADOBase.customLevel?.events;
        if (events == null) return ms.ToArray();
        foreach (LevelEvent levelEvent in events)
        {
            if (levelEvent == null) continue;
            switch (levelEvent.eventType)
            {
                case LevelEventType.SetSpeed:
                    ms.WriteInt(levelEvent.floor);
                    ms.WriteByte(0);
                    ms.WriteByte((byte)(SpeedType)levelEvent["speedType"]);
                    ms.WriteFloat((float)levelEvent[(SpeedType)levelEvent["speedType"] == SpeedType.Bpm ? "beatsPerMinute" : "bpmMultiplier"]);
                    break;
                case LevelEventType.Twirl:
                    ms.WriteInt(levelEvent.floor); ms.WriteByte(1); break;
                case LevelEventType.Hold:
                    ms.WriteInt(levelEvent.floor); ms.WriteByte(2);
                    ms.WriteInt((int)levelEvent["duration"]); break;
                case LevelEventType.MultiPlanet:
                    ms.WriteInt(levelEvent.floor); ms.WriteByte(3);
                    ms.WriteByte((byte)(PlanetCount)levelEvent["planets"]); break;
                case LevelEventType.Pause:
                    ms.WriteInt(levelEvent.floor); ms.WriteByte(4);
                    ms.WriteFloat((float)levelEvent["duration"]); break;
                case LevelEventType.AutoPlayTiles:
                    ms.WriteInt(levelEvent.floor); ms.WriteByte(5);
                    ms.WriteBoolean((bool)levelEvent["enabled"]); break;
                case LevelEventType.ScaleMargin:
                    ms.WriteInt(levelEvent.floor); ms.WriteByte(6);
                    ms.WriteFloat((float)levelEvent["scale"]); break;
                case LevelEventType.Multitap:
                    ms.WriteInt(levelEvent.floor); ms.WriteByte(7);
                    ms.WriteFloat((float)levelEvent["taps"]); break;
                case LevelEventType.KillPlayer:
                    ms.WriteInt(levelEvent.floor); ms.WriteByte(8); break;
            }
        }
        return ms.ToArray();
    }

    public readonly struct Hash(byte[] data) : IEquatable<Hash>
    {
        public readonly byte[] data = data;
        public override bool Equals(object obj) => obj is Hash h ? Equals(h) : obj is byte[] b && Equals(b);
        public bool Equals(Hash other) => Equals(other.data);
        public bool Equals(byte[] hash)
        {
            if (data == hash) return true;
            if (data == null || hash == null || data.Length != hash.Length) return false;
            for (int i = 0; i < data.Length; i++)
                if (data[i] != hash[i]) return false;
            return true;
        }
        public override int GetHashCode()
        {
            if (data == null) return 0;
            int h = 0;
            for (int i = 0; i < data.Length && i < 16; i++)
                h = h * 31 + data[i];
            return h;
        }
        public static bool operator ==(Hash left, Hash right) => left.Equals(right);
        public static bool operator !=(Hash left, Hash right) => !(left == right);
        public static implicit operator Hash(byte[] hash) => new(hash);
        public static implicit operator byte[](Hash hash) => hash.data;
        public override string ToString()
        {
            if (data == null) return string.Empty;
            char[] chars = new char[data.Length * 2];
            for (int i = 0; i < data.Length; i++)
            {
                chars[i * 2] = "0123456789abcdef"[data[i] >> 4];
                chars[i * 2 + 1] = "0123456789abcdef"[data[i] & 0xF];
            }
            return new string(chars);
        }
    }
}

// Extension methods for binary I/O matching JALib's ByteTool
internal static class StreamExtensions
{
    public static int ReadInt(this Stream stream)
    {
        byte[] buf = new byte[4];
        if (stream.Read(buf, 0, 4) < 4) throw new EndOfStreamException();
        return buf[0] | (buf[1] << 8) | (buf[2] << 16) | (buf[3] << 24);
    }

    public static float ReadFloat(this Stream stream)
    {
        byte[] buf = new byte[4];
        if (stream.Read(buf, 0, 4) < 4) throw new EndOfStreamException();
        return BitConverter.ToSingle(buf, 0);
    }

    public static byte[] ReadBytes(this Stream stream, int count)
    {
        byte[] buf = new byte[count];
        int offset = 0;
        while (offset < count)
        {
            int read = stream.Read(buf, offset, count - offset);
            if (read == 0) throw new EndOfStreamException();
            offset += read;
        }
        return buf;
    }

    public static void WriteInt(this Stream stream, int value)
    {
        stream.WriteByte((byte)(value & 0xFF));
        stream.WriteByte((byte)((value >> 8) & 0xFF));
        stream.WriteByte((byte)((value >> 16) & 0xFF));
        stream.WriteByte((byte)((value >> 24) & 0xFF));
    }

    public static void WriteFloat(this Stream stream, float value)
    {
        byte[] buf = BitConverter.GetBytes(value);
        stream.Write(buf, 0, 4);
    }

    public static void WriteBoolean(this Stream stream, bool value) =>
        stream.WriteByte((byte)(value ? 1 : 0));

    public static void WriteUTF(this Stream stream, string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value ?? "");
        stream.WriteInt(bytes.Length);
        stream.Write(bytes, 0, bytes.Length);
    }

    public static void WriteObject(this Stream stream, object value)
    {
        if (value is float[] arr)
        {
            stream.WriteInt(arr.Length);
            foreach (float f in arr) stream.WriteFloat(f);
        }
    }
}
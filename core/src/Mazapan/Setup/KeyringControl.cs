using System.Buffers.Binary;
using System.Net.Sockets;
using Mazapan.Util;

namespace Mazapan.Setup;

/// <summary>
/// KeyringControl changes the login keyring's password through
/// gnome-keyring-daemon's control socket, exactly as its own PAM module
/// does when an account's password changes (gkr-pam-client.c:
/// GKD_CONTROL_OP_CHANGE with the current and the new password). The PAM
/// module itself can't be used from here: it only acts on a password a
/// module before it (pam_unix) already asked for.
/// </summary>
public static class KeyringControl
{
    const uint OpChange = 2;

    public enum Result { Ok = 0, Denied = 1, Failed = 2, NoDaemon = 3 }

    public static Result Change(string original, string password)
    {
        var dir = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR") is { Length: > 0 } d ? d : "/run/user/" + Environment.GetEnvironmentVariable("UID");
        var path = Path.Join(dir, "keyring", "control");
        if (!File.Exists(path)) return Result.NoDaemon;
        try
        {
            using var s = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            s.Connect(new UnixDomainSocketEndPoint(path));
            // The credentials byte: the daemon checks who's asking (SO_PEERCRED).
            s.Send([0]);
            var o = Files.Utf8.GetBytes(original);
            var p = Files.Utf8.GetBytes(password);
            var packet = new byte[8 + 4 + o.Length + 4 + p.Length];
            var at = 0;
            void U32(uint v) { BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(at), v); at += 4; }
            U32((uint)packet.Length);
            U32(OpChange);
            U32((uint)o.Length); o.CopyTo(packet, at); at += o.Length;
            U32((uint)p.Length); p.CopyTo(packet, at);
            s.Send(packet);
            var reply = new byte[8];
            for (var got = 0; got < 8;)
            {
                var n = s.Receive(reply, got, 8 - got, SocketFlags.None);
                if (n <= 0) return Result.Failed;
                got += n;
            }
            if (BinaryPrimitives.ReadUInt32BigEndian(reply) != 8) return Result.Failed;
            var r = BinaryPrimitives.ReadUInt32BigEndian(reply.AsSpan(4));
            return r <= 3 ? (Result)r : Result.Failed;
        }
        catch (SocketException)
        {
            return Result.NoDaemon;
        }
    }
}

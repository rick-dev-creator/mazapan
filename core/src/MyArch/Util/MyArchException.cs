namespace MyArch.Util;

/// <summary>
/// An error meant for the person: myarch prints its message and exits 1.
/// </summary>
public sealed class MyArchException(string message, Exception? inner = null) : Exception(message, inner);

namespace Mazapan.Util;

/// <summary>
/// An error meant for the person: mazapan prints its message and exits 1.
/// </summary>
public sealed class MazapanException(string message, Exception? inner = null) : Exception(message, inner);

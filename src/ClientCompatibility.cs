using System.Security.Cryptography;

namespace MinimapZoom;

internal static class ClientCompatibility
{
    public static string ReadVersion(string executable) =>
        File.ReadAllText(Path.Combine(Path.GetDirectoryName(executable)!, "ffxivgame.ver")).Trim();

    // Read once at initialization, before any native scan or hook creation.
    public static void ValidateExecutable(string executable, string version)
    {
        ValidateVersion(version);
        using var stream = File.OpenRead(executable);
        ValidateHash(Convert.ToHexString(SHA256.HashData(stream)));
    }

    internal static void ValidateVersion(string version)
    {
        if (version != NativeContracts.GameVersion)
            throw new NotSupportedException(
                $"Client {version} non pris en charge. Version vérifiée : {NativeContracts.GameVersion}. " +
                "Effets désactivés ; recherchez une mise à jour de Minimap Zoom dans /xlplugins.");
    }

    internal static void ValidateHash(string digest)
    {
        if (digest != NativeContracts.ExecutableSha256)
            throw new NotSupportedException(
                "L’exécutable diffère du client vérifié. Effets désactivés ; une vérification de compatibilité est nécessaire.");
    }
}

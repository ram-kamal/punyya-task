using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PHF.Client.Core.Storage;

public static class CredentialStorage
{
    private record CredentialRecord(string Username, string Password, bool Remember);

    private static string GetConfigDirectory()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dir = Path.Combine(localAppData, "PHFWorkManagement");
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        return dir;
    }

    private static string GetConfigFilePath()
    {
        return Path.Combine(GetConfigDirectory(), "workstation_auth.dat");
    }

    private static byte[] GetMachineKey()
    {
        // Device-bound hardware & OS user secret key
        var rawKey = $"{Environment.MachineName}_{Environment.UserName}_PHF_WORKSTATION_CREDENTIAL_VAULT_2026";
        return SHA256.HashData(Encoding.UTF8.GetBytes(rawKey));
    }

    public static void SaveCredentials(string username, string password, bool remember = true)
    {
        try
        {
            if (!remember)
            {
                ClearCredentials();
                return;
            }

            var record = new CredentialRecord(username, password, true);
            var json = JsonSerializer.Serialize(record);
            var plainBytes = Encoding.UTF8.GetBytes(json);

            var key = GetMachineKey();
            using var aes = Aes.Create();
            aes.Key = key;
            aes.GenerateIV();

            using var ms = new MemoryStream();
            // Write IV first
            ms.Write(aes.IV, 0, aes.IV.Length);

            using (var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
            {
                cs.Write(plainBytes, 0, plainBytes.Length);
                cs.FlushFinalBlock();
            }

            File.WriteAllBytes(GetConfigFilePath(), ms.ToArray());
        }
        catch
        {
            // Silently swallow storage errors to prevent crashes
        }
    }

    public static (string Username, string Password, bool Remember) LoadCredentials()
    {
        try
        {
            var filePath = GetConfigFilePath();
            if (!File.Exists(filePath))
            {
                return (string.Empty, string.Empty, false);
            }

            var cipherData = File.ReadAllBytes(filePath);
            if (cipherData.Length < 16)
            {
                return (string.Empty, string.Empty, false);
            }

            var key = GetMachineKey();
            using var aes = Aes.Create();
            aes.Key = key;

            // Extract IV
            var iv = new byte[16];
            Array.Copy(cipherData, 0, iv, 0, 16);
            aes.IV = iv;

            using var ms = new MemoryStream(cipherData, 16, cipherData.Length - 16);
            using var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read);
            using var reader = new StreamReader(cs, Encoding.UTF8);
            var json = reader.ReadToEnd();

            var record = JsonSerializer.Deserialize<CredentialRecord>(json);
            if (record != null && record.Remember)
            {
                return (record.Username ?? string.Empty, record.Password ?? string.Empty, true);
            }
        }
        catch
        {
            // If file is corrupted or moved between machines, clear it safely
            ClearCredentials();
        }

        return (string.Empty, string.Empty, false);
    }

    public static void ClearCredentials()
    {
        try
        {
            var filePath = GetConfigFilePath();
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch
        {
            // Ignore deletion errors
        }
    }
}

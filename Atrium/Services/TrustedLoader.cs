using Extensions.PlayfulPlatforms.Windows;
using Extensions.PrometheusTypes;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using static Extensions.PlayfulPlatforms.Windows.WinTrust;

namespace Atrium.Services
{

    public static class TrustedLoader
    {
        // GUID for the Action to verify a file using the Authenticode Policy Provider
        private static readonly Guid WINTRUST_ACTION_GENERIC_VERIFY_V2 = new("{00AAC56B-CD44-11d0-8CC2-00C04FC295EE}");

        public static bool VerifyDotNet(string filePath, string expectedPublicKeyToken)
        {
            if (!File.Exists(filePath)) return false;

            // 1. Layer One: Strong Name / Identity Check
            try
            {
                var assemblyName = AssemblyName.GetAssemblyName(filePath);
                byte[]? tokenBytes = assemblyName.GetPublicKeyToken();
                string actualToken = Convert.ToHexString(tokenBytes);

                if (!string.Equals(actualToken, expectedPublicKeyToken, StringComparison.OrdinalIgnoreCase))
                    return false; // Not your assembly

                return true;
            }
            catch { return false; } // Not a .NET assembly at all
        }


        public static bool VerifyStrongName(string filePath, string expectedToken)
        {
            if (!File.Exists(filePath)) return false;

            try
            {
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var peReader = new PEReader(fs);

                // Ensure it's actually a CLI/Managed assembly
                if (!peReader.HasMetadata) return false;

                var metadataReader = peReader.GetMetadataReader();
                var assemblyDefinition = metadataReader.GetAssemblyDefinition();

                // Get the public key (if it exists)
                byte[] publicKey = metadataReader.GetBlobBytes(assemblyDefinition.PublicKey);

                if (publicKey == null || publicKey.Length == 0)
                    return false; // Assembly is not signed

                // Calculate the token from the full public key
                byte[] tokenBytes = CalculatePublicKeyToken(publicKey);
                string actualToken = Convert.ToHexString(tokenBytes);

                return string.Equals(actualToken, expectedToken, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                // Likely a native DLL or corrupt file
                return false;
            }
        }

        private static byte[] CalculatePublicKeyToken(byte[] publicKey)
        {
            // The Public Key Token is the last 8 bytes of the SHA-1 hash 
            // of the public key, preceded by a specific header if necessary.
            byte[] hash = System.Security.Cryptography.SHA1.HashData(publicKey);

            byte[] token = new byte[8];
            for (int i = 0; i < 8; i++)
            {
                token[i] = hash[hash.Length - 1 - i];
            }
            // Token is typically represented in reverse order of the hash tail
            Array.Reverse(token);
            return token;
        }


        public static bool IsTrustedBinary(string filePath, string expectedPublicKeyToken)
        {
            // Layer 1: .NET Identity (Works on Windows, macOS, Linux)
            if (!VerifyStrongName(filePath, expectedPublicKeyToken)) return false;

            if (!VerifyDotNet(filePath, expectedPublicKeyToken)) return false;


            // Layer 2: OS-Level Integrity
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return VerifyWindowsSignature(filePath);

            //if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                //return VerifyMacSignature(filePath);

            //if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                //return VerifyLinuxIntegrity(filePath);

            return false;
        }

        public static bool VerifyWindowsSignature(string filePath)
        {
            Guid actionId = new("{00AAC56B-CD44-11d0-8CC2-00C04FC295EE}");

            var fileInfo = new WinTrustFileInfo(filePath);
            IntPtr fileInfoPtr = Marshal.AllocHGlobal(Marshal.SizeOf(fileInfo));
            Marshal.StructureToPtr(fileInfo, fileInfoPtr, false);

            var trustData = new WinTrustData(fileInfoPtr);
            IntPtr trustDataPtr = Marshal.AllocHGlobal(Marshal.SizeOf(trustData));
            Marshal.StructureToPtr(trustData, trustDataPtr, false);

            try
            {
                uint result = WinTrust.WinVerifyTrust(IntPtr.Zero, actionId, trustDataPtr);
                return result == 0; // 0 = ERROR_SUCCESS
            }
            finally
            {
                Marshal.FreeHGlobal(fileInfoPtr);
                Marshal.FreeHGlobal(trustDataPtr);
            }
        }

        public static bool IsMine(string filePath)
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var peReader = new PEReader(fs);
            var metadataReader = peReader.GetMetadataReader();

            return metadataReader.GetAssemblyInfo().IsMine();
        }

    }
}

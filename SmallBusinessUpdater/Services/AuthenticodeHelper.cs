using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;

namespace SmallBusinessUpdater.Services;

internal static class AuthenticodeHelper
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WINTRUST_FILE_INFO(string filePath)
    {
        public uint cbStruct = (uint)Marshal.SizeOf<WINTRUST_FILE_INFO>();

        [MarshalAs(UnmanagedType.LPWStr)]
        public string pcwszFilePath = filePath;

        public nint hFile = IntPtr.Zero;

        public nint pgKnownSubject = IntPtr.Zero;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WINTRUST_DATA()
    {
        public uint cbStruct = (uint)Marshal.SizeOf<WINTRUST_DATA>();

        public nint pPolicyCallbackData = IntPtr.Zero;

        public nint pSIPClientData = IntPtr.Zero;

        public uint dwUIChoice = 2u;

        public uint fdwRevocationChecks = 0u;

        public uint dwUnionChoice = 1u;

        public nint pFile = IntPtr.Zero;

        public uint dwStateAction = 0u;

        public nint hWVTStateData = IntPtr.Zero;

        [MarshalAs(UnmanagedType.LPWStr)]
        public string? pwszURLReference = null;

        public uint dwProvFlags = 0u;

        public uint dwUIContext = 0u;
    }

    private static readonly Guid WinTrustActionGenericVerifyV2 = new Guid("00AAC56B-CD44-11D0-8CC2-00C04FC295EE");

    private const uint WtdUiNone = 2u;

    private const uint WtdRevokeNone = 0u;

    private const uint WtdChoiceFile = 1u;

    private const uint WtdStateActionIgnore = 0u;

    public static bool IsSignatureValid(string filePath) => Verify(filePath, false, out _);

    private static bool Verify(string filePath, bool readCertificate, out X509Certificate2? certificate)
    {
        certificate = null;
        WINTRUST_FILE_INFO wINTRUST_FILE_INFO = new WINTRUST_FILE_INFO(filePath);
        WINTRUST_DATA pWVTData = new WINTRUST_DATA();
        nint num = IntPtr.Zero;
        try
        {
            num = Marshal.AllocCoTaskMem(Marshal.SizeOf(typeof(WINTRUST_FILE_INFO)));
            Marshal.StructureToPtr(wINTRUST_FILE_INFO, num, fDeleteOld: false);
            pWVTData.pFile = num;
            pWVTData.dwStateAction = readCertificate ? 1u : 0u;
            if (WinVerifyTrust(IntPtr.Zero, WinTrustActionGenericVerifyV2, ref pWVTData) != 0)
                return false;

            if (readCertificate)
            {
                nint provider = WTHelperProvDataFromStateData(pWVTData.hWVTStateData);
                nint signer = provider == 0 ? 0 : WTHelperGetProvSignerFromChain(provider, 0, false, 0);
                nint entry = signer == 0 ? 0 : WTHelperGetProvCertFromChain(signer, 0);
                if (entry != 0)
                {
                    // CRYPT_PROVIDER_CERT begins with cbStruct followed by pCert.
                    nint context = Marshal.ReadIntPtr(entry, IntPtr.Size);
                    if (context != 0)
                        certificate = new X509Certificate2(context);
                }
            }

            return true;
        }
        finally
        {
            if (pWVTData.hWVTStateData != 0)
            {
                pWVTData.dwStateAction = 2u;
                WinVerifyTrust(IntPtr.Zero, WinTrustActionGenericVerifyV2, ref pWVTData);
            }
            if (num != IntPtr.Zero)
            {
                Marshal.FreeCoTaskMem(num);
            }
        }
    }

    public static X509Certificate2? GetSignerCertificate(string filePath)
    {
        try { return Verify(filePath, true, out var certificate) ? certificate : null; }
        catch
        {
            return null;
        }
    }

    [DllImport("wintrust.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    private static extern uint WinVerifyTrust(nint hwnd, [MarshalAs(UnmanagedType.LPStruct)] Guid pgActionID, ref WINTRUST_DATA pWVTData);

    [DllImport("wintrust.dll", ExactSpelling = true)]
    private static extern nint WTHelperProvDataFromStateData(nint stateData);

    [DllImport("wintrust.dll", ExactSpelling = true)]
    private static extern nint WTHelperGetProvSignerFromChain(nint provider, uint index, bool counterSigner, uint counterSignerIndex);

    [DllImport("wintrust.dll", ExactSpelling = true)]
    private static extern nint WTHelperGetProvCertFromChain(nint signer, uint index);
}


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

	public static bool IsSignatureValid(string filePath)
	{
		WINTRUST_FILE_INFO wINTRUST_FILE_INFO = new WINTRUST_FILE_INFO(filePath);
		WINTRUST_DATA pWVTData = new WINTRUST_DATA();
		nint num = IntPtr.Zero;
		try
		{
			num = Marshal.AllocCoTaskMem(Marshal.SizeOf(typeof(WINTRUST_FILE_INFO)));
			Marshal.StructureToPtr(wINTRUST_FILE_INFO, num, fDeleteOld: false);
			pWVTData.pFile = num;
			return WinVerifyTrust(IntPtr.Zero, WinTrustActionGenericVerifyV2, ref pWVTData) == 0;
		}
		finally
		{
			if (num != IntPtr.Zero)
			{
				Marshal.FreeCoTaskMem(num);
			}
		}
	}

	public static X509Certificate2? GetSignerCertificate(string filePath)
	{
		try
		{
			return new X509Certificate2(X509Certificate.CreateFromSignedFile(filePath));
		}
		catch
		{
			return null;
		}
	}

	[DllImport("wintrust.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
	private static extern uint WinVerifyTrust(nint hwnd, [MarshalAs(UnmanagedType.LPStruct)] Guid pgActionID, ref WINTRUST_DATA pWVTData);
}

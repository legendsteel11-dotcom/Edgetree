# Compiles an HLSL pixel shader to legacy D3D9 bytecode using d3dcompiler_47.dll,
# which ships with Windows - so no SDK install is needed to produce the .ps blob
# WPF's ShaderEffect wants. All the interop lives in C# because PowerShell 5.1
# cannot build the generic delegates the blob's vtable would otherwise need.
param(
    [Parameter(Mandatory = $true)][string]$Source,
    [Parameter(Mandatory = $true)][string]$Output,
    [string]$Target = 'ps_2_0',
    [string]$Entry = 'main'
)

$code = @'
using System;
using System.IO;
using System.Runtime.InteropServices;

[ComImport, Guid("8BA5FB08-5195-40e2-AC58-0D989C3A0102"),
 InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface ID3DBlob
{
    // PreserveSig on both: neither returns an HRESULT, and without it the CLR
    // reads the pointer as one and hands back zero.
    [PreserveSig] IntPtr GetBufferPointer();
    [PreserveSig] IntPtr GetBufferSize();
}

public static class Hlsl
{
    // IntPtr rather than a marshalled interface: with an out-interface the CLR
    // treats the int return as an HRESULT and throws before we can read the
    // compiler's error text, which is the one thing worth having when it fails.
    [DllImport("d3dcompiler_47.dll", CharSet = CharSet.Ansi, PreserveSig = true)]
    private static extern int D3DCompile(
        byte[] pSrcData, IntPtr SrcDataSize,
        [MarshalAs(UnmanagedType.LPStr)] string pSourceName,
        IntPtr pDefines, IntPtr pInclude,
        [MarshalAs(UnmanagedType.LPStr)] string pEntrypoint,
        [MarshalAs(UnmanagedType.LPStr)] string pTarget,
        uint Flags1, uint Flags2,
        out IntPtr ppCode, out IntPtr ppErrorMsgs);

    private static byte[] Read(ID3DBlob blob)
    {
        int n = (int)blob.GetBufferSize();
        byte[] bytes = new byte[n];
        Marshal.Copy(blob.GetBufferPointer(), bytes, 0, n);
        return bytes;
    }

    public static string Compile(string source, string output, string target, string entry)
    {
        byte[] src = File.ReadAllBytes(source);
        IntPtr codePtr, errorPtr;
        int hr = D3DCompile(src, (IntPtr)src.Length, source, IntPtr.Zero, IntPtr.Zero,
            entry, target, 0, 0, out codePtr, out errorPtr);

        string log = "";
        if (errorPtr != IntPtr.Zero)
        {
            var errors = (ID3DBlob)Marshal.GetObjectForIUnknown(errorPtr);
            log = Marshal.PtrToStringAnsi(errors.GetBufferPointer(), (int)errors.GetBufferSize());
        }

        if (hr != 0 || codePtr == IntPtr.Zero)
        {
            return "FAILED hr=0x" + hr.ToString("X8") + "\n" + log;
        }

        byte[] bytes = Read((ID3DBlob)Marshal.GetObjectForIUnknown(codePtr));
        File.WriteAllBytes(output, bytes);
        return "OK " + target + "  " + bytes.Length + " bytes -> " + output +
               (log.Length > 0 ? "\n" + log : "");
    }
}
'@

Add-Type -TypeDefinition $code -Language CSharp
Write-Output "source=[$Source] output=[$Output] target=[$Target] entry=[$Entry]"
[Hlsl]::Compile([string]$Source, [string]$Output, [string]$Target, [string]$Entry)

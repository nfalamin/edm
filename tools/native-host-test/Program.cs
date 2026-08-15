using System;
using System.Buffers.Binary;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

// Simple native host test harness: launches the target native host executable and sends a
// length-prefixed JSON message on stdin, then reads an ack from stdout.

if (args.Length < 1)
{
    Console.WriteLine("Usage: NativeHostTester <path-to-native-host-exe> [json]");
    return 1;
}

var exe = args[0];
var json = args.Length >= 2 ? args[1] : JsonSerializer.Serialize(new { url = "https://example.com/file.mp4", filename = "file.mp4" });

if (!File.Exists(exe))
{
    Console.WriteLine($"Error: host executable not found: {exe}");
    return 2;
}

var psi = new ProcessStartInfo
{
    FileName = exe,
    UseShellExecute = false,
    RedirectStandardInput = true,
    RedirectStandardOutput = true,
    RedirectStandardError = true,
    CreateNoWindow = true
};

using var proc = Process.Start(psi);
if (proc == null)
{
    Console.WriteLine("Failed to start host process");
    return 3;
}

// Write length-prefixed message
var utf8 = Encoding.UTF8.GetBytes(json);
var len = BitConverter.GetBytes(utf8.Length);
await proc.StandardInput.BaseStream.WriteAsync(len, 0, len.Length);
await proc.StandardInput.BaseStream.WriteAsync(utf8, 0, utf8.Length);
await proc.StandardInput.BaseStream.FlushAsync();
proc.StandardInput.Close();

// Read ack: first 4 bytes length
var outStream = proc.StandardOutput.BaseStream;
var lenBuf = new byte[4];
int r = await ReadExactAsync(outStream, lenBuf, 0, 4);
if (r == 0)
{
    Console.WriteLine("No response from host");
    return 4;
}
int respLen = BinaryPrimitives.ReadInt32LittleEndian(lenBuf);
var resp = new byte[respLen];
await ReadExactAsync(outStream, resp, 0, respLen);
var respText = Encoding.UTF8.GetString(resp);
Console.WriteLine("Host response: " + respText);

// Also print stderr
var err = await proc.StandardError.ReadToEndAsync();
if (!string.IsNullOrEmpty(err)) Console.WriteLine("Host stderr: " + err);

return 0;

static async Task<int> ReadExactAsync(Stream s, byte[] buffer, int offset, int count)
{
    int total = 0;
    while (total < count)
    {
        int r = await s.ReadAsync(buffer, offset + total, count - total);
        if (r == 0) return total;
        total += r;
    }
    return total;
}
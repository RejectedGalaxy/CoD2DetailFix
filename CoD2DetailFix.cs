// CoD2DetailFix.exe - optional add-on for Permanent Corpses (Call of Duty 2): bodies keep their belts,
// pouches and gear up close.
//
// Call of Duty 2 swaps models for simpler versions only a few feet from the camera. Its r_lodBias
// setting fixes that, but the game locks it as a cheat and resets it on every level load. This program
// starts the game exactly as Steam asked and, while single player runs, keeps that one setting at -500
// in the running game. The setting's record is checked by name, type and range first, so nothing else is
// ever written. It changes no files.
//
// Set as the game's Steam launch option (with PermaCorpse's own options after %command%):
//     "<CoD2 folder>\mods\permacorpse\CoD2DetailFix.exe" %command% +set fs_game mods/permacorpse +set cg_marksLimit 4096
// Optional before %command%:  --lod-bias <-1000..0>   (default -500)
//
// Build: csc -target:winexe -optimize -out:CoD2DetailFix.exe -r:System.Windows.Forms.dll CoD2DetailFix.cs

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

static class CoD2DetailFix
{
    static float LodBias = -500f;   // user picked -500 on Hill 400 (2026-10-13); -300 was enough

    [STAThread]
    static int Main(string[] args)
    {
        int a = 0;
        while (a + 1 < args.Length && args[a] == "--lod-bias")
        {
            float v;
            if (float.TryParse(args[a + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out v))
                LodBias = Math.Max(-1000f, Math.Min(0f, v));
            a += 2;
        }
        string here = AppDomain.CurrentDomain.BaseDirectory;
        if (a >= args.Length || !args[a].EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || !File.Exists(args[a]))
        {
            MessageBox.Show("CoD2DetailFix is started by Steam, not by double-clicking it.\n\n"
                + "In Steam, right-click Call of Duty 2 > Properties > General > Launch Options, and enter:\n\n\""
                + Path.Combine(here, "CoD2DetailFix.exe") + "\" %command% +set fs_game mods/permacorpse +set cg_marksLimit 4096\n\nThen press Play.",
                "CoD2DetailFix", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return 1;
        }

        string exe = Path.GetFullPath(args[a]);
        var rest = new StringBuilder();
        for (int i = a + 1; i < args.Length; i++)
        {
            if (rest.Length > 0) rest.Append(' ');
            rest.Append(args[i].IndexOf(' ') >= 0 ? "\"" + args[i] + "\"" : args[i]);
        }
        Process game;
        try
        {
            game = Process.Start(new ProcessStartInfo(exe, rest.ToString()) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(exe) });
        }
        catch (Exception e)
        {
            MessageBox.Show("Could not start " + exe + ":\n" + e.Message, "CoD2DetailFix", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
        bool singlePlayer = Path.GetFileNameWithoutExtension(exe).Equals("CoD2SP_s", StringComparison.OrdinalIgnoreCase);
        if (singlePlayer && LodBias != 0f)
            KeepLodBias(game);          // returns when the game exits
        else
            game.WaitForExit();         // Multiplayer, or --lod-bias 0: started unchanged
        return 0;
    }

    [DllImport("kernel32.dll")] static extern IntPtr OpenProcess(int access, bool inherit, int pid);
    [DllImport("kernel32.dll")] static extern bool ReadProcessMemory(IntPtr h, IntPtr addr, byte[] buf, int size, out int read);
    [DllImport("kernel32.dll")] static extern bool WriteProcessMemory(IntPtr h, IntPtr addr, byte[] buf, int size, out int written);
    [DllImport("kernel32.dll")] static extern int VirtualQueryEx(IntPtr h, IntPtr addr, out MEMINFO info, int size);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
    [StructLayout(LayoutKind.Sequential)]
    struct MEMINFO { public IntPtr Base, AllocBase; public uint AllocProtect; public IntPtr Size; public uint State, Protect, Type; }

    static void KeepLodBias(Process game)
    {
        IntPtr h = IntPtr.Zero;
        try
        {
            h = OpenProcess(0x0438, false, game.Id);   // query, read, write, operation
            if (h == IntPtr.Zero) { game.WaitForExit(); return; }
            long dvar = 0;
            byte[] want = BitConverter.GetBytes(LodBias);
            while (!game.HasExited)
            {
                if (dvar == 0)
                    dvar = FindLodBias(h);
                if (dvar != 0)
                {
                    var cur = new byte[12];
                    int n;
                    if (!ReadProcessMemory(h, (IntPtr)(dvar + 8), cur, 12, out n) || n != 12)
                        dvar = 0;
                    else if (BitConverter.ToSingle(cur, 0) != LodBias || BitConverter.ToSingle(cur, 8) != LodBias)
                    {
                        // current, latched and reset values (a level load resets locked settings to "reset")
                        var all = new byte[12];
                        for (int i = 0; i < 3; i++) Buffer.BlockCopy(want, 0, all, i * 4, 4);
                        WriteProcessMemory(h, (IntPtr)(dvar + 8), all, 12, out n);
                    }
                }
                Thread.Sleep(2000);
            }
        }
        catch { }
        finally { if (h != IntPtr.Zero) CloseHandle(h); }
        try { game.WaitForExit(); } catch { }
    }

    // The game's settings records live in its own image: { name*, flags:16 type:8 modified:8,
    // current, latched, reset, min, max, ... }. r_lodBias is a float (type 1) from -1000 to 0.
    static long FindLodBias(IntPtr h)
    {
        long a = 0x00400000;
        MEMINFO m;
        while (a < 0x10000000 && VirtualQueryEx(h, (IntPtr)a, out m, Marshal.SizeOf(typeof(MEMINFO))) != 0)
        {
            long size = (long)m.Size;
            bool readable = m.State == 0x1000 && (m.Protect & 0x66) != 0 && (m.Protect & 0x100) == 0;
            if (readable && m.Type == 0x1000000 && size < 64 * 1024 * 1024)
            {
                var b = new byte[size];
                int n;
                if (ReadProcessMemory(h, m.Base, b, (int)size, out n))
                    for (int i = 0; i + 28 <= n; i += 4)
                    {
                        if (b[i + 6] != 1 || BitConverter.ToUInt32(b, i + 20) != 0xC47A0000 || BitConverter.ToUInt32(b, i + 24) != 0)
                            continue;
                        var name = new byte[10];
                        int r;
                        if (ReadProcessMemory(h, (IntPtr)BitConverter.ToUInt32(b, i), name, 10, out r) && r == 10
                            && Encoding.ASCII.GetString(name) == "r_lodBias\0")
                            return (long)m.Base + i;
                    }
            }
            a = (long)m.Base + size;
        }
        return 0;
    }
}

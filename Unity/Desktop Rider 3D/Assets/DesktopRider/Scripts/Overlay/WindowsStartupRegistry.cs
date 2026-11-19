#nullable enable

using System;
using System.Runtime.InteropServices;
using System.Text;
using DesktopRider.Core.Desktop;

namespace DesktopRider.Overlay
{
    /// <summary>
    /// The current user's sign-in list (HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run), read and written
    /// with plain Windows calls because Unity's class library has no registry access. Needs no administrator rights.
    /// </summary>
    public sealed class WindowsStartupRegistry : IStartupRegistry
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        static readonly UIntPtr CurrentUser = new UIntPtr(0x80000001u);
        const int KEY_QUERY_VALUE = 0x0001, KEY_SET_VALUE = 0x0002;
        const int REG_SZ = 1, ERROR_SUCCESS = 0, ERROR_FILE_NOT_FOUND = 2, ERROR_MORE_DATA = 234;

        public string? Read(string name)
        {
            var key = Open(KEY_QUERY_VALUE);
            try
            {
                int size = 0;
                int result = RegQueryValueExW(key, name, IntPtr.Zero, out int type, null, ref size);
                if (result == ERROR_FILE_NOT_FOUND) return null;
                if (result != ERROR_SUCCESS && result != ERROR_MORE_DATA) throw Failure("read", result);
                if (type != REG_SZ) return null;
                var buffer = new byte[size];
                result = RegQueryValueExW(key, name, IntPtr.Zero, out type, buffer, ref size);
                if (result != ERROR_SUCCESS) throw Failure("read", result);
                return Encoding.Unicode.GetString(buffer, 0, size).TrimEnd('\0');
            }
            finally { RegCloseKey(key); }
        }

        public void Write(string name, string command)
        {
            var key = Open(KEY_SET_VALUE);
            try
            {
                var data = Encoding.Unicode.GetBytes(command + "\0");
                int result = RegSetValueExW(key, name, 0, REG_SZ, data, data.Length);
                if (result != ERROR_SUCCESS) throw Failure("written", result);
            }
            finally { RegCloseKey(key); }
        }

        public void Remove(string name)
        {
            var key = Open(KEY_SET_VALUE);
            try
            {
                int result = RegDeleteValueW(key, name);
                if (result != ERROR_SUCCESS && result != ERROR_FILE_NOT_FOUND) throw Failure("removed", result);
            }
            finally { RegCloseKey(key); }
        }

        static IntPtr Open(int access)
        {
            int result = RegOpenKeyExW(CurrentUser, RunKey, 0, access, out IntPtr key);
            if (result != ERROR_SUCCESS) throw Failure("opened", result);
            return key;
        }

        // Access refusals are reported the way .NET reports them, so the caller's single catch covers every case.
        static Exception Failure(string what, int code) => code == 5
            ? new UnauthorizedAccessException("The sign-in list could not be " + what + " (access denied).")
            : (Exception)new InvalidOperationException("The sign-in list could not be " + what + " (Windows error " + code + ").");

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode)] static extern int RegOpenKeyExW(UIntPtr root, string subKey, int options, int access, out IntPtr key);
        [DllImport("advapi32.dll", CharSet = CharSet.Unicode)] static extern int RegQueryValueExW(IntPtr key, string name, IntPtr reserved, out int type, byte[]? data, ref int size);
        [DllImport("advapi32.dll", CharSet = CharSet.Unicode)] static extern int RegSetValueExW(IntPtr key, string name, int reserved, int type, byte[] data, int size);
        [DllImport("advapi32.dll", CharSet = CharSet.Unicode)] static extern int RegDeleteValueW(IntPtr key, string name);
        [DllImport("advapi32.dll")] static extern int RegCloseKey(IntPtr key);
    }
}

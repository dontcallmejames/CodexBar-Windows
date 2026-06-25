using System.Management;
using System.Net;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace CodexBar.Core.Providers.Antigravity;

/// <summary>
/// Discovers the running Antigravity language server by inspecting process command lines (WMI)
/// and mapping each match to its loopback listening ports (iphlpapi). Windows-only.
/// </summary>
public sealed partial class WindowsAntigravityProcessLocator : IAntigravityProcessLocator
{
    [GeneratedRegex(@"(^|[/\\])language[_-]server([_-][a-z0-9]+)*(\.exe)?(\s|$)", RegexOptions.IgnoreCase)]
    private static partial Regex LanguageServerPattern();

    [GeneratedRegex(@"(^|[/\\])(antigravity[-_]cli|agy)(\.exe)?(\s|$|[/\\])", RegexOptions.IgnoreCase)]
    private static partial Regex CliPattern();

    [GeneratedRegex(@"--csrf_token[=\s]+(\S+)")]
    private static partial Regex CsrfPattern();

    [GeneratedRegex(@"--extension_server_port[=\s]+(\d+)")]
    private static partial Regex ExtensionPortPattern();

    [GeneratedRegex(@"--extension_server_csrf_token[=\s]+(\S+)")]
    private static partial Regex ExtensionCsrfPattern();

    public IReadOnlyList<AntigravityCandidate> FindCandidates()
    {
        var candidates = new List<AntigravityCandidate>();
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT ProcessId, Name, ExecutablePath, CommandLine FROM Win32_Process");
            foreach (var item in searcher.Get())
            {
                using var process = item;
                var commandLine = process["CommandLine"] as string ?? string.Empty;
                var exePath = process["ExecutablePath"] as string ?? string.Empty;
                var haystack = $"{exePath} {commandLine}";

                var isCli = CliPattern().IsMatch(haystack);
                var isLanguageServer = LanguageServerPattern().IsMatch(haystack) && HasAntigravityMarker(commandLine);
                if (!isCli && !isLanguageServer)
                {
                    continue;
                }

                var pid = Convert.ToInt32(process["ProcessId"]);
                var ports = ListeningLoopbackPorts(pid);
                if (ports.Count == 0)
                {
                    continue;
                }

                var csrf = Match(CsrfPattern(), commandLine);
                // The agy CLI requires no token; the IDE language server does. Skip IDE matches
                // that expose no token — there is nothing we can authenticate with.
                if (!isCli && string.IsNullOrEmpty(csrf))
                {
                    continue;
                }

                var extPortText = Match(ExtensionPortPattern(), commandLine);
                int? extPort = int.TryParse(extPortText, out var p) ? p : null;

                candidates.Add(new AntigravityCandidate(
                    pid,
                    ports,
                    csrf ?? string.Empty,
                    extPort,
                    Match(ExtensionCsrfPattern(), commandLine),
                    isCli));
            }
        }
        catch (ManagementException)
        {
            // WMI unavailable or query rejected — treat as "not found".
        }

        // CLI candidates (empty-token, simplest path) first.
        return candidates.OrderByDescending(c => c.IsCli).ToList();
    }

    private static bool HasAntigravityMarker(string commandLine) =>
        commandLine.Contains("--app_data_dir antigravity", StringComparison.OrdinalIgnoreCase) ||
        commandLine.Contains("antigravity", StringComparison.OrdinalIgnoreCase);

    private static string? Match(Regex pattern, string text)
    {
        var match = pattern.Match(text);
        return match.Success ? match.Groups[1].Value : null;
    }

    // --- iphlpapi: list the loopback TCP ports a given PID is LISTENING on ---

    private const int AF_INET = 2;
    private const int TCP_TABLE_OWNER_PID_LISTENER = 3;

    [StructLayout(LayoutKind.Sequential)]
    private struct MIB_TCPROW_OWNER_PID
    {
        public uint state;
        public uint localAddr;
        public uint localPort;
        public uint remoteAddr;
        public uint remotePort;
        public uint owningPid;
    }

    [LibraryImport("iphlpapi.dll", SetLastError = true)]
    private static partial uint GetExtendedTcpTable(
        IntPtr pTcpTable, ref int pdwSize, [MarshalAs(UnmanagedType.Bool)] bool bOrder,
        int ulAf, int tableClass, uint reserved);

    private static IReadOnlyList<int> ListeningLoopbackPorts(int pid)
    {
        var ports = new List<int>();
        AddIpv4LoopbackPorts(ports, pid);
        AddIpv6LoopbackPorts(ports, pid);
        return ports;
    }

    private static void AddIpv4LoopbackPorts(List<int> ports, int pid)
    {
        int size = 0;
        GetExtendedTcpTable(IntPtr.Zero, ref size, true, AF_INET, TCP_TABLE_OWNER_PID_LISTENER, 0);
        if (size == 0)
        {
            return;
        }

        var table = Marshal.AllocHGlobal(size);
        try
        {
            if (GetExtendedTcpTable(table, ref size, true, AF_INET, TCP_TABLE_OWNER_PID_LISTENER, 0) != 0)
            {
                return;
            }

            int count = Marshal.ReadInt32(table);
            var rowPtr = table + 4;
            int rowSize = Marshal.SizeOf<MIB_TCPROW_OWNER_PID>();
            for (int i = 0; i < count; i++)
            {
                var row = Marshal.PtrToStructure<MIB_TCPROW_OWNER_PID>(rowPtr);
                rowPtr += rowSize;
                if (row.owningPid != (uint)pid)
                {
                    continue;
                }

                if (new IPAddress(row.localAddr).Equals(IPAddress.Loopback))
                {
                    ports.Add(NetworkOrderPort(row.localPort));
                }
            }
        }
        finally
        {
            Marshal.FreeHGlobal(table);
        }
    }

    // MIB_TCP6ROW_OWNER_PID is 56 bytes: localAddr[16], localScopeId(4), localPort(4),
    // remoteAddr[16], remoteScopeId(4), remotePort(4), state(4), owningPid(4). Read the fields we
    // need by offset rather than marshalling the fixed-size address arrays.
    private const int AF_INET6 = 23;
    private const int Ipv6RowSize = 56;
    private const int Ipv6LocalPortOffset = 20;
    private const int Ipv6OwningPidOffset = 52;

    private static void AddIpv6LoopbackPorts(List<int> ports, int pid)
    {
        int size = 0;
        GetExtendedTcpTable(IntPtr.Zero, ref size, true, AF_INET6, TCP_TABLE_OWNER_PID_LISTENER, 0);
        if (size == 0)
        {
            return;
        }

        var table = Marshal.AllocHGlobal(size);
        try
        {
            if (GetExtendedTcpTable(table, ref size, true, AF_INET6, TCP_TABLE_OWNER_PID_LISTENER, 0) != 0)
            {
                return;
            }

            int count = Marshal.ReadInt32(table);
            var rowPtr = table + 4;
            var address = new byte[16];
            for (int i = 0; i < count; i++)
            {
                if ((uint)Marshal.ReadInt32(rowPtr + Ipv6OwningPidOffset) == (uint)pid)
                {
                    Marshal.Copy(rowPtr, address, 0, 16);
                    if (new IPAddress(address).Equals(IPAddress.IPv6Loopback))
                    {
                        ports.Add(NetworkOrderPort((uint)Marshal.ReadInt32(rowPtr + Ipv6LocalPortOffset)));
                    }
                }

                rowPtr += Ipv6RowSize;
            }
        }
        finally
        {
            Marshal.FreeHGlobal(table);
        }
    }

    // localPort is the port in network byte order packed into the low word of the DWORD.
    private static int NetworkOrderPort(uint localPort) =>
        ((int)(localPort & 0xFF) << 8) | (int)((localPort >> 8) & 0xFF);
}

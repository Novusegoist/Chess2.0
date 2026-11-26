using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Chess2.Core.Net;

/// <summary>Помощники для игры по IP: адреса этого компьютера и разбор «адрес:порт».</summary>
public static class NetInfo
{
    /// <summary>
    /// IPv4-адреса этого компьютера с названием сети («Ethernet: 192.168.1.5», «Radmin VPN: 26.1.2.3»).
    /// Их хост сообщает сопернику.
    /// </summary>
    public static List<string> LocalAddresses()
    {
        var result = new List<string>();
        try
        {
            foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up || nic.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                    continue;
                foreach (UnicastIPAddressInformation addr in nic.GetIPProperties().UnicastAddresses)
                {
                    if (addr.Address.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(addr.Address))
                        continue;
                    string ip = addr.Address.ToString();
                    if (ip.StartsWith("169.254.", StringComparison.Ordinal))
                        continue; // адрес «без сети» — бесполезен
                    result.Add($"{nic.Name}: {ip}");
                }
            }
        }
        catch (Exception)
        {
            // На некоторых платформах список сетей недоступен — просто не покажем его.
        }
        return result;
    }

    /// <summary>«1.2.3.4» или «1.2.3.4:7777» → адрес и порт.</summary>
    public static bool TryParseEndpoint(string text, out string host, out int port)
    {
        host = (text ?? "").Trim();
        port = TcpTransport.DefaultPort;
        if (host.Length == 0)
            return false;

        int colon = host.LastIndexOf(':');
        if (colon > 0)
        {
            if (!int.TryParse(host.Substring(colon + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out port)
                || port <= 0 || port > 65535)
                return false;
            host = host.Substring(0, colon).Trim();
        }
        return host.Length > 0;
    }
}

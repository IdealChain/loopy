using Loopy.Core.Data;
using System.Diagnostics;
using System.Net;

namespace Loopy.Comm.Interfaces;

public static class NetMQRpcDefaults
{
    public const ushort NodeApiPort = 1337;
    public const ushort ClientApiPort = 1338;

    public static string Localhost(NodeId id)
    {
        Debug.Assert(id.Id >= 0 && id.Id < 0xFFFFFF);
        return IPAddress.Parse("127.0.0.1").Offset(id.Id).ToString();
    }

    private static IPAddress Offset(this IPAddress @base, int offset)
    {
        checked
        {
            var addr = BitConverter.ToUInt32(@base.GetAddressBytes().Reverse().ToArray());
            addr += (uint)offset;
            return new IPAddress(BitConverter.GetBytes(addr).Reverse().ToArray());
        }
    }
}

using System.Net.Sockets;
namespace SoundMesh;
static class NetFailure {
    public static string Describe(Exception e)=>e switch {
        OperationCanceledException or TimeoutException => "超时",
        SocketException se when se.SocketErrorCode==SocketError.ConnectionRefused => "对端拒绝连接，请确认已开始融合",
        SocketException se when se.SocketErrorCode is SocketError.NetworkUnreachable or SocketError.HostUnreachable => "路由不可达",
        IOException when e.InnerException is SocketException se => se.SocketErrorCode.ToString(),
        EndOfStreamException => "对端已关闭连接",
        _=>e.Message
    };
}
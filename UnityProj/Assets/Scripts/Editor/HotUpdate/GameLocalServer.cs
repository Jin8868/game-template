using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>无需额外安装环境的本机补丁静态文件服务器。</summary>
    [InitializeOnLoad]
    public static class GameLocalServer
    {
        private static volatile TcpListener m_listener; // 监听 Boot 配置指定的本机地址和端口。
        private static Thread m_thread; // 不访问 Unity API 的文件服务线程。
        private static string m_root; // 服务器允许读取的绝对发布根目录。
        private static string m_serverURL; // 启动时固定的 Boot 服务器地址。
        private static string m_urlPrefix; // 配置中的服务器路径前缀。
        private static volatile string m_error; // 最近一次请求错误，供状态菜单读取。

        static GameLocalServer()
        {
            AssemblyReloadEvents.beforeAssemblyReload += Stop;
            EditorApplication.quitting += Stop;
        }

        [MenuItem("★AlloyFramework★/热更新/启动本地服务器")]
        private static void Start()
        {
            if (m_listener != null)
            {
                Debug.Log($"[HotUpdate] 本地服务器已运行：{m_serverURL}");
                return;
            }
            m_root = Path.GetFullPath("BuildArtifacts/LocalCDN") + Path.DirectorySeparatorChar;
            Directory.CreateDirectory(m_root);
            var server = new Uri(GameHotUpdateSettings.Load().ServerURL);
            if (server.Scheme != Uri.UriSchemeHttp)
            {
                throw new InvalidOperationException("本地测试服务器提供 HTTP；HTTPS 地址请使用已有远端服务器。");
            }
            IPAddress address;
            if (server.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
            {
                address = IPAddress.Loopback;
            }
            else if (!IPAddress.TryParse(server.Host, out address))
            {
                throw new InvalidOperationException("本地服务器需要本机 IP 或 localhost；域名地址请使用远端服务器。");
            }
            var listener = new TcpListener(address, server.Port);
            listener.Start();
            m_serverURL = server.AbsoluteUri.TrimEnd('/');
            m_urlPrefix = server.AbsolutePath.TrimEnd('/') + "/";
            m_listener = listener;
            m_error = null;
            m_thread = new Thread(Serve) { IsBackground = true, Name = "GameLocalCDN" };
            m_thread.Start(listener);
            Debug.Log($"[HotUpdate] 本地服务器已启动：{m_serverURL}，目录={m_root}");
        }

        [MenuItem("★AlloyFramework★/热更新/停止本地服务器")]
        private static void Stop()
        {
            TcpListener listener = m_listener;
            m_listener = null;
            listener?.Stop();
            m_thread?.Join(200);
            m_thread = null;
        }

        [MenuItem("★AlloyFramework★/热更新/查询本地服务器状态")]
        private static void ShowStatus()
        {
            Debug.Log(m_listener == null ? "[HotUpdate] 本地服务器未运行。" :
                $"[HotUpdate] 本地服务器运行中：{m_serverURL}；最近错误={m_error ?? "无"}");
        }

        [MenuItem("★AlloyFramework★/热更新/打开发布目录")]
        private static void OpenDirectory()
        {
            Directory.CreateDirectory("BuildArtifacts/LocalCDN");
            EditorUtility.RevealInFinder(Path.GetFullPath("BuildArtifacts/LocalCDN"));
        }

        private static void Serve(object state)
        {
            var listener = (TcpListener)state;
            while (ReferenceEquals(m_listener, listener))
            {
                try
                {
                    using (TcpClient client = listener.AcceptTcpClient())
                    {
                        client.ReceiveTimeout = 5000;
                        client.SendTimeout = 30000;
                        using (NetworkStream stream = client.GetStream())
                        {
                            ServeRequest(stream);
                        }
                    }
                }
                catch (Exception exception)
                {
                    if (ReferenceEquals(m_listener, listener))
                    {
                        m_error = exception.Message;
                    }
                }
            }
        }

        private static void ServeRequest(NetworkStream stream)
        {
            // 限制请求头大小，只支持静态文件 GET 和 HEAD，禁止路径逃逸。
            var header = new StringBuilder();
            while (header.Length < 16384)
            {
                int next = stream.ReadByte();
                if (next < 0)
                {
                    return;
                }
                header.Append((char)next);
                if (header.Length >= 4 && header.ToString(header.Length - 4, 4) == "\r\n\r\n")
                {
                    break;
                }
            }
            string[] request = header.ToString().Split('\r')[0].Split(' ');
            if (request.Length < 2 || (request[0] != "GET" && request[0] != "HEAD"))
            {
                WriteHeader(stream, "405 Method Not Allowed", 0);
                return;
            }
            string requestPath = request[1].Split('?')[0];
            if (!requestPath.StartsWith(m_urlPrefix, StringComparison.Ordinal))
            {
                WriteHeader(stream, "404 Not Found", 0);
                return;
            }
            string relative = Uri.UnescapeDataString(requestPath.Substring(m_urlPrefix.Length));
            string path = Path.GetFullPath(Path.Combine(m_root, relative));
            if (!path.StartsWith(m_root, StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
            {
                WriteHeader(stream, "404 Not Found", 0);
                return;
            }
            using (FileStream file = File.OpenRead(path))
            {
                WriteHeader(stream, "200 OK", file.Length);
                if (request[0] == "GET")
                {
                    file.CopyTo(stream);
                }
            }
        }

        private static void WriteHeader(NetworkStream stream, string status, long length)
        {
            byte[] bytes = Encoding.ASCII.GetBytes($"HTTP/1.1 {status}\r\nContent-Length: {length}\r\n" +
                "Content-Type: application/octet-stream\r\nCache-Control: no-store\r\nConnection: close\r\n\r\n");
            stream.Write(bytes, 0, bytes.Length);
        }
    }
}

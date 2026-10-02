using System;
using System.Reactive.Linq;
using Websocket.Client;
using System.Threading.Tasks;

public class ProtocolClient
{
    private WebsocketClient client;
    private readonly Uri serverUri;
    string deviceName = "", _accessKey = "";
    private IDisposable heartbeatTimer;

    public event Action<string> OnLog;
    public event Action<string> OnMessage;
    public event Action OnConnected;
    public event Action OnDisconnected;

    public ProtocolClient(string url, string targetDeviceName, string accessKey)
    {
        _accessKey = accessKey;
        deviceName = targetDeviceName;
        serverUri = new Uri(url);
    }

    public async Task StartAsync()
    {
        var ws = new WebsocketClient(serverUri);
        ws.ReconnectTimeout = TimeSpan.FromSeconds(30);
        ws.ErrorReconnectTimeout = TimeSpan.FromSeconds(5);

        client = ws;

        client.MessageReceived.Where(msg => msg.Text != null).Subscribe(msg =>
        {
            OnMessage?.Invoke(msg.Text);
        });

        client.DisconnectionHappened.Subscribe(info =>
        {
            OnLog?.Invoke($"Disconnected: {info.Type}");
            OnDisconnected?.Invoke();
        });

        await client.Start();

        OnLog?.Invoke("Connected!");
        OnConnected?.Invoke();

        StartHeartbeat();
        OnLog?.Invoke("Connection INIT");
        Send($"{{\"type\":\"INIT\", \"name\": \"{deviceName}\",\"accessKey\":\"{_accessKey}\"}}");
    }

    private void StartHeartbeat()
    {
        heartbeatTimer = Observable.Interval(TimeSpan.FromSeconds(10)).Subscribe(_ => 
        { 
            SendKeepAlive(deviceName);
        });
    }

    public void SendKeepAlive(string name)
    {
        string json = $"{{\"type\":\"KEEP_ME_ALIVE\",\"name\":\"{name}\"}}";
        client.Send(json);
        OnLog?.Invoke($"Sent KEEP_ME_ALIVE: {json}");
    }

    public void Send(string json)
    {
        client.Send(json);
        OnLog?.Invoke($"Sent: {json}");
    }

    public void Stop()
    {
        client?.Stop(System.Net.WebSockets.WebSocketCloseStatus.NormalClosure, "Client closed");
        heartbeatTimer?.Dispose();
        OnLog?.Invoke("Connection closed.");
    }
}
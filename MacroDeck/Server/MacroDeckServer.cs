using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using Fleck;
using MacroDeck.RPC.Enum;
using MacroDeck.RPC.Exceptions;
using MacroDeck.RPC;
using Newtonsoft.Json.Linq;
using SuchByte.MacroDeck.Device;
using SuchByte.MacroDeck.Enums;
using SuchByte.MacroDeck.Folders;
using SuchByte.MacroDeck.JSON;
using SuchByte.MacroDeck.Language;
using SuchByte.MacroDeck.Logging;
using SuchByte.MacroDeck.Profiles;
using static System.Threading.Tasks.Task;
using MessageBox = SuchByte.MacroDeck.GUI.CustomControls.MessageBox;
using System.Text.Json.Nodes;
using MacroDeck.RPC.Models;
using SuchByte.MacroDeck.Factories;
using SuchByte.MacroDeck.Interfaces;
using SuchByte.MacroDeck.Server.V3;
using SuchByte.MacroDeck.Utils;
using SuchByte.MacroDeck.Icons;
using System.Collections.Concurrent;
using SuchByte.MacroDeck.Parsers;

namespace SuchByte.MacroDeck.Server;

public class MacroDeckServer : IObservable<RpcAction>
{
    public static MacroDeckServer Instance { get; } = new();

    public event EventHandler? OnDeviceConnectionStateChanged;
    public event EventHandler? OnServerStateChanged;
    public event EventHandler? OnFolderChanged;

    public WebSocketServer? WebSocketServer { get; private set; }

    public List<MacroDeckClient> Clients { get; } = new();
    private List<IObserver<RpcAction>> Observers { get; } = new();

    public MacroDeckServer()
    {
        var factoryHelper = new RCPHandlerFactoryHelper();
         IRpcHandlerFactory rpcHandlerFactory = new RpcHandlerFactory(factoryHelper.GetHandlers);
         var rcpDispatcher = new RpcDispatcher(rpcHandlerFactory);
         Subscribe(rcpDispatcher);
    }

    public IDisposable Subscribe(IObserver<RpcAction> observer)
    {
        Observers.Add(observer);

        return new DisposeAction(delegate { Observers.Remove(observer); });
    }

    internal class DisposeAction : IDisposable
    {
        private readonly Action _disposeAction;

        public DisposeAction(Action disposeAction)
        {
            _disposeAction = disposeAction;
        }

        public void Dispose() => _disposeAction();
    }

    public void Start(int port)
    {
        DeviceManager.LoadKnownDevices();
        StartWebSocketServer(IPAddress.Any.ToString(), port);
    }

    private void StartWebSocketServer(string ipAddress, int port)
    {
        if (WebSocketServer != null)
        {
            MacroDeckLogger.Info("Stopping websocket server...");
            foreach (var macroDeckClient in Clients.Where(macroDeckClient => macroDeckClient.SocketConnection is { IsAvailable: true }))
            {
                macroDeckClient.SocketConnection.Close();
            }
            WebSocketServer.Dispose();
            Clients.Clear();
            MacroDeckLogger.Info("Websocket server stopped");
            OnServerStateChanged?.Invoke(WebSocketServer, EventArgs.Empty);
        }
        MacroDeckLogger.Info($"Starting websocket server @ {ipAddress}:{port}");
        WebSocketServer = new WebSocketServer("ws://" + ipAddress + ":" + port);
        WebSocketServer.ListenerSocket.NoDelay = true;
        try
        {
            WebSocketServer.Start(socket =>
            {
                var macroDeckClient = new MacroDeckClient(socket);
                socket.OnOpen = () => OnOpen(macroDeckClient);
                socket.OnClose = () => OnClose(macroDeckClient);
                socket.OnError = delegate { OnClose(macroDeckClient); };
                socket.OnMessage = message => OnMessage(macroDeckClient, message);
            });
            OnServerStateChanged?.Invoke(WebSocketServer, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            OnServerStateChanged?.Invoke(WebSocketServer, EventArgs.Empty);

            MacroDeckLogger.Error("Failed to start server: " + ex.Message + Environment.NewLine + ex.StackTrace);

            using var msgBox = new MessageBox();
            msgBox.ShowDialog(LanguageManager.Strings.Error, LanguageManager.Strings.FailedToStartServer + Environment.NewLine + ex.Message, MessageBoxButtons.OK);
        }
    }

    private void OnOpen(MacroDeckClient macroDeckClient)
    {
        if (MacroDeck.Configuration.BlockNewConnections ||
            Clients.Count >= 10 ||
            ProfileManager.CurrentProfile?.Folders.Count < 1)
        {
            CloseClient(macroDeckClient);
            return;
        }

        Clients.Add(macroDeckClient);
    }

    private void OnClose(MacroDeckClient macroDeckClient)
    {
        Clients.Remove(macroDeckClient);
        MacroDeckLogger.Info(macroDeckClient.ClientId + " connection closed");
        OnDeviceConnectionStateChanged?.Invoke(macroDeckClient, EventArgs.Empty);
    }

    /// <summary>
    /// Closes the connection
    /// </summary>
    /// <param name="macroDeckClient"></param>
    public void CloseClient(MacroDeckClient macroDeckClient)
    {
        if (macroDeckClient?.SocketConnection == null ||
            !macroDeckClient.SocketConnection.IsAvailable) return;
        MacroDeckLogger.Info("Close connection to " + macroDeckClient.ClientId);
        macroDeckClient.SocketConnection.Close();
    }
        

    private void OnMessage(MacroDeckClient macroDeckClient, string jsonMessageString)
    {
        var jsonObject = JObject.Parse(jsonMessageString);
        if (macroDeckClient.ProtocolVersion == DeviceProtocolVersion.Unknown)
        {
            if (jsonObject["jsonrpc"] != null)
            {
                macroDeckClient.ProtocolVersion = DeviceProtocolVersion.V3;
            } else if (jsonObject["Method"] != null)
            {
                macroDeckClient.ProtocolVersion = DeviceProtocolVersion.V2;
            }
        }

        switch (macroDeckClient.ProtocolVersion)
        {
            case DeviceProtocolVersion.V2:
                Run(() => ProcessV2Async(macroDeckClient, jsonObject));
                break;
            case DeviceProtocolVersion.V3:
                Run(() => ProcessV3Async(macroDeckClient, jsonMessageString));
                break;
        }
    }

    #region Process v3 request
    private Task ProcessV3Async(MacroDeckClient macroDeckClient, string message)
    {
        MacroDeckLogger.Trace(message);
        return Run(() =>
        {
            try
            {
                var request = JsonSerializer.Deserialize<Request>(message);
                if (request == null) return;

                MacroDeckLogger.Trace($"Invoking Request! {request}");
                foreach (var observer in Observers)
                {
                    Task Callback(object? result)
                    {
                        var response = new Response { Id = request.Id, Result = result };

                        macroDeckClient.SocketConnection?.Send(JsonSerializer.Serialize(response));
                        return CompletedTask;
                    }

                    var actionable = new RpcAction(macroDeckClient, request, Callback);
                    observer.OnNext(actionable);
                }
            }
            catch (ActionException ae)
            {
                ReturnErrorFromException("Action error:", ae.Message, message, macroDeckClient, ae.Code);
            }
            catch (JsonException je)
            {
                ReturnErrorFromException("Json invalid:", je.Message, message, macroDeckClient, ErrorCode.JsonFormat);
            }
            catch (Exception ex)
            {
                ReturnErrorFromException("Unknown error occurred during message processing:", ex.Message, message, macroDeckClient, ErrorCode.Generic);
            }
        });
    }

    private static void ReturnErrorFromException(string exceptionMessageContext, string exceptionMessage, string message, MacroDeckClient macroDeckClient, ErrorCode errorCode = ErrorCode.Generic)
    {
        MacroDeckLogger.Error($"WebSocket Server Error: {exceptionMessageContext} {exceptionMessage}");
        var error = new Error(errorCode, $"{exceptionMessage}");
        string? id = null;
        try
        {
            var root = JsonNode.Parse(message);
            id = root?["Id"]?.ToString();
        }
        catch
        {
            // ignored
        }

        var response = new Response { Error = error, Id = id };
        macroDeckClient.SocketConnection?.Send(JsonSerializer.Serialize(response));
    }
    #endregion
    
    #region Process v2 request
    private Task ProcessV2Async(MacroDeckClient macroDeckClient, JObject jsonObject)
    {
        return Run(() =>
        {
            if (jsonObject["Method"] == null) return;

            if (!Enum.TryParse<JsonMethod>(jsonObject["Method"].ToString(), out var method)) return;

            MacroDeckLogger.Trace("Received method: " + method);

            switch (method)
            {
                case JsonMethod.CONNECTED:
                    if (jsonObject["API"] == null || jsonObject["Client-Id"] == null || jsonObject["Device-Type"] == null || jsonObject["API"].ToObject<int>() < MacroDeck.ApiVersion)
                    {
                        CloseClient(macroDeckClient);
                        return;
                    }

                    macroDeckClient.ClientId = jsonObject["Client-Id"].ToString();

                    MacroDeckLogger.Info("Connection request from " + macroDeckClient.ClientId);

                    var deviceType = DeviceType.Unknown;
                    Enum.TryParse(jsonObject["Device-Type"].ToString(), out deviceType);
                    macroDeckClient.DeviceType = deviceType;

                    if (!DeviceManager.RequestConnection(macroDeckClient))
                    {
                        CloseClient(macroDeckClient);
                        return;
                    }

                    if (!macroDeckClient.SocketConnection.IsAvailable || DeviceManager.GetMacroDeckDevice(macroDeckClient.ClientId) == null)
                    {
                        return;
                    }

                    if (string.IsNullOrWhiteSpace(DeviceManager.GetMacroDeckDevice(macroDeckClient.ClientId).ProfileId))
                    {
                        DeviceManager.GetMacroDeckDevice(macroDeckClient.ClientId).ProfileId = ProfileManager.Profiles.FirstOrDefault().ProfileId;
                    }

                    DeviceManager.SaveKnownDevices();

                    macroDeckClient.Profile = ProfileManager.FindProfileById(DeviceManager.GetMacroDeckDevice(macroDeckClient.ClientId).ProfileId);

                    if (macroDeckClient.Profile == null)
                    {
                        macroDeckClient.Profile = ProfileManager.Profiles.FirstOrDefault();
                    }

                    macroDeckClient.Folder = macroDeckClient.Profile.Folders.FirstOrDefault();

                    macroDeckClient.DeviceMessage.Connected(macroDeckClient);


                    InvokeOnDeviceConnectionStateChanged(macroDeckClient);
                    MacroDeckLogger.Info(macroDeckClient.ClientId + " connected");
                    break;
                case JsonMethod.BUTTON_PRESS:
                case JsonMethod.BUTTON_RELEASE:
                case JsonMethod.BUTTON_LONG_PRESS:
                case JsonMethod.BUTTON_LONG_PRESS_RELEASE:
                    var buttonPressType = method switch
                    {
                        JsonMethod.BUTTON_PRESS => ButtonPressType.SHORT,
                        JsonMethod.BUTTON_RELEASE => ButtonPressType.SHORT_RELEASE,
                        JsonMethod.BUTTON_LONG_PRESS => ButtonPressType.LONG,
                        JsonMethod.BUTTON_LONG_PRESS_RELEASE => ButtonPressType.LONG_RELEASE,
                        _ => ButtonPressType.SHORT
                    };

                    try
                    {
                        if (macroDeckClient == null || macroDeckClient.Folder == null || macroDeckClient.Folder.ActionButtons == null) return;
                        var row = int.Parse(jsonObject["Message"].ToString().Split('_')[0]);
                        var column = int.Parse(jsonObject["Message"].ToString().Split('_')[1]);

                        var actionButton = macroDeckClient.Folder.ActionButtons.Find(aB => aB.Position_X == column && aB.Position_Y == row);
                        if (actionButton != null)
                        {
                            Execute(actionButton, macroDeckClient.ClientId, buttonPressType);
                        }
                    }
                    catch (Exception ex)
                    {
                        MacroDeckLogger.Warning("Action button long press release caused an exception: " + ex.Message);
                    }
                    break;
                case JsonMethod.GET_BUTTONS:
                    Run(() => SendAllButtonsAsync(macroDeckClient));
                    break;
            }
        });
    }
    #endregion


    internal void Execute(ActionButton.ActionButton actionButton, string clientId, ButtonPressType buttonPressType)
    {
        var actions = buttonPressType switch
        {
            ButtonPressType.SHORT => actionButton.Actions,
            ButtonPressType.SHORT_RELEASE => actionButton.ActionsRelease,
            ButtonPressType.LONG => actionButton.ActionsLongPress,
            ButtonPressType.LONG_RELEASE => actionButton.ActionsLongPressRelease,
            _ => actionButton.Actions
        };

        Run(() =>
        {
            foreach (var action in actions)
            {
                try
                {
                    action.Trigger(clientId, actionButton);
                }
                catch
                {
                    // ignored
                }
            }
        });
    }

    internal void SetAuthorized(string clientId, bool authorized)
    {
        var macroDeckClient = Clients.FirstOrDefault(client => client.ClientId == clientId);
        if (macroDeckClient != null) SetAuthorized(macroDeckClient, authorized);
    }

    internal void SetAuthorized(MacroDeckClient macroDeckClient, bool authorized)
    {
        macroDeckClient.IsAuthorized = authorized;
        var clientOfList = Clients.FirstOrDefault(x => x.ClientId == macroDeckClient.ClientId);
        MacroDeckLogger.Trace("Authorized: " + clientOfList.IsAuthorized);
    }


    public void SetProfile(string clientId, MacroDeckProfile macroDeckProfile)
    {
        var macroDeckClient = Clients.FirstOrDefault(client => client.ClientId == clientId);
        if (macroDeckClient != null) SetProfile(macroDeckClient, macroDeckProfile);
    }
    /// <summary>
    /// Sets the current profile of a client
    /// </summary>
    /// <param name="macroDeckClient"></param>
    /// <param name="macroDeckProfile"></param>
    public void SetProfile(MacroDeckClient macroDeckClient, MacroDeckProfile macroDeckProfile)
    {
        macroDeckClient.Profile = macroDeckProfile;
        switch (macroDeckClient.ProtocolVersion)
        {
            case DeviceProtocolVersion.V2:
                macroDeckClient.DeviceMessage.SendConfiguration(macroDeckClient);
                break;
            case DeviceProtocolVersion.V3:
                SetFolder(macroDeckClient, macroDeckProfile.Folders.First());
                break;
        }
            
        SetFolder(macroDeckClient, macroDeckProfile.Folders[0]);
    }

    public void SetFolder(string clientId, MacroDeckFolder macroDeckFolder)
    {
        var macroDeckClient = Clients.FirstOrDefault(client => client.ClientId == clientId);
        if (macroDeckClient != null) SetFolder(macroDeckClient, macroDeckFolder);
    }

    /// <summary>
    /// Sets the current folder of a client
    /// </summary>
    /// <param name="macroDeckClient"></param>
    /// <param name="folder"></param>
    public void SetFolder(MacroDeckClient macroDeckClient, MacroDeckFolder folder)
    {
        macroDeckClient.Folder = folder;
        Run(() => SendAllButtonsAsync(macroDeckClient));
        OnFolderChanged?.Invoke(macroDeckClient, EventArgs.Empty);
    }

    /// <summary>
    /// Updates the folder on all clients with this folder as the current folder
    /// </summary>
    /// <param name="folder"></param>
    public void UpdateFolder(MacroDeckFolder folder)
    {
        foreach (var macroDeckClient in Clients.FindAll(macroDeckClient => macroDeckClient.Folder.Equals(folder)))
        {
            Run(() => SendAllButtonsAsync(macroDeckClient));
        }
    }

    /// <summary>
    /// Sends all buttons of the current folder to the client
    /// </summary>
    /// <param name="macroDeckClient"></param>
    internal static Task SendAllButtonsAsync(MacroDeckClient macroDeckClient)
    {
        if (macroDeckClient.Folder is null) return FromResult(0);
        return Run(() =>
        {
            switch (macroDeckClient.ProtocolVersion)
            {
                case DeviceProtocolVersion.V2:
                    macroDeckClient?.DeviceMessage?.SendAllButtons(macroDeckClient);
                    break;
                case DeviceProtocolVersion.V3:
                    var pageDto = macroDeckClient.Folder.ToPageDto();
                    var request = new Request("2.0", "SetPage", pageDto);
                    macroDeckClient.SocketConnection.Send(JsonSerializer.Serialize(request));
                    break;
            }
        });
    }

    /// <summary>
    /// Sends a single button to the client
    /// </summary>
    /// <param name="macroDeckClient"></param>
    /// <param name="actionButton"></param>
    public static Task SendButtonAsync(MacroDeckClient macroDeckClient, ActionButton.ActionButton actionButton)
    {
        return Run(() =>
        {
            switch (macroDeckClient.ProtocolVersion)
            {
                case DeviceProtocolVersion.V2:
                    macroDeckClient?.DeviceMessage?.UpdateButton(macroDeckClient, actionButton);
                    break;
                case DeviceProtocolVersion.V3:
                    var widgetDto = actionButton.ToWidgetDto();
                    var request = new Request("2.0", "UpdateWidget", widgetDto);
                    macroDeckClient.SocketConnection.Send(JsonSerializer.Serialize(request));
                    break;
            }
        });
    }

    /// <summary>
    /// Set button state on or off
    /// </summary>
    /// <param name="actionButton">ActionButton</param>
    /// <param name="state">State true = on, off = false</param>
    public void SetState(ActionButton.ActionButton actionButton, bool state)
    {
        actionButton.State = state;
    }

    internal void UpdateState(ActionButton.ActionButton actionButton)
    {
        foreach (var macroDeckClient in Clients.FindAll(macroDeckClient =>
                     macroDeckClient.Folder?.ActionButtons?.Contains(actionButton) ?? false))
        {
            Run(() => SendButtonAsync(macroDeckClient, actionButton));
        }
    }

    /// <summary>
    /// Get the MacroDeckClient from the client id
    /// </summary>
    /// <param name="macroDeckClientId">Client-ID</param>
    /// <returns></returns>
    public MacroDeckClient? GetMacroDeckClient(string macroDeckClientId)
    {
        return string.IsNullOrWhiteSpace(macroDeckClientId) ? null : Clients.Find(macroDeckClient => macroDeckClient.ClientId == macroDeckClientId);
    }

    /// <summary>
    /// Raw send function
    /// </summary>
    /// <param name="socketConnection"></param>
    /// <param name="jObject"></param>
    internal void Send(IWebSocketConnection socketConnection, JObject jObject)
    {
        socketConnection.Send(jObject.ToString());
    }
    public void InvokeOnDeviceConnectionStateChanged(MacroDeckClient macroDeckClient)
    {
        OnDeviceConnectionStateChanged?.Invoke(macroDeckClient, EventArgs.Empty);
    }
}
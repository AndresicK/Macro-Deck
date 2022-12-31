using System;
using System.Linq;
using System.Text.Json.Nodes;
using MacroDeck.RPC.Exceptions;
using SuchByte.MacroDeck.Device;
using SuchByte.MacroDeck.Interfaces;
using SuchByte.MacroDeck.Logging;
using SuchByte.MacroDeck.Profiles;

namespace SuchByte.MacroDeck.Server.V3.Handlers;


public sealed class RpcAuthHandler : IRpcHandler
{
    public string Command => "Auth";
    public object? Do(object? client, JsonNode? args)
    {
        IRpcHandler.ThrowIfParamsNull(args);
        IRpcHandler.ThrowIfClientNull(client);

        var clientId = args["Id"]?.ToString();
        var deviceTypeValid = Enum.TryParse(args["Type"]?.ToString(), out DeviceType deviceType);

        if (client is not MacroDeckClient macroDeckClient|| clientId is null || deviceTypeValid == false)
        {
            throw new ActionInvalidAuthException();
        }

        macroDeckClient.ClientId = clientId;
        macroDeckClient.DeviceType = deviceType;

        if (!DeviceManager.RequestConnection(macroDeckClient))
        {
            throw new ActionInvalidAuthException();
        }

        if (string.IsNullOrWhiteSpace(DeviceManager.GetMacroDeckDevice(macroDeckClient.ClientId).ProfileId))
        {
            DeviceManager.GetMacroDeckDevice(macroDeckClient?.ClientId).ProfileId = ProfileManager.Profiles.FirstOrDefault()?.ProfileId;
        }

        DeviceManager.SaveKnownDevices();
        
        MacroDeckServer.Instance.SetAuthorized(macroDeckClient.ClientId, true);
        var profile =
            ProfileManager.FindProfileById(DeviceManager.GetMacroDeckDevice(macroDeckClient?.ClientId)?.ProfileId) ??
            ProfileManager.Profiles.FirstOrDefault();
        var folder =
            macroDeckClient.Folder = profile.Folders.FirstOrDefault();
        
        MacroDeckServer.Instance.SetProfile(macroDeckClient.ClientId, profile);
        MacroDeckServer.Instance.SetFolder(macroDeckClient.ClientId, folder);

        MacroDeckServer.Instance.InvokeOnDeviceConnectionStateChanged(macroDeckClient);
        MacroDeckLogger.Info(macroDeckClient.ClientId + " connected");

        return "AuthOk";
    }
}
using Fleck;
using SuchByte.MacroDeck.Device;
using SuchByte.MacroDeck.Enums;
using SuchByte.MacroDeck.Folders;
using SuchByte.MacroDeck.Logging;
using SuchByte.MacroDeck.Model;
using SuchByte.MacroDeck.Profiles;
using SuchByte.MacroDeck.Server.DeviceMessage;

namespace SuchByte.MacroDeck.Server;

public class MacroDeckClient
{
    private DeviceType _deviceType;

    public MacroDeckClient(IWebSocketConnection socket)
    {
        SocketConnection = socket;
    }
    
    public IWebSocketConnection SocketConnection { get; }
    
    public bool IsAuthorized { get; set; }

    public MacroDeckFolder? Folder { get; set; }

    public MacroDeckProfile? Profile { get; set; }

    public string ClientId { get; set; }

    public DeviceProtocolVersion ProtocolVersion { get; set; } = DeviceProtocolVersion.Unknown;

    public DeviceClass DeviceClass { get; set; } = DeviceClass.SoftwareClient;


    public DeviceType DeviceType
    {
        get => _deviceType;
        set
        {
            _deviceType = value;
            switch (_deviceType)
            {
                case DeviceType.Unknown:
                case DeviceType.Web:
                case DeviceType.Android:
                case DeviceType.StreamDeck:
                case DeviceType.iOS:
                    DeviceClass = DeviceClass.SoftwareClient;
                    DeviceMessage = new SoftwareClientMessage();
                    break;
            }
        }
    }

    public IDeviceMessage DeviceMessage { get; set; }
}
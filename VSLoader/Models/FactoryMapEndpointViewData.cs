namespace VSLoader.Models;

public sealed class FactoryMapEndpointViewData
{
    public string Kind { get; set; } = FactoryMapEndpointKinds.Device;

    public string Id { get; set; } = string.Empty;

    public FactoryMapDeviceViewNode? Device { get; set; }

    public FactoryMapConnectorViewNode? Connector { get; set; }

    public string Key => Id;

    public string Name => Device?.Name ?? string.Empty;

    public double X
    {
        get => Device?.X ?? Connector?.X ?? 0;
        set
        {
            if (Device is not null)
            {
                Device.X = value;
            }
            else if (Connector is not null)
            {
                Connector.X = value;
            }
        }
    }

    public double Y
    {
        get => Device?.Y ?? Connector?.Y ?? 0;
        set
        {
            if (Device is not null)
            {
                Device.Y = value;
            }
            else if (Connector is not null)
            {
                Connector.Y = value;
            }
        }
    }

    public static FactoryMapEndpointViewData FromDevice(FactoryMapDeviceViewNode device)
    {
        return new FactoryMapEndpointViewData
        {
            Kind = FactoryMapEndpointKinds.Device,
            Id = device.Key,
            Device = device
        };
    }

    public static FactoryMapEndpointViewData FromConnector(FactoryMapConnectorViewNode connector)
    {
        return new FactoryMapEndpointViewData
        {
            Kind = FactoryMapEndpointKinds.Connector,
            Id = connector.Id,
            Connector = connector
        };
    }

    public static implicit operator FactoryMapEndpointViewData(FactoryMapDeviceViewNode device)
    {
        return FromDevice(device);
    }

    public static implicit operator FactoryMapEndpointViewData(FactoryMapConnectorViewNode connector)
    {
        return FromConnector(connector);
    }
}

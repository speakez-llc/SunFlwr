namespace SunFlwrXPlat.ViewModels

module BluetoothModule =

    open System
    open Plugin.BLE
    open Plugin.BLE.Abstractions.Contracts
    open Plugin.BLE.Abstractions.EventArgs

    type BLEConnectionResult =
        | Success of unit
        | Failure of string

    type BluetoothModel =
        {
            Devices: IDevice list
        }
    let discoverBLEDevices (dispatch: BluetoothModel -> unit) =
        async {
            try
                let adapter = CrossBluetoothLE.Current.Adapter
                do! adapter.StartScanningForDevicesAsync() |> Async.AwaitTask

                let devices = adapter.DiscoveredDevices
                let updatedModel = { Devices = List.ofSeq devices }
                dispatch updatedModel

                // Return the list of discovered devices
                return devices
            with
            | ex -> printfn($"Error while scanning BLE devices: {ex.Message}")
                    return [] // Return an empty list in case of an error
        }

    let connectBLEDevice (selectedDevice: IDevice) : Async<BLEConnectionResult> =
        async {
            printfn("Connecting to device...")
            
            try
                let adapter = CrossBluetoothLE.Current.Adapter
                let! connectedDevice = Async.AwaitTask(adapter.ConnectToDeviceAsync(selectedDevice))

                printfn("Connected to device.")
                printfn($"Device name: {selectedDevice.Name}")
                return Success connectedDevice
            with
            | ex -> 
                printfn($"Error while connecting to device: {ex.Message}")
                return Failure ex.Message // Return the error message as a Failure result
        }

    let getServicesFromDevice (connectedDevice: IDevice) : Async<IService array> =
        async {
            try
                let! services = Async.AwaitTask (connectedDevice.GetServicesAsync())
                let serviceArray = Seq.toArray services
                return serviceArray
            with
            | ex -> 
                printfn($"Error while getting services from device: {ex.Message}")
                return [||] // Return an empty array if there's an error
        }
        
    let getServiceByGuid (connectedDevice: IDevice) (serviceGuid: string) : Async<IService option> =
        async {
            try
                let! services = Async.AwaitTask (connectedDevice.GetServicesAsync())
                let matchingService = services
                                      |> Seq.tryFind (fun service -> service.Id = Guid.Parse(serviceGuid))
                return matchingService
            with
            | ex -> 
                printfn($"Error while getting service by GUID: {ex.Message}")
                return None
        }

    let getCharacteristicsFromService (service: IService) : Async<ICharacteristic[]> =
        async {
            try
                let! characteristics = Async.AwaitTask (service.GetCharacteristicsAsync())
                let characteristicArray = Seq.toArray characteristics
                return characteristicArray
            with
            | ex -> 
                printfn($"Error while getting characteristics from service: {ex.Message}")
                return [||] // Return an empty array if there's an error
        }
        
    let readCharacteristic (characteristic: ICharacteristic) : Async<byte[]> =
        async {
            try
                let! bytes = Async.AwaitTask (characteristic.ReadAsync())
                return bytes
            with
            | ex -> 
                printfn($"Error while reading characteristic: {ex.Message}")
                return [||] // Return an empty byte array if there's an error
        }
        
        
    let writeCharacteristic (characteristic: ICharacteristic) (data: byte[]) : Async<bool> =
        async {
            try
                let! success = Async.AwaitTask (characteristic.WriteAsync(data))
                printfn("Write operation successful.")
                return success
            with
            | ex -> 
                printfn($"Error while writing characteristic: {ex.Message}")
                return false
        }
        
    let characteristicValueChangedHandler (sender: obj) (args: CharacteristicUpdatedEventArgs) =
        let bytes = args.Characteristic.Value
        printfn $"Received data: %A{bytes}"

    let startCharacteristicNotifications (characteristic: ICharacteristic) : Async<unit> =
        async {
            try
                let handler = EventHandler<CharacteristicUpdatedEventArgs>(characteristicValueChangedHandler)
                characteristic.ValueUpdated.AddHandler(handler)
                do! Async.AwaitTask (characteristic.StartUpdatesAsync())
                printfn("Notifications started.")
            with
            | ex -> 
                printfn($"Error while starting characteristic notifications: {ex.Message}")
        }
        
    let getDescriptorsFromCharacteristic (characteristic: ICharacteristic) : Async<IDescriptor array> =
        async {
            try
                let! descriptors = Async.AwaitTask (characteristic.GetDescriptorsAsync())
                let descriptorArray = Seq.toArray descriptors
                return descriptorArray
            with
            | ex -> 
                printfn($"Error while getting descriptors from characteristic: {ex.Message}")
                return [||] // Return an empty array if there's an error
        }
        
    let readDescriptor (descriptor: IDescriptor) : Async<byte[]> =
        async {
            try
                let! bytes = Async.AwaitTask (descriptor.ReadAsync())
                return bytes
            with
            | ex -> 
                printfn($"Error while reading descriptor: {ex.Message}")
                return [||] // Return an empty byte array if there's an error
        }
        
    let writeDescriptor (descriptor: IDescriptor) (data: byte[]) : Async<bool> =
        async {
            try
                do! Async.AwaitTask (descriptor.WriteAsync(data))
                printfn("Write to descriptor successful.")
                return true
            with
            | ex -> 
                printfn($"Error while writing to descriptor: {ex.Message}")
                return false
        }
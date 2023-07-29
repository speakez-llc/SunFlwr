module SunFlwrXPlat.ViewModels.AngleViewModel

open Elmish
open Elmish.Avalonia
open Plugin.BLE.Abstractions.Contracts
open Messaging
open BluetoothModule

type Model = 
    {
        AngleValue: float
        IsScanning: bool
        Devices:  IDevice list
        SelectedDevice: IDevice option 
        IsConnected: bool 
    }

type Msg = 
    | Ok
    | MsgSent of unit
    | SearchForDevices
    | DevicesFound of IDevice list
    | SelectDevice of IDevice option
    | DeviceSelected of IDevice
    | ToggleConnection
    | ConnectionChanged of bool
    | GetAngle
    | AngleReceived of float

let init() = 
    { 
        AngleValue = 0.0
        IsScanning = false
        Devices = []
        SelectedDevice = None
        IsConnected = false
    }
    
let update (msg: Msg) (model: Model) = 
    match msg with
    | Ok -> 
        let sendMessage () = bus.OnNext(GlobalMsg.GoHome)
        model, Cmd.OfFunc.perform sendMessage () MsgSent

    | MsgSent _ -> 
        model, Cmd.none

    | SearchForDevices ->
        let searchForDevicesCmd : Cmd<Msg> =
            async {
                try
                    let! discoveredDevices = discoverBLEDevices DevicesFound
                    return DevicesFound discoveredDevices
                with
                | ex ->
                    printfn($"Error while scanning BLE devices: {ex.Message}")
                    return DevicesFound [] // Return an empty list in case of an error
            }
            |> Cmd.OfAsync.perform id

        { model with IsScanning = true }, searchForDevicesCmd
        
    | DevicesFound devices ->
        { model with Devices = devices; IsScanning = false }, Cmd.none

    | SelectDevice deviceOption ->
        { model with SelectedDevice = deviceOption }, Cmd.none

    | ConnectionChanged isConnected ->
        { model with IsConnected = isConnected }, Cmd.none

    | ToggleConnection ->
        let toggleConnection () = 
            match model.SelectedDevice with
            | Some selectedDevice -> 
                let connectionResult = BluetoothModule.connectBLEDevice selectedDevice
                match connectionResult with
                | Success _ -> ConnectionChanged true
                | Failure errorMessage ->
                    printfn($"Failed to connect: {errorMessage}")
                    ConnectionChanged false
            | None -> ConnectionChanged false
        model, Cmd.OfFunc.perform toggleConnection () id // Perform the toggleConnection function and return the result unchanged

    | GetAngle ->
        let getAngle () =  
            match model.SelectedDevice with
            | Some selectedDevice -> 
                let angleResult = getAngleFromDevice selectedDevice
                match angleResult with
                | Success angle -> AngleReceived angle
                | Failure errorMessage ->
                    printfn($"Error while getting angle: {errorMessage}")
                    AngleReceived 0.0 // Return a default angle value in case of failure
            | None -> AngleReceived 0.0 // Return a default angle value when no device is selected

        model, Cmd.OfFunc.perform getAngle () id // Perform the getAngle function and return the result unchanged

    | DeviceSelected bluetoothDevice -> failwith "todo"

    | AngleReceived angle ->
        { model with AngleValue = angle }, Cmd.none

let bindings ()  : Binding<Model, Msg> list = [
    "Ok" |> Binding.cmd Ok
    "SearchForDevices" |> Binding.cmd SearchForDevices
    "IsScanning" |> Binding.oneWay (fun m -> m.IsScanning)
    "ToggleConnection" |> Binding.cmd ToggleConnection
    "DeviceSelected" |> Binding.cmd DeviceSelected
    "GetAngle" |> Binding.cmd GetAngle
    "AngleValue" |> Binding.oneWay (fun m -> m.AngleValue)
]

let designVM = ViewModel.designInstance (init()) (bindings())

let vm = ElmishViewModel(AvaloniaProgram.mkProgram init update bindings)

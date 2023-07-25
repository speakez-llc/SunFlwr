namespace SunFlwrMeadow

open System
open System.Threading.Tasks
open Meadow.Foundation.Leds
open Meadow
open Meadow.Devices
open Meadow.Hardware
open Meadow.Gateways.Bluetooth
open SunFlwrMeadow.LedController
open Meadow.Foundation.Sensors.Motion
open Meadow.Units
open Helpers


type MeadowApp() =
    inherit App<F7FeatherV2>()

    let mutable Accelerometer : Adxl345 = null
    let mutable accelerometerData : Acceleration3D = Acceleration3D()
    let mutable boardYoffset = 0.0
    let mutable boardYangle = -22.0
    
    let rgbLed =
        RgbLed(MeadowApp.Device.Pins.OnboardLedRed, MeadowApp.Device.Pins.OnboardLedGreen, MeadowApp.Device.Pins.OnboardLedBlue)
    
    let mutable ledController = LedController.Create(rgbLed)

    let mutable On = Unchecked.defaultof<ICharacteristic>
    let mutable Off = Unchecked.defaultof<ICharacteristic>
    let mutable StartBlink = Unchecked.defaultof<ICharacteristic>
    let mutable StartRunningColors = Unchecked.defaultof<ICharacteristic>
    let mutable ReadAngle = Unchecked.defaultof<ICharacteristic>
          
    member private this.GetDefinition() =
        let Name = "MeadowRGB"
        let uuid: uint16 = 180us
        let onCharacteristic =
            CharacteristicInt32("On", 
                            "73cfbc6f61fa4d80a92feec2a90f8a3e",
                            CharacteristicPermission.Write,
                            CharacteristicProperty.Write)

        let offCharacteristic =
            CharacteristicBool("Off", "6315119dd61949bba21def9e99941948",
                           CharacteristicPermission.Write,
                           CharacteristicProperty.Write)

        let startBlinkCharacteristic =
            CharacteristicInt32("StartBlink", "3a6cc4f2a6ab4709a9bfc9611c6bf892",
                           CharacteristicPermission.Write,
                           CharacteristicProperty.Write)

        let startRunningColorsCharacteristic =
            CharacteristicBool("StartRunningColors", "30df1258f42b4788af2ea8ed9d0b932f",
                           CharacteristicPermission.Write,
                           CharacteristicProperty.Write)

        let readAngleCharacteristic =
            CharacteristicInt32("GetAccelerometerData", "FDC76B01153C4666AD2A78CA8E76BD11",
                           CharacteristicPermission.Read,
                           CharacteristicProperty.Read)

        let service =
            Service(Name,
                    uuid,
                    onCharacteristic,
                    offCharacteristic,
                    startBlinkCharacteristic,
                    startRunningColorsCharacteristic,
                    readAngleCharacteristic)
            
        [| service :> IService |]

    override this.Initialize() =
        let mutable bleTreeDefinition = Unchecked.defaultof<IDefinition>

        Resolver.Log.Info("Initialize...")
        bleTreeDefinition <- Definition("MeadowRGB", this.GetDefinition())
        MeadowApp.Device.BluetoothAdapter.StartBluetoothServer(bleTreeDefinition) |> ignore

        if bleTreeDefinition = null then
            Resolver.Log.Error("Bluetooth tree definition is null.")
            ()
        else
            Resolver.Log.Info("Bluetooth is ready")


        let servicesArray = bleTreeDefinition.Services
        if servicesArray.Count = 0 then
            Resolver.Log.Error("Bluetooth tree definition has no services.")
        else
            let service = servicesArray.[0] :?> Service
            On <- service.Characteristics.[0]
            Off <- service.Characteristics.[1]
            StartBlink <- service.Characteristics.[2]
            StartRunningColors <- service.Characteristics.[3]
            ReadAngle <- service.Characteristics.[4]

        
        On.add_ValueSet(fun (sender : ICharacteristic) (newValue : obj) ->
            match newValue with
            | :? int as 0 -> ledController.TurnOn(Some RgbLedColors.Red)
            | :? int as 1 -> ledController.TurnOn(Some RgbLedColors.Green)
            | :? int as 2 -> ledController.TurnOn(Some RgbLedColors.Blue)
            | :? int as 3 -> ledController.TurnOn(Some RgbLedColors.Cyan)
            | :? int as 4 -> ledController.TurnOn(Some RgbLedColors.Magenta)
            | :? int as 5 -> ledController.TurnOn(Some RgbLedColors.Yellow)
            | :? int as 6 -> ledController.TurnOn(Some RgbLedColors.White)
            | _ -> ledController.TurnOn(None) 
        )
        Off.add_ValueSet(fun sender args -> ledController.TurnOff())
        StartBlink.add_ValueSet(fun (sender : ICharacteristic) (newValue : obj) ->
            match newValue with
            | :? int as 0 -> ledController.StartBlink(Some RgbLedColors.Red)
            | :? int as 1 -> ledController.StartBlink(Some RgbLedColors.Green)
            | :? int as 2 -> ledController.StartBlink(Some RgbLedColors.Blue)
            | :? int as 3 -> ledController.StartBlink(Some RgbLedColors.Cyan)
            | :? int as 4 -> ledController.StartBlink(Some RgbLedColors.Magenta)
            | :? int as 5 -> ledController.StartBlink(Some RgbLedColors.Yellow)
            | :? int as 6 -> ledController.StartBlink(Some RgbLedColors.White)
            | _ -> ledController.StartBlink(None) 
        )
        StartRunningColors.add_ValueSet(fun sender args -> ledController.StartRunningColors())
        ReadAngle.add_ServerValueSet(fun (sender : ICharacteristic) (newValue) ->
            let data = boardYangle
            let bytes = BitConverter.GetBytes(data)
            sender.SetValue(bytes)
        )


        Resolver.Log.Info("Initialize Accelerometer...")
        
        let i2cBus = MeadowApp.Device.CreateI2cBus(I2cBusSpeed.Standard)
        Accelerometer <- new Adxl345(i2cBus)
        Accelerometer.SetPowerState(false, false, true, false, Adxl345.Frequencies.OneHz)

     

        base.Initialize()

    // Override the Run method
    override this.Run() =
        async {
            do! async { Accelerometer.StartUpdating(TimeSpan.FromMilliseconds(1000.0)) }
           
            while true do
                let! result = Async.AwaitTask (Accelerometer.Read())
                accelerometerData <- result
                boardYangle <- calculateAccYangle accelerometerData.X accelerometerData.Z - boardYoffset
                Resolver.Log.Info(sprintf "Y angle: %.1f%%" boardYangle)
                do! Async.Sleep(TimeSpan.FromMilliseconds(1000.0))
        } |> Async.StartAsTask :> Task

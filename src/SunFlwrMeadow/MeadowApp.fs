namespace SunFlwrMeadow

open System
open System.Threading
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
    let mutable boardYangle = 0.0
    
    let rgbLed =
        RgbLed(MeadowApp.Device.Pins.OnboardLedRed, MeadowApp.Device.Pins.OnboardLedGreen, MeadowApp.Device.Pins.OnboardLedBlue)
    
    let mutable ledController = LedController.Create(rgbLed)
          
    member private this.GetDefinition() =
        let Name = "MeadowRGB"
        let uuid: uint16 = 180us
        let onCharacteristic =
            CharacteristicBool("On", "73cfbc6f61fa4d80a92feec2a90f8a3e",
                           CharacteristicPermission.Read ||| CharacteristicPermission.Write,
                           CharacteristicProperty.Read ||| CharacteristicProperty.Write)

        let offCharacteristic =
            CharacteristicBool("Off", "6315119dd61949bba21def9e99941948",
                           CharacteristicPermission.Read ||| CharacteristicPermission.Write,
                           CharacteristicProperty.Read ||| CharacteristicProperty.Write)

        let startBlinkCharacteristic =
            CharacteristicBool("StartBlink", "3a6cc4f2a6ab4709a9bfc9611c6bf892",
                           CharacteristicPermission.Read ||| CharacteristicPermission.Write,
                           CharacteristicProperty.Read ||| CharacteristicProperty.Write)

        let startRunningColorsCharacteristic =
            CharacteristicBool("StartRunningColors", "30df1258f42b4788af2ea8ed9d0b932f",
                           CharacteristicPermission.Read ||| CharacteristicPermission.Write,
                           CharacteristicProperty.Read ||| CharacteristicProperty.Write)

        let service =
            Service(Name,
                    uuid,
                    onCharacteristic,
                    offCharacteristic,
                    startBlinkCharacteristic,
                    startRunningColorsCharacteristic)
            
        [| service :> IService |]

    override this.Initialize() =
        let mutable bleTreeDefinition = Unchecked.defaultof<IDefinition>
        let mutable On = Unchecked.defaultof<ICharacteristic>
        let mutable Off = Unchecked.defaultof<ICharacteristic>
        let mutable StartBlink = Unchecked.defaultof<ICharacteristic>
        let mutable StartRunningColors = Unchecked.defaultof<ICharacteristic>
        let mutable getRandomeColor = Unchecked.defaultof<ICharacteristic>
        Resolver.Log.Info("Initialize...")
        bleTreeDefinition <- Definition("MeadowRGB", this.GetDefinition())
        MeadowApp.Device.BluetoothAdapter.StartBluetoothServer(bleTreeDefinition) |> ignore

        Resolver.Log.Info("Initialize Accelerometer...")
        
        let i2cBus = MeadowApp.Device.CreateI2cBus(I2cBusSpeed.Standard)
        Accelerometer <- new Adxl345(i2cBus)
        Accelerometer.SetPowerState(false, false, true, false, Adxl345.Frequencies.FourHz)
        
        // find the initial fractional degree position of the board to
        // compensate relative to new changes when the panel is repositioned
        let offsetResult = Async.AwaitTask (Accelerometer.Read())
        accelerometerData <- Async.RunSynchronously offsetResult
        boardYoffset <- calculateAccYangle accelerometerData.X accelerometerData.Z
                
        match boardYoffset with 
            | x when x > -2.0 && x < 2.0 ->
                // set "offset" if board is between +/- 2 degrees of level
                boardYoffset <- x
                Resolver.Log.Info(sprintf "Y offset: %.1f%%" boardYoffset)
            | _ -> 
                // otherwise exit
                failwith $"Intiial angle out of range. [{boardYoffset}]" 

        base.Initialize()

    // Override the Run method
    override this.Run() =
        async {
            // put on a light show, just for show
            ledController.TurnOn(None)
            Thread.Sleep(2000)
            ledController.TurnOff()
            Thread.Sleep(2000)
            ledController.StartBlink(Some RgbLedColors.Yellow) |> ignore
            Thread.Sleep(6000)
            ledController.StartBlink(Some RgbLedColors.Cyan) |> ignore
            Thread.Sleep(6000)
            ledController.StartBlink(None) |> ignore
            Thread.Sleep(6000)
            ledController.TurnOff()
            Thread.Sleep(2000)
            ledController.StartRunningColors()

            do! Async.Sleep(Timeout.Infinite) // Keep the app running indefinitely
        } |> Async.StartAsTask :> Task |> ignore

        async {
            do! async { Accelerometer.StartUpdating(TimeSpan.FromMilliseconds(250.0)) }
           
            while true do
                let! result = Async.AwaitTask (Accelerometer.Read())
                accelerometerData <- result
                boardYangle <- calculateAccYangle accelerometerData.X accelerometerData.Z - boardYoffset
                Resolver.Log.Info(sprintf "Y angle: %.1f%%" boardYangle)
                do! Async.Sleep(TimeSpan.FromMilliseconds(250.0))
        } |> Async.StartAsTask :> Task

﻿namespace SunFlwrMeadow

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
          
    member private this.GetDefinition() =
        let Name = "MeadowRGB"
        let uuid: uint16 = 180us
        let onCharacteristic =
            CharacteristicString("On", 
                            "73cfbc6f61fa4d80a92feec2a90f8a3e",
                            CharacteristicPermission.Read ||| CharacteristicPermission.Write,
                            CharacteristicProperty.Read ||| CharacteristicProperty.Write,
                            10,[||])

        let offCharacteristic =
            CharacteristicBool("Off", "6315119dd61949bba21def9e99941948",
                           CharacteristicPermission.Read ||| CharacteristicPermission.Write,
                           CharacteristicProperty.Read ||| CharacteristicProperty.Write)

        let startBlinkCharacteristic =
            CharacteristicString("StartBlink", "3a6cc4f2a6ab4709a9bfc9611c6bf892",
                           CharacteristicPermission.Read ||| CharacteristicPermission.Write,
                           CharacteristicProperty.Read ||| CharacteristicProperty.Write,
                           10,[||])

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

        
        On.add_ValueSet(fun (sender : ICharacteristic) (newValue : obj) ->
            match newValue with
            | :? string as "0" -> ledController.TurnOn(Some RgbLedColors.Red)
            | :? string as "1" -> ledController.TurnOn(Some RgbLedColors.Green)
            | :? string as "2" -> ledController.TurnOn(Some RgbLedColors.Blue)
            | :? string as "3" -> ledController.TurnOn(Some RgbLedColors.Cyan)
            | :? string as "4" -> ledController.TurnOn(Some RgbLedColors.Magenta)
            | :? string as "5" -> ledController.TurnOn(Some RgbLedColors.Yellow)
            | :? string as "6" -> ledController.TurnOn(Some RgbLedColors.White)
            | _ -> ledController.TurnOn(None) 
        )
        Off.add_ValueSet(fun sender args -> ledController.TurnOff())
        StartBlink.add_ValueSet(fun (sender : ICharacteristic) (newValue : obj) ->
                   match newValue with
                   | :? string as "0" -> ledController.StartBlink(Some RgbLedColors.Red)
                   | :? string as "1" -> ledController.StartBlink(Some RgbLedColors.Green)
                   | :? string as "2" -> ledController.StartBlink(Some RgbLedColors.Blue)
                   | :? string as "3" -> ledController.StartBlink(Some RgbLedColors.Cyan)
                   | :? string as "4" -> ledController.StartBlink(Some RgbLedColors.Magenta)
                   | :? string as "5" -> ledController.StartBlink(Some RgbLedColors.Yellow)
                   | :? string as "6" -> ledController.StartBlink(Some RgbLedColors.White)
                   | _ -> ledController.StartBlink(None) 
               )
        StartRunningColors.add_ValueSet(fun sender args -> ledController.StartRunningColors())


        Resolver.Log.Info("Initialize Accelerometer...")
        
        let i2cBus = MeadowApp.Device.CreateI2cBus(I2cBusSpeed.Standard)
        Accelerometer <- new Adxl345(i2cBus)
        Accelerometer.SetPowerState(false, false, true, false, Adxl345.Frequencies.FourHz)

     

        base.Initialize()

    // Override the Run method
    override this.Run() =
        async {
            do! async { Accelerometer.StartUpdating(TimeSpan.FromMilliseconds(250.0)) }
           
            while true do
                let! result = Async.AwaitTask (Accelerometer.Read())
                accelerometerData <- result
                boardYangle <- calculateAccYangle accelerometerData.X accelerometerData.Z - boardYoffset
                Resolver.Log.Info(sprintf "Y angle: %.1f%%" boardYangle)
                do! Async.Sleep(TimeSpan.FromMilliseconds(250.0))
        } |> Async.StartAsTask :> Task

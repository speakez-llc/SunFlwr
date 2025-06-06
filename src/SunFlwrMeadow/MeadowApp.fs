﻿namespace SunFlwrMeadow

open System
open System.Threading.Tasks
open Meadow
open Meadow.Devices
open Meadow.Hardware
open Meadow.Peripherals
open Meadow.Gateways.Bluetooth
open Meadow.Foundation.Leds
open Meadow.Foundation.Sensors.Motion
open Meadow.Foundation.Motors.Stepper
open Meadow.Foundation.Sensors.Power
open Meadow.Peripherals.Leds
open SunFlwrMeadow.LedController
open Helpers

[<Measure>] type V  // Voltage in volts
[<Measure>] type A // Current in amperes
[<Measure>] type W  // Power in watts




type MeadowApp() =
    inherit App<F7FeatherV2>()
    
    let i2cBus = MeadowApp.Device.CreateI2cBus(I2cBusSpeed.Fast)

    // set up motion sensor
    let mutable Accelerometer : Adxl345 = null
    let mutable continueRotation = false
    let mutable continueMonitoring = false

    // set up stepper motor
    let mutable a4988 : A4988  = null

    let mutable ina260 = new Ina260(i2cBus, byte 0x40)
    let mutable ina219 = new Ina260(i2cBus, byte 0x41)

    let rgbLed =
        new RgbLed(MeadowApp.Device.Pins.OnboardLedRed, MeadowApp.Device.Pins.OnboardLedGreen, MeadowApp.Device.Pins.OnboardLedBlue)
    
    let mutable ledController = Led.Create(rgbLed)

    let mutable LedOn = Unchecked.defaultof<ICharacteristic>
    let mutable LedOff = Unchecked.defaultof<ICharacteristic>
    let mutable StartBlink = Unchecked.defaultof<ICharacteristic>
    let mutable StartRunningColors = Unchecked.defaultof<ICharacteristic>
    let mutable ReadAngle = Unchecked.defaultof<ICharacteristic>
    let mutable MotorOff = Unchecked.defaultof<ICharacteristic>
    let mutable MonitorToggle = Unchecked.defaultof<ICharacteristic>
    let mutable SetAngle = Unchecked.defaultof<ICharacteristic>
          
    member private this.GetDefinition() =
        let Name = "SunFlwr"
        let uuid: uint16 = 180us
        let ledOnCharacteristic =
            CharacteristicInt32("LedOn", 
                            "73cfbc6f61fa4d80a92feec2a90f8a3e",
                            CharacteristicPermission.Write,
                            CharacteristicProperty.Write)
        let ledOffCharacteristic =
            CharacteristicBool("LedOff", "6315119dd61949bba21def9e99941948",
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
            CharacteristicString("GetAccelerometerData", "FDC76B01153C4666AD2A78CA8E76BD11",
                           CharacteristicPermission.Read,
                           CharacteristicProperty.Read, 8)
        let motorOffCharacteristic =
            CharacteristicBool("MotorOff", "2447D48D92CF407FB853714B6F5FE639",
                           CharacteristicPermission.Write,
                           CharacteristicProperty.Write)
        let monitorToggleCharacteristic =
            CharacteristicBool("MonitorToggle", "490CBA885C404CCEAD08C8BDADE23DC9",
                           CharacteristicPermission.Write,
                           CharacteristicProperty.Write)
        let setAngleCharacteristic =
            CharacteristicString("SetAngle", "2C1F7033F8F843C2A84176A23CE63400",
                            CharacteristicPermission.Write,
                            CharacteristicProperty.Write, 5)
        let service =
            Service(Name,
                    uuid,
                    ledOnCharacteristic,
                    ledOffCharacteristic,
                    startBlinkCharacteristic,
                    startRunningColorsCharacteristic,
                    readAngleCharacteristic,
                    motorOffCharacteristic,
                    monitorToggleCharacteristic,
                    setAngleCharacteristic)
        [| service :> IService |]


    override this.Initialize() =

        Resolver.Log.Info("Initialize Motor Driver...")
        a4988 <- new A4988(
            step = MeadowApp.Device.Pins.D01,
            direction = MeadowApp.Device.Pins.D00,
            ms1Pin = MeadowApp.Device.Pins.D04,
            ms2Pin = MeadowApp.Device.Pins.D03,
            ms3Pin = MeadowApp.Device.Pins.D02)


        let mutable bleTreeDefinition = Unchecked.defaultof<IDefinition>

        Resolver.Log.Info("Initialize BLE...")
        bleTreeDefinition <- Definition("SunFlwr", this.GetDefinition())

        MeadowApp.Device.BluetoothAdapter.StartBluetoothServer(bleTreeDefinition) |> ignore

        let servicesArray = bleTreeDefinition.Services
        let service = servicesArray.[0] :?> Service
        LedOn <- service.Characteristics.[0]
        LedOff <- service.Characteristics.[1]
        StartBlink <- service.Characteristics.[2]
        StartRunningColors <- service.Characteristics.[3]
        ReadAngle <- service.Characteristics.[4]
        MotorOff <- service.Characteristics.[5]
        MonitorToggle <- service.Characteristics.[6]
        SetAngle <- service.Characteristics.[7]

        Resolver.Log.Info("Initialize Accelerometer...")
        Accelerometer <- new Adxl345(i2cBus)
        Accelerometer.SetPowerState(false, false, true, false, Adxl345.Frequencies.TwoHz)


        // power sensor setup
        //Resolver.Log.Info "-- INA Sample App ---"
        //Resolver.Log.Info "Initialize INA260..."
        //Resolver.Log.Info (sprintf "INA260 Manufacturer: %A" ina260.ManufacturerID)
        //Resolver.Log.Info (sprintf "INA260 Die: %A" ina260.DieID)
        //Resolver.Log.Info "Initialize INA219..."
        //Resolver.Log.Info (sprintf "INA219 Manufacturer: %A" ina219.ManufacturerID)
        //Resolver.Log.Info (sprintf "INA260 Die: %A" ina219.DieID)

        base.Initialize()

    override this.Run() =
        Resolver.Log.Info("Running...")
        rgbLed.IsOn <- false
    
        let motorControlTask (angle : float) = Task.Run(fun () ->
            continueRotation <- true
            while continueRotation do
                let result = Accelerometer.Read().Result
                let newestAngle = calculateAccYangle result.X result.Z
                let motorDirection = if newestAngle < angle then RotationDirection.CounterClockwise else RotationDirection.Clockwise
                let ledColor = if newestAngle < angle then RgbLedColors.Green else RgbLedColors.Cyan
                let stepDivisor = if newestAngle < 10 then StepDivisor.Divisor_8 else StepDivisor.Divisor_16
                a4988.RotationSpeedDivisor <- 8
                a4988.StepDivisor <- stepDivisor
                a4988.Direction <- motorDirection
                a4988.Rotate(90.0f)
                ledController.TurnOn (Some ledColor)
        )

        let monitorTask (angle : float) = Task.Run(fun () ->
            continueMonitoring <- true
            while continueMonitoring do
                let result = Accelerometer.Read().Result
                let newestAngle = calculateAccYangle result.X result.Z
                Resolver.Log.Info(sprintf "Angle: %.1f" newestAngle)
                if newestAngle <= (angle + 1.0) && newestAngle >= (angle - 1.0 ) then
                    continueRotation <- false
                    continueMonitoring <- false
                    Resolver.Log.Info("Angle change completed")
                    ledController.TurnOff()
                asyncSleep 50 |> Async.RunSynchronously |> ignore
        )

        LedOn.add_ValueSet(fun (sender : ICharacteristic) (newValue : obj) ->
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
        LedOff.add_ValueSet(fun sender args -> ledController.TurnOff())
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
        MotorOff.add_ValueSet(fun sender args -> 
            continueRotation <- false
            continueMonitoring <- false
            )
        MonitorToggle.add_ValueSet(fun sender args -> 
            if continueMonitoring then
                continueMonitoring <- false
            else
                continueMonitoring <- true
            while continueMonitoring do
                let result = Accelerometer.Read().Result
                let newestAngle = calculateAccYangle result.X result.Z
                Resolver.Log.Info(sprintf "Angle: %.1f" newestAngle)
                asyncSleep 500 |> Async.RunSynchronously |> ignore
        )
        SetAngle.add_ValueSet(fun (sender : ICharacteristic) (newValue : obj) ->
            match newValue with
            | :? string as str -> 
                match System.Double.TryParse(str) with
                | true, angle -> 
                    Resolver.Log.Info(sprintf "Provided Angle: %.1f" angle)
                    let motorT = Task.Run(fun () -> motorControlTask angle)
                    let monitorT = Task.Run(fun () -> monitorTask angle)
                    Task.WhenAll([| motorT; monitorT |]) |> ignore
                | _ ->
                    Resolver.Log.Info(sprintf "Could Not Parse Angle")
                    ()
            | _ -> 
                Resolver.Log.Info(sprintf "Could Not Read Value")
                ()
        )
        
        asyncSleep 2000 |> Async.RunSynchronously |> ignore
        let result = Accelerometer.Read().Result
        let initialAngle = calculateAccYangle result.X result.Z
        Resolver.Log.Info(sprintf "Initial Angle at Startup: %.1f" initialAngle)

        //let consumer = ina260.Subscribe(fun result -> 
        //    let newPowerReading = 
        //        match result.New with 
        //        | struct (newwattage, newvoltage, newcurrent) ->
        //            (Option.ofNullable newwattage, Option.ofNullable newvoltage, Option.ofNullable newcurrent)

        //    match newPowerReading with
        //    | (Some newWattage, Some newVoltage, Some newCurrent) ->
        //        let fmt f = sprintf "%.2f" f
        //        let wattagefloat = newWattage.Milliwatts
        //        let voltagefloat = newVoltage.Millivolts
        //        let currentfloat = newCurrent.Milliamps
        //        Resolver.Log.Info (sprintf "ina260 values: %s mv * %s ma = %s mw" (fmt voltagefloat) (fmt currentfloat) (fmt wattagefloat))
        //    | _ -> 
        //        Resolver.Log.Info (sprintf "no joy in power sensor land"))

        //let consumer2 = ina219.Subscribe(fun result ->  
        //    let newCurrentReading = 
        //        match result.New with 
        //        | struct (_, _, newCurrent) ->
        //            (Option.ofNullable newCurrent)

        //    match newCurrentReading with
        //    | (Some newCurrent) ->
        //        let fmt f = sprintf "%.2f" f
        //        let currentFloat = newCurrent.Milliamps
        //        Resolver.Log.Info (sprintf "INA219 high side current: %s mA"  (fmt currentFloat))
        //        | _ -> 
        //            Resolver.Log.Info (sprintf "NO JOY IN POWER SENSOR LAND"))


        //do ina260.StartUpdating(TimeSpan.FromSeconds(2.0))
        //do ina219.StartUpdating(TimeSpan.FromSeconds(2.0))

        base.Run()
namespace SunFlwrMeadow

open System
open System.Threading.Tasks
open Meadow
open Meadow.Devices
open Meadow.Hardware
open Meadow.Foundation.Sensors.Motion
open Meadow.Units
open Helpers

type MeadowApp() =
    inherit App<F7FeatherV2>()

    let mutable Accelerometer : Adxl345 = null
    let mutable accelerometerData : Acceleration3D = Acceleration3D()
    let mutable boardYoffset = 0.0
    let mutable boardYangle = 0.0

    override this.Initialize() =
        Resolver.Log.Info("Initialize...")

        let i2cBus = MeadowApp.Device.CreateI2cBus(I2cBusSpeed.Standard)
        Accelerometer <- new Adxl345(i2cBus)
        Accelerometer.SetPowerState(false, false, true, false, Adxl345.Frequencies.FourHz)
        let offsetResult = Async.AwaitTask (Accelerometer.Read())
        accelerometerData <- Async.RunSynchronously offsetResult
        boardYoffset <- calculateAccYangle accelerometerData.X accelerometerData.Z
        Resolver.Log.Info(sprintf "Y offset: %.1f%%" boardYoffset)
        
        Task.CompletedTask

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

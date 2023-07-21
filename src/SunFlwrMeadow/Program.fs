namespace SunFlwrMeadow

open System
open Meadow
open Meadow.Devices
open Meadow.Foundation.Sensors.Motion
open System.Threading.Tasks
open Meadow.Hardware
open Meadow.Units

type MeadowApp() =
    // Change F7FeatherV2 to F7FeatherV1 for V1.x boards
    inherit App<F7FeatherV2>()

    let mutable Accelerometer : Adxl345 = null
    let mutable accelerometerData : Acceleration3D = Acceleration3D()

    override this.Initialize() =
        Resolver.Log.Info("Initialize...")
        
        let i2cBus = MeadowApp.Device.CreateI2cBus(I2cBusSpeed.Standard)

        // Create a new ADXL345 sensor on the I2C bus
        Accelerometer <- new Adxl345(i2cBus)
        Accelerometer.SetPowerState(false, false, true, false, Adxl345.Frequencies.FourHz)

        // classical .NET events can also be used:
        Accelerometer.Updated.Add(fun args ->
            accelerometerData <- args.New
            Resolver.Log.Info(
                sprintf "Accel: [X:%f, Y:%f, Z:%f (m/s^2)]" 
                        accelerometerData.X.MetersPerSecondSquared 
                        accelerometerData.Y.MetersPerSecondSquared 
                        accelerometerData.Z.MetersPerSecondSquared
            )
        )

        Task.CompletedTask

    override this.Run() =
        async {
            let updatingTask = async { Accelerometer.StartUpdating(TimeSpan.FromMilliseconds(250.0)) }
            
            // Use do! to handle the result of Async.StartImmediate
            do! updatingTask

            Resolver.Log.Info(
                sprintf "Final Accel: [X:%f, Y:%f, Z:%f (m/s^2)]" 
                        accelerometerData.X.MetersPerSecondSquared 
                        accelerometerData.Y.MetersPerSecondSquared 
                        accelerometerData.Z.MetersPerSecondSquared
            )
        } |> Async.StartAsTask :> Task

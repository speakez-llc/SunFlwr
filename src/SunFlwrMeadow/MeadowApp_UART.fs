namespace SunFlwrMeadow

open System
open System.IO.Ports
open System.Threading.Tasks
open Meadow
open Meadow.Units
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

    // set up stepper motor - will determine mode in Initialize()
    let mutable tmc2209 : Tmc2209 = null

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

    // Fallback to step/dir mode if UART initialization fails
    member private this.InitializeStepDirMode() =
        Resolver.Log.Info("Falling back to Step/Dir mode...")
        
        tmc2209 <- new Tmc2209(
            step = MeadowApp.Device.Pins.D01,
            direction = MeadowApp.Device.Pins.D00,
            enable = MeadowApp.Device.Pins.D02)
            
        tmc2209.Enable(true)
        Resolver.Log.Info("Step/Dir mode initialized")

    override this.Initialize() =
        Resolver.Log.Info("Initializing TMC2209 in UART mode...")
        
        try
            // Get available port names
            let portNames = F7SerialPort.GetPortNames()
            Resolver.Log.Info(sprintf "Available F7 serial ports: %d" portNames.Length)
            
            // Print all available port names for debugging
            for i = 0 to portNames.Length - 1 do
                let portName = portNames.[i]
                Resolver.Log.Info(sprintf "Port %d: %A" i portName)
            
            // Select the port name string
            let portNameString = 
                if portNames.Length > 1 then
                    portNames.[1]  // Use the second port
                elif portNames.Length > 0 then
                    portNames.[0]  // Use the first port
                else
                    failwith "No serial ports available"
                    
            Resolver.Log.Info(sprintf "Using port: %A" portNameString)
                
            // Create a SerialPortName with null controller
            let portNameObj = SerialPortName.Create(portNameString, null :> obj)
                
            // Create the serial port with the SerialPortName object
            let serialPort = 
                MeadowApp.Device.CreateSerialPort(
                    portNameObj,
                    115200,
                    8,
                    Parity.None, 
                    StopBits.One)
                
            // Make sure the port is open before using
            if not serialPort.IsOpen then
                Resolver.Log.Info("Opening serial port...")
                serialPort.Open()
                
            // Wait for the port to stabilize
            Task.Delay(100).Wait()
                
            // Verify port is open
            if serialPort.IsOpen then
                Resolver.Log.Info("Serial port opened successfully")
                
                // Now initialize the TMC2209 with the open serial port
                tmc2209 <- new Tmc2209(serialPort, 0uy)
                
                // Configure after a delay to ensure initialization is complete
                Task.Delay(500).Wait()
                
                // Configure advanced features available in UART mode
                Task.Run(fun () ->
                    try
                        // Allow time for initialization
                        Task.Delay(1000).Wait()
                        
                        // Set microstepping (only available in UART mode)
                        Resolver.Log.Info("Setting microstepping to 1/16...")
                        tmc2209.SetMicrosteppingAsync(Tmc2209.StepDivisor.Divisor16).Wait()
                        
                        // Use a predefined motion profile
                        Resolver.Log.Info("Configuring motion profile...")
                        tmc2209.ConfigureMotionProfileAsync(Tmc2209.MotionProfile.Standard).Wait()
                        
                        // Set motor current
                        Resolver.Log.Info("Setting motor current...")
                        tmc2209.SetMotorCurrentAsync(
                            new Current(1.0, Current.UnitType.Amps),
                            new Current(0.5, Current.UnitType.Amps)).Wait()
                            
                        Resolver.Log.Info("TMC2209 UART configuration complete")
                    with
                    | ex -> 
                        Resolver.Log.Error(sprintf "Error configuring TMC2209 via UART: %s" ex.Message)
                        this.InitializeStepDirMode()
                ) |> ignore
            else
                Resolver.Log.Error("Failed to open serial port")
                this.InitializeStepDirMode()
                
        with
        | ex -> 
            Resolver.Log.Error(sprintf "Error setting up UART mode: %s" ex.Message)
            Resolver.Log.Error(sprintf "Stack trace: %s" ex.StackTrace)
            this.InitializeStepDirMode()

        // Rest of your initialization code
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

        // Call base class initialization as the last step
        base.Initialize()

    override this.Run() =
        Resolver.Log.Info("Running...")
        rgbLed.IsOn <- false
        
        // Function to determine if we're in UART mode
        let isUartMode() = 
            tmc2209 <> null && tmc2209.CurrentMode = Tmc2209.InterfaceMode.Uart
    
        let motorControlTask (angle : float) = Task.Run(fun () ->
            continueRotation <- true
            
            while continueRotation do
                try
                    // Read current angle
                    let result = Accelerometer.Read().Result
                    let newestAngle = calculateAccYangle result.X result.Z
                    
                    if isUartMode() then
                        // UART mode control
                        let angleDiff = abs (newestAngle - angle)
                        
                        // Direction is based on sign of velocity
                        let direction = if newestAngle < angle then 1 else -1
                        
                        // Speed proportional to angle difference
                        let speed = 
                            if angleDiff > 20.0 then 400   // Fast for big differences
                            elif angleDiff > 5.0 then 200  // Medium for moderate differences
                            else 100                       // Slow for fine adjustments
                        
                        let velocity = direction * speed
                        Resolver.Log.Info(sprintf "Current: %.1f, Target: %.1f, Velocity: %d" 
                                        newestAngle angle velocity)
                        
                        // Set velocity using UART control
                        tmc2209.SetVelocityAsync(velocity).Wait()
                    else
                        // Step/Dir mode control
                        let motorDirection = 
                            if newestAngle < angle then 
                                RotationDirection.CounterClockwise 
                            else 
                                RotationDirection.Clockwise
                        
                        // Calculate step size based on distance to target
                        let stepSize = 
                            if abs(newestAngle - angle) > 10.0 then 20.0f
                            else 5.0f
                            
                        Resolver.Log.Info(sprintf "Current: %.1f, Target: %.1f, Step: %.1f" 
                                        newestAngle angle stepSize)
                        
                        // Rotate the motor
                        tmc2209.Direction <- motorDirection
                        tmc2209.Rotate(stepSize)
                    
                    // Visual feedback                  
                    let ledColor = 
                        if newestAngle < angle then 
                            RgbLedColors.Green 
                        else 
                            RgbLedColors.Cyan
                    ledController.TurnOn(Some ledColor)
                    
                    // Check if we've reached the target
                    if newestAngle <= (angle + 1.0) && newestAngle >= (angle - 1.0) then
                        // If close enough to target, slow down or stop
                        if isUartMode() then
                            // In UART mode, reduce speed as we approach target
                            let finalSpeed = 
                                if newestAngle <= (angle + 0.5) && newestAngle >= (angle - 0.5) then
                                    // Very close - stop
                                    0
                                else
                                    // Close but not quite - very slow
                                    let dir = if newestAngle < angle then 1 else -1
                                    dir * 50
                                    
                            tmc2209.SetVelocityAsync(finalSpeed).Wait()
                with
                | ex -> Resolver.Log.Error(sprintf "Motor control error: %s" ex.Message)
                
                // Delay between adjustments
                Task.Delay(200).Wait()
        )

        let monitorTask (angle : float) = Task.Run(fun () ->
            continueMonitoring <- true
            while continueMonitoring do
                let result = Accelerometer.Read().Result
                let newestAngle = calculateAccYangle result.X result.Z
                Resolver.Log.Info(sprintf "Angle: %.1f" newestAngle)
                if newestAngle <= (angle + 0.5) && newestAngle >= (angle - 0.5 ) then
                    continueRotation <- false
                    continueMonitoring <- false
                    Resolver.Log.Info("Angle change completed")
                    ledController.TurnOff()
                    
                    // Make sure motor is stopped
                    if isUartMode() then
                        try
                            tmc2209.SetVelocityAsync(0).Wait()
                        with
                        | _ -> ()
                        
                asyncSleep 500 |> Async.RunSynchronously |> ignore
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
            
            // Make sure to stop the motor
            if isUartMode() then
                try
                    tmc2209.SetVelocityAsync(0).Wait()
                with
                | ex -> Resolver.Log.Error(sprintf "Error stopping motor: %s" ex.Message)
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
        
        // Test motor
        Task.Run(fun () ->
            // Give time for initialization
            asyncSleep 3000 |> Async.RunSynchronously |> ignore
            Resolver.Log.Info("Starting motor test...")
            
            if tmc2209 <> null then
                try
                    if isUartMode() then
                        // UART mode test
                        Resolver.Log.Info("Testing UART mode velocity control...")
                        
                        // Clockwise rotation
                        Resolver.Log.Info("Setting velocity to 100 (clockwise)...")
                        tmc2209.SetVelocityAsync(100).Wait()
                        Task.Delay(2000).Wait()
                        
                        // Counter-clockwise rotation
                        Resolver.Log.Info("Setting velocity to -100 (counter-clockwise)...")
                        tmc2209.SetVelocityAsync(-100).Wait()
                        Task.Delay(2000).Wait()
                        
                        // Stop motor
                        Resolver.Log.Info("Stopping motor...")
                        tmc2209.SetVelocityAsync(0).Wait()
                    else
                        // Step/Dir mode test
                        Resolver.Log.Info("Testing step/dir mode rotation...")
                        
                        // Clockwise rotation
                        Resolver.Log.Info("Rotating 45° clockwise...")
                        tmc2209.Direction <- RotationDirection.Clockwise
                        tmc2209.Rotate(45.0f)
                        Task.Delay(1000).Wait()
                        
                        // Counter-clockwise rotation
                        Resolver.Log.Info("Rotating 45° counter-clockwise...")
                        tmc2209.Direction <- RotationDirection.CounterClockwise
                        tmc2209.Rotate(45.0f)
                        Task.Delay(1000).Wait()
                    
                    Resolver.Log.Info("Motor test complete")
                with
                | ex -> Resolver.Log.Error(sprintf "Motor test error: %s" ex.Message)
        ) |> ignore
        
        // Read initial angle
        asyncSleep 2000 |> Async.RunSynchronously |> ignore
        let result = Accelerometer.Read().Result
        let initialAngle = calculateAccYangle result.X result.Z
        Resolver.Log.Info(sprintf "Initial Angle at Startup: %.1f" initialAngle)

        // Call base class Run method as the last step
        base.Run()
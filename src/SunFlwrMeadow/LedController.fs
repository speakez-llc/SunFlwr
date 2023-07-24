namespace MeadowApp

open System
open System.Threading
open System.Threading.Tasks
open Meadow.Foundation.Leds

type ILedController =
    abstract member TurnOn : unit -> unit
    abstract member TurnOff : unit -> unit
    abstract member StartBlink : unit -> unit
    abstract member StartRunningColors : unit -> unit

type LedController (app: MeadowApp) =
    let rgbLed =
        RgbLed(app.Device.Pins.OnboardLedRed, app.Device.Pins.OnboardLedGreen, app.Device.Pins.OnboardLedBlue)
    let mutable cancellationTokenSource = new CancellationTokenSource()

    let stop () =
        rgbLed.StopAnimation() |> ignore
        cancellationTokenSource.Cancel()
        
    // Private method to initialize the LED controller
    member private this.Initialize () =
        rgbLed.SetColor(RgbLedColors.White)
        rgbLed.IsOn <- true
        stop()

    // Factory method to create an instance of LedController
    static member Create (app: MeadowApp) =
        let ledController = LedController(app)
        ledController.Initialize()
        ledController
    
    member this.getRandomColor () =
        let random = Random()
        let color : RgbLedColors =
            match random.Next(0, 6) with
            | 0 -> RgbLedColors.Red
            | 1 -> RgbLedColors.Green
            | 2 -> RgbLedColors.Blue
            | 3 -> RgbLedColors.Cyan
            | 4 -> RgbLedColors.Magenta
            | 5 -> RgbLedColors.Yellow
            | _ -> RgbLedColors.White
        rgbLed.SetColor(color)
        color

    member this.SetColor (color) =
        stop()
        rgbLed.SetColor(color)

    member this.TurnOn () =
        stop()
        rgbLed.SetColor(this.getRandomColor())
        rgbLed.IsOn <- true

    member this.TurnOff () =
        stop()
        rgbLed.IsOn <- false

    member this.StartBlink () =
        rgbLed.StartBlink(this.getRandomColor())

    member this.StartRunningColors (cancellationToken: CancellationToken) =
        async {
            while true do
                if cancellationToken.IsCancellationRequested then
                    return ()

                rgbLed.SetColor(this.getRandomColor())
                do! Task.Delay(1000) |> Async.AwaitTask
        }

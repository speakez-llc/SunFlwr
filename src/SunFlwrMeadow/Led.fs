namespace SunFlwrMeadow.LedController

open System
open System.Threading
open System.Threading.Tasks
open Meadow.Foundation.Leds

type ILedController =
    abstract member TurnOn : unit -> unit
    abstract member TurnOff : unit -> unit
    abstract member StartBlink : unit -> unit
    abstract member StartRunningColors : unit -> unit

type Led(rgbLed: RgbLed) =
    let mutable cts = new CancellationTokenSource()

    let lockObj = obj()

    let resetCancellationTokenSource () =
        let newCancellationTokenSource = new CancellationTokenSource()
        cts.Dispose() // Dispose the old instance
        cts <- newCancellationTokenSource 

    let stop () =
        rgbLed.StopAnimation() |> ignore
        cts.Cancel()
        Thread.Sleep(500) // Give the StartRunningColors task time to stop
        resetCancellationTokenSource()

    member private this.Initialize () =
        rgbLed.SetColor(RgbLedColors.Yellow)
        rgbLed.IsOn <- true
        stop()

    member private this.getRandomColor () =
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

    member this.TurnOn (color: RgbLedColors option) =
        lock lockObj (fun () ->
            match color with
                | Some color ->
                    stop()
                    rgbLed.SetColor(color)
                    rgbLed.IsOn <- true
                | None ->
                    stop()
                    rgbLed.SetColor(this.getRandomColor())
                    rgbLed.IsOn <- true
        )

    member this.TurnOff () =
        lock lockObj (fun () ->
            stop()
            rgbLed.IsOn <- false
        )

    member this.StartBlink (color: RgbLedColors option) =
        lock lockObj (fun () ->
            match color with
            | Some color ->
                stop()
                rgbLed.StartBlink(color) |> ignore
                rgbLed.IsOn <- true
            | None ->
                stop()
                rgbLed.StartBlink(this.getRandomColor()) |> ignore
                rgbLed.IsOn <- true
        )

    member this.StartRunningColors () =
        async {
            while not (cts.Token.IsCancellationRequested) do
                rgbLed.SetColor(this.getRandomColor())
                do! Task.Delay(500) |> Async.AwaitTask
        } |> Async.StartImmediate
        
    static member Create (rgbLed: RgbLed) =
        let ledController = Led(rgbLed)
        ledController.Initialize()
        ledController
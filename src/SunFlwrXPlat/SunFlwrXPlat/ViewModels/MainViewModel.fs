﻿module SunFlwrXPlat.ViewModels.MainViewModel

open Elmish
open Elmish.Avalonia

type Model = 
    {
        ContentVM: IElmishViewModel
    }

type Msg = 
    | ShowCounter
    | ShowChart
    | ShowBoardAngle
    | ShowAbout

let init() = 
    { 
        ContentVM = CounterViewModel.vm
    }

let update (msg: Msg) (model: Model) = 
    match msg with
    | ShowCounter -> 
        { model with ContentVM = CounterViewModel.vm }
    | ShowChart -> 
        { model with ContentVM = ChartViewModel.vm }
    | ShowBoardAngle -> 
        { model with ContentVM = AngleViewModel.vm }
    | ShowAbout -> 
        { model with ContentVM = AboutViewModel.vm }

let bindings() : Binding<Model, Msg> list = [ 
    // Properties
    "ContentVM" |> Binding.oneWay (fun m -> m.ContentVM)
    "ShowCounter" |> Binding.cmd ShowCounter
    "ShowChart" |> Binding.cmd ShowChart
    "ShowAngle" |> Binding.cmd ShowBoardAngle
    "ShowAbout" |> Binding.cmd ShowAbout
]

let designVM = ViewModel.designInstance (init()) (bindings())


let vm : IElmishViewModel = 
    let program =
        let subscriptions (model: Model) : Sub<Msg> =
            let messageBusSubscription (dispatch: Msg -> unit) = 
                Messaging.bus.Subscribe(fun msg -> 
                    match msg with
                    | Messaging.GlobalMsg.GoHome -> 
                        dispatch ShowCounter
                )

            [ 
                [ nameof messageBusSubscription ], messageBusSubscription
            ]

        AvaloniaProgram.mkSimple init update bindings
        |> AvaloniaProgram.withSubscription subscriptions
        |> AvaloniaProgram.withElmishErrorHandler
                (fun msg exn ->
                    printfn $"ElmishErrorHandler: msg={msg}\n{exn.Message}\n{exn.StackTrace}"
                )

    ElmishViewModel(program)
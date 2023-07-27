namespace SunFlwrXPlat

open Avalonia
open Avalonia.Controls.ApplicationLifetimes
open Avalonia.Markup.Xaml
open SunFlwrXPlat.ViewModels
open SunFlwrXPlat.Views

type App() =
    inherit Application()

    override this.Initialize() =
        // Initialize Avalonia controls from NuGet packages:
        let _ = typeof<Avalonia.Controls.DataGrid>

        AvaloniaXamlLoader.Load(this)

    override this.OnFrameworkInitializationCompleted() =
        match this.ApplicationLifetime with
        | :? IClassicDesktopStyleApplicationLifetime as desktopLifetime ->
            let view = MainWindow()
            desktopLifetime.MainWindow <- view
            try
                MainViewModel.vm.StartElmishLoop(view)
            with x ->
                printfn $"Exception: {x.Message} \n {x.StackTrace}"
        | :? ISingleViewApplicationLifetime as singleViewLifetime ->
            try
                let view = MainView()
                singleViewLifetime.MainView <- view
                let x = MainViewModel.vm
                x.StartElmishLoop(view)
            with x ->
                printfn $"Exception: {x.Message} \n {x.StackTrace}"

        | _ ->
            // leave this here for design view re-renders
            ()

        base.OnFrameworkInitializationCompleted()

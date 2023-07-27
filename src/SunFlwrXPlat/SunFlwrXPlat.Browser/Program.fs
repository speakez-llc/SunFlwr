open System.Runtime.Versioning
open Avalonia
open Avalonia.Browser
open Elmish.Avalonia.AppBuilder

open SunFlwrXPlat

module Program =
    [<assembly: SupportedOSPlatform("browser")>]
    do ()

    [<CompiledName "BuildAvaloniaApp">] 
    let buildAvaloniaApp () = 
        AppBuilder
            .Configure<App>()

    [<EntryPoint>]
    let main argv =
        task {
            do! (buildAvaloniaApp()
            .WithInterFont()
            .UseElmishBindings()
            .StartBrowserAppAsync("out"))
        }
        |> ignore
        0
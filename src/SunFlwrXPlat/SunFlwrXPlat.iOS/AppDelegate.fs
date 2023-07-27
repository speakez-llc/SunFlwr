namespace SunFlwrXPlat.iOS
open Foundation
open Avalonia
open Avalonia.iOS
open Elmish.Avalonia.AppBuilder

// The UIApplicationDelegate for the application. This class is responsible for launching the 
// User Interface of the application, as well as listening (and optionally responding) to 
// application events from iOS.
type [<Register("AppDelegate")>] AppDelegate() =
    inherit AvaloniaAppDelegate<SunFlwrXPlat.App>()

    override _.CustomizeAppBuilder(builder) =
        base.CustomizeAppBuilder(builder)
            .WithInterFont()
            .UseElmishBindings()
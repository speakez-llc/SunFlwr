namespace SunFlwrXPlat.Views

open Avalonia.Controls
open Avalonia.Markup.Xaml

type MainWindow () as this =
    // HACKHACK
    // inherit Window ()
    inherit Window ()

    do this.InitializeComponent()

    member private this.InitializeComponent() =
//#if DEBUG
//        this.AttachDevTools()
//#endif
        AvaloniaXamlLoader.Load(this)
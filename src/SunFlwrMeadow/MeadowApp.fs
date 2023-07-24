namespace SunFlwrMeadow

open System.Threading
open System.Threading.Tasks
open Meadow.Foundation.Leds
open Meadow
open Meadow.Devices
open Meadow.Gateways.Bluetooth
open SunFlwrMeadow.LedController

type MeadowApp() =
    inherit App<F7FeatherV2>()
    
    let rgbLed =
        RgbLed(MeadowApp.Device.Pins.OnboardLedRed, MeadowApp.Device.Pins.OnboardLedGreen, MeadowApp.Device.Pins.OnboardLedBlue)
    
    let mutable ledController = LedController.Create(rgbLed)
          
    member private this.GetDefinition() =
        let Name = "MeadowRGB"
        let uuid: uint16 = 180us
        let onCharacteristic =
            CharacteristicBool("On", "73cfbc6f61fa4d80a92feec2a90f8a3e",
                           CharacteristicPermission.Read ||| CharacteristicPermission.Write,
                           CharacteristicProperty.Read ||| CharacteristicProperty.Write)

        let offCharacteristic =
            CharacteristicBool("Off", "6315119dd61949bba21def9e99941948",
                           CharacteristicPermission.Read ||| CharacteristicPermission.Write,
                           CharacteristicProperty.Read ||| CharacteristicProperty.Write)

        let startBlinkCharacteristic =
            CharacteristicBool("StartBlink", "3a6cc4f2a6ab4709a9bfc9611c6bf892",
                           CharacteristicPermission.Read ||| CharacteristicPermission.Write,
                           CharacteristicProperty.Read ||| CharacteristicProperty.Write)

        let startRunningColorsCharacteristic =
            CharacteristicBool("StartRunningColors", "30df1258f42b4788af2ea8ed9d0b932f",
                           CharacteristicPermission.Read ||| CharacteristicPermission.Write,
                           CharacteristicProperty.Read ||| CharacteristicProperty.Write)

        let service =
            Service(Name,
                    uuid,
                    onCharacteristic,
                    offCharacteristic,
                    startBlinkCharacteristic,
                    startRunningColorsCharacteristic)
            
        [| service :> IService |]

    override this.Initialize() =
        let mutable bleTreeDefinition = Unchecked.defaultof<IDefinition>
        let mutable On = Unchecked.defaultof<ICharacteristic>
        let mutable Off = Unchecked.defaultof<ICharacteristic>
        let mutable StartBlink = Unchecked.defaultof<ICharacteristic>
        let mutable StartRunningColors = Unchecked.defaultof<ICharacteristic>
        let mutable getRandomeColor = Unchecked.defaultof<ICharacteristic>
        Resolver.Log.Info("Initialize...")
        bleTreeDefinition <- Definition("MeadowRGB", this.GetDefinition())
        MeadowApp.Device.BluetoothAdapter.StartBluetoothServer(bleTreeDefinition)

        base.Initialize()

    // Override the Run method
    override this.Run() =
        async {
            // put on a light show, just for show
            ledController.TurnOn(None)
            Thread.Sleep(2000)
            ledController.TurnOff()
            Thread.Sleep(2000)
            ledController.StartBlink(Some RgbLedColors.Yellow) |> ignore
            Thread.Sleep(6000)
            ledController.StartBlink(Some RgbLedColors.Cyan) |> ignore
            Thread.Sleep(6000)
            ledController.StartBlink(None) |> ignore
            Thread.Sleep(6000)
            ledController.TurnOff()
            Thread.Sleep(2000)
            ledController.StartRunningColors()

            do! Async.Sleep(Timeout.Infinite) // Keep the app running indefinitely
        } |> Async.StartAsTask :> Task

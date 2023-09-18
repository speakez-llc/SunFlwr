module ConfigFileModule

type Device = {
    Name: string
}

type Coprocessor = {
    AutomaticallyStartNetwork: bool
    AutomaticallyReconnect: bool
    MaximumRetryCount: int
}

type Network = {
    GetNetworkTimeAtStartup: int
    NtpRefreshPeriod: int
    NtpServers: string array
    DnsServers: string array
}

type MeadowConfigFile = {
    Device: Device
    Coprocessor: Coprocessor
    Network: Network
}

let defaultMeadowConfigFile = {
    Device = { Name = "SunFlwrMeadow" }
    Coprocessor = { AutomaticallyStartNetwork = true; AutomaticallyReconnect = true; MaximumRetryCount = 7 }
    Network = { 
        GetNetworkTimeAtStartup = 1
        NtpRefreshPeriod = 600
        NtpServers = [| "0.pool.ntp.org"; "1.pool.ntp.org"; "2.pool.ntp.org"; "3.pool.ntp.org" |]
        DnsServers = [| "1.1.1.1"; "8.8.8.8" |]
    }
}

type Credentials = {
    Ssid: string
    Password: string
}

type WifiConfigFile = {
    Credentials: Credentials
}

let createWifiConfig (ssid: string) (password: string) = {
    Credentials = { Ssid = ssid; Password = password }
}

open Meadow
open System
open System.IO
open YamlDotNet.Serialization

let private createMeadowConfigFile () =
    try
        let configFile = defaultMeadowConfigFile 
        let serializer = (new SerializerBuilder()).Build()
        let yaml = serializer.Serialize(configFile)

        use fs = File.CreateText(Path.Combine(MeadowOS.FileSystem.UserFileSystemRoot, "meadow.config.yaml"))
        fs.WriteLine(yaml)
    with
    | ex -> Console.WriteLine(ex.ToString())

let private createWifiConfigFile (ssid: string) (password: string) =
    try
        let configFile = createWifiConfig ssid password
        let serializer = (new YamlDotNet.Serialization.SerializerBuilder()).Build()
        let yaml = serializer.Serialize(configFile)

        use fs = File.CreateText(Path.Combine(MeadowOS.FileSystem.UserFileSystemRoot, "wifi.config.yaml"))
        fs.WriteLine(yaml)
    with
    | ex -> Console.WriteLine(ex.ToString())

let private deleteMeadowConfigFile () =
    try
        File.Delete(sprintf "%smeadow.config.yaml" MeadowOS.FileSystem.UserFileSystemRoot)
    with
    | ex -> Console.WriteLine(ex.ToString())

let private deleteWifiConfigFile () =
    try
        File.Delete(sprintf "%swifi.config.yaml" MeadowOS.FileSystem.UserFileSystemRoot)
    with
    | ex -> Console.WriteLine(ex.ToString())

let createConfigFiles ssid password =
    createMeadowConfigFile ()
    createWifiConfigFile ssid password

let deleteConfigFiles () =
    deleteMeadowConfigFile ()
    deleteWifiConfigFile ()

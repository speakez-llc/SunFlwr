#r "nuget: CoordinateSharp"

open System

open CoordinateSharp


let get_Time_At_Solar_Azimuth (azimuth : float) (c : Coordinate) : DateTime =
    let get_Hour (d : DateTime) (azimuth : float) (c : Coordinate) : DateTime =
        let el = EagerLoad(EagerLoadType.Celestial)
        el.Extensions <- EagerLoad_Extensions(EagerLoad_ExtensionsType.Solar_Cycle)
        let mutable closeHour = 0
        let mutable az = 999.0
        let nd = DateTime(d.Year, d.Month, d.Day)
        for x in 0 .. 23 do
            let nc = Coordinate(c.Latitude.ToDouble(), c.Longitude.ToDouble(), nd.AddHours(float x), el)
            nc.Offset <- c.Offset
            if Math.Abs(nc.CelestialInfo.SunAzimuth - azimuth) < az then
                az <- Math.Abs(nc.CelestialInfo.SunAzimuth - azimuth)
                closeHour <- x
        d.AddHours(float closeHour)

    let get_Minute (d : DateTime) (azimuth : float) (c : Coordinate) : DateTime =
        let el = EagerLoad(EagerLoadType.Celestial)
        el.Extensions <- EagerLoad_Extensions(EagerLoad_ExtensionsType.Solar_Cycle)
        let mutable closeMinutes = 0
        let mutable az = 999.0
        let nd = DateTime(d.Year, d.Month, d.Day, d.Hour, 0, 0)
        for x in 0 .. 59 do
            let nc = Coordinate(c.Latitude.ToDouble(), c.Longitude.ToDouble(), nd.AddMinutes(float x), el)
            nc.Offset <- c.Offset
            if Math.Abs(nc.CelestialInfo.SunAzimuth - azimuth) < az then
                az <- Math.Abs(nc.CelestialInfo.SunAzimuth - azimuth)
                closeMinutes <- x
        for x in 0 .. 59 do
            let nc = Coordinate(c.Latitude.ToDouble(), c.Longitude.ToDouble(), nd.AddMinutes(-float x), el)
            nc.Offset <- c.Offset
            if Math.Abs(nc.CelestialInfo.SunAzimuth - azimuth) < az then
                az <- Math.Abs(nc.CelestialInfo.SunAzimuth - azimuth)
                closeMinutes <- -x
        d.AddMinutes(float closeMinutes)

    let get_Seconds (d : DateTime) (azimuth : float) (c : Coordinate) : DateTime =
        let el = EagerLoad(EagerLoadType.Celestial)
        el.Extensions <- EagerLoad_Extensions(EagerLoad_ExtensionsType.Solar_Cycle)
        let mutable closeSeconds = 0
        let mutable az = 999.0
        let nd = DateTime(d.Year, d.Month, d.Day, d.Hour, d.Minute, 0)
        for x in 0 .. 59 do
            let nc = Coordinate(c.Latitude.ToDouble(), c.Longitude.ToDouble(), nd.AddSeconds(float x), el)
            nc.Offset <- c.Offset
            if Math.Abs(nc.CelestialInfo.SunAzimuth - azimuth) < az then
                az <- Math.Abs(nc.CelestialInfo.SunAzimuth - azimuth)
                closeSeconds <- x
        for x in 0 .. 59 do
            let nc = Coordinate(c.Latitude.ToDouble(), c.Longitude.ToDouble(), nd.AddSeconds(-float x), el)
            nc.Offset <- c.Offset
            if Math.Abs(nc.CelestialInfo.SunAzimuth - azimuth) < az then
                az <- Math.Abs(nc.CelestialInfo.SunAzimuth - azimuth)
                closeSeconds <- -x
        d.AddSeconds(float closeSeconds)

    let hour = get_Hour c.GeoDate azimuth c
    let minutes = get_Minute hour azimuth c
    let seconds = get_Seconds minutes azimuth c
    seconds

let main () =
    let lateAzimuth = 202.00
    let earlyAzimuth = 158.00
    let lat = 35.0
    let lon = -82.5
    let offset = -5.0
    let d = DateTime(2024, 1, 1)
    let c = Coordinate(lat, lon, d)
    c.Offset <- offset
    let timeAtEarlyAzimuth = get_Time_At_Solar_Azimuth earlyAzimuth c
    let timeAtLateAzimuth = get_Time_At_Solar_Azimuth lateAzimuth c
    printfn "Time at Sunrise: %A" c.CelestialInfo.SunRise
    printfn "Time at early azimuth: %A" timeAtEarlyAzimuth
    printfn "Solar noon: %A" c.CelestialInfo.SolarNoon
    printfn "Time at late azimuth: %A" timeAtLateAzimuth
    printfn "Time at Sunset: %A" c.CelestialInfo.SunSet
    if c.CelestialInfo.SolarNoon.HasValue then
        let ts = timeAtLateAzimuth - c.CelestialInfo.SolarNoon.Value
        printfn "Time range from Solar noon: %d:%d" ts.Hours (abs ts.Minutes)
    let c = Coordinate(lat, lon, timeAtEarlyAzimuth)
    c.Offset <- offset
    printfn "Early azimuth at time verification: %A" c.CelestialInfo.SunAzimuth

main ()

#r "nuget: CoordinateSharp"

open System

open CoordinateSharp

let find_Closest_TimeUnit =
    let el = EagerLoad(EagerLoadType.Celestial)
    el.Extensions <- EagerLoad_Extensions(EagerLoad_ExtensionsType.Solar_Cycle)
    fun addTimeFn rangeStart rangeEnd (c : Coordinate) azimuth adjustNegatively ->
        let start = if adjustNegatively then -rangeEnd else rangeStart
        [start..rangeEnd]
        |> List.map (fun x -> 
            let newTime = addTimeFn (float x)
            let nc = Coordinate(c.Latitude.ToDouble(), c.Longitude.ToDouble(), newTime, el)
            nc.Offset <- c.Offset
            (Math.Abs(nc.CelestialInfo.SunAzimuth - azimuth), x)
        )
        |> List.minBy (fun (diff, _) -> diff)
        |> fun (_, closestTime) -> addTimeFn (float closestTime)

let get_Time_At_Solar_Azimuth azimuth (c : Coordinate) =
    let hour = find_Closest_TimeUnit (c.GeoDate.AddHours) 0 23 c azimuth false
    let minute = find_Closest_TimeUnit (hour.AddMinutes) 0 59 c azimuth true
    let second = find_Closest_TimeUnit (minute.AddSeconds) 0 59 c azimuth true
    second

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

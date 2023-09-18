#r "nuget: CoordinateSharp"

open System

open CoordinateSharp 



let find_Closest_TimeUnit addTimeFn rangeStart rangeEnd (c : Coordinate) azimuth adjustNegatively =
    let el = EagerLoad(EagerLoadType.Celestial)
    el.Extensions <- EagerLoad_Extensions(EagerLoad_ExtensionsType.Solar_Cycle)
    let mutable closestTime = 0
    let mutable minDiff = 999.0

    for direction in [1; -1] do
        if direction = 1 || adjustNegatively then
            for x in rangeStart .. rangeEnd do
                let newTime = addTimeFn (float (x * direction))
                let nc = Coordinate(c.Latitude.ToDouble(), c.Longitude.ToDouble(), newTime, el)
                nc.Offset <- c.Offset
                let currentAzimuth = nc.CelestialInfo.SunAzimuth
                let diff = Math.Abs(currentAzimuth - azimuth)
                if diff < minDiff then
                    minDiff <- diff
                    closestTime <- x * direction
    addTimeFn (float closestTime)

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
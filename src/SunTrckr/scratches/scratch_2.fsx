#r "nuget: CoordinateSharp"

open System
open CoordinateSharp


let calculateSolarNoon latitude longitude (date: DateTime) =
    let celestialTimes = Celestial.CalculateCelestialTimes(latitude, longitude, date, 0.0)
    celestialTimes.SolarNoon.Value.ToLocalTime()

// Helper function to convert degrees to radians
let toRadians (degrees: float) = degrees * Math.PI / 180.0

// Helper for time zone offset
let localTimeZoneOffset = DateTimeOffset.Now.Offset

// helper to square a number
let sqr x = x ** 2.0


// Calculate the Equation of Time (EOT) for a given date
let calculateEoT (date: DateTime) =
    let n = float date.DayOfYear
    let b = (n - 1.0) * 2.0 * Math.PI / 365.0
    let g = 357.0 + b
    let e = 0.0167
    let sinG = Math.Sin g
    let cosG = Math.Cos g
    let c = sinG * sinG + cosG * cosG * e * e
    let d = Math.Sqrt c
    let eot = 720.0 * (d / Math.PI)

    // Helper to hack the difference in days from July 19, 2023
    let dayDifference (date: DateTime) : int =
        let staticDate = DateTime(2023, 7, 19)
        let daysPassed = (date - staticDate).Days
        daysPassed

       
    // Helper to hack EoT to be more accurate for 90 days after July 19, 2023
    let interpolateEmpiricalCorrection (dayOfYear: int) =
        let days = [| 0, -10.0; 15, 40.0; 30, 100.0; 45, 130.0; 60, 145.0; 75, 100.0; 90, 57.0; |]

        let rec findDataPoints (days: (int * float)[]) prevDay currentDay =
            match days with
            | [||] | [| (_, _)|] -> (prevDay, currentDay)
            | _ ->
                match days.[1] with
                | (day, _) when day < dayOfYear ->
                    findDataPoints (Array.tail days) days.[0] days.[1]
                | _ -> (prevDay, currentDay)

        let (prevDay, currentDay) = findDataPoints days days.[0] days.[1]

        let prevCorrection = snd prevDay
        let currentCorrection = snd currentDay

        let prevDayOfYear = fst prevDay
        let currentDayOfYear = fst currentDay

        let weight = float (dayOfYear - prevDayOfYear) / float (currentDayOfYear - prevDayOfYear)

        prevCorrection + weight * (currentCorrection - prevCorrection)


    // Empirical correction factor from July 2023 to October 2023
    let empiricalCorrection = interpolateEmpiricalCorrection (dayDifference date) 

    TimeSpan.FromMinutes (eot + empiricalCorrection)


// Calculate the time from azimuth with Equation of Time (EOT) compensation
let calculateTimeFromAzimuth latitude longitude (date: DateTime) azimuthCompensation =
    let timeZoneOffset = localTimeZoneOffset
    let observer = Coordinate(latitude, longitude, date)
    let celestialInfo = observer.CelestialInfo
    let eot = calculateEoT date
    let solarNoonTime = celestialInfo.SolarNoon.Value.ToLocalTime().TimeOfDay
    let azimuthOffset = azimuthCompensation / 15.0 // Convert azimuth compensation to hours

    // Adjust azimuth offset based on latitude to refine the result
    let azimuthMultiplier =
        if latitude > 0.0 then 1.15
        else if latitude < 0.0 then 0.85
        else 1.0

    let adjustedAzimuthOffset = azimuthOffset * azimuthMultiplier

    let observerDateTime = date.Date + solarNoonTime + eot + TimeSpan.FromHours(adjustedAzimuthOffset) + timeZoneOffset

    // Adjust the observerDateTime if it falls on a different day
    if observerDateTime.Date <> date.Date then
        observerDateTime - TimeSpan.FromDays(1.0)
    else
        observerDateTime

// Example usage
let latitude = 35.5951 // Latitude of Asheville, NC
let longitude = -82.5515 // Longitude of Asheville, NC
let date = DateTime.Today.AddDays(0.0)

let maxTime = calculateTimeFromAzimuth latitude longitude date (22.0)
let solarNoon = calculateSolarNoon latitude longitude date

let minTime =
    let positiveTimeDifference = maxTime - solarNoon
    let minDateTime = solarNoon - positiveTimeDifference
    minDateTime

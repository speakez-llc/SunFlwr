#r "nuget: CoordinateSharp"

open System
open CoordinateSharp
open CoordinateSharp.Formatters

// Helper function to convert degrees to radians
let toRadians (degrees: float) = degrees * Math.PI / 180.0
// Helper for time zone offset
let localTimeZoneOffset = DateTimeOffset.Now.Offset
// helper to square a number
let sqr x = x ** 2.0



// Calculate the obliquity of the ecliptic for a given date
let calculateObliquityOfEcliptic (d: DateTime) =
    let jde = JulianConversions.GetJulian d
    let t = (jde - 2451545.0) / 365250.0
    let epsilonDeg = 23.43929111 - 0.013004167 * t - 1.63889E-7 * (t ** 2.0) + 5.036111E-7 * (t ** 3.0)
    epsilonDeg
    
// Calculate the Equation of Time (EOT) for a given date
let calculateEoT (d: DateTime) =
    let jde = JulianConversions.GetJulian d
    let r = (jde - 2451545.0) / 365250.0
    let l0 = 280.4664567 + 360007.6982779 * r + 0.03032028 * Math.Pow(r, 2) + Math.Pow(r, 3) / 49931.0 - Math.Pow(r, 4) / 15300.0 - Math.Pow(r, 5) / 2000000.0
    let l0Normalized = l0.NormalizeDegrees360()
    let sc = Celestial.Get_Solar_Coordinate d
    let rightAscension = sc.RightAscension
    let nutationInLong = -17.20 * sin sc.Longitude - 1.32 * sin (2.0 * r) - 0.23 * sin (2.0 * r) + 0.21 * sin (2.0 * sc.Longitude) // 22.1
    let nutDeg = nutationInLong / 3600.0 // convert arcseconds to degrees.
    let ob = calculateObliquityOfEcliptic d // Calculate the obliquity of the ecliptic
    let E = l0Normalized - 0.0057183 - rightAscension + nutDeg * cos (ob)
    E * (Math.PI / 180.0) / (Math.PI / 720.0)

// Helper to compute degrees to minutes
let degreesToMinutes (degrees: float) =
    let minutesPerDegree = 60.0
    let minutes = degrees * minutesPerDegree
    TimeSpan.FromMinutes(minutes)
    
// Calculate the time from azimuth with Equation of Time (EOT) compensation
let calculateTimeFromAzimuth latitude longitude (date: DateTime) azimuthCompensation =
    let timeZoneOffset = localTimeZoneOffset
    let observer = Coordinate(latitude, longitude, date)
    let celestialInfo = observer.CelestialInfo
    let eot = calculateEoT date
    let solarNoonTime = celestialInfo.SolarNoon.Value.ToLocalTime().TimeOfDay
    let azimuthOffset = azimuthCompensation / 15.0 // Convert azimuth compensation to hours

    let observerDateTime = date + solarNoonTime + degreesToMinutes(eot) + TimeSpan.FromHours(azimuthOffset) + timeZoneOffset

    observerDateTime

let calculateSolarNoon latitude longitude (date: DateTime) =
    let celestialTimes = Celestial.CalculateCelestialTimes(latitude, longitude, date, 0.0)
    celestialTimes.SolarNoon.Value.ToLocalTime()

// Example usage
let latitude = 35.5951 // Latitude of Asheville, NC
let longitude = -82.5515 // Longitude of Asheville, NC
let date = DateTime.Today.AddDays(0.0)
let maxTime = calculateTimeFromAzimuth latitude longitude date (22.0) // last value is degrees from solar noon
let solarNoon = calculateSolarNoon latitude longitude date
let minTime = calculateTimeFromAzimuth latitude longitude date (-22.0) // last value is degrees from solar noon

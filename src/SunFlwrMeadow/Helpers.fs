module Helpers

open System
open Meadow.Units

// Standard factor to calculate a degree value from the "raw" 
// meters-per-second-squared output from the accelerometer
let radToDeg = 57.29578

// Subtracting 180.0 here is to "flip" the value such that when 
// the device is facing up the reported Y angle value is "0".
// This allows a direct association with positioning the panel
// relative to +/- 22 degrees from solar noon
let calculateAccYangle (x: Acceleration) (z: Acceleration) =
    ((atan2 x.MetersPerSecondSquared z.MetersPerSecondSquared + System.Math.PI) * radToDeg) - 180.0

let asyncSleep (milliseconds : int) =
    async {
        do! Async.Sleep(TimeSpan.FromMilliseconds(float milliseconds))
    }
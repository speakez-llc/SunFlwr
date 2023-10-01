module SunTrckr.Server

open System
open System.IO
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Cors.Infrastructure
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Configuration
open Giraffe
open Marten
open Newtonsoft.Json
open Weasel.Core

// ---------------------------------
// Models 
// ---------------------------------

type Message =
    {
        Text : string
    }

// ---------------------------------
// Views
// ---------------------------------

module Views =
    open Giraffe.ViewEngine

    let layout (content: XmlNode list) =
        html [] [
            head [] [
                title []  [ encodedText "SunTrckrServer" ]
                link [ _rel  "stylesheet"
                       _type "text/css"
                       _href "/main.css" ]
            ]
            body [] content
        ]

    let partial () =
        h1 [] [ encodedText "SunTrckrServer" ]

    let index (model : Message) =
        [
            partial()
            p [] [ encodedText model.Text ]
        ] |> layout

let configuration =
    ConfigurationBuilder()
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile("Secrets.json")
        .Build()

let connString = "Server=localhost;Username=postgres;Database=postgres;Password=peanut"

let createDatabase (storeOptions: StoreOptions) =
    storeOptions.CreateDatabasesForTenants(fun c ->
        c.MaintenanceDatabase(connString) |> ignore

        c.ForTenant()
            .CheckAgainstPgDatabase()
            .WithOwner("postgres")
            .WithEncoding("UTF-8")
            .ConnectionLimit(-1)
            .OnDatabaseCreated(fun _ ->
                // Handle database created event here
                ()
            ) |> ignore
        ()
    )

[<Measure>] type V  // Voltage in volts
[<Measure>] type A // Current in amperes
[<Measure>] type W  // Power in watts
    
type PowerData =
    {   
        Id: Guid
        DeviceId: Guid
        SensorAddress: int
        ObsDateTime: DateTime
        Voltage: float<V>
        Current: float<A>
        Power: float<W>
    }

let store = 
    DocumentStore.For(fun opts ->
        opts.Connection(connString)  // Specify the connection string
        opts.Schema.For<PowerData>().Identity(fun x -> x.Id) |> ignore
        opts.AutoCreateSchemaObjects <- AutoCreate.All
    )
    
let jsonPostHandler next (ctx: HttpContext) =
    task {
        let session = ctx.RequestServices.GetService<IDocumentSession>()
        if isNull session then printfn "Session is null."
        use reader = new StreamReader(ctx.Request.Body)
        let! json = reader.ReadToEndAsync() |> Async.AwaitTask
        let data = JsonConvert.DeserializeObject<PowerData>(json)
        let dataWithId = { data with Id = Guid.NewGuid() }
        printfn "Deserialized data: %A" dataWithId
        session.Store(dataWithId)
        session.SaveChanges()
        return! text "OK" next ctx
    }

// ---------------------------------
// Web app
// ---------------------------------

let indexHandler (name : string) =
    let greetings = sprintf "Hello %s, from Giraffe!" name
    let model     = { Text = greetings }
    let view      = Views.index model
    htmlView view

let webApp =
    choose [
        GET >=>
            choose [
                route "/" >=> indexHandler "world"
                routef "/hello/%s" indexHandler
            ]
        POST >=>
            choose [
                route "/powerEvent" >=> jsonPostHandler
            ]
        setStatusCode 404 >=> text "Not Found"
    ]

// ---------------------------------
// Error handler
// ---------------------------------

let errorHandler (ex : Exception) (logger : ILogger) =
    logger.LogError(ex, "An unhandled exception has occurred while executing the request.")
    clearResponse >=> setStatusCode 500 >=> text ex.Message

// ---------------------------------
// Config and Main
// ---------------------------------

let configureCors (builder : CorsPolicyBuilder) =
    builder
        .WithOrigins(
            "http://localhost:5000",
            "https://localhost:5001")
       .AllowAnyMethod()
       .AllowAnyHeader()
       |> ignore

let configureApp (app : IApplicationBuilder) =
    let env = app.ApplicationServices.GetService<IWebHostEnvironment>()
    (match env.IsDevelopment() with
    | true  ->
        app.UseDeveloperExceptionPage()
    | false ->
        app .UseGiraffeErrorHandler(errorHandler)
            .UseHttpsRedirection())
        .UseCors(configureCors)
        .UseStaticFiles()
        .UseGiraffe(webApp)

let configureServices (services : IServiceCollection) =
    services.AddSingleton<IDocumentStore>(store) |> ignore
    services.AddScoped<IDocumentSession>(fun _ -> store.LightweightSession()) |> ignore
    services.AddCors()    |> ignore
    services.AddGiraffe() |> ignore

let configureLogging (builder : ILoggingBuilder) =
    builder.AddConsole()
           .AddDebug() |> ignore

[<EntryPoint>]
let main args =
    let contentRoot = Directory.GetCurrentDirectory()
    let webRoot     = Path.Combine(contentRoot, "WebRoot")
    
    Host.CreateDefaultBuilder(args)
        .ConfigureWebHostDefaults(
            fun webHostBuilder ->
                webHostBuilder
                    .UseContentRoot(contentRoot)
                    .UseWebRoot(webRoot)
                    .Configure(Action<IApplicationBuilder> configureApp)
                    .ConfigureServices(configureServices)
                    .ConfigureLogging(configureLogging)
                    |> ignore)
        .Build()
        .Run()
    0
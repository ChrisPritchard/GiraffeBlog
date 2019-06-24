open System
open System.IO
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Authentication.Cookies
open Microsoft.Extensions.Logging
open Microsoft.Extensions.DependencyInjection
open Microsoft.EntityFrameworkCore
open Giraffe
open Microsoft.Extensions.Configuration
open System.Globalization
open Microsoft.AspNetCore.Localization

let webApp = 
    let mustBeUser = requiresAuthentication Handlers.accessDenied
    choose [
        GET >=>
            choose [
                route "/" >=> Handlers.latest 0
                routef "/page/%i" Handlers.latest
                routef "/post/%s" (fun key -> Handlers.single key Views.NoCommentError)
                route "/login" >=> Handlers.login
                route "/archives" >=> Handlers.archives
                routef "/month/%s/%i" Handlers.month
                route "/search" >=> Handlers.search            
                route "/about" >=> Handlers.about
                route "/new" >=> mustBeUser >=> Handlers.editor None
                routef "/edit/%s" (fun key -> mustBeUser >=> (Some key |> Handlers.editor)) 
            ]
        POST >=> 
            choose [
                route "/login" >=> Handlers.tryLogin
                routef "/post/%s" Handlers.createComment
                route "/new" >=> mustBeUser >=> Handlers.createPost
                routef "/edit/%s" (fun key -> mustBeUser >=> Handlers.editPost key)
                route "/api/savework" >=> mustBeUser >=> Handlers.saveWork
            ]
    ]

[<EntryPoint>]
let main __ =
    let contentRoot = Directory.GetCurrentDirectory()
    let webRoot = Path.Combine(contentRoot, "wwwroot")
    let configuration = 
        (new ConfigurationBuilder())
            .SetBasePath(contentRoot)
            .AddEnvironmentVariables().Build()

    let cookieAuth (o : CookieAuthenticationOptions) =
        do
            o.SlidingExpiration   <- true
            o.ExpireTimeSpan      <- TimeSpan.FromDays 1.

    let configureServices (services : IServiceCollection) =
        let connString = configuration.GetConnectionString("default")
        services.AddDbContext<Data.BlogData>(fun o -> o.UseSqlite connString |> ignore) |> ignore
        services
            .AddDistributedMemoryCache()
            .AddSession()
            .AddGiraffe()
            .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(cookieAuth) |> ignore

    let configureCulture (o : RequestLocalizationOptions) = 
        let nzCulture = new CultureInfo("en-nz")
        let nzCultureList = new System.Collections.Generic.List<CultureInfo>([ nzCulture ])
        o.SupportedCultures <- nzCultureList
        o.SupportedUICultures <- nzCultureList
        o.DefaultRequestCulture <- new RequestCulture(nzCulture)

    let configureApp (app : IApplicationBuilder) =
        let env = app.ApplicationServices.GetService<IHostingEnvironment>()
        (match env.IsDevelopment() with
        | true  -> app.UseDeveloperExceptionPage()
        | false -> app.UseGiraffeErrorHandler Handlers.error)
            .UseRequestLocalization(configureCulture)
            .UseStaticFiles()
            .UseAuthentication()
            .UseSession()
            .UseGiraffe(webApp)

    let configureLogging (builder : ILoggingBuilder) =
        let filter (l : LogLevel) = l.Equals LogLevel.Error
        builder.AddFilter(filter).AddConsole().AddDebug() |> ignore

    WebHostBuilder()
        .UseKestrel()
        .UseContentRoot(contentRoot)
        .UseIISIntegration()
        .UseWebRoot(webRoot)
        .UseConfiguration(configuration)
        .Configure(Action<IApplicationBuilder> configureApp)
        .ConfigureServices(configureServices)
        .ConfigureLogging(configureLogging)
        .Build()
        .Run()
    0
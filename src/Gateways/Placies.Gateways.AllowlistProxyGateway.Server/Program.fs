module Placies.Gateways.AllowlistProxyGateway.Server.Program

open System
open FsToolkit.ErrorHandling
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.DependencyInjection
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http

open Placies.Multiformats
open Placies.Gateways.ProxyGateway
open Placies.Gateways.AllowlistProxyGateway.Server.Utils

[<EntryPoint>]
let main args =

    let builder = WebApplication.CreateBuilder(args)

    builder.Services.AddSingleton<IMultiBaseProvider, _>(fun _services ->
        MultiBaseRegistry.CreateDefault()
    ) |> ignore
    builder.Services.AddSingleton<IContentRootAllowlistProvider, _>(fun _services ->
        FileContentRootAllowlistProvider(builder.Configuration.["AllowlistProvider:FilePath"])
    ) |> ignore

    // TODO: Check that this address is only host
    let proxiedGatewayAddress = Uri(builder.Configuration.["ProxiedGateway:Address"])
    builder.Services.AddIpfsProxyGateway(proxiedGatewayAddress) |> ignore

    let app = builder.Build()

    app.UseIpfsProxyGateway(
        onRequesting=(fun ctx gatewayRequest -> taskResult {
            let contentRootAllowlistProvider = ctx.RequestServices.GetRequiredService<IContentRootAllowlistProvider>()
            let! isAllowed = contentRootAllowlistProvider.IsAllowed(gatewayRequest.ContentRoot)
            if not isAllowed then
                return! Error (Results.Gone("This content root is not allowed"))
        }),
        onResponded=(fun _ _ _ -> taskResult { () })
    ) |> ignore

    app.Run()

    0

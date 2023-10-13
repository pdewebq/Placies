module Placies.SigningGateway.Server.Program

open System
open System.IO
open System.Net.Http
open System.Threading.Tasks
open FsToolkit.ErrorHandling
open Microsoft.Extensions.Primitives
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Microsoft.Extensions.DependencyInjection
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http.Extensions
open Microsoft.AspNetCore.WebUtilities
open Microsoft.AspNetCore.Http
open Microsoft.Net.Http.Headers
open Org.BouncyCastle.Crypto.Parameters
open Org.BouncyCastle.OpenSsl

open Placies
open Placies.Utils
open Placies.Multiformats
open Placies.Gateway
open Placies.SigningGateway


[<EntryPoint>]
let main args =

    let builder = WebApplication.CreateBuilder(args)

    builder.Services.AddSingleton<IMultiBaseProvider, _>(fun _services -> MultiBaseRegistry.CreateDefault()) |> ignore

    // TODO: Check that this address is only host
    let proxiedGatewayAddress = Uri(builder.Configuration.["ProxiedGateway:Address"])

    let httpClientIpfsHttpGatewayName = "IpfsHttpGateway"

    builder.Services.AddHttpClient< >(httpClientIpfsHttpGatewayName, fun httpClient ->
        httpClient.BaseAddress <- proxiedGatewayAddress
    ).ConfigurePrimaryHttpMessageHandler(fun () ->
        new HttpClientHandler(
            AllowAutoRedirect = false
        ) :> HttpMessageHandler
    ) |> ignore

    let app = builder.Build()

    let importPublicKey (pem: string) : RsaKeyParameters =
        let pemStringReader = new StringReader(pem)
        let pemReader = PemReader(pemStringReader)
        let publicKey = pemReader.ReadObject() :?> RsaKeyParameters
        publicKey
    let publicKeyPath = app.Configuration.["SigningPublicKeyPath"]
    let publicKey = importPublicKey (File.ReadAllText(publicKeyPath))


    let signingAddressCookieKey (contentRoot: IpfsContentRoot) =
        let ns, value = IpfsContentRoot.toNamespaceAndValue contentRoot
        $"IpfsSigningGatewayProxy_%s{ns}-%s{value}_Signature"

    let tryGetVarsig (request: HttpRequest) (contentRoot: IpfsContentRoot) : Result<string, IResult> = result {
        match request.Query.TryGetValue("sig") |> Option.ofTryByref with
        | None ->
            match request.Cookies.TryGetValue(signingAddressCookieKey contentRoot) |> Option.ofTryByref with
            | None ->
                return! Error (Results.Unauthorized("No signature"))
            | Some varsig ->
                return varsig
        | Some varsig ->
            let! varsig = varsig |> Seq.tryExactlyOne |> Result.requireSome (Results.BadRequest("Multiple signatures"))
            return varsig
    }

    app.Use(Func<HttpContext, RequestDelegate, Task>(fun ctx next -> task {
        match ctx.Request.Method with
        | Equals HttpMethods.Get ->
            let! res = taskResult {
                let gatewayRequest = GatewayRequest.ofHttpRequest ctx.Request
                match gatewayRequest with
                | None ->
                    return! next.Invoke(ctx)
                | Some gatewayRequest ->
                    let! gatewayRequest = gatewayRequest
                    app.Logger.LogInformation("Requesting {@GatewayRequest}", gatewayRequest)

                    let multibaseProvider = ctx.RequestServices.GetRequiredService<IMultiBaseProvider>()

                    let! varsigStr = tryGetVarsig ctx.Request gatewayRequest.ContentRoot
                    let! isValid =
                        varsigStr
                        |> SigningContentRoot.verifyVarsigSignature multibaseProvider gatewayRequest.ContentRoot publicKey
                        |> Result.mapError (fun err -> Results.BadRequest(err))
                    do! if not isValid then Result.Error (Results.Unauthorized("Signature is not verified")) else Ok ()
                    app.Logger.LogInformation("Valid")

                    let httpClient = ctx.RequestServices.GetRequiredService<IHttpClientFactory>().CreateClient(httpClientIpfsHttpGatewayName)
                    let! gatewayResponse = Gateway.send httpClient gatewayRequest

                    if gatewayResponse.StatusCode = StatusCodes.Status301MovedPermanently then
                        match gatewayResponse.ResponseHeaders.TryGetValue(HeaderNames.Location) |> Option.ofTryByref with
                        | None -> ()
                        | Some locationValues ->
                            let proxyRedirectLocation (locationValue: string) (requestUri: Uri) =
                                let locationUri = Uri(locationValue, UriKind.RelativeOrAbsolute)
                                if locationUri.IsAbsoluteUri then
                                    let originHostWithoutSubdomainIpfs (requestHost: string) =
                                        match requestHost |> GatewayRequest.parseHostToContentRoot multibaseProvider with
                                        | Some res ->
                                            let _, hostRemainder = res |> Result.getOk
                                            hostRemainder
                                        | None ->
                                            requestHost
                                    match locationUri.Host |> GatewayRequest.parseHostToContentRoot multibaseProvider with
                                    | Some res ->
                                        let contentRoot, _ = res |> Result.getOk
                                        let uriBuilder = UriBuilder(locationUri)
                                        uriBuilder.Scheme <- requestUri.Scheme
                                        uriBuilder.Host <- originHostWithoutSubdomainIpfs requestUri.Host
                                        uriBuilder.Port <- requestUri.Port
                                        IpfsContentRoot.appendToUriSubdomain uriBuilder contentRoot
                                        uriBuilder.Uri.ToString()
                                    | None ->
                                        match PathString(locationUri.AbsolutePath) |> GatewayRequest.parsePathToContentRoot multibaseProvider  with
                                        | Some _ ->
                                            let uriBuilder = UriBuilder(locationUri)
                                            uriBuilder.Scheme <- requestUri.Scheme
                                            uriBuilder.Host <- originHostWithoutSubdomainIpfs requestUri.Host
                                            uriBuilder.Port <- requestUri.Port
                                            uriBuilder.Uri.ToString()
                                        | None ->
                                            locationValue
                                else
                                    locationValue
                            let locationValues =
                                locationValues
                                |> Seq.map ^fun locationValue ->
                                    proxyRedirectLocation locationValue (Uri(UriHelper.GetEncodedUrl(ctx.Request)))
                                |> Seq.map ^fun locationValue ->
                                    QueryHelpers.AddQueryString(locationValue, "sig", varsigStr)
                                |> Seq.toArray |> StringValues
                            gatewayResponse.ResponseHeaders.Remove(HeaderNames.Location) |> ignore
                            gatewayResponse.ResponseHeaders.Add(HeaderNames.Location, locationValues)

                    ctx.Response.Cookies.Append(
                        signingAddressCookieKey gatewayRequest.ContentRoot,
                        varsigStr,
                        CookieBuilder(
                            Expiration = TimeSpan.Parse(app.Configuration.["SigningGateway:CookieExpiration"])
                        ).Build(ctx)
                    )
                    do! GatewayResponse.toHttpResponse ctx.Response gatewayResponse
                    return! ctx.Response.CompleteAsync()
            }
            match res with
            | Error errorResult ->
                do! errorResult.ExecuteAsync(ctx)
                return! ctx.Response.CompleteAsync()
            | Ok () ->
                ()
        | _ ->
            return! next.Invoke(ctx)
    })) |> ignore

    app.Run()

    0

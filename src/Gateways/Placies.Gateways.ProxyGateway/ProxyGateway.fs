namespace Placies.Gateways.ProxyGateway

open System
open System.Net.Http
open System.Threading.Tasks
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Primitives
open FsToolkit.ErrorHandling
open Microsoft.Net.Http.Headers
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Http.Extensions
open Placies.Utils
open Placies.Multiformats
open Placies.Gateways

[<AutoOpen>]
module ProxyGatewayApplicationBuilderExtensions =

    let internal HttpClientIpfsProxyGatewayName = "IpfsProxyGateway"

    type IServiceCollection with
        member this.AddIpfsProxyGateway(baseGatewayAddress: Uri): IServiceCollection =
            this.AddHttpClient< >(HttpClientIpfsProxyGatewayName, fun httpClient ->
                httpClient.BaseAddress <- baseGatewayAddress
            ).ConfigurePrimaryHttpMessageHandler(fun () ->
                new HttpClientHandler(
                    AllowAutoRedirect = false
                ) :> HttpMessageHandler
            ) |> ignore
            this

    type IApplicationBuilder with

        member this.UseIpfsProxyGateway(
            onRequesting: HttpContext -> GatewayRequest -> TaskResult<unit, IResult>,
            onResponded: HttpContext -> GatewayRequest -> GatewayResponse -> TaskResult<unit, IResult>
        ): IApplicationBuilder =
            this.Use(Func<HttpContext, RequestDelegate, Task>(fun ctx next -> task {
                let! res = taskResult {
                    match ctx.Request.Method with
                    | Equals HttpMethods.Get ->
                        let gatewayRequest = GatewayRequest.ofHttpRequest ctx.Request
                        match gatewayRequest with
                        | None -> return! next.Invoke(ctx)
                        | Some gatewayRequest ->
                            let multibaseProvider = ctx.RequestServices.GetRequiredService<IMultiBaseProvider>()
                            let httpClient = ctx.RequestServices.GetRequiredService<IHttpClientFactory>().CreateClient(HttpClientIpfsProxyGatewayName)

                            let! gatewayRequest = gatewayRequest
                            do! onRequesting ctx gatewayRequest
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
                                        |> Seq.toArray |> StringValues
                                    gatewayResponse.ResponseHeaders.Remove(HeaderNames.Location) |> ignore
                                    gatewayResponse.ResponseHeaders.Add(HeaderNames.Location, locationValues)

                            do! onResponded ctx gatewayRequest gatewayResponse

                            do! GatewayResponse.toHttpResponse ctx.Response gatewayResponse
                            return! ctx.Response.CompleteAsync()
                    | _ ->
                        return! next.Invoke(ctx)
                }
                match res with
                | Ok () -> ()
                | Error errorResult ->
                    do! errorResult.ExecuteAsync(ctx)
                    return! ctx.Response.CompleteAsync()
            }))

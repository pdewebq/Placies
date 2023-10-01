module IpfsSigningGatewayProxy.Server.Program

open System
open System.IO
open System.Net.Http
open System.Threading.Tasks
open System.Web
open System.Security.Cryptography
open FsToolkit.ErrorHandling
open Microsoft.Extensions.Primitives
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Microsoft.Extensions.DependencyInjection
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Http.Extensions
open AspNetCore.Proxy
open Ipfs
open Org.BouncyCastle.Crypto
open Org.BouncyCastle.Crypto.Parameters
open Org.BouncyCastle.OpenSsl
open Org.BouncyCastle.Security

open IpfsSigningGatewayProxy


[<EntryPoint>]
let main args =

    MultiCodec.registerMore ()
    MultiBase.registerMore ()

    let builder = WebApplication.CreateBuilder(args)

    builder.Services.AddHttpClient() |> ignore
    builder.Services.AddProxies() |> ignore

    let app = builder.Build()

    let importPublicKey (pem: string) : RSACryptoServiceProvider =
        let pemReader = PemReader(new StringReader(pem))
        let publicKey = pemReader.ReadObject() :?> AsymmetricKeyParameter
        let rsaParams = DotNetUtilities.ToRSAParameters(publicKey :?> RsaKeyParameters)

        let csp = new RSACryptoServiceProvider()
        csp.ImportParameters(rsaParams)
        csp
    let publicKeyPath = app.Configuration.["SigningPublicKeyPath"]
    let publicKey = importPublicKey (File.ReadAllText(publicKeyPath))


    let proxiedGatewayAddress = Uri(app.Configuration.["ProxiedGateway:Address"])

    let signingAddressCookieKey (signingAddress: SigningAddress) =
        let ns, value =
            match signingAddress with
            | SigningAddress.Ipfs cid -> "ipfs", cid.ToString()
            | SigningAddress.Ipns (SigningIpnsAddress.Key key) -> "ipns", key.ToString()
            | SigningAddress.Ipns (SigningIpnsAddress.DnsName dns) -> "ipns", dns
        $"IpfsSigningGatewayProxy_%s{ns}-%s{value}_Signature"

    let tryGetVarsig (request: HttpRequest) (signingAddress: SigningAddress) : Result<string, IResult> = result {
        match request.Query.TryGetValue("sig") |> Option.ofTryByref with
        | None ->
            match request.Cookies.TryGetValue(signingAddressCookieKey signingAddress) |> Option.ofTryByref with
            | None ->
                return! Error (Results.Unauthorized("No signature"))
            | Some varsig ->
                return varsig
        | Some varsig ->
            let! varsig = varsig |> Seq.tryExactlyOne |> Result.requireSome (Results.BadRequest("Multiple signatures"))
            return varsig
    }

    // let aspHttpRequestToHttpRequestMessage (request: HttpRequest) : HttpRequestMessage =
    //     new HttpRequestMessage(
    //         HttpMethod(request.Method),
    //         request.GetEncodedUrl(),
    //         // TODO: Copy protocol version
    //         Content = null // TODO: Copy content too
    //     )
    //
    // let httpResponseMessageToAspHttpResponse (responseMessage: HttpResponseMessage) (response: HttpResponse) (ct: CancellationToken) : Task = task {
    //     response.StatusCode <- int response.StatusCode
    //
    //     for KeyValue (name, values) in response.Headers do
    //         response.Headers.Append(name, StringValues(values |> Seq.toArray))
    //
    //     use! proxiedResponseStream = responseMessage.Content.ReadAsStreamAsync()
    //     do! proxiedResponseStream.CopyToAsync(response.Body, ct)
    // }

    let proxyGateway (ctx: HttpContext) (proxiedGatewayAddress: Uri) (signingAddress: SigningAddress) (signatureStr: string) = task {
        let httpClient = ctx.RequestServices.GetRequiredService<IHttpClientFactory>().CreateClient()

        let originalUri = Uri(ctx.Request.GetEncodedUrl())
        let proxiedUri =
            let proxiedUriBuilder = UriBuilder(originalUri)
            proxiedUriBuilder.Host <- proxiedGatewayAddress.Host
            proxiedUriBuilder.Port <- proxiedGatewayAddress.Port
            proxiedUriBuilder.Query <-
                let query = HttpUtility.ParseQueryString(originalUri.Query)
                query.Remove("sig")
                query.ToString()
            proxiedUriBuilder.Uri

        let response = ctx.Response
        let! proxiedResponse = httpClient.SendAsync(new HttpRequestMessage(HttpMethod(ctx.Request.Method), proxiedUri), ctx.RequestAborted)

        response.StatusCode <- int proxiedResponse.StatusCode
        for KeyValue (name, values) in proxiedResponse.Headers do
            response.Headers.Append(name, StringValues(values |> Seq.toArray))

        response.Cookies.Append(signingAddressCookieKey signingAddress, signatureStr)

        use! proxiedResponseStream = proxiedResponse.Content.ReadAsStreamAsync()
        do! proxiedResponseStream.CopyToAsync(response.Body, ctx.RequestAborted)

        do! response.CompleteAsync()
    }

    // let proxyGateway (ctx: HttpContext) (proxiedGatewayAddress: Uri) (signingAddress: SigningAddress) (varsigStr: string) = task {
    //     // let httpClient = ctx.RequestServices.GetRequiredService<IHttpClientFactory>().CreateClient()
    //
    //     // ctx.Request.QueryString <-
    //     //     let query = HttpUtility.ParseQueryString(ctx.Request.QueryString.Value)
    //     //     query.Remove("sig")
    //     //     QueryString(query.ToString())
    //
    //     // ctx.Request.Cookies. .Remove(signingAddressCookieKey signingAddress) |> ignore
    //
    //     do! ctx.HttpProxyAsync(
    //         proxiedGatewayAddress.ToString(),
    //         HttpProxyOptionsBuilder.Instance
    //             .WithBeforeSend(fun ctx requestMessage -> task {
    //                 requestMessage.RequestUri <-
    //                     let uriBuilder = UriBuilder(requestMessage.RequestUri)
    //                     uriBuilder.Query <-
    //                         let query = HttpUtility.ParseQueryString(requestMessage.RequestUri.Query)
    //                         query.Remove("sig")
    //                         query.ToString()
    //                     uriBuilder.Uri
    //                 ()
    //             })
    //             .WithAfterReceive(fun ctx responseMessage -> task {
    //                 responseMessage.Content.Headers.Add(HeaderNames.SetCookie, $"{signingAddressCookieKey signingAddress}={varsigStr}")
    //             })
    //             .Build()
    //     )
    //
    //     do! ctx.Response.CompleteAsync()
    // }

    app.Use(Func<HttpContext, RequestDelegate, Task>(fun ctx next -> task {
        match ctx.Request.Method with
        | Equals HttpMethods.Get ->
            let signingAddress = result {
                if ctx.Request.Path.StartsWithSegments("/ipfs") then
                    let segments = ctx.Request.Path.ToString().Split('/', StringSplitOptions.RemoveEmptyEntries)
                    let cidStr = segments.[1]
                    let! cid = Result.tryWith (fun () -> Cid.Decode(cidStr)) |> Result.mapError (fun _ex -> Results.BadRequest("Invalid CID"))
                    return SigningAddress.Ipfs cid |> Some
                elif ctx.Request.Path.StartsWithSegments("/ipns") then
                    let segments = ctx.Request.Path.ToString().Split('/', StringSplitOptions.RemoveEmptyEntries)
                    let ipnsName = segments.[1]
                    let ipnsName = SigningIpnsAddress.parseIpnsName ipnsName false
                    return SigningAddress.Ipns ipnsName |> Some
                else
                    let hostTokens = ctx.Request.Host.Host.Split('.') |> Array.toList
                    match hostTokens with
                    | cidStr :: "ipfs" :: _ ->
                        let! cid = Result.tryWith (fun () -> Cid.Decode(cidStr)) |> Result.mapError (fun _ex -> Results.BadRequest("Invalid CID"))
                        return SigningAddress.Ipfs cid |> Some
                    | ipnsName :: "ipns" :: _ ->
                        let ipnsName = SigningIpnsAddress.parseIpnsName ipnsName true
                        return SigningAddress.Ipns ipnsName |> Some
                    | _ ->
                        return None
            }

            match signingAddress with
            | Error errorResult ->
                do! errorResult.ExecuteAsync(ctx)
                return! ctx.Response.CompleteAsync()
            | Ok None ->
                return! next.Invoke(ctx)
            | Ok (Some signingAddress) ->
                app.Logger.LogInformation("Requesting {@SigningAddress}", signingAddress)
                match tryGetVarsig ctx.Request signingAddress with
                | Error errorResult ->
                    do! errorResult.ExecuteAsync(ctx)
                    return! ctx.Response.CompleteAsync()
                | Ok varsigStr ->
                    match SigningAddress.verifyVarsigSignature signingAddress publicKey varsigStr with
                    | Error err ->
                        do! Results.BadRequest(err).ExecuteAsync(ctx)
                        return! ctx.Response.CompleteAsync()
                    | Ok isValid ->
                        if isValid then
                            app.Logger.LogInformation("Valid")
                            return! proxyGateway ctx proxiedGatewayAddress signingAddress varsigStr
                        else
                            do! Results.Unauthorized("Signature is not verified").ExecuteAsync(ctx)
                            return! ctx.Response.CompleteAsync()
        | _ ->
            return! next.Invoke(ctx)
    })) |> ignore

    app.Run()

    0

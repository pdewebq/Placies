module Placies.Gateways.SigningGateway.Server.Program

open System
open System.IO
open FsToolkit.ErrorHandling
open Microsoft.Extensions.Primitives
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Microsoft.Extensions.DependencyInjection
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.WebUtilities
open Microsoft.AspNetCore.Http
open Microsoft.Net.Http.Headers
open Org.BouncyCastle.Crypto.Parameters
open Org.BouncyCastle.OpenSsl

open Placies.Utils
open Placies.Multiformats
open Placies.Gateways
open Placies.Gateways.ProxyGateway
open Placies.Gateways.SigningGateway


[<EntryPoint>]
let main args =

    let builder = WebApplication.CreateBuilder(args)

    builder.Services.AddSingleton<IMultiBaseProvider, _>(fun _services -> MultiBaseRegistry.CreateDefault()) |> ignore

    // TODO: Check that this address is only host
    let proxiedGatewayAddress = Uri(builder.Configuration.["ProxiedGateway:Address"])
    builder.Services.AddIpfsProxyGateway(proxiedGatewayAddress) |> ignore

    let app = builder.Build()

    let verifyVarsigSignature =
        let publicKeyAlgorithmName = app.Configuration.["SigningPublicKey:SigningAlgorithm"]
        let publicKeyPath = app.Configuration.["SigningPublicKey:PublicKeyPath"]
        match publicKeyAlgorithmName.ToLower() with
        | "rsa" ->
            let pem = File.ReadAllText(publicKeyPath)
            let pemStringReader = new StringReader(pem)
            let pemReader = PemReader(pemStringReader)
            let rsaPublicKey = pemReader.ReadObject() :?> RsaKeyParameters
            fun multibaseProvider contentRoot varsig ->
                SigningContentRoot.verifyVarsigSignatureRsa multibaseProvider contentRoot rsaPublicKey varsig
        | "ed25519" ->
            let pem = File.ReadAllText(publicKeyPath)
            let pemStringReader = new StringReader(pem)
            let pemReader = PemReader(pemStringReader)
            let ed25519PublicKey = pemReader.ReadObject() :?> Ed25519PublicKeyParameters
            fun multibaseProvider contentRoot varsig ->
                SigningContentRoot.verifyVarsigSignatureEd25519 multibaseProvider contentRoot ed25519PublicKey varsig
        | _ ->
            failwith $"Unsupported signing algorithm: {publicKeyAlgorithmName}"

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

    app.UseIpfsProxyGateway(
        onRequesting=(fun ctx gatewayRequest -> taskResult {
            let multiBaseProvider = ctx.RequestServices.GetRequiredService<IMultiBaseProvider>()
            let! varsigStr = tryGetVarsig ctx.Request gatewayRequest.ContentRoot
            let! isValid =
                varsigStr
                |> verifyVarsigSignature multiBaseProvider gatewayRequest.ContentRoot
                |> Result.mapError (fun err -> Results.BadRequest(err))
            do! if not isValid then Result.Error (Results.Unauthorized("Signature is not verified")) else Ok ()
            app.Logger.LogInformation("Valid")
            ctx.Items.["VarsigStr"] <- varsigStr
        }),
        onResponded=(fun ctx gatewayRequest gatewayResponse -> taskResult {
            let varsigStr = ctx.Items.["VarsigStr"] :?> string

            if gatewayResponse.StatusCode = StatusCodes.Status301MovedPermanently then
                match gatewayResponse.ResponseHeaders.TryGetValue(HeaderNames.Location) |> Option.ofTryByref with
                | None -> ()
                | Some locationValues ->
                    let locationValues =
                        locationValues
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
        })
    ) |> ignore

    app.Run()

    0

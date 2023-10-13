module Placies.SigningGateway.Signer.Program

open System.IO
open System.Security.Cryptography
open Argu
open Org.BouncyCastle.Crypto.Parameters
open Org.BouncyCastle.OpenSsl

open Placies
open Placies.Utils
open Placies.Multiformats
open Placies.Gateway
open Placies.SigningGateway

type CliArguments =
    | [<ExactlyOnce>] Private_Key of path: string
    | [<ExactlyOnce>] Signing_Alg of algorithmName: string
    | [<MainCommand; ExactlyOnce; Last>] Content_Root of contentRoot: string
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Private_Key _path -> "Path to a key path"
            | Signing_Alg _algorithmName -> "Name of the signing algorithm"
            | Content_Root _contentRoot -> "Content root to sign"

[<EntryPoint>]
let main args =

    let argumentParser = ArgumentParser.Create<CliArguments>()
    let argumentResults = argumentParser.Parse(args)

    let multibaseProvider = MultiBaseRegistry.CreateDefault()

    let privateKeyPath = argumentResults.GetResult(CliArguments.Private_Key)
    let privateKey =
        let pem = File.ReadAllText(privateKeyPath)
        use pemStringReader = new StringReader(pem)
        let pemReader = PemReader(pemStringReader)
        let privateKey = pemReader.ReadObject() :?> RsaPrivateCrtKeyParameters
        privateKey

    let signingAlgorithmName = argumentResults.GetResult(CliArguments.Signing_Alg)
    let signContentRoot =
        match signingAlgorithmName.ToLower() with
        | "rsa" -> fun contentRoot -> SigningContentRoot.signContentRootRsa contentRoot privateKey HashAlgorithmName.SHA256
        | _ ->
            failwith $"Unsupported signing algorithm: {signingAlgorithmName}"

    let contentRootInput = argumentResults.GetResult(CliArguments.Content_Root)

    let contentRoot =
        match contentRootInput with
        | Regex @"^\/ipfs\/(.+)$" [ cidStr ] ->
            IpfsContentRoot.Ipfs (cidStr |> Cid.parse multibaseProvider)
        | Regex @"^\/ipns\/(.+)$" [ ipnsName ] ->
            let contentRootIpns = ipnsName |> IpfsContentRootIpns.parseIpnsName multibaseProvider false
            IpfsContentRoot.Ipns contentRootIpns
        | _ ->
            failwith $"Invalid content root: '{contentRootInput}'"

    let varsig = signContentRoot contentRoot
    let varsig = varsig.ToArray() |> MultiBase.encode MultiBaseInfos.Base58Btc

    match contentRoot with
    | IpfsContentRoot.Ipfs cid ->
        printfn $"/ipfs/cidv1/{cid |> Cid.encode} varsig: {varsig}"
    | IpfsContentRoot.Ipns (IpfsContentRootIpns.Key libp2pKey) ->
        printfn $"/ipns/libp2p/{libp2pKey |> Cid.encode} varsig: {varsig}"
    | IpfsContentRoot.Ipns (IpfsContentRootIpns.DnsName dnsName) ->
        printfn $"/ipns/dns/{dnsName} varsig: {varsig}"

    0

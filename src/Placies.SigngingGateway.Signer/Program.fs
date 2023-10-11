module Placies.SigningGateway.Signer.Program

open System.IO
open System.Security.Cryptography
open Org.BouncyCastle.Crypto.Parameters
open Org.BouncyCastle.OpenSsl
open Org.BouncyCastle.Security

open Placies
open Placies.Utils
open Placies.Multiformats
open Placies.Gateway
open Placies.SigningGateway

[<EntryPoint>]
let main args =

    let multibaseProvider = MultiBaseRegistry.CreateDefault()

    let importPrivateKey (pem: string) : RSACryptoServiceProvider =
        let pemReader = PemReader(new StringReader(pem))
        let privateKey = pemReader.ReadObject() :?> RsaPrivateCrtKeyParameters
        let rsaParams = DotNetUtilities.ToRSAParameters(privateKey)

        let csp = new RSACryptoServiceProvider()
        csp.ImportParameters(rsaParams)
        csp
    let privateKeyPath = args.[0]
    let privateKey = importPrivateKey (File.ReadAllText(privateKeyPath))

    let input = args.[1]
    match input with
    | Regex @"^\/ipfs\/(.+)$" [ cidStr ] ->
        let contentRoot = IpfsContentRoot.Ipfs (cidStr |> Cid.parse multibaseProvider)
        let varsig = SigningContentRoot.signContentRoot contentRoot privateKey HashAlgorithmName.SHA256 RSASignaturePadding.Pkcs1
        let varsig = varsig.ToArray() |> MultiBase.encode MultiBaseInfos.Base58Btc
        printfn $"/ipfs/cidv1/{cidStr} varsig: {varsig}"
    | Regex @"^\/ipns\/(.+)$" [ ipnsName ] ->
        let contentRootIpns = ipnsName |> IpfsContentRootIpns.parseIpnsName multibaseProvider false
        let contentRoot = IpfsContentRoot.Ipns contentRootIpns
        let varsig = SigningContentRoot.signContentRoot contentRoot privateKey HashAlgorithmName.SHA256 RSASignaturePadding.Pkcs1
        let varsig = varsig.ToArray() |> MultiBase.encode MultiBaseInfos.Base58Btc
        match contentRootIpns with
        | IpfsContentRootIpns.Key _ ->
            printfn $"/ipns/libp2p-key/{ipnsName} varsig: {varsig}"
        | IpfsContentRootIpns.DnsName _ ->
            printfn $"/ipns/dns/{ipnsName} varsig: {varsig}"
    | _ ->
        failwith $"Invalid input '{input}'"

    0

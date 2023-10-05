module Placies.SigningGateway.Signer.Program

open System.IO
open System.Security.Cryptography
open Ipfs
open Org.BouncyCastle.Crypto.Parameters
open Org.BouncyCastle.OpenSsl
open Org.BouncyCastle.Security

open Placies
open Placies.Gateway
open Placies.SigningGateway

[<EntryPoint>]
let main args =

    MultiCodec.registerMore ()
    MultiBase.registerMore ()

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
        let contentRoot = IpfsContentRoot.Ipfs (Cid.Decode(cidStr))
        let varsig = SigningContentRoot.signContentRoot contentRoot privateKey HashAlgorithmName.SHA256 RSASignaturePadding.Pkcs1
        let varsig = MultiBase.Encode(varsig.ToArray(), "base58btc")
        printfn $"/ipfs/cidv1/{cidStr} varsig: {varsig}"
    | Regex @"^\/ipns\/(.+)$" [ ipnsName ] ->
        let contentRootIpns = IpfsContentRootIpns.parseIpnsName ipnsName false
        let contentRoot = IpfsContentRoot.Ipns contentRootIpns
        let varsig = SigningContentRoot.signContentRoot contentRoot privateKey HashAlgorithmName.SHA256 RSASignaturePadding.Pkcs1
        let varsig = MultiBase.Encode(varsig.ToArray(), "base58btc")
        match contentRootIpns with
        | IpfsContentRootIpns.Key _ ->
            printfn $"/ipns/libp2p-key/{ipnsName} varsig: {varsig}"
        | IpfsContentRootIpns.DnsName _ ->
            printfn $"/ipns/dns/{ipnsName} varsig: {varsig}"
    | _ ->
        failwith $"Invalid input '{input}'"

    0

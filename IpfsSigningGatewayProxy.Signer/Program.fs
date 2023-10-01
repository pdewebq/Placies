module IpfsSigningGatewayProxy.Signer.Program

open System.IO
open System.Security.Cryptography
open Ipfs
open Org.BouncyCastle.Crypto.Parameters
open Org.BouncyCastle.OpenSsl
open Org.BouncyCastle.Security

open IpfsSigningGatewayProxy

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
        let signingAddress = SigningAddress.Ipfs (Cid.Decode(cidStr))
        let signature = SigningAddress.signAddress signingAddress privateKey HashAlgorithmName.SHA256 RSASignaturePadding.Pkcs1
        let signature = MultiBase.Encode(signature.ToArray(), "base58btc")
        printfn $"/ipfs/cidv1/{cidStr} varsig: {signature}"
    | Regex @"^\/ipns\/(.+)$" [ ipnsName ] ->
        let signingIpnsAddress = SigningIpnsAddress.parseIpnsName ipnsName false
        let signingAddress = SigningAddress.Ipns signingIpnsAddress
        let varsig = SigningAddress.signAddress signingAddress privateKey HashAlgorithmName.SHA256 RSASignaturePadding.Pkcs1
        let varsig = MultiBase.Encode(varsig.ToArray(), "base58btc")
        match signingIpnsAddress with
        | SigningIpnsAddress.Key _ ->
            printfn $"/ipns/libp2p-key/{ipnsName} varsig: {varsig}"
        | SigningIpnsAddress.DnsName _ ->
            printfn $"/ipns/dns/{ipnsName} varsig: {varsig}"
    | _ ->
        failwith $"Invalid input '{input}'"

    0

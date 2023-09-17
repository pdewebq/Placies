module IpfsSigningGatewayProxy.Signer.Program

open System.IO
open System.Security.Cryptography
open System.Text
open Ipfs
open Org.BouncyCastle.Crypto.Parameters
open Org.BouncyCastle.OpenSsl
open Org.BouncyCastle.Security

open IpfsSigningGatewayProxy

let signAddress (signingAddress: SigningAddress) (privateKey: RSA) (hashAlg: HashAlgorithmName) (padding: RSASignaturePadding) =
    let stream = new MemoryStream()
    use streamWriter = new BinaryWriter(stream)
    stream.WriteMultiCodec("varsig")
    stream.WriteMultiCodec("rsa-pub")
    match hashAlg with
    | Equals HashAlgorithmName.SHA256 ->
        stream.WriteMultiCodec("sha2-256")
        match signingAddress with
        | SigningAddress.Ipfs cid ->
            let signingDataBytes = cid.ToArray()
            let signatureBytes = privateKey.SignData(signingDataBytes, hashAlg, padding)
            stream.WriteVarint(signatureBytes.Length)
            stream.WriteMultiCodec("ipfs")
            stream.WriteMultiCodec("cidv1")
            streamWriter.Write(signatureBytes)
        | SigningAddress.Ipns (SigningIpnsAddress.Key libp2pKey) ->
            let signingDataBytes = libp2pKey.ToArray()
            let signatureBytes = privateKey.SignData(signingDataBytes, hashAlg, padding)
            stream.WriteVarint(signatureBytes.Length)
            stream.WriteMultiCodec("ipns")
            stream.WriteMultiCodec("libp2p-key")
            streamWriter.Write(signatureBytes)
        | SigningAddress.Ipns (SigningIpnsAddress.DnsName dnsName) ->
            let signingDataBytes = Encoding.UTF8.GetBytes(dnsName)
            let signatureBytes = privateKey.SignData(signingDataBytes, hashAlg, padding)
            stream.WriteVarint(signatureBytes.Length)
            stream.WriteMultiCodec("ipns")
            stream.WriteMultiCodec("dns")
            streamWriter.Write(signatureBytes)
    | _ ->
        invalidOp $"Not supported hash algorithm: {hashAlg}"
    stream

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
        let signature = signAddress signingAddress privateKey HashAlgorithmName.SHA256 RSASignaturePadding.Pkcs1
        let signature = MultiBase.Encode(signature.ToArray(), "base58btc")
        printfn $"/ipfs/cidv1/{cidStr} signature: {signature}"
    | Regex @"^\/ipns\/(.+)$" [ ipnsName ] ->
        let signingIpnsAddress = SigningIpnsAddress.parseIpnsName ipnsName true
        let signingAddress = SigningAddress.Ipns signingIpnsAddress
        let signature = signAddress signingAddress privateKey HashAlgorithmName.SHA256 RSASignaturePadding.Pkcs1
        let signature = MultiBase.Encode(signature.ToArray(), "base58btc")
        match signingIpnsAddress with
        | SigningIpnsAddress.Key _ ->
            printfn $"/ipns/libp2p-key/{ipnsName} signature: {signature}"
        | SigningIpnsAddress.DnsName _ ->
            printfn $"/ipns/dns/{ipnsName} signature: {signature}"
    | _ ->
        failwith $"Invalid input '{input}'"

    0

namespace IpfsSigningGatewayProxy

open System
open System.IO
open System.Security.Cryptography
open System.Text
open FsToolkit.ErrorHandling
open Ipfs

[<RequireQualifiedAccess>]
type SigningIpnsAddress =
    | Key of libp2pKey: Cid
    | DnsName of dnsName: string

[<RequireQualifiedAccess>]
module SigningIpnsAddress =

    let unescapeIpnsDnsName (dnsName: string) : string =
        // TODO: Do it without a crutchy intermediate symbol
        dnsName.Replace("--", "$").Replace('-', '.').Replace('$', '-')

    let parseLibp2pKey (input: string) = result {
        let! cidOfLibp2pKey = Result.tryWith (fun () -> Cid.Decode(input)) |> Result.mapError (fun ex -> $"Not CID: {ex}")
        do! (cidOfLibp2pKey.ContentType = "libp2p-key") |> Result.requireTrue "Not libp2p-key"
        return cidOfLibp2pKey
    }

    let parseIpnsName (ipnsName: string) (shouldUnescapeDnsName: bool) : SigningIpnsAddress =
        let cidOfLibp2pKey = parseLibp2pKey ipnsName
        match cidOfLibp2pKey with
        | Ok cidOfLibp2pKey ->
            SigningIpnsAddress.Key cidOfLibp2pKey
        | Error _err ->
            let dnsName = if shouldUnescapeDnsName then unescapeIpnsDnsName ipnsName else ipnsName
            SigningIpnsAddress.DnsName dnsName


[<RequireQualifiedAccess>]
type SigningAddress =
    | Ipfs of cid: Cid
    | Ipns of SigningIpnsAddress


[<RequireQualifiedAccess>]
module SigningAddress =

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

    let verifyVarsigSignature (signingAddress: SigningAddress) (publicKey: RSA) (varsigStr: string) = result {
        let varsigBytes = MultiBase.Decode(varsigStr)
        use stream = new MemoryStream(varsigBytes)
        let varsigCodec = stream.ReadMultiCodec()
        do! Result.requireEqual varsigCodec.Name "varsig" $"{varsigCodec.Name} is not varsig"
        let varsigHeaderCodec = stream.ReadMultiCodec()
        match varsigHeaderCodec.Name with
        | "rsa-pub" ->
            let rsaHashAlgorithmCodec = stream.ReadMultiCodec()
            match rsaHashAlgorithmCodec.Name with
            | "sha2-256" ->
                let signatureByteLength = stream.ReadVarint32()
                let readSigningDataBytes (stream: Stream) = result {
                    match signingAddress with
                    | SigningAddress.Ipfs cid ->
                        do! stream.ReadMultiCodec().Name = "ipfs" |> Result.requireTrue "Signature is not for /ipfs"
                        do! stream.ReadMultiCodec().Name = "cidv1" |> Result.requireTrue "Signature is not for /ipfs/cidv1"
                        let cidBytes = cid.ToArray()
                        return cidBytes
                    | SigningAddress.Ipns (SigningIpnsAddress.Key libp2pKey) ->
                        do! stream.ReadMultiCodec().Name = "ipns" |> Result.requireTrue "Signature is not for /ipns"
                        do! stream.ReadMultiCodec().Name = "libp2p-key" |> Result.requireTrue "Signature is not for /ipns/libp2p-key"
                        let libp2pKeyBytes = libp2pKey.ToArray()
                        return libp2pKeyBytes
                    | SigningAddress.Ipns (SigningIpnsAddress.DnsName dnsName) ->
                        do! stream.ReadMultiCodec().Name = "ipns" |> Result.requireTrue "Signature is not for /ipns"
                        do! stream.ReadMultiCodec().Name = "dns" |> Result.requireTrue "Signature is not for /ipns/dns"
                        let dnsNameBytes = Encoding.UTF8.GetBytes(dnsName)
                        return dnsNameBytes
                }
                let! signingDataBytes = readSigningDataBytes stream
                let signatureBytes = Array.zeroCreate signatureByteLength
                stream.Read(signatureBytes.AsSpan()) |> ignore
                let isValid = publicKey.VerifyData(signingDataBytes, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
                return isValid
            | _ ->
                return! Error $"Not supported hash algorithm: {rsaHashAlgorithmCodec.Name}"
        | _ ->
            return! Error $"Not supported signature header: {varsigHeaderCodec.Name}"
    }

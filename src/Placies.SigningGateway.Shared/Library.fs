namespace Placies.SigningGateway

open System
open System.IO
open System.Security.Cryptography
open System.Text
open FsToolkit.ErrorHandling
open Ipfs
open Placies
open Placies.Gateway

[<RequireQualifiedAccess>]
module SigningContentRoot =

    let signContentRoot (contentRoot: IpfsContentRoot) (privateKey: RSA) (hashAlg: HashAlgorithmName) (padding: RSASignaturePadding) =
        let stream = new MemoryStream()
        use streamWriter = new BinaryWriter(stream)
        stream.WriteMultiCodec("varsig")
        stream.WriteMultiCodec("rsa-pub")
        match hashAlg with
        | Equals HashAlgorithmName.SHA256 ->
            stream.WriteMultiCodec("sha2-256")
            match contentRoot with
            | IpfsContentRoot.Ipfs cid ->
                let signingDataBytes = cid.ToArray()
                let signatureBytes = privateKey.SignData(signingDataBytes, hashAlg, padding)
                stream.WriteVarint(signatureBytes.Length)
                stream.WriteMultiCodec("ipfs")
                stream.WriteMultiCodec("cidv1")
                streamWriter.Write(signatureBytes)
            | IpfsContentRoot.Ipns (IpfsContentRootIpns.Key libp2pKey) ->
                let signingDataBytes = libp2pKey.ToArray()
                let signatureBytes = privateKey.SignData(signingDataBytes, hashAlg, padding)
                stream.WriteVarint(signatureBytes.Length)
                stream.WriteMultiCodec("ipns")
                stream.WriteMultiCodec("libp2p-key")
                streamWriter.Write(signatureBytes)
            | IpfsContentRoot.Ipns (IpfsContentRootIpns.DnsName dnsName) ->
                let signingDataBytes = Encoding.UTF8.GetBytes(dnsName)
                let signatureBytes = privateKey.SignData(signingDataBytes, hashAlg, padding)
                stream.WriteVarint(signatureBytes.Length)
                stream.WriteMultiCodec("ipns")
                stream.WriteMultiCodec("dns")
                streamWriter.Write(signatureBytes)
        | _ ->
            invalidOp $"Not supported hash algorithm: {hashAlg}"
        stream

    let verifyVarsigSignature (contentRoot: IpfsContentRoot) (publicKey: RSA) (varsigStr: string) = result {
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
                    match contentRoot with
                    | IpfsContentRoot.Ipfs cid ->
                        do! stream.ReadMultiCodec().Name = "ipfs" |> Result.requireTrue "Signature is not for /ipfs"
                        do! stream.ReadMultiCodec().Name = "cidv1" |> Result.requireTrue "Signature is not for /ipfs/cidv1"
                        let cidBytes = cid.ToArray()
                        return cidBytes
                    | IpfsContentRoot.Ipns (IpfsContentRootIpns.Key libp2pKey) ->
                        do! stream.ReadMultiCodec().Name = "ipns" |> Result.requireTrue "Signature is not for /ipns"
                        do! stream.ReadMultiCodec().Name = "libp2p-key" |> Result.requireTrue "Signature is not for /ipns/libp2p-key"
                        let libp2pKeyBytes = libp2pKey.ToArray()
                        return libp2pKeyBytes
                    | IpfsContentRoot.Ipns (IpfsContentRootIpns.DnsName dnsName) ->
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
